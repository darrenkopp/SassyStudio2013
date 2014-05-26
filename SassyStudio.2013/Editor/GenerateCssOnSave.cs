using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using SassyStudio.Compilation;
using SassyStudio.Integration.Compass;
using SassyStudio.Integration.LibSass;
using SassyStudio.Integration.SassGem;
using Yahoo.Yui.Compressor;

namespace SassyStudio.Editor
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Microsoft.Web.Editor.ScssContentTypeDefinition.ScssContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    class GenerateCssOnSave : IWpfTextViewCreationListener
    {
        static readonly Encoding UTF8_ENCODING = new UTF8Encoding(true);
        readonly Lazy<ScssOptions> _Options = new Lazy<ScssOptions>(() => SassyStudioPackage.Instance.Options.Scss, true);
        private ScssOptions Options { get { return _Options.Value; } }

        public void TextViewCreated(IWpfTextView textView)
        {
            ITextDocument document;
            if (textView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
            {
                document.FileActionOccurred += OnFileActionOccurred;
            }
            else
            {
                if (Options.IsDebugLoggingEnabled)
                    Logger.Log("Eh? Couldn't find text document. Can't handle saving documents now.");
            }
        }

        private async void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
            {
                if (Options.IsDebugLoggingEnabled)
                    Logger.Log("Detected file saved: " + e.FilePath);

                if (!Options.GenerateCssOnSave) return;

                var filename = Path.GetFileName(e.FilePath);

                // ignore anything that isn't .scss and not a root document
                if (!filename.EndsWith(".scss", StringComparison.OrdinalIgnoreCase))
                    return;

                if (filename.StartsWith("_"))
                {
                    if (Options.IsDebugLoggingEnabled)
                        Logger.Log("Compiling all files referencing include file: " + filename);
                }
                else
                {
                    if (Options.IsDebugLoggingEnabled)
                        Logger.Log("Compiling: " + filename);

                    await GenerateRootDocument(e.Time, e.FilePath);
                }
            }
        }

        private async Task GenerateRootDocument(DateTime time, string path)
        {
            try
            {
                await GenerateCss(time, path);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Unhandled exception.");
            }
        }

        private async Task GenerateCss(DateTime time, string path)
        {
            if (Options.IsDebugLoggingEnabled)
                Logger.Log("Beginning compile: " + path);

            var source = new FileInfo(path);
            // file is stale, likely another request coming in
            if (time < source.LastWriteTime)
            {
                if (Options.IsDebugLoggingEnabled)
                    Logger.Log("Ignoring compile due to stale document.");

                return;
            }

            var filename = Path.GetFileNameWithoutExtension(source.Name);
            var document = new FileInfo(path);
            var compiler = PickCompiler(document);
            var output = compiler.GetOutput(document);

            try
            {
                await compiler.CompileAsync(document, output);

                // minify
                if (Options.GenerateMinifiedCssOnSave && output != null)
                    Minify(File.ReadAllText(output.FullName), new FileInfo(Path.Combine(output.Directory.FullName, filename + ".min.css")));

                // add to project
                if (Options.IncludeCssInProject && output != null && string.IsNullOrWhiteSpace(Options.CssGenerationOutputDirectory))
                    AddFileToProject(source, output, Options);
            }
            catch (Exception ex)
            {
                if (Options.ReplaceCssWithException && output != null)
                    SaveExceptionToFile(ex, output);

                Logger.Log(ex, "Failed to compile css.");
            }

            if (Options.IsDebugLoggingEnabled)
                Logger.Log("Compile complete.");
        }

        private IDocumentCompiler PickCompiler(FileInfo document)
        {
            if (CompassSupport.IsCompassInstalled && CompassSupport.IsInCompassProject(document.Directory))
                return new CompassDocumentCompiler();

            if (SassSupport.IsSassGemInstalled)
                return new SassDocumentCompiler(Options);

            return new LibSassNetDocumentCompiler(Options);
        }

        private void Minify(string css, FileInfo file)
        {
            if (Options.IsDebugLoggingEnabled)
                Logger.Log("Generating minified css file.");

            try
            {
                string minified = "";
                if (!string.IsNullOrEmpty(css))
                {
                    var compressor = new CssCompressor { RemoveComments = true };
                    minified = compressor.Compress(css);
                }

                InteropHelper.CheckOut(file.FullName);
                File.WriteAllText(file.FullName, minified, UTF8_ENCODING);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Failed to generate minified css file.");
                if (Options.ReplaceCssWithException)
                    SaveExceptionToFile(ex, file);
            }
        }

        private void SaveExceptionToFile(Exception error, FileInfo target)
        {
            try
            {
                File.WriteAllText(target.FullName,
                    new StringBuilder()
                        .AppendLine("/*")
                        .AppendLine(error.Message)
                        .AppendLine(error.StackTrace)
                        .AppendLine("*/")
                    .ToString(),
                    UTF8_ENCODING
                );
            }
            catch
            {
                // ignore
            }
        }

        private static void AddFileToProject(FileInfo source, FileInfo target, ScssOptions options)
        {
            try
            {
                if (options.IsDebugLoggingEnabled)
                    Logger.Log(string.Format("Nesting {0} under {1}", target.Name, source.Name));

                var buildAction = options.IncludeCssInProjectOutput ? InteropHelper.BuildActionType.Content : InteropHelper.BuildActionType.None;
                InteropHelper.AddNestedFile(SassyStudioPackage.Instance.DTE, source.FullName, target.FullName, buildAction);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, string.Format("Failed to include {0} in project under {1}", target.Name, source.Name));
            }
        }
    }
}

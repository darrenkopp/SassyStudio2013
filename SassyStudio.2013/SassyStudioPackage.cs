using System;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SassyStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "0.1.5.0", IconResourceID = 400)]
    [GuidAttribute(Guids.guidSassyStudioPkgString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideOptionPage(typeof(ScssOptions), "Sassy Studio", "General", 101, 102, true, new[] { "CSS", "SCSS" })]
    public sealed class SassyStudioPackage : Package
    {
        readonly Lazy<ScssOptions> _ScssOptions;
        readonly OptionsProvider _Options;
        readonly Lazy<DTE2> _DTE;

        public SassyStudioPackage()
        {
            _ScssOptions = new Lazy<ScssOptions>(() => GetDialogPage(typeof(ScssOptions)) as ScssOptions, true);
            _Options = new OptionsProvider(this);
            _DTE = new Lazy<DTE2>(() => ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2);
        }

        internal static SassyStudioPackage Instance { get; private set; }
        internal CompositionContainer Composition { get; private set; }
        internal OptionsProvider Options { get { return _Options; } }
        internal DTE2 DTE { get { return _DTE.Value; } }

        protected override void Initialize()
        {
            base.Initialize();
            Instance = this;

            //OutputLogger.MessageReceived += (s, e) => Logger.Log(e.Message);
            //OutputLogger.ExceptionReceived += (s, e) => Logger.Log(e.Error, e.Message);

            if (Options.Scss.IsDebugLoggingEnabled)
                System.Threading.Tasks.Task.Run(() => LogInitialization(Options));
        }

        static void LogInitialization(OptionsProvider options)
        {
            Logger.Log("Sassy studio initializing.", true);
            Logger.Log("CssGenerationOutputDirectory = " + options.Scss.CssGenerationOutputDirectory);
            Logger.Log("GenerateCssOnSave = " + options.Scss.GenerateCssOnSave);
            Logger.Log("GenerateMinifiedCssOnSave = " + options.Scss.GenerateMinifiedCssOnSave);
            Logger.Log("IncludeCssInProject = " + options.Scss.IncludeCssInProject);
            Logger.Log("IncludeCssInProjectOutput = " + options.Scss.IncludeCssInProjectOutput);
            Logger.Log("IncludeSourceComments = " + options.Scss.IncludeSourceComments);
            Logger.Log("ReplaceCssWithException = " + options.Scss.ReplaceCssWithException);
            Logger.Log("RubyInstallPath = " + options.Scss.RubyInstallPath);
        }

        public int LocaleId
        {
            get
            {
                IUIHostLocale globalService = (IUIHostLocale)GetGlobalService(typeof(IUIHostLocale));
                uint localeId;
                if (globalService != null && globalService.GetUILocale(out localeId) == 0)
                    return (int)localeId;

                return 0;
            }
        }

        internal class OptionsProvider
        {
            readonly SassyStudioPackage _Package;
            public OptionsProvider(SassyStudioPackage package)
            {
                _Package = package;
            }

            public ScssOptions Scss { get { return _Package._ScssOptions.Value; } }
        }
    }
}

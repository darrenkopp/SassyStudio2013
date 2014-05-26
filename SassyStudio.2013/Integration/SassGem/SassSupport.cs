using System.IO;

namespace SassyStudio.Integration.SassGem
{
    class SassSupport
    {
        public static string SassBatchFile { get { return Path.Combine(SassyStudioPackage.Instance.Options.Scss.RubyInstallPath, "bin", "sass.bat"); } }

        public static bool IsSassGemInstalled
        {
            get
            {
                var ruby = SassyStudioPackage.Instance.Options.Scss.RubyInstallPath;
                if (string.IsNullOrEmpty(ruby) || !Directory.Exists(ruby))
                    return false;

                return File.Exists(Path.Combine(ruby, "bin", "sass.bat"));
            }
        }
    }
}

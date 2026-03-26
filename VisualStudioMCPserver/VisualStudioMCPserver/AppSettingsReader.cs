using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// Reads the VSIX's own <c>vsix-settings.json</c> from the extension directory and returns
    /// a populated <see cref="VsixOptions"/> instance.
    /// Falls back to default values silently on any failure so the VSIX always starts.
    /// </summary>
    internal static class AppSettingsReader
    {
        private const string FileName = "vsix-settings.json";

        /// <summary>
        /// Loads settings from <c>vsix-settings.json</c> located in <paramref name="vsixDir"/>.
        /// </summary>
        /// <param name="vsixDir">Directory that contains the VSIX extension DLL.</param>
        /// <returns>
        /// Populated <see cref="VsixOptions"/>; defaults are used for any missing or
        /// unparseable values.
        /// </returns>
        public static VsixOptions Read(string vsixDir)
        {
            try
            {
                if (string.IsNullOrEmpty(vsixDir) || !Directory.Exists(vsixDir))
                {
                    return new VsixOptions();
                }

                IConfiguration config = new ConfigurationBuilder()
                    .SetBasePath(vsixDir)
                    .AddJsonFile(FileName, optional: true, reloadOnChange: false)
                    .Build();

                VsixOptions options = config
                    .GetSection(VsixOptions.SectionName)
                    .Get<VsixOptions>() ?? new VsixOptions();

                return options;
            }
            catch (Exception)
            {
                // Never let a config failure prevent the extension from loading.
                return new VsixOptions();
            }
        }
    }
}

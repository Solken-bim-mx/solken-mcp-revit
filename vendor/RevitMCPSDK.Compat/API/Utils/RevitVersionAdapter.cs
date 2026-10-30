using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;

namespace RevitMCPSDK.API.Utils
{
    /// <summary>
    /// Resuelve la version de Revit en ejecucion y si un comando declara
    /// soporte para ella, usado por CommandManager al cargar comandos externos.
    /// </summary>
    public class RevitVersionAdapter
    {
        private readonly Application _app;

        public RevitVersionAdapter(Application app)
        {
            _app = app;
        }

        public string GetRevitVersion()
        {
            return _app?.VersionNumber ?? string.Empty;
        }

        public bool IsVersionSupported(IEnumerable<string> supportedVersions)
        {
            if (supportedVersions == null)
                return true;

            var current = GetRevitVersion();
            return supportedVersions.Any(v => v == current);
        }
    }
}

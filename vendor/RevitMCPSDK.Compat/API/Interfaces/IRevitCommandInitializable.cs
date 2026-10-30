using Autodesk.Revit.UI;

namespace RevitMCPSDK.API.Interfaces
{
    /// <summary>
    /// Comandos que requieren inicializacion explicita con la UIApplication
    /// activa, en vez de recibirla por constructor.
    /// </summary>
    public interface IRevitCommandInitializable
    {
        void Initialize(UIApplication uiApp);
    }
}

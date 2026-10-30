using Newtonsoft.Json.Linq;

namespace RevitMCPSDK.API.Interfaces
{
    /// <summary>
    /// Contrato base de un comando ejecutable por el plugin de Revit.
    /// </summary>
    public interface IRevitCommand
    {
        string CommandName { get; }

        object Execute(JObject parameters, string requestId);
    }
}

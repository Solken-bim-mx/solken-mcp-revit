using Autodesk.Revit.UI;

namespace RevitMCPSDK.API.Interfaces
{
    /// <summary>
    /// Extiende IExternalEventHandler de Revit para poder esperar (de forma
    /// sincrona, con timeout) a que el ExternalEvent termine de ejecutarse en
    /// el hilo de Revit antes de devolver el resultado al llamador MCP.
    /// </summary>
    public interface IWaitableExternalEventHandler : IExternalEventHandler
    {
        bool TaskCompleted { get; set; }

        bool WaitForCompletion(int timeoutMilliseconds = 10000);
    }
}

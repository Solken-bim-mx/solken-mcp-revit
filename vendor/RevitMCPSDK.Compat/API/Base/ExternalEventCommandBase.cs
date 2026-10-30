using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPSDK.API.Base
{
    /// <summary>
    /// Base para comandos que necesitan ejecutar codigo en el hilo de Revit a
    /// traves de un ExternalEvent y esperar (sincronamente, con timeout) a que
    /// termine antes de devolver el resultado al llamador MCP.
    /// </summary>
    public abstract class ExternalEventCommandBase : IRevitCommand
    {
        private readonly ExternalEvent _externalEvent;

        protected IWaitableExternalEventHandler Handler { get; }

        protected ExternalEventCommandBase(IWaitableExternalEventHandler handler, UIApplication uiApp)
        {
            Handler = handler;
            _externalEvent = ExternalEvent.Create((IExternalEventHandler)handler);
        }

        public abstract string CommandName { get; }

        public abstract object Execute(JObject parameters, string requestId);

        /// <summary>
        /// Dispara el ExternalEvent y bloquea el hilo actual hasta que el
        /// handler reporte haber terminado o se cumpla el timeout.
        /// </summary>
        protected bool RaiseAndWaitForCompletion(int timeoutMilliseconds)
        {
            _externalEvent.Raise();
            return Handler.WaitForCompletion(timeoutMilliseconds);
        }
    }
}

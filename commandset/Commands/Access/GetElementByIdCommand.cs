using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    /// <summary>
    /// Lee un elemento arbitrario de Revit por su ElementId, sin necesidad de
    /// tenerlo seleccionado en la UI (a diferencia de GetSelectedElementsCommand)
    /// ni de que aparezca en una schedule. Agregado para cerrar el hueco de
    /// verificación post-escritura de revit_actualizar_parametros_elemento
    /// (Solken, pendiente del 2026-10-28).
    /// </summary>
    public class GetElementByIdCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetElementByIdEventHandler _handler => (GetElementByIdEventHandler)Handler;

        public override string CommandName => "get_element_by_id";

        public GetElementByIdCommand(UIApplication uiApp)
            : base(new GetElementByIdEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    long? elementId = parameters?["elementId"]?.Value<long>();
                    if (elementId == null)
                        throw new ArgumentException("El parámetro 'elementId' es obligatorio");

                    _handler.TargetElementId = elementId.Value;

                    bool? includeTypeParameters = parameters?["includeTypeParameters"]?.Value<bool>();
                    if (includeTypeParameters.HasValue)
                        _handler.IncludeTypeParameters = includeTypeParameters.Value;

                    // 触发外部事件并等待完成
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    else
                    {
                        throw new TimeoutException("Lectura de elemento por ElementId agotó el tiempo de espera");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"get_element_by_id falló: {ex.Message}");
                }
            }
        }
    }
}

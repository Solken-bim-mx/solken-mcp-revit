using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Structure;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Comando get_schedule_data: extrae encabezados y filas de una o varias
    /// tablas de planificación y cantidades (ViewSchedule), por ElementId.
    /// </summary>
    public class GetScheduleDataCommand : ExternalEventCommandBase
    {
        private GetScheduleDataEventHandler _handler => (GetScheduleDataEventHandler)Handler;
        public override string CommandName => "get_schedule_data";
        public GetScheduleDataCommand(UIApplication uiApp)
            : base(new GetScheduleDataEventHandler(), uiApp)
        {
        }
        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                ScheduleDataQuery query = parameters["data"].ToObject<ScheduleDataQuery>();
                if (query == null)
                    throw new ArgumentNullException(nameof(query), "Parametros de consulta vacios");

                _handler.SetParameters(query);
                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("La lectura de tablas de planificación excedió el tiempo de espera");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al leer datos de tablas: {ex.Message}");
            }
        }
    }
}

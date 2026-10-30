using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Structure;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Comando get_rebar_host_data: dado un listado de ElementId de
    /// hosts (zapatas, contratrabes, castillos), devuelve el armado
    /// real colocado dentro de cada uno via RebarHostData -- sin
    /// depender de bounding box ni de traer toda la categoria OST_Rebar.
    /// </summary>
    public class GetRebarHostDataCommand : ExternalEventCommandBase
    {
        private GetRebarHostDataEventHandler _handler => (GetRebarHostDataEventHandler)Handler;

        public override string CommandName => "get_rebar_host_data";

        public GetRebarHostDataCommand(UIApplication uiApp)
            : base(new GetRebarHostDataEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                RebarHostQuery query = parameters["data"].ToObject<RebarHostQuery>();
                if (query == null)
                    throw new ArgumentNullException(nameof(query), "Parametros de consulta vacios");

                _handler.SetParameters(query);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("La consulta de armado por anfitrion excedio el tiempo de espera");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener armado por anfitrion: {ex.Message}");
            }
        }
    }
}

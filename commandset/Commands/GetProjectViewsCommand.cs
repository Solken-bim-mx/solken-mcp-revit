using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Structure;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Comando get_project_views: lista las vistas del proyecto, por defecto
    /// solo tablas de planificación y cantidades (ViewType.Schedule).
    /// </summary>
    public class GetProjectViewsCommand : ExternalEventCommandBase
    {
        private GetProjectViewsEventHandler _handler => (GetProjectViewsEventHandler)Handler;
        public override string CommandName => "get_project_views";
        public GetProjectViewsCommand(UIApplication uiApp)
            : base(new GetProjectViewsEventHandler(), uiApp)
        {
        }
        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // "data" es opcional en este comando -- si no viene, se listan
                // las tablas de planificación y cantidades por defecto.
                ProjectViewsQuery query = parameters != null && parameters["data"] != null
                    ? parameters["data"].ToObject<ProjectViewsQuery>()
                    : new ProjectViewsQuery();

                _handler.SetParameters(query);
                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("La consulta de vistas del proyecto excedió el tiempo de espera");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al listar vistas del proyecto: {ex.Message}");
            }
        }
    }
}

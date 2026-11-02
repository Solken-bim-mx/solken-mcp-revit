using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Versión en lote de get_element_by_id: lee varios ElementIds en una sola
    /// llamada. Parámetros esperados ("data" o nivel raíz, se acepta ambos):
    ///   elementIds (array de long, obligatorio)
    ///   includeTypeParameters (bool, opcional, default true)
    /// </summary>
    public class GetElementsByIdsCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetElementsByIdsEventHandler _handler => (GetElementsByIdsEventHandler)Handler;

        public override string CommandName => "get_elements_by_ids";

        public GetElementsByIdsCommand(UIApplication uiApp)
            : base(new GetElementsByIdsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    JObject data = parameters?["data"] as JObject ?? parameters;

                    JArray idsArray = data?["elementIds"] as JArray;
                    if (idsArray == null || idsArray.Count == 0)
                        throw new ArgumentException("El parámetro 'elementIds' (arreglo, no vacío) es obligatorio");

                    List<long> ids = idsArray.Select(t => t.Value<long>()).ToList();
                    _handler.TargetElementIds = ids;

                    bool? includeTypeParameters = data?["includeTypeParameters"]?.Value<bool>();
                    if (includeTypeParameters.HasValue)
                        _handler.IncludeTypeParameters = includeTypeParameters.Value;

                    if (RaiseAndWaitForCompletion(20000))
                    {
                        return _handler.Result;
                    }
                    else
                    {
                        throw new TimeoutException("Lectura en lote de elementos agotó el tiempo de espera");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"get_elements_by_ids falló: {ex.Message}");
                }
            }
        }
    }
}

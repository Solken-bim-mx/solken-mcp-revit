using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Escribe varios (elemento, parámetro, valor) en una sola transacción
    /// (todo o nada). "data.updates" es un arreglo de objetos:
    ///   { elementId, parameterName, value, level?, unit? }
    /// Mismas reglas de nivel y de unidad que update_element_parameter --
    /// "unit" es obligatorio por item cuando ese parámetro es Double con
    /// unidad real (Longitud/Área/Volumen), ej. "m", "cm", "mm", "m2", "m3".
    /// </summary>
    public class BatchUpdateElementParametersCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private BatchUpdateElementParametersEventHandler _handler => (BatchUpdateElementParametersEventHandler)Handler;

        public override string CommandName => "batch_update_element_parameters";

        public BatchUpdateElementParametersCommand(UIApplication uiApp)
            : base(new BatchUpdateElementParametersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    JObject data = parameters?["data"] as JObject ?? parameters;

                    JArray updatesArray = data?["updates"] as JArray;
                    if (updatesArray == null || updatesArray.Count == 0)
                        throw new ArgumentException("El parámetro 'updates' (arreglo, no vacío) es obligatorio");

                    var requests = new List<BatchParameterUpdateRequest>();
                    foreach (JToken token in updatesArray)
                    {
                        var obj = token as JObject;
                        if (obj == null)
                            throw new ArgumentException("Cada elemento de 'updates' debe ser un objeto");

                        long? elementId = obj["elementId"]?.Value<long>();
                        string parameterName = obj["parameterName"]?.Value<string>();
                        string value = obj["value"]?.Value<string>();

                        if (elementId == null || string.IsNullOrWhiteSpace(parameterName) || value == null)
                            throw new ArgumentException("Cada item de 'updates' requiere elementId, parameterName y value");

                        var req = new BatchParameterUpdateRequest
                        {
                            ElementId = elementId.Value,
                            ParameterName = parameterName,
                            Value = value
                        };

                        string level = obj["level"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(level))
                            req.Level = level;

                        string unit = obj["unit"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(unit))
                            req.Unit = unit;

                        requests.Add(req);
                    }

                    _handler.Requests = requests;

                    // Más tiempo que las herramientas de un solo elemento: el lote
                    // puede tener muchos items.
                    if (RaiseAndWaitForCompletion(30000))
                    {
                        return new
                        {
                            Success = _handler.OverallSuccess,
                            Message = _handler.OverallMessage,
                            Response = _handler.Result
                        };
                    }
                    else
                    {
                        throw new TimeoutException("Escritura en lote agotó el tiempo de espera");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"batch_update_element_parameters falló: {ex.Message}");
                }
            }
        }
    }
}

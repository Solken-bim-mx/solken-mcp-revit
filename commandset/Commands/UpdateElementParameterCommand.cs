using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Escribe (y confirma) un parámetro de instancia o de tipo en un elemento
    /// por su ElementId. Complementa a get_element_by_id: esa herramienta ya
    /// deja claro si un parámetro aparece como instancia o como "Tipo: &lt;nombre&gt;"
    /// -- esta herramienta usa exactamente ese mismo dato para decidir dónde
    /// escribir, en vez de asumir siempre instancia.
    ///
    /// Parámetros esperados en "data":
    ///   elementId (long, obligatorio)
    ///   parameterName (string, obligatorio) -- nombre EXACTO del parámetro, tal
    ///     como lo devuelve get_element_by_id (sin el prefijo "Tipo: " aunque se
    ///     esté escribiendo a nivel tipo).
    ///   value (string, obligatorio) -- se convierte internamente según el
    ///     StorageType real del parámetro (texto, entero o número).
    ///   level (string, opcional, default "instance") -- "instance", "type" o
    ///     "auto" (intenta instancia primero, si no existe cae a tipo).
    ///   unit (string, opcional) -- OBLIGATORIO si el parámetro es Double con
    ///     una unidad real (Longitud/Área/Volumen...), ej. "m", "cm", "mm",
    ///     "m2", "m3". Ignorado para texto, enteros y números sin unidad
    ///     (ej. Avance). Si falta y el parámetro lo requiere, la escritura
    ///     falla con un mensaje claro en vez de adivinar.
    /// </summary>
    public class UpdateElementParameterCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private UpdateElementParameterEventHandler _handler => (UpdateElementParameterEventHandler)Handler;

        public override string CommandName => "update_element_parameter";

        public UpdateElementParameterCommand(UIApplication uiApp)
            : base(new UpdateElementParameterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    JObject data = parameters?["data"] as JObject ?? parameters;

                    long? elementId = data?["elementId"]?.Value<long>();
                    if (elementId == null)
                        throw new ArgumentException("El parámetro 'elementId' es obligatorio");

                    string parameterName = data?["parameterName"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(parameterName))
                        throw new ArgumentException("El parámetro 'parameterName' es obligatorio");

                    string value = data?["value"]?.Value<string>();
                    if (value == null)
                        throw new ArgumentException("El parámetro 'value' es obligatorio");

                    _handler.TargetElementId = elementId.Value;
                    _handler.ParameterName = parameterName;
                    _handler.Value = value;

                    string level = data?["level"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(level))
                        _handler.Level = level;

                    string unit = data?["unit"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(unit))
                        _handler.Unit = unit;

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    else
                    {
                        throw new TimeoutException("Escritura de parámetro agotó el tiempo de espera");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"update_element_parameter falló: {ex.Message}");
                }
            }
        }
    }
}

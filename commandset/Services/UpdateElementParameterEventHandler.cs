using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Escribe un único parámetro (de instancia o de tipo) en un elemento por
    /// su ElementId. A diferencia del OperateElementEventHandler (que solo
    /// cubre acciones gráficas: select/color/hide/etc.), este handler escribe
    /// valores de parámetro reales, y es explícito sobre si el parámetro
    /// encontrado vive en la instancia o en el tipo -- porque escribir a
    /// nivel Tipo afecta a TODAS las instancias que comparten ese tipo, no
    /// solo al elemento consultado (ver ParameterUpdateInfo.SharedInstanceCount).
    ///
    /// Agregado 2026-10-30 a pedido de Carlos: varias familias del proyecto
    /// (ej. "Plantilla eje A", antes de convertir sus parámetros a instancia)
    /// solo tenían ciertos campos de gestión (Estado, Avance, etc.) a nivel
    /// Tipo, y la única herramienta de escritura existente en el stack de
    /// Solken (revit_actualizar_parametros_elemento) solo sabía escribir a
    /// nivel instancia -- cualquier parámetro de Tipo fallaba con
    /// NO_ENCONTRADO:&lt;nombre&gt;. Esta herramienta cierra ese hueco de forma
    /// nativa (sin recurrir a send_code_to_revit, que ejecuta C# arbitrario).
    /// </summary>
    public class UpdateElementParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // Entrada
        public long TargetElementId { get; set; }
        public string ParameterName { get; set; }
        public string Value { get; set; }

        /// <summary>"instance" (default), "type", o "auto" (instancia primero, si no existe cae a tipo).</summary>
        public string Level { get; set; } = "instance";

        // Salida
        public AIResult<ParameterUpdateInfo> Result { get; private set; }

        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;

#if REVIT2024_OR_GREATER
                var id = new ElementId(TargetElementId);
#else
                var id = new ElementId((int)TargetElementId);
#endif
                Element element = doc.GetElement(id);

                if (element == null)
                {
                    Result = new AIResult<ParameterUpdateInfo>
                    {
                        Success = false,
                        Message = $"ELEMENTO_NO_ENCONTRADO: no existe ningún elemento con ElementId {TargetElementId} en el documento activo."
                    };
                    return;
                }

                string level = (Level ?? "instance").Trim().ToLowerInvariant();
                if (level != "instance" && level != "type" && level != "auto")
                {
                    Result = new AIResult<ParameterUpdateInfo>
                    {
                        Success = false,
                        Message = $"NIVEL_INVALIDO: '{Level}' -- usar 'instance', 'type' o 'auto'."
                    };
                    return;
                }

                Parameter param = null;
                string resolvedLevel = null;
                Element typeElement = null;

                if (level == "instance" || level == "auto")
                {
                    param = element.LookupParameter(ParameterName);
                    if (param != null)
                        resolvedLevel = "instance";
                }

                if (param == null && (level == "type" || level == "auto"))
                {
                    ElementId typeId = element.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        typeElement = doc.GetElement(typeId);
                        if (typeElement != null)
                        {
                            param = typeElement.LookupParameter(ParameterName);
                            if (param != null)
                                resolvedLevel = "type";
                        }
                    }
                }

                if (param == null)
                {
                    string dondeSeBusco = level == "auto" ? "ni en instancia ni en tipo" : $"a nivel {level}";
                    Result = new AIResult<ParameterUpdateInfo>
                    {
                        Success = false,
                        Message = $"NO_ENCONTRADO:{ParameterName} -- no se encontró el parámetro '{ParameterName}' {dondeSeBusco} del elemento {TargetElementId}."
                    };
                    return;
                }

                if (param.IsReadOnly)
                {
                    Result = new AIResult<ParameterUpdateInfo>
                    {
                        Success = false,
                        Message = $"NO_ESCRIBIBLE:{ParameterName} -- el parámetro existe a nivel {resolvedLevel} pero es de solo lectura (probablemente calculado por Revit)."
                    };
                    return;
                }

                string oldValue = GetParameterValueAsString(param);

                int? sharedInstanceCount = null;
                if (resolvedLevel == "type" && typeElement != null)
                {
                    sharedInstanceCount = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.GetTypeId() == typeElement.Id)
                        .Count();
                }

                using (Transaction trans = new Transaction(doc, $"Actualizar parámetro {ParameterName}"))
                {
                    trans.Start();
                    SetParameterValue(param, Value);
                    trans.Commit();
                }

                string newValue = GetParameterValueAsString(param);

                Result = new AIResult<ParameterUpdateInfo>
                {
                    Success = true,
                    Message = resolvedLevel == "type"
                        ? $"Escrito a nivel TIPO -- afecta a {sharedInstanceCount} instancia(s) que comparten ese tipo, no solo a {TargetElementId}."
                        : "Escrito a nivel instancia -- solo afecta a este elemento.",
                    Response = new ParameterUpdateInfo
                    {
                        ElementId = TargetElementId,
                        ParameterName = ParameterName,
                        Level = resolvedLevel,
                        OldValue = oldValue,
                        NewValue = newValue,
                        SharedInstanceCount = sharedInstanceCount
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<ParameterUpdateInfo>
                {
                    Success = false,
                    Message = $"update_element_parameter falló: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Escribe el valor según el StorageType real del parámetro. Deliberadamente
        /// NO intenta conversión de unidades para parámetros Double de tipo
        /// Length/Area/Volume (ej. una cota en metros) -- esos deben seguir
        /// escribiéndose con send_code_to_revit hasta que se agregue soporte
        /// explícito de unidades aquí. Los parámetros Double "Número" sin unidad
        /// (como Avance, 0-1) sí son seguros con Set(double) directo.
        /// </summary>
        private static void SetParameterValue(Parameter param, string rawValue)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(rawValue);
                    break;

                case StorageType.Integer:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                        throw new Exception($"'{rawValue}' no es un entero válido para este parámetro.");
                    param.Set(intValue);
                    break;

                case StorageType.Double:
                    if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                        throw new Exception($"'{rawValue}' no es un número válido para este parámetro.");
                    param.Set(doubleValue);
                    break;

                case StorageType.ElementId:
                    throw new Exception("Escribir parámetros de tipo ElementId (referencias a otro elemento) no está soportado por esta herramienta.");

                default:
                    throw new Exception($"Tipo de almacenamiento de parámetro no soportado: {param.StorageType}");
            }
        }

        private static string GetParameterValueAsString(Parameter param)
        {
            if (!param.HasValue)
                return null;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();
                case StorageType.Integer:
                    return param.AsValueString() ?? param.AsInteger().ToString();
                case StorageType.Double:
                    return param.AsValueString() ?? param.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    return param.AsValueString();
                default:
                    return null;
            }
        }

        public string GetName()
        {
            return "Actualizar parámetro de elemento (instancia o tipo)";
        }
    }
}

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Versión en lote de UpdateElementParameterEventHandler: escribe varios
    /// (elemento, parámetro, valor) en UNA SOLA transacción de Revit -- todo o
    /// nada. Agregada 2026-10-30 porque la escritura real de las 12 zapatas
    /// del frente 5-7 el mismo día necesitó 24 llamadas individuales (12
    /// elementos x 2 parámetros), cada una en su propia transacción; si el
    /// elemento 8 de 12 hubiera fallado, los 7 anteriores habrían quedado
    /// escritos a medias sin que hubiera forma de revertirlos automáticamente.
    ///
    /// Estrategia en dos fases:
    ///   Fase 1 (validación, SIN transacción): resuelve elemento + parámetro
    ///     para cada item del lote, exactamente igual que la versión de un
    ///     solo elemento (instance/type/auto). Si CUALQUIER item falla aquí
    ///     (elemento no existe, parámetro no encontrado, de solo lectura), se
    ///     aborta todo el lote sin escribir nada -- se listan todos los
    ///     errores encontrados de una vez, en vez de fallar uno por uno.
    ///   Fase 2 (escritura, UNA transacción): solo si la fase 1 pasó completa.
    ///     Si algo falla durante el Set() de algún item (valor no parseable,
    ///     error de la API de Revit), se hace rollback de la transacción
    ///     COMPLETA -- ningún item del lote queda escrito a medias.
    /// </summary>
    public class BatchUpdateElementParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<BatchParameterUpdateRequest> Requests { get; set; } = new List<BatchParameterUpdateRequest>();

        public List<BatchParameterUpdateResult> Result { get; private set; }
        public bool OverallSuccess { get; private set; }
        public string OverallMessage { get; private set; }

        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        private class ResolvedItem
        {
            public BatchParameterUpdateRequest Request;
            public Parameter Param;
            public string ResolvedLevel;
            public Element TypeElement; // solo si ResolvedLevel == "type"
            public string ErrorMessage; // null si se resolvió bien
        }

        public void Execute(UIApplication app)
        {
            var results = new List<BatchParameterUpdateResult>();

            try
            {
                var doc = app.ActiveUIDocument.Document;

                // --- Fase 1: resolver todo sin escribir nada ---
                var resolved = new List<ResolvedItem>();
                foreach (var req in Requests)
                {
                    resolved.Add(ResolveItem(doc, req));
                }

                var fallos = resolved.Where(r => r.ErrorMessage != null).ToList();
                if (fallos.Count > 0)
                {
                    foreach (var r in resolved)
                    {
                        results.Add(new BatchParameterUpdateResult
                        {
                            ElementId = r.Request.ElementId,
                            ParameterName = r.Request.ParameterName,
                            Success = r.ErrorMessage == null,
                            Message = r.ErrorMessage ?? "No escrito: el lote se abortó por otros items con error (ver los demás resultados)."
                        });
                    }

                    OverallSuccess = false;
                    OverallMessage = $"LOTE_ABORTADO: {fallos.Count} de {resolved.Count} item(s) fallaron en validación -- no se escribió NADA (ver el detalle de cada item).";
                    Result = results;
                    return;
                }

                // --- Fase 2: todos resolvieron bien -- escribir en una sola transacción ---
                var oldValues = resolved.ToDictionary(r => r, r => GetParameterValueAsString(r.Param));

                using (Transaction trans = new Transaction(doc, $"Actualizar {resolved.Count} parámetro(s) en lote"))
                {
                    trans.Start();
                    try
                    {
                        foreach (var r in resolved)
                        {
                            SetParameterValue(r.Param, r.Request.Value, r.Request.Unit);
                        }
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();

                        foreach (var r in resolved)
                        {
                            results.Add(new BatchParameterUpdateResult
                            {
                                ElementId = r.Request.ElementId,
                                ParameterName = r.Request.ParameterName,
                                Success = false,
                                Message = $"LOTE_REVERTIDO: {ex.Message}"
                            });
                        }

                        OverallSuccess = false;
                        OverallMessage = $"LOTE_REVERTIDO: falló la escritura, ningún item quedó aplicado. Causa: {ex.Message}";
                        Result = results;
                        return;
                    }
                }

                foreach (var r in resolved)
                {
                    int? sharedInstanceCount = null;
                    if (r.ResolvedLevel == "type" && r.TypeElement != null)
                    {
                        sharedInstanceCount = new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .Where(e => e.GetTypeId() == r.TypeElement.Id)
                            .Count();
                    }

                    results.Add(new BatchParameterUpdateResult
                    {
                        ElementId = r.Request.ElementId,
                        ParameterName = r.Request.ParameterName,
                        Success = true,
                        Message = r.ResolvedLevel == "type"
                            ? $"Escrito a nivel TIPO -- afecta a {sharedInstanceCount} instancia(s)."
                            : "Escrito a nivel instancia.",
                        Detail = new ParameterUpdateInfo
                        {
                            ElementId = r.Request.ElementId,
                            ParameterName = r.Request.ParameterName,
                            Level = r.ResolvedLevel,
                            OldValue = oldValues[r],
                            NewValue = GetParameterValueAsString(r.Param),
                            SharedInstanceCount = sharedInstanceCount
                        }
                    });
                }

                OverallSuccess = true;
                OverallMessage = $"Lote completo: {results.Count} parámetro(s) escrito(s) en una sola transacción.";
                Result = results;
            }
            catch (Exception ex)
            {
                OverallSuccess = false;
                OverallMessage = $"batch_update_element_parameters falló: {ex.Message}";
                Result = results;
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private ResolvedItem ResolveItem(Document doc, BatchParameterUpdateRequest req)
        {
            var item = new ResolvedItem { Request = req };

#if REVIT2024_OR_GREATER
            var id = new ElementId(req.ElementId);
#else
            var id = new ElementId((int)req.ElementId);
#endif
            Element element = doc.GetElement(id);
            if (element == null)
            {
                item.ErrorMessage = $"ELEMENTO_NO_ENCONTRADO: no existe ningún elemento con ElementId {req.ElementId}.";
                return item;
            }

            string level = (req.Level ?? "instance").Trim().ToLowerInvariant();
            if (level != "instance" && level != "type" && level != "auto")
            {
                item.ErrorMessage = $"NIVEL_INVALIDO: '{req.Level}' -- usar 'instance', 'type' o 'auto'.";
                return item;
            }

            Parameter param = null;
            string resolvedLevel = null;
            Element typeElement = null;

            if (level == "instance" || level == "auto")
            {
                param = element.LookupParameter(req.ParameterName);
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
                        param = typeElement.LookupParameter(req.ParameterName);
                        if (param != null)
                            resolvedLevel = "type";
                    }
                }
            }

            if (param == null)
            {
                string dondeSeBusco = level == "auto" ? "ni en instancia ni en tipo" : $"a nivel {level}";
                item.ErrorMessage = $"NO_ENCONTRADO:{req.ParameterName} -- no se encontró en el elemento {req.ElementId} {dondeSeBusco}.";
                return item;
            }

            if (param.IsReadOnly)
            {
                item.ErrorMessage = $"NO_ESCRIBIBLE:{req.ParameterName} -- el parámetro existe a nivel {resolvedLevel} pero es de solo lectura.";
                return item;
            }

            item.Param = param;
            item.ResolvedLevel = resolvedLevel;
            item.TypeElement = typeElement;
            return item;
        }

        private static void SetParameterValue(Parameter param, string rawValue, string unit)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(rawValue);
                    break;
                case StorageType.Integer:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                        throw new Exception($"'{rawValue}' no es un entero válido.");
                    param.Set(intValue);
                    break;
                case StorageType.Double:
                    if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                        throw new Exception($"'{rawValue}' no es un número válido.");

                    if (ParameterValueConverter.RequiresUnitConversion(param))
                        doubleValue = ParameterValueConverter.ConvertToInternal(param, doubleValue, unit);

                    param.Set(doubleValue);
                    break;
                case StorageType.ElementId:
                    throw new Exception("Escribir parámetros ElementId no está soportado.");
                default:
                    throw new Exception($"StorageType no soportado: {param.StorageType}");
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
            return "Actualizar parámetros en lote (una transacción)";
        }
    }
}

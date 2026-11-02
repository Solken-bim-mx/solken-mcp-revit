using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Versión en lote de GetElementByIdEventHandler: lee varios elementos por
    /// su ElementId en una sola llamada, en vez de una llamada por elemento.
    /// Agregada 2026-10-30 porque auditorías como la del frente 5-7 (14
    /// elementos con IDs ya conocidos) no deberían necesitar 14 llamadas ni
    /// depender de qué esté seleccionado en pantalla.
    ///
    /// Reutiliza EXACTAMENTE la misma lógica de extracción de parámetros que
    /// GetElementByIdEventHandler (instancia + tipo con prefijo "Tipo: "),
    /// para que el formato de salida sea idéntico entre ambas herramientas.
    /// </summary>
    public class GetElementsByIdsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<long> TargetElementIds { get; set; } = new List<long>();
        public bool IncludeTypeParameters { get; set; } = true;

        /// <summary>
        /// Un ElementInfo por cada Id solicitado, en el mismo orden. null si
        /// ese ElementId no existe en el documento activo -- nunca se omite
        /// una posición, para que el índice del resultado siga correspondiendo
        /// al índice del Id solicitado.
        /// </summary>
        public List<ElementInfo> Result { get; private set; }

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
                var results = new List<ElementInfo>();

                foreach (long targetId in TargetElementIds)
                {
#if REVIT2024_OR_GREATER
                    var id = new ElementId(targetId);
#else
                    var id = new ElementId((int)targetId);
#endif
                    Element element = doc.GetElement(id);

                    if (element == null)
                    {
                        results.Add(null);
                        continue;
                    }

                    results.Add(new ElementInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = element.Id.Value,
#else
                        Id = element.Id.IntegerValue,
#endif
                        UniqueId = element.UniqueId,
                        Name = element.Name,
                        Category = element.Category?.Name,
                        Properties = GetElementProperties(doc, element)
                    });
                }

                Result = results;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "get_elements_by_ids falló: " + ex.Message);
                Result = null;
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private Dictionary<string, string> GetElementProperties(Document doc, Element element)
        {
            var result = new Dictionary<string, string>();

            AddParameters(element.Parameters, result);

            if (IncludeTypeParameters)
            {
                try
                {
                    ElementId typeId = element.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        Element elementType = doc.GetElement(typeId);
                        if (elementType != null)
                        {
                            AddParameters(elementType.Parameters, result, prefix: "Tipo: ");
                        }
                    }
                }
                catch
                {
                    // Si falla la lectura de parámetros de tipo, seguimos solo con los de instancia
                }
            }

            return result;
        }

        private void AddParameters(ParameterSet parameters, Dictionary<string, string> target, string prefix = "")
        {
            foreach (Parameter param in parameters)
            {
                try
                {
                    if (param == null || param.Definition == null)
                        continue;

                    string paramName = param.Definition.Name;
                    string value = GetParameterValueAsString(param);

                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    string key = prefix + paramName;

                    if (!target.ContainsKey(key))
                    {
                        target[key] = value;
                    }
                }
                catch
                {
                    // Si un parámetro puntual falla, lo saltamos sin romper el resto
                }
            }
        }

        private string GetParameterValueAsString(Parameter param)
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
                    return param.AsValueString() ?? param.AsDouble().ToString();
                case StorageType.ElementId:
                    return param.AsValueString();
                default:
                    return null;
            }
        }

        public string GetName()
        {
            return "Leer varios elementos por ElementId (lote)";
        }
    }
}

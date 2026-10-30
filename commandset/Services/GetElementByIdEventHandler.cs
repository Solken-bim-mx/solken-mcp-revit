using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Lee un elemento arbitrario de Revit por su ElementId. Reutiliza la misma
    /// lógica de extracción de parámetros (instancia + tipo) que
    /// GetSelectedElementsEventHandler, para mantener el mismo formato de salida
    /// en ambas herramientas y no duplicar el criterio de qué parámetros exponer.
    /// </summary>
    public class GetElementByIdEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // Entrada
        // Nombrado TargetElementId (no "ElementId") a propósito: un miembro
        // llamado igual que el tipo Autodesk.Revit.DB.ElementId lo sombrea
        // dentro de esta clase, y rompe referencias como "ElementId.InvalidElementId".
        public long TargetElementId { get; set; }
        public bool IncludeTypeParameters { get; set; } = true;
        public List<string> ParameterNames { get; set; }

        // Salida (null si el ElementId no existe en el documento activo)
        public ElementInfo Result { get; private set; }

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
                    Result = null;
                }
                else
                {
                    Result = new ElementInfo
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
                    };
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "get_element_by_id falló: " + ex.Message);
                Result = null;
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Extrae parámetros de instancia (y opcionalmente de tipo) de un elemento
        /// y los devuelve como diccionario nombre->valor en texto. Idéntico al
        /// método homónimo de GetSelectedElementsEventHandler.
        /// </summary>
        private Dictionary<string, string> GetElementProperties(Document doc, Element element)
        {
            var result = new Dictionary<string, string>();

            // --- Parámetros de INSTANCIA ---
            AddParameters(element.Parameters, result);

            // --- Parámetros de TIPO ---
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

                    if (ParameterNames != null && ParameterNames.Count > 0 &&
                        !ParameterNames.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                        continue;

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
            return "Leer elemento por ElementId";
        }
    }
}

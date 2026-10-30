using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class GetSelectedElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // 执行结果
        public List<Models.Common.ElementInfo> ResultElements { get; private set; }
        // 状态同步对象
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        // 限制返回的元素数量
        public int? Limit { get; set; }

        // Lista opcional de nombres de parámetros a incluir (instancia y/o tipo).
        // Si es null o vacía, se incluyen todos los parámetros con valor no vacío.
        public List<string> ParameterNames { get; set; }

        // Si es true, también se leen los parámetros del ElementType asociado
        // (donde suelen vivir "Marca de tipo", comentarios de tipo, etc.)
        public bool IncludeTypeParameters { get; set; } = true;

        // 实现IWaitableExternalEventHandler接口
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;

                // 获取当前选中的元素
                var selectedIds = uiDoc.Selection.GetElementIds();
                var selectedElements = selectedIds.Select(id => doc.GetElement(id)).ToList();

                // 应用数量限制
                if (Limit.HasValue && Limit.Value > 0)
                {
                    selectedElements = selectedElements.Take(Limit.Value).ToList();
                }

                // 转换为ElementInfo列表
                ResultElements = selectedElements.Select(element => new ElementInfo
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
                }).ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "获取选中元素失败: " + ex.Message);
                ResultElements = new List<Models.Common.ElementInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Extrae parámetros de instancia (y opcionalmente de tipo) de un elemento
        /// y los devuelve como diccionario nombre->valor en texto.
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

        /// <summary>
        /// Vuelca un ParameterSet dentro del diccionario destino, aplicando
        /// el filtro ParameterNames si fue especificado.
        /// </summary>
        private void AddParameters(ParameterSet parameters, Dictionary<string, string> target, string prefix = "")
        {
            foreach (Parameter param in parameters)
            {
                try
                {
                    if (param == null || param.Definition == null)
                        continue;

                    string paramName = param.Definition.Name;

                    // Filtro opcional por nombre de parámetro
                    if (ParameterNames != null && ParameterNames.Count > 0 &&
                        !ParameterNames.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    string value = GetParameterValueAsString(param);

                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    string key = prefix + paramName;

                    // Evitar sobrescribir si ya existe una clave igual (ej. mismo nombre en instancia y tipo)
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

        /// <summary>
        /// Convierte el valor de un Parameter a texto según su StorageType.
        /// </summary>
        private string GetParameterValueAsString(Parameter param)
        {
            if (!param.HasValue)
                return null;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();

                case StorageType.Integer:
                    // Los parámetros Yes/No también son Integer (0/1)
                    return param.AsValueString() ?? param.AsInteger().ToString();

                case StorageType.Double:
                    // AsValueString() devuelve el valor ya formateado con las unidades
                    // del proyecto (ej. "3.786 m"), más útil que el double crudo interno.
                    return param.AsValueString() ?? param.AsDouble().ToString();

                case StorageType.ElementId:
                    return param.AsValueString();

                default:
                    return null;
            }
        }

        public string GetName()
        {
            return "获取选中元素";
        }
    }
}
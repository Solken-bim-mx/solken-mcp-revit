using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Handler para get_project_views: lista las vistas del documento activo,
    /// por defecto solo ViewType.Schedule (tablas de planificación y
    /// cantidades), tal como las ve el usuario en el Explorador de proyecto.
    ///
    /// Resuelve la limitante documentada: Claude no podía contar/enumerar el
    /// contenido de esa carpeta del Explorador de proyecto sin que el usuario
    /// lo hiciera manualmente.
    /// </summary>
    public class GetProjectViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private Document doc => uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public ProjectViewsQuery Query { get; private set; }
        public AIResult<List<ProjectViewInfo>> Result { get; private set; }

        public void SetParameters(ProjectViewsQuery query)
        {
            Query = query;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;
            try
            {
                var query = Query ?? new ProjectViewsQuery();

                var allViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate || query.IncludeTemplates);

                IEnumerable<View> filtered;
                switch ((query.ViewTypeFilter ?? "schedule").ToLowerInvariant())
                {
                    case "sheet":
                        filtered = allViews.Where(v => v.ViewType == ViewType.DrawingSheet);
                        break;
                    case "plan":
                        filtered = allViews.Where(v =>
                            v.ViewType == ViewType.FloorPlan ||
                            v.ViewType == ViewType.CeilingPlan ||
                            v.ViewType == ViewType.EngineeringPlan);
                        break;
                    case "all":
                        filtered = allViews;
                        break;
                    default:
                        filtered = allViews.Where(v => v.ViewType == ViewType.Schedule);
                        break;
                }

                // Las tablas de revisión de llaves (Key Schedule) y de revisión
                // de materiales internas de Revit no son navegables como
                // contenido real -- se excluyen para que el conteo coincida con
                // lo que el usuario ve a simple vista en esa carpeta.
                filtered = filtered.Where(v =>
                    !(v is ViewSchedule vs &&
                      (vs.IsTitleblockRevisionSchedule || vs.Definition == null)));

                if (!string.IsNullOrWhiteSpace(query.NameContains))
                {
                    string needle = query.NameContains.Trim();
                    filtered = filtered.Where(v =>
                        v.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var list = filtered
                    .Select(v => new ProjectViewInfo
                    {
                        Id = v.Id.IntegerValue,
                        UniqueId = v.UniqueId,
                        Name = v.Name,
                        ViewType = v.ViewType.ToString(),
                        BrowserFolder = GetBrowserFolder(v),
                        IsTemplate = v.IsTemplate
                    })
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Result = new AIResult<List<ProjectViewInfo>>
                {
                    Success = true,
                    Message = $"Se encontraron {list.Count} vista(s).",
                    Response = list
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<ProjectViewInfo>>
                {
                    Success = false,
                    Message = $"Error al listar vistas: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Intenta leer la carpeta de organización del Explorador de proyecto
        /// (folder browser organization). Devuelve cadena vacía si el modelo no
        /// tiene esa organización configurada -- no todos los proyectos la usan.
        /// </summary>
        private static string GetBrowserFolder(View v)
        {
            try
            {
                Parameter p = v.LookupParameter("Folder");
                if (p != null && p.HasValue && p.StorageType == StorageType.String)
                    return p.AsString();
            }
            catch
            {
                // Ignorado a propósito: la organización por carpetas es opcional
                // y su ausencia no debe romper el listado completo.
            }
            return string.Empty;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Listar vistas del proyecto";
        }
    }
}

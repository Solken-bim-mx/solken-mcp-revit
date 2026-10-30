using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Structure
{
    /// <summary>
    /// Parámetros de entrada para get_project_views.
    /// </summary>
    public class ProjectViewsQuery
    {
        /// <summary>
        /// Filtro opcional por tipo: "schedule" (tablas de planificación/cantidades,
        /// default), "sheet" (hojas), "plan" (plantas), "all" (todas las vistas).
        /// </summary>
        public string ViewTypeFilter { get; set; } = "schedule";

        /// <summary>
        /// Filtro opcional de texto (case-insensitive, coincidencia parcial) contra
        /// el nombre de la vista/tabla. Null o vacío = sin filtro.
        /// </summary>
        public string NameContains { get; set; }

        /// <summary>
        /// Si es true, incluye plantillas de vista (ViewTemplate). Default: false,
        /// porque las plantillas no son tablas reales que el usuario vea en el
        /// Explorador de proyecto como contenido navegable.
        /// </summary>
        public bool IncludeTemplates { get; set; } = false;
    }

    /// <summary>
    /// Resultado por cada vista/tabla encontrada.
    /// </summary>
    public class ProjectViewInfo
    {
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Tipo real de Revit: Schedule, DrawingSheet, FloorPlan, etc.
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// Carpeta del Explorador de proyecto donde vive, si Revit expone esa
        /// agrupación (browser organization). Puede venir vacío si el modelo no
        /// tiene organización por carpetas configurada.
        /// </summary>
        public string BrowserFolder { get; set; }

        public bool IsTemplate { get; set; }
    }
}

using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Structure
{
    /// <summary>
    /// Parámetros de entrada para get_schedule_data.
    /// </summary>
    public class ScheduleDataQuery
    {
        /// <summary>
        /// ElementId(s) de la(s) ViewSchedule a leer (obtenidos de
        /// get_project_views). Se puede pedir más de una tabla en la misma
        /// llamada.
        /// </summary>
        public List<int> ScheduleViewIds { get; set; } = new List<int>();

        /// <summary>
        /// Si es true (default), incluye la fila de encabezados (nombres de
        /// columna) como el primer elemento de "Headers". Si la tabla no
        /// tiene sección de encabezado separada, se omite sin error.
        /// </summary>
        public bool IncludeHeaders { get; set; } = true;

        /// <summary>
        /// Límite opcional de filas de cuerpo a devolver por tabla (para
        /// tablas muy largas). Null = todas las filas.
        /// </summary>
        public int? MaxRows { get; set; }
    }

    /// <summary>
    /// Resultado por cada tabla leída.
    /// </summary>
    public class ScheduleDataResult
    {
        public int ViewId { get; set; }
        public string ViewName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Nombres de columna, en orden, tal como los ve el usuario en Revit
        /// (respeta encabezados personalizados/agrupados cuando es posible).
        /// </summary>
        public List<string> Headers { get; set; } = new List<string>();

        /// <summary>
        /// Filas del cuerpo de la tabla. Cada fila es un array de strings ya
        /// formateados tal como Revit los muestra (incluye totales/unidades
        /// cuando la tabla los formatea así) -- se lee con GetCellText, no
        /// con los valores crudos, para que coincida exactamente con lo que
        /// el usuario ve en pantalla.
        /// </summary>
        public List<List<string>> Rows { get; set; } = new List<List<string>>();

        public int RowCount { get; set; }
    }
}

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Structure;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Handler para get_schedule_data: extrae encabezados y filas del cuerpo
    /// de una o varias ViewSchedule (tablas de planificación y cantidades)
    /// vía ViewSchedule.GetTableData(), sin necesitar que la tabla esté
    /// activa en pantalla ni que el usuario cambie de vista.
    ///
    /// LIMITACIÓN CONOCIDA, sin resolver (investigado 2026-11-02): en filas
    /// de instancia individual (no fusionadas), las columnas de parámetro
    /// COMPARTIDO (Clave OPUS, Avance, Estado, Frente, Intersección,
    /// Resistencia, Nivel) devuelven "" pese a que Revit sí muestra su texto
    /// en pantalla -- confirmado comparando contra una captura real. Los
    /// campos nativos/calculados (Volumen, Acero) siempre funcionan. Se
    /// descartaron dos hipótesis: (a) caché de tabla sin refrescar --
    /// ViewSchedule.RefreshData() no cambia el resultado (se deja la llamada
    /// de todos modos, es inofensiva); (b) la tabla necesita ser la vista
    /// activa -- forzar UIDocument.ActiveView tampoco cambió el resultado
    /// (se descartó ese intento por completo, generaba parpadeo visual sin
    /// beneficio). Pendiente: investigar si hace falta resolver el valor vía
    /// el parámetro real de la celda (algo como GetCellParamId) en vez de
    /// GetCellText puro.
    ///
    /// Complementa get_project_views: esa herramienta enumera QUÉ tablas
    /// existen; esta lee su CONTENIDO real (filas de cantidades).
    /// </summary>
    public class GetScheduleDataEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private Document doc => uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public ScheduleDataQuery Query { get; private set; }
        public AIResult<List<ScheduleDataResult>> Result { get; private set; }

        public void SetParameters(ScheduleDataQuery query)
        {
            Query = query;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;
            try
            {
                var query = Query ?? new ScheduleDataQuery();
                var results = new List<ScheduleDataResult>();

                if (query.ScheduleViewIds == null || query.ScheduleViewIds.Count == 0)
                {
                    throw new ArgumentException("Debe indicar al menos un scheduleViewId a leer.");
                }

                foreach (int idInt in query.ScheduleViewIds)
                {
                    var itemResult = new ScheduleDataResult { ViewId = idInt };

                    try
                    {
                        ElementId viewId = new ElementId(idInt);
                        Element el = doc.GetElement(viewId);
                        ViewSchedule schedule = el as ViewSchedule;

                        if (schedule == null)
                        {
                            itemResult.Success = false;
                            itemResult.ErrorMessage = "El elemento no existe o no es una ViewSchedule.";
                            results.Add(itemResult);
                            continue;
                        }

                        itemResult.ViewName = schedule.Name;

                        try
                        {
                            schedule.RefreshData();
                        }
                        catch
                        {
                            // Si el refresh falla (p.ej. API más vieja sin este
                            // método, o la tabla no lo necesita), seguimos con
                            // los datos ya cacheados -- no es un error fatal.
                        }

                        TableData tableData = schedule.GetTableData();

                        // La sección Header suele traer los nombres de columna
                        // tal como se ven en Revit. No todas las tablas tienen
                        // una sección Header separada -- si no existe, se
                        // continúa sin encabezados (no es un error).
                        if (query.IncludeHeaders)
                        {
                            try
                            {
                                TableSectionData headerSection =
                                    tableData.GetSectionData(SectionType.Header);
                                if (headerSection != null && headerSection.NumberOfRows > 0)
                                {
                                    int lastHeaderRow = headerSection.NumberOfRows - 1;
                                    for (int c = 0; c < headerSection.NumberOfColumns; c++)
                                    {
                                        itemResult.Headers.Add(
                                            headerSection.GetCellText(lastHeaderRow, c));
                                    }
                                }
                            }
                            catch
                            {
                                // Sin sección Header separada -- se omite,
                                // no es un error de la consulta.
                            }
                        }

                        TableSectionData bodySection =
                            tableData.GetSectionData(SectionType.Body);

                        if (bodySection != null)
                        {
                            int rowLimit = query.MaxRows.HasValue
                                ? Math.Min(query.MaxRows.Value, bodySection.NumberOfRows)
                                : bodySection.NumberOfRows;

                            // Bug real encontrado 2026-10-29/30 y arreglado 2026-11-02:
                            // en tablas con celdas fusionadas (filas de grupo por
                            // tipo/categoría), GetCellText(r, c) solo devuelve texto
                            // en la celda superior-izquierda de cada grupo fusionado
                            // -- el resto de las celdas del mismo grupo devuelven "",
                            // aunque visualmente en Revit se vea el texto repetido/
                            // heredado en todas ellas. Esto hacía que TODAS las
                            // columnas de texto (Avance, Estado, Clave OPUS, etc.)
                            // salieran vacías en tablas agrupadas, dejando solo la
                            // columna numérica de cantidad/volumen legible.
                            for (int r = 0; r < rowLimit; r++)
                            {
                                var row = new List<string>();
                                for (int c = 0; c < bodySection.NumberOfColumns; c++)
                                {
                                    row.Add(GetEffectiveCellText(bodySection, r, c));
                                }
                                itemResult.Rows.Add(row);
                            }
                        }

                        itemResult.RowCount = itemResult.Rows.Count;
                        itemResult.Success = true;
                    }
                    catch (Exception exItem)
                    {
                        itemResult.Success = false;
                        itemResult.ErrorMessage = exItem.Message;
                    }

                    results.Add(itemResult);
                }

                Result = new AIResult<List<ScheduleDataResult>>
                {
                    Success = true,
                    Message = $"Se leyeron {results.Count} tabla(s), " +
                              $"{results.FindAll(r => r.Success).Count} exitosamente.",
                    Response = results
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<ScheduleDataResult>>
                {
                    Success = false,
                    Message = $"Error al leer datos de tablas: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Lee el texto real de una celda, redirigiendo a la celda
        /// superior-izquierda de su grupo si está fusionada.
        /// TableSectionData.GetMergedCell(row, col) devuelve DIRECTAMENTE la
        /// región fusionada que contiene esa celda (Top/Bottom/Left/Right) --
        /// para una celda no fusionada, la región es ella misma (Top==Bottom==row,
        /// Left==Right==col), así que llamar siempre a GetCellText(merged.Top,
        /// merged.Left) es correcto en ambos casos sin necesitar una rama aparte.
        /// </summary>
        private static string GetEffectiveCellText(TableSectionData section, int row, int col)
        {
            try
            {
                TableMergedCell merged = section.GetMergedCell(row, col);
                return section.GetCellText(merged.Top, merged.Left);
            }
            catch
            {
                // Si la API de fusión falla por algún motivo, no perder la
                // lectura entera -- caer al comportamiento anterior para esta celda.
                return section.GetCellText(row, col);
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Leer datos de tabla de planificación";
        }
    }
}

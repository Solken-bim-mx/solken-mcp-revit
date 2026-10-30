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

                            for (int r = 0; r < rowLimit; r++)
                            {
                                var row = new List<string>();
                                for (int c = 0; c < bodySection.NumberOfColumns; c++)
                                {
                                    row.Add(bodySection.GetCellText(r, c));
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

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
    /// Handler para get_rebar_host_data: dado un host (zapata, contratrabe,
    /// castillo), devuelve las barras de armado que Revit reporta como
    /// colocadas dentro de el, usando la relacion interna real
    /// (RebarHostData), no bounding box ni cercania espacial.
    ///
    /// Esto resuelve la limitante documentada: OST_Rebar no se puede
    /// filtrar por elemento anfitrion via ai_element_filter, y el
    /// bounding box de barras dobladas/con ganchos no es confiable.
    /// </summary>
    public class GetRebarHostDataEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private Document doc => uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public RebarHostQuery Query { get; private set; }
        public AIResult<List<RebarHostResult>> Result { get; private set; }

        public void SetParameters(RebarHostQuery query)
        {
            Query = query;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;
            try
            {
                if (Query == null || Query.HostElementIds == null || !Query.HostElementIds.Any())
                    throw new ArgumentException("Debe indicar al menos un hostElementId a consultar.");

                var results = new List<RebarHostResult>();

                foreach (int idInt in Query.HostElementIds)
                {
                    ElementId hostId = new ElementId(idInt);
                    Element hostElement = doc.GetElement(hostId);

                    if (hostElement == null)
                    {
                        results.Add(new RebarHostResult
                        {
                            HostElementId = idInt,
                            HostElementName = "(elemento no encontrado)",
                            HasRebarHostData = false
                        });
                        continue;
                    }

                    var hostResult = new RebarHostResult
                    {
                        HostElementId = idInt,
                        HostElementName = hostElement.Name,
                        HostCategory = hostElement.Category?.Name
                    };

                    // RebarHostData.GetRebarHostData(Element) solo aplica a
                    // elementos que pueden alojar armado (cimentaciones,
                    // muros, losas, columnas/vigas estructurales, etc.).
                    // Si el host no aplica, Revit devuelve null -- no es
                    // un error, simplemente no hay relacion de armado ahi.
                    RebarHostData hostData = RebarHostData.GetRebarHostData(hostElement);

                    if (hostData == null)
                    {
                        hostResult.HasRebarHostData = false;
                        results.Add(hostResult);
                        continue;
                    }

                    hostResult.HasRebarHostData = true;

                    // CONFIRMADO (2026-08-05, IntelliSense en Revit 2025):
                    // GetRebarsInHost() devuelve IList<Rebar> directo -- ya
                    // son los elementos Rebar reales, no hace falta volver
                    // a resolverlos con doc.GetElement.
                    IList<Rebar> rebars = hostData.GetRebarsInHost();

                    if (rebars != null)
                    {
                        foreach (Rebar rebar in rebars)
                        {
                            hostResult.RebarBars.Add(BuildRebarBarInfo(rebar));
                        }
                    }

                    results.Add(hostResult);
                }

                Result = new AIResult<List<RebarHostResult>>
                {
                    Success = true,
                    Message = $"Se consultaron {results.Count} elemento(s) anfitrion. " +
                              $"{results.Count(r => r.HasRebarHostData)} tienen armado colocado.",
                    Response = results
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<RebarHostResult>>
                {
                    Success = false,
                    Message = $"Error al consultar armado por anfitrion: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private RebarBarInfo BuildRebarBarInfo(Rebar rebar)
        {
            var info = new RebarBarInfo
            {
                Id = rebar.Id.IntegerValue,
                UniqueId = rebar.UniqueId,
                Name = rebar.Name
            };

            try
            {
                info.Quantity = rebar.NumberOfBarPositions;
            }
            catch { info.Quantity = 1; }

            try
            {
                RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                if (barType != null)
                {
                    info.BarTypeName = barType.Name;
                    // CONFIRMADO (2026-08-17, metadata real de RevitAPI.dll):
                    // la propiedad correcta es BarNominalDiameter (pies,
                    // unidades internas de Revit). Se convierte a mm con
                    // el factor 304.8 (1 pie = 304.8 mm).
                    info.BarDiameterMm = barType.BarNominalDiameter * 304.8;
                }
            }
            catch { info.BarDiameterMm = 0; }

            try
            {
                // CONFIRMADO (2026-08-17, metadata real de RevitAPI.dll):
                // Rebar no tiene GetLength()/GetCenterlineLength() -- la
                // propiedad correcta es TotalLength (pies, unidades
                // internas), que ya trae la longitud del SET COMPLETO
                // (todas las barras juntas). Se convierte a mm y se
                // divide entre Quantity para obtener la longitud de UNA
                // barra individual (trazabilidad por elemento).
                double totalLengthMm = rebar.TotalLength * 304.8;
                int qty = info.Quantity > 0 ? info.Quantity : 1;

                info.LengthMmTotal = totalLengthMm;
                info.LengthMm = totalLengthMm / qty;
            }
            catch
            {
                info.LengthMm = 0;
                info.LengthMmTotal = 0;
            }

            try
            {
                RebarShape shape = doc.GetElement(rebar.GetShapeId()) as RebarShape;
                info.ShapeName = shape?.Name;
            }
            catch { /* no todas las barras tienen RebarShape asociado (ej. barras libres) */ }

            return info;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Obtener armado por elemento anfitrion";
        }
    }
}
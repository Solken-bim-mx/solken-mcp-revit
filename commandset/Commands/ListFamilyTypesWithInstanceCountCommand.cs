using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// data.categoryName (string, obligatorio): BuiltInCategory de Revit, ej.
    ///   "OST_StructuralFoundation" (cimentación), "OST_Floors" (suelos/plantillas).
    /// data.nameContains (string, opcional): filtro por FamilyName o TypeName.
    /// </summary>
    public class ListFamilyTypesWithInstanceCountCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private ListFamilyTypesWithInstanceCountEventHandler _handler => (ListFamilyTypesWithInstanceCountEventHandler)Handler;

        public override string CommandName => "list_family_types_with_instance_count";

        public ListFamilyTypesWithInstanceCountCommand(UIApplication uiApp)
            : base(new ListFamilyTypesWithInstanceCountEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    JObject data = parameters?["data"] as JObject ?? parameters;

                    string categoryName = data?["categoryName"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(categoryName))
                        throw new ArgumentException("El parámetro 'categoryName' es obligatorio (ej. 'OST_StructuralFoundation')");

                    _handler.CategoryName = categoryName;
                    _handler.NameContains = data?["nameContains"]?.Value<string>();

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        if (_handler.ErrorMessage != null)
                        {
                            return new { Success = false, Message = _handler.ErrorMessage };
                        }
                        return new { Success = true, Message = $"{_handler.Result.Count} tipo(s) encontrado(s).", Response = _handler.Result };
                    }
                    else
                    {
                        throw new TimeoutException("Listado de tipos con conteo agotó el tiempo de espera");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"list_family_types_with_instance_count falló: {ex.Message}");
                }
            }
        }
    }
}

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
    /// Lista los tipos de familia (FamilySymbol/ElementType) de una categoría,
    /// con cuántas instancias colocadas tiene cada uno -- incluido 0.
    ///
    /// Agregada 2026-10-30: get_available_family_types (heredada del fork
    /// original) lista todos los tipos del CATÁLOGO del proyecto sin decir
    /// cuáles tienen alguna instancia colocada. El 2026-10-30 eso llevó a una
    /// cuenta equivocada (28 tipos "existentes" en zapatas de eje 6-7, cuando
    /// solo 14 tenían instancia real) porque varios eran tipos huérfanos
    /// creados y nunca usados. Esta herramienta hace la distinción explícita.
    /// </summary>
    public class ListFamilyTypesWithInstanceCountEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        /// <summary>Nombre del BuiltInCategory de Revit (ej. "OST_StructuralFoundation"). Obligatorio.</summary>
        public string CategoryName { get; set; }

        /// <summary>Filtro opcional, insensible a mayúsculas, sobre FamilyName o TypeName.</summary>
        public string NameContains { get; set; }

        public List<FamilyTypeInstanceCount> Result { get; private set; }
        public string ErrorMessage { get; private set; }

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

                if (!Enum.TryParse(CategoryName, true, out BuiltInCategory builtInCategory))
                {
                    ErrorMessage = $"CATEGORIA_INVALIDA: '{CategoryName}' no es un BuiltInCategory de Revit válido (ej. 'OST_StructuralFoundation', 'OST_Floors').";
                    Result = null;
                    return;
                }

                // Conteo de instancias por TypeId, limitado a la misma categoría
                var instanceCounts = new FilteredElementCollector(doc)
                    .OfCategory(builtInCategory)
                    .WhereElementIsNotElementType()
                    .GroupBy(e => e.GetTypeId())
                    .ToDictionary(g => g.Key, g => g.Count());

                var types = new FilteredElementCollector(doc)
                    .OfCategory(builtInCategory)
                    .WhereElementIsElementType()
                    .ToElements();

                var result = new List<FamilyTypeInstanceCount>();

                foreach (Element typeElement in types)
                {
                    string familyName = (typeElement as FamilySymbol)?.Family?.Name;
                    string typeName = typeElement.Name;

                    if (!string.IsNullOrWhiteSpace(NameContains))
                    {
                        bool matches =
                            (familyName != null && familyName.IndexOf(NameContains, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (typeName != null && typeName.IndexOf(NameContains, StringComparison.OrdinalIgnoreCase) >= 0);

                        if (!matches)
                            continue;
                    }

                    int count = instanceCounts.TryGetValue(typeElement.Id, out int c) ? c : 0;

                    result.Add(new FamilyTypeInstanceCount
                    {
#if REVIT2024_OR_GREATER
                        TypeId = typeElement.Id.Value,
#else
                        TypeId = typeElement.Id.IntegerValue,
#endif
                        FamilyName = familyName,
                        TypeName = typeName,
                        Category = typeElement.Category?.Name,
                        InstanceCount = count
                    });
                }

                Result = result.OrderByDescending(r => r.InstanceCount).ThenBy(r => r.TypeName).ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"list_family_types_with_instance_count falló: {ex.Message}";
                Result = null;
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Listar tipos de familia con conteo de instancias";
        }
    }
}

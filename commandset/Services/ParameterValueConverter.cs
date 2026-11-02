using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Convierte valores de texto a las unidades internas de Revit (pies,
    /// pies², pies³...) para parámetros Double que tienen una especificación
    /// de unidad REAL (Longitud, Área, Volumen...), a diferencia de un
    /// número "pelado" sin unidad (spec Number, como Avance 0-1, que no
    /// necesita conversión y nunca la tuvo).
    ///
    /// Agregado 2026-11-02: update_element_parameter y
    /// batch_update_element_parameters solo escribían el valor crudo tal
    /// cual, correcto para parámetros sin unidad pero equivocado para
    /// cualquier Double con unidad real (ej. escribir "2.5" en una cota
    /// pensando en metros, cuando Revit lo interpreta como 2.5 PIES).
    /// </summary>
    public static class ParameterValueConverter
    {
        private static readonly Dictionary<string, ForgeTypeId> UnitAliases = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m", UnitTypeId.Meters }, { "meters", UnitTypeId.Meters }, { "metros", UnitTypeId.Meters },
            { "mm", UnitTypeId.Millimeters }, { "millimeters", UnitTypeId.Millimeters }, { "milimetros", UnitTypeId.Millimeters },
            { "cm", UnitTypeId.Centimeters }, { "centimeters", UnitTypeId.Centimeters }, { "centimetros", UnitTypeId.Centimeters },
            { "ft", UnitTypeId.Feet }, { "feet", UnitTypeId.Feet }, { "pies", UnitTypeId.Feet },
            { "in", UnitTypeId.Inches }, { "inches", UnitTypeId.Inches }, { "pulgadas", UnitTypeId.Inches },
            { "m2", UnitTypeId.SquareMeters }, { "m²", UnitTypeId.SquareMeters }, { "square meters", UnitTypeId.SquareMeters }, { "metros cuadrados", UnitTypeId.SquareMeters },
            { "ft2", UnitTypeId.SquareFeet }, { "ft²", UnitTypeId.SquareFeet }, { "square feet", UnitTypeId.SquareFeet },
            { "m3", UnitTypeId.CubicMeters }, { "m³", UnitTypeId.CubicMeters }, { "cubic meters", UnitTypeId.CubicMeters }, { "metros cubicos", UnitTypeId.CubicMeters },
            { "ft3", UnitTypeId.CubicFeet }, { "ft³", UnitTypeId.CubicFeet }, { "cubic feet", UnitTypeId.CubicFeet },
        };

        /// <summary>
        /// true si este parámetro Double necesita que se le indique una
        /// unidad al escribirlo (tiene spec Longitud/Área/Volumen/etc.),
        /// false si es un número sin unidad (spec Number, o cualquier
        /// StorageType distinto de Double).
        /// </summary>
        public static bool RequiresUnitConversion(Parameter param)
        {
            if (param.StorageType != StorageType.Double)
                return false;

            try
            {
                ForgeTypeId spec = param.Definition.GetDataType();
                return spec != null && spec != SpecTypeId.Number && !string.IsNullOrEmpty(spec.TypeId);
            }
            catch
            {
                // Si no se puede determinar la spec, no forzamos conversión --
                // mejor el comportamiento anterior (valor crudo) que romper
                // una escritura que antes funcionaba.
                return false;
            }
        }

        /// <summary>
        /// Convierte rawValue (expresado en la unidad "unit" que indicó quien
        /// llama) a unidades internas de Revit, listo para Parameter.Set(double).
        /// Lanza una excepción clara y accionable si falta la unidad, no se
        /// reconoce, o no corresponde a la magnitud del parámetro.
        /// </summary>
        public static double ConvertToInternal(Parameter param, double rawValue, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                throw new Exception(
                    $"UNIDAD_REQUERIDA:{param.Definition.Name} -- este parámetro tiene unidades reales " +
                    "(no es un número sin unidad como Avance) -- hay que indicar explícitamente 'unit' " +
                    "(ej. 'm', 'cm', 'mm', 'm2', 'm3') para no escribir un valor equivocado.");
            }

            if (!UnitAliases.TryGetValue(unit.Trim(), out ForgeTypeId unitTypeId))
            {
                throw new Exception($"UNIDAD_NO_RECONOCIDA:{unit} -- usar una de: " +
                    string.Join(", ", new List<string>(UnitAliases.Keys)));
            }

            try
            {
                return UnitUtils.ConvertToInternalUnits(rawValue, unitTypeId);
            }
            catch (Exception ex)
            {
                throw new Exception($"UNIDAD_INCOMPATIBLE:{unit} -- no es una unidad válida para el parámetro " +
                    $"'{param.Definition.Name}' (probablemente la magnitud no coincide, ej. longitud vs. área). Detalle: {ex.Message}");
            }
        }
    }
}

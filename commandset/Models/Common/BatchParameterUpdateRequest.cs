namespace RevitMCPCommandSet.Models.Common;

/// <summary>Un elemento del arreglo de entrada de batch_update_element_parameter.</summary>
public class BatchParameterUpdateRequest
{
    public long ElementId { get; set; }
    public string ParameterName { get; set; }
    public string Value { get; set; }

    /// <summary>"instance" (default), "type" o "auto".</summary>
    public string Level { get; set; } = "instance";

    /// <summary>
    /// Unidad en la que viene expresado Value, SOLO para parámetros Double
    /// con especificación de unidad real (Longitud/Área/Volumen -- ver
    /// ParameterValueConverter). Ignorado para texto, enteros, y números
    /// sin unidad como Avance. ej. "m", "cm", "mm", "m2", "m3".
    /// </summary>
    public string Unit { get; set; }
}

/// <summary>Un elemento del arreglo de salida de batch_update_element_parameter.</summary>
public class BatchParameterUpdateResult
{
    public long ElementId { get; set; }
    public string ParameterName { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }

    /// <summary>Poblado solo si Success == true.</summary>
    public ParameterUpdateInfo Detail { get; set; }
}

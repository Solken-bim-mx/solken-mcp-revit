namespace RevitMCPCommandSet.Models.Common;

/// <summary>Un elemento del arreglo de entrada de batch_update_element_parameter.</summary>
public class BatchParameterUpdateRequest
{
    public long ElementId { get; set; }
    public string ParameterName { get; set; }
    public string Value { get; set; }

    /// <summary>"instance" (default), "type" o "auto".</summary>
    public string Level { get; set; } = "instance";
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

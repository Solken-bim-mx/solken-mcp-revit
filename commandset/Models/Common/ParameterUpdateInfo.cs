namespace RevitMCPCommandSet.Models.Common;

/// <summary>
/// Resultado estructurado de update_element_parameter: qué se escribió, a qué
/// nivel (instancia o tipo), el valor anterior y el nuevo, y -- cuando se
/// escribió a nivel Tipo -- cuántas otras instancias comparten ese tipo y se
/// vieron afectadas por el cambio, para que quien llama a la herramienta no
/// asuma por error que el cambio fue quirúrgico sobre un solo elemento.
/// </summary>
public class ParameterUpdateInfo
{
    public long ElementId { get; set; }
    public string ParameterName { get; set; }

    /// <summary>"instance" o "type" -- el nivel real donde se encontró y escribió el parámetro.</summary>
    public string Level { get; set; }

    public string OldValue { get; set; }
    public string NewValue { get; set; }

    /// <summary>
    /// Solo poblado cuando Level == "type". Número de instancias (incluida la
    /// consultada) que comparten ese FamilySymbol/ElementType, y por lo tanto
    /// recibieron el mismo cambio. Un valor mayor a 1 es la señal de que la
    /// escritura NO fue quirúrgica sobre un solo elemento físico.
    /// </summary>
    public int? SharedInstanceCount { get; set; }
}

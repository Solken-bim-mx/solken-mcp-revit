namespace RevitMCPCommandSet.Models.Common;

/// <summary>
/// Un tipo de familia (FamilySymbol/ElementType) con cuántas instancias
/// colocadas tiene en el documento. InstanceCount == 0 identifica un tipo
/// huérfano: existe en el catálogo del proyecto pero nunca se colocó en el
/// modelo -- el caso real encontrado el 2026-10-30 con "zapata aislada
/// intermedia doble 6-B/6-C/6-E", que confundía a herramientas que solo
/// listan tipos (como get_available_family_types) sin poder distinguirlos
/// de los tipos sí usados.
/// </summary>
public class FamilyTypeInstanceCount
{
    public long TypeId { get; set; }
    public string FamilyName { get; set; }
    public string TypeName { get; set; }
    public string Category { get; set; }
    public int InstanceCount { get; set; }
}

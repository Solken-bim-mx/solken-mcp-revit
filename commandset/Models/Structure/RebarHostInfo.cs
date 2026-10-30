using Newtonsoft.Json;
using System.Collections.Generic;
namespace RevitMCPCommandSet.Models.Structure
{
    /// <summary>
    /// Parametros de entrada: lista de ElementId de elementos anfitrion
    /// (zapatas, contratrabes, castillos, etc.) de los que se quiere
    /// extraer el armado (rebar) real colocado dentro de ellos.
    /// </summary>
    public class RebarHostQuery
    {
        /// <summary>
        /// IDs de los elementos anfitrion a consultar (ej. zapata 4-A = 664771).
        /// </summary>
        [JsonProperty("hostElementIds")]
        public List<int> HostElementIds { get; set; } = new List<int>();
    }
    /// <summary>
    /// Resultado: para cada elemento anfitrion, la lista de barras de
    /// armado que Revit reporta como colocadas dentro de el.
    /// </summary>
    public class RebarHostResult
    {
        [JsonProperty("hostElementId")]
        public int HostElementId { get; set; }
        [JsonProperty("hostElementName")]
        public string HostElementName { get; set; }
        [JsonProperty("hostCategory")]
        public string HostCategory { get; set; }
        /// <summary>
        /// Si el host no tiene armado colocado (o RebarHostData no aplica
        /// a esta categoria de elemento), esto queda en false y RebarBars
        /// queda vacio -- no es un error, es informacion legitima ("aun
        /// sin armar").
        /// </summary>
        [JsonProperty("hasRebarHostData")]
        public bool HasRebarHostData { get; set; }
        [JsonProperty("rebarBars")]
        public List<RebarBarInfo> RebarBars { get; set; } = new List<RebarBarInfo>();
    }
    /// <summary>
    /// Informacion de una barra de armado individual, ya vinculada a su
    /// host real (no por bounding box, no por cercania -- por la relacion
    /// interna que guarda Revit).
    /// </summary>
    public class RebarBarInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("uniqueId")]
        public string UniqueId { get; set; }
        /// <summary>
        /// Nombre del tipo de barra (ej. "N°4 (1/2\") : Forma 11").
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        /// <summary>
        /// Calibre / diametro de la barra, tomado del RebarBarType.
        /// </summary>
        [JsonProperty("barTypeName")]
        public string BarTypeName { get; set; }
        [JsonProperty("barDiameterMm")]
        public double BarDiameterMm { get; set; }
        /// <summary>
        /// Numero de barras en el set (Rebar.Quantity).
        /// </summary>
        [JsonProperty("quantity")]
        public int Quantity { get; set; }
        /// <summary>
        /// Longitud de UNA barra individual del set, en mm.
        /// Usar este campo para trazabilidad por elemento.
        /// </summary>
        [JsonProperty("lengthMm")]
        public double LengthMm { get; set; }
        /// <summary>
        /// Longitud total del set completo (LengthMm x Quantity), en mm.
        /// Util para sumar directo el metraje total de acero por frente
        /// sin tener que multiplicar del lado del cliente.
        /// </summary>
        [JsonProperty("lengthMmTotal")]
        public double LengthMmTotal { get; set; }
        /// <summary>
        /// Nombre de la forma de doblado (RebarShape), si aplica.
        /// </summary>
        [JsonProperty("shapeName")]
        public string ShapeName { get; set; }
    }
}
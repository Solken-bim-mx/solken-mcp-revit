using Newtonsoft.Json;

namespace RevitMCPSDK.API.Models.JsonRPC
{
    public class JsonRPCErrorResponse : IJsonRPCResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("error")]
        public JsonRPCError Error { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

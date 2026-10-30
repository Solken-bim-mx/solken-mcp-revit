using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPSDK.API.Models.JsonRPC
{
    public class JsonRPCSuccessResponse : IJsonRPCResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("result")]
        public JToken Result { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

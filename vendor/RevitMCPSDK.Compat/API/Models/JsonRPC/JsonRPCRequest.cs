using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPSDK.API.Models.JsonRPC
{
    public class JsonRPCRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Params { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonIgnore]
        public bool IsNotification => string.IsNullOrEmpty(Id);

        public bool IsValid()
        {
            return JsonRpc == "2.0" && !string.IsNullOrEmpty(Method);
        }

        public bool TryGetParamsAs<T>(out T result)
        {
            try
            {
                result = Params == null ? default : Params.ToObject<T>();
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public T GetParamsAs<T>()
        {
            return Params == null ? default : Params.ToObject<T>();
        }

        public JArray GetParamsArray()
        {
            return Params as JArray;
        }

        public JObject GetParamsObject()
        {
            return Params as JObject;
        }

        public bool TryGetParamAt<T>(int index, out T result)
        {
            try
            {
                var array = GetParamsArray();
                if (array != null && index >= 0 && index < array.Count)
                {
                    result = array[index].ToObject<T>();
                    return true;
                }
            }
            catch
            {
                // fallthrough
            }

            result = default;
            return false;
        }

        public bool TryGetParam<T>(string name, out T result)
        {
            try
            {
                var obj = GetParamsObject();
                if (obj != null && obj.TryGetValue(name, out var token))
                {
                    result = token.ToObject<T>();
                    return true;
                }
            }
            catch
            {
                // fallthrough
            }

            result = default;
            return false;
        }

        public static JsonRPCRequest Create(string method, object parameters, string id)
        {
            return new JsonRPCRequest
            {
                JsonRpc = "2.0",
                Method = method,
                Params = parameters != null ? JToken.FromObject(parameters) : null,
                Id = id ?? Guid.NewGuid().ToString()
            };
        }

        public static JsonRPCRequest CreateNotification(string method, object parameters)
        {
            return new JsonRPCRequest
            {
                JsonRpc = "2.0",
                Method = method,
                Params = parameters != null ? JToken.FromObject(parameters) : null,
                Id = null
            };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

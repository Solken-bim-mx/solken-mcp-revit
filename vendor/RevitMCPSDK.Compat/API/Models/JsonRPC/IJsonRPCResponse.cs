namespace RevitMCPSDK.API.Models.JsonRPC
{
    public interface IJsonRPCResponse
    {
        string JsonRpc { get; }
        string Id { get; set; }

        string ToJson();
    }
}

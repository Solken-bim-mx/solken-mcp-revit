using System;

namespace RevitMCPSDK.API.Models.JsonRPC
{
    public class JsonRPCSerializationException : Exception
    {
        public JsonRPCSerializationException(string message) : base(message) { }
        public JsonRPCSerializationException(string message, Exception inner) : base(message, inner) { }
    }
}

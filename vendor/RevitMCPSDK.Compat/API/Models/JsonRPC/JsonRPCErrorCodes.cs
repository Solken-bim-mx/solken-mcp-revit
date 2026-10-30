namespace RevitMCPSDK.API.Models.JsonRPC
{
    /// <summary>
    /// Codigos de error JSON-RPC 2.0 estandar (-32700..-32600) mas codigos
    /// propios del dominio Revit/MCP (-32000 en adelante).
    /// </summary>
    public static class JsonRPCErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        public const int ServerErrorStart = -32000;
        public const int ServerErrorEnd = -32099;

        public const int RevitApiError = -32000;
        public const int CommandExecutionTimeout = -32001;
        public const int DocumentNotAvailable = -32002;
        public const int TransactionFailed = -32003;
        public const int ElementNotFound = -32004;
        public const int ElementCreationFailed = -32005;
        public const int ElementModificationFailed = -32006;
        public const int ElementDeletionFailed = -32007;
        public const int InvalidGeometryData = -32008;
        public const int ViewNotFound = -32009;
        public const int CommandRegistrationFailed = -32010;
        public const int ServiceStartupFailed = -32011;
        public const int ExternalEventCreationFailed = -32012;
        public const int ExternalEventExecutionFailed = -32013;
        public const int CommandCancelled = -32014;
        public const int CommandParameterParsingFailed = -32015;
        public const int Unauthorized = -32016;
        public const int ResourceUnavailable = -32017;
        public const int RequestTimeout = -32018;
        public const int InvalidSession = -32019;
        public const int ConfigurationError = -32020;
        public const int IOError = -32021;

        public static string GetErrorDescription(int code)
        {
            switch (code)
            {
                case ParseError: return "Parse error";
                case InvalidRequest: return "Invalid Request";
                case MethodNotFound: return "Method not found";
                case InvalidParams: return "Invalid params";
                case InternalError: return "Internal error";
                case RevitApiError: return "Revit API error";
                case CommandExecutionTimeout: return "Command execution timeout";
                case DocumentNotAvailable: return "Document not available";
                case TransactionFailed: return "Transaction failed";
                case ElementNotFound: return "Element not found";
                case ElementCreationFailed: return "Element creation failed";
                case ElementModificationFailed: return "Element modification failed";
                case ElementDeletionFailed: return "Element deletion failed";
                case InvalidGeometryData: return "Invalid geometry data";
                case ViewNotFound: return "View not found";
                case CommandRegistrationFailed: return "Command registration failed";
                case ServiceStartupFailed: return "Service startup failed";
                case ExternalEventCreationFailed: return "External event creation failed";
                case ExternalEventExecutionFailed: return "External event execution failed";
                case CommandCancelled: return "Command cancelled";
                case CommandParameterParsingFailed: return "Command parameter parsing failed";
                case Unauthorized: return "Unauthorized";
                case ResourceUnavailable: return "Resource unavailable";
                case RequestTimeout: return "Request timeout";
                case InvalidSession: return "Invalid session";
                case ConfigurationError: return "Configuration error";
                case IOError: return "I/O error";
                default: return "Unknown error";
            }
        }
    }
}

using System;

namespace RevitMCPSDK.Exceptions
{
    /// <summary>
    /// Excepcion con codigo de error JSON-RPC asociado, capturada por
    /// CommandExecutor para construir una respuesta de error bien formada.
    /// </summary>
    public class CommandExecutionException : Exception
    {
        public int ErrorCode { get; }
        public object ErrorData { get; }

        public CommandExecutionException(string message)
            : base(message)
        {
            ErrorCode = API.Models.JsonRPC.JsonRPCErrorCodes.InternalError;
        }

        public CommandExecutionException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public CommandExecutionException(string message, int errorCode, object errorData)
            : base(message)
        {
            ErrorCode = errorCode;
            ErrorData = errorData;
        }
    }
}

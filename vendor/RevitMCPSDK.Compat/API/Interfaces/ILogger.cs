namespace RevitMCPSDK.API.Interfaces
{
    public interface ILogger
    {
        void Log(LogLevel level, string message, params object[] args);
        void Debug(string message, params object[] args);
        void Info(string message, params object[] args);
        void Warning(string message, params object[] args);
        void Error(string message, params object[] args);
    }
}

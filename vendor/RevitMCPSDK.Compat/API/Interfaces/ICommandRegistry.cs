namespace RevitMCPSDK.API.Interfaces
{
    public interface ICommandRegistry
    {
        void RegisterCommand(IRevitCommand command);

        bool TryGetCommand(string commandName, out IRevitCommand command);
    }
}

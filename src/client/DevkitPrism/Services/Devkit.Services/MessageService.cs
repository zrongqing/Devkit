using Devkit.Services.Interfaces;

namespace Devkit.Services;

public class MessageService : IMessageService
{
    #region IMessageService Members
    public string GetMessage()
    {
        return "Hello from the Message Service";
    }
    #endregion
}

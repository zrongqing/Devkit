using Devkit.Services.Interfaces;

namespace Devkit.Services;

public class MessageService : IMessageService
{
    public string GetMessage()
    {
        return "Hello from the Message Service";
    }
}
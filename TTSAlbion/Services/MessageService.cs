using TTSAlbion.Albion.Handler.Event.Model;

namespace TTSAlbion.Services;

public class MessageService
{
    public string NameUser { get; private set; }
    public const string CommandPath = "!!";
    
    public void RegisterUser(string user)
    {
        NameUser = user;
    }
    
    public void RunCommand(MessageModel message)
    {
        if (message.Text.StartsWith(CommandPath) && message.User.Equals(NameUser, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Run Command: {message.Text}");
        }
    }
    
    
}
namespace ISO11820.Models;

public class MasterMessage
{
    public string Time { get; set; } = "";
    public string Message { get; set; } = "";

    public MasterMessage() { }

    public MasterMessage(string message)
    {
        Time = DateTime.Now.ToString("HH:mm:ss");
        Message = message;
    }
}

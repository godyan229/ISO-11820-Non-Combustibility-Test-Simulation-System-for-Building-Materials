using ISO11820.Models;

namespace ISO11820.Core;

public class DataBroadcastEventArgs : EventArgs
{
    public Dictionary<string, double> Temperatures { get; set; } = new();
    public string StatusText { get; set; } = "";
    public int ElapsedSeconds { get; set; }
    public double TemperatureDrift { get; set; }
    public List<double> Tf1History { get; set; } = new();
    public List<double> Tf2History { get; set; } = new();
    public List<double> TsHistory { get; set; } = new();
    public List<double> TcHistory { get; set; } = new();
    public List<double> TimeAxis { get; set; } = new();
    public List<MasterMessage> Messages { get; set; } = new();
}

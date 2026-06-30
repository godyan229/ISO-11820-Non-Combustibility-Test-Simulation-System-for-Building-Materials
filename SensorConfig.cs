namespace ISO11820.Models;

public class SensorConfig
{
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = "";
    public double RangeMin { get; set; }
    public double RangeMax { get; set; }
    public string Unit { get; set; } = "°C";
}

namespace ISO11820.Models;

public class Apparatus
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTime CalibrationDate { get; set; }
    public double ConstPower { get; set; }
    public string SerialPort { get; set; } = "";
}

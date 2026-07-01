namespace ISO11820.Models;
///1111111
public class CalibrationRecord
{
    public int Id { get; set; }
    public DateTime CalDate { get; set; }
    public string Operator { get; set; } = "";
    public double StandardTemp { get; set; }
    public double MeasuredTemp { get; set; }
    public double Deviation { get; set; }
    public string Remark { get; set; } = "";
}

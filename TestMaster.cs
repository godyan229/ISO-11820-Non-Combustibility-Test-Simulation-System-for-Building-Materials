namespace ISO11820.Models;

public class TestMaster
{
    public string ProductId { get; set; } = "";
    public string TestId { get; set; } = "";
    public DateTime TestDate { get; set; }
    public string Operator { get; set; } = "";
    public double AmbientTemp { get; set; }
    public double AmbientHumidity { get; set; }
    public double PreWeight { get; set; }
    public double PostWeight { get; set; }
    public double LostWeight { get; set; }
    public double LostWeightPer { get; set; }
    public double DeltaTf { get; set; }
    public double DeltaTs { get; set; }
    public double DeltaTc { get; set; }
    public double DeltaTf1 { get; set; }
    public double DeltaTf2 { get; set; }
    public int TotalTestTime { get; set; }
    public string DurationMode { get; set; } = "standard";
    public int TargetDurationSeconds { get; set; }
    public int FlameOccurred { get; set; }
    public int FlameStartTime { get; set; }
    public int FlameDuration { get; set; }
    public string Remark { get; set; } = "";
    public string Flag { get; set; } = "";
    public double FinalTf1 { get; set; }
    public double FinalTf2 { get; set; }
    public double FinalTs { get; set; }
    public double FinalTc { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public double ConstPower { get; set; }
    public string PassFail { get; set; } = "";
}

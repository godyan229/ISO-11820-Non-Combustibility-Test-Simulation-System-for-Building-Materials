using Microsoft.Extensions.Configuration;

namespace ISO11820.Services;

public enum SimulationPhase
{
    Heating,
    Stable,
    Recording,
    Cooling
}

public class SensorSimulator
{
    private readonly IConfiguration _config;
    private readonly Random _rng = new();

    public double HeatingRatePerSecond { get; }
    public double TargetTemp { get; }
    public double TempFluctuation { get; }
    public double StableThreshold { get; }
    public double InitialFurnaceTemp { get; }
    public bool IsSimulating { get; set; } = true;

    public double Tf1 { get; set; }
    public double Tf2 { get; set; }
    public double Ts { get; set; }
    public double Tc { get; set; }
    public double TCal { get; set; }

    public int StableCounter { get; set; }
    public bool IsStable { get; set; }
    public SimulationPhase Phase { get; set; } = SimulationPhase.Heating;

    public Queue<double> PidOutputQueue { get; } = new(600);

    public List<double> Tf1History { get; } = new();
    public List<double> Tf2History { get; } = new();
    public List<double> TsHistory { get; } = new();
    public List<double> TcHistory { get; } = new();
    public double ElapsedSeconds { get; set; }

    public SensorSimulator(IConfiguration config)
    {
        _config = config;
        HeatingRatePerSecond = double.Parse(config["Simulation:HeatingRatePerSecond"] ?? "40");
        TargetTemp = double.Parse(config["Simulation:TargetFurnaceTemp"] ?? "750");
        TempFluctuation = double.Parse(config["Simulation:TempFluctuation"] ?? "0.5");
        StableThreshold = double.Parse(config["Simulation:StableThreshold"] ?? "3.0");
        InitialFurnaceTemp = double.Parse(config["Simulation:InitialFurnaceTemp"] ?? "25");
        Reset();
    }

    public void Reset()
    {
        Tf1 = InitialFurnaceTemp;
        Tf2 = InitialFurnaceTemp;
        Ts = InitialFurnaceTemp * 0.3;
        Tc = InitialFurnaceTemp * 0.25;
        TCal = InitialFurnaceTemp;
        StableCounter = 0;
        IsStable = false;
        Phase = SimulationPhase.Heating;
        PidOutputQueue.Clear();
        Tf1History.Clear();
        Tf2History.Clear();
        TsHistory.Clear();
        TcHistory.Clear();
        ElapsedSeconds = 0;
    }

    public void Update()
    {
        if (!IsSimulating) return;

        double noise = Noise();
        double noise2 = Noise();

        switch (Phase)
        {
            case SimulationPhase.Heating:
                if (Tf1 < TargetTemp - StableThreshold)
                {
                    Tf1 += HeatingRatePerSecond * 0.8 + noise;
                    Tf2 += HeatingRatePerSecond * 0.8 + noise2;
                    Ts = Tf1 * 0.3 + Noise();
                    Tc = Tf1 * 0.25 + Noise();
                    TCal = Tf1 + noise * 2;
                }
                else
                {
                    Phase = SimulationPhase.Stable;
                    StableCounter = 0;
                    IsStable = false;
                }
                break;

            case SimulationPhase.Stable:
                Tf1 = TargetTemp + noise;
                Tf2 = TargetTemp + noise2;
                Ts = Tf1 * 0.3 + Noise();
                Tc = Tf1 * 0.25 + Noise();
                TCal = Tf1 + noise * 2;
                StableCounter++;
                if (StableCounter > 3)
                    IsStable = true;
                break;

            case SimulationPhase.Recording:
                Tf1 = TargetTemp + noise;
                Tf2 = TargetTemp + noise2;

                double surfaceTarget = Math.Min(Tf1 * 0.95, 800);
                Ts += (surfaceTarget - Ts) * 0.02 + Noise();

                double centerTarget = Math.Min(Tf1 * 0.85, 750);
                Tc += (centerTarget - Tc) * 0.01 + Noise();

                TCal = Tf1 + noise * 2;

                PidOutputQueue.Enqueue(2048 + Noise() * 10);
                if (PidOutputQueue.Count > 600)
                    PidOutputQueue.Dequeue();
                break;

            case SimulationPhase.Cooling:
                Tf1 -= 0.5 + Math.Abs(noise) * 0.1;
                Tf2 -= 0.5 + Math.Abs(noise2) * 0.1;
                if (Tf1 < InitialFurnaceTemp) Tf1 = InitialFurnaceTemp;
                if (Tf2 < InitialFurnaceTemp) Tf2 = InitialFurnaceTemp;
                Ts = Tf1 * 0.3 + Noise();
                Tc = Tf1 * 0.25 + Noise();
                TCal = Tf1 + noise * 2;
                break;
        }

        Tf1History.Add(Tf1);
        Tf2History.Add(Tf2);
        TsHistory.Add(Ts);
        TcHistory.Add(Tc);
        ElapsedSeconds += 0.8;
    }

    private double Noise()
    {
        return (_rng.NextDouble() * 2 - 1) * TempFluctuation;
    }

    public Dictionary<string, double> GetCurrentTemperatures()
    {
        return new Dictionary<string, double>
        {
            ["TF1"] = Tf1,
            ["TF2"] = Tf2,
            ["TS"] = Ts,
            ["TC"] = Tc,
            ["TCal"] = TCal
        };
    }

    public bool CheckStartCriteria()
    {
        return Tf1 >= 745 && Tf1 <= 755 && IsStable;
    }

    public double GetAveragePidOutput()
    {
        if (PidOutputQueue.Count == 0) return 2048;
        return PidOutputQueue.Average();
    }
}

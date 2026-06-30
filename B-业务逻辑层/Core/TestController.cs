using ISO11820.Data;
using ISO11820.Models;
using ISO11820.Services;
using Microsoft.Extensions.Configuration;
using MathNet.Numerics;

namespace ISO11820.Core;

public enum TestState
{
    Idle,
    Preparing,
    Ready,
    Recording,
    Complete
}

public class TestController
{
    private readonly DbHelper _db;
    private readonly IConfiguration _config;
    private readonly SensorSimulator _simulator;
    public SensorSimulator Sim => _simulator;

    public TestState State { get; private set; } = TestState.Idle;
    public TestMaster? CurrentTest { get; set; }

    public int RecordElapsedSeconds { get; set; }

    public List<string> CsvLines { get; } = new();
    public List<MasterMessage> PendingMessages { get; } = new();

    public event EventHandler<DataBroadcastEventArgs>? DataBroadcast;

    private int _lastCheckMinute = 0;
    public double MaxTemperatureDrift { get; }

    private readonly Queue<double> _tf1Recent = new();
    private readonly Queue<double> _tf2Recent = new();
    private const int DriftWindowSeconds = 600;

    public TestController(DbHelper db, IConfiguration config, SensorSimulator simulator)
    {
        _db = db;
        _config = config;
        _simulator = simulator;
        MaxTemperatureDrift = double.Parse(config["Simulation:MaxTemperatureDriftPerTenMinutes"] ?? "2.0");
    }

    public void CreateTest(TestMaster test)
    {
        CurrentTest = test;
        _db.InsertTestMaster(test);
        State = TestState.Idle;
        CsvLines.Clear();
        CsvLines.Add("Time,Temp1,Temp2,TempSurface,TempCenter,TempCalibration");
        RecordElapsedSeconds = 0;
        AddMessage("系统初始化，操作员：" + test.Operator);
    }

    public void StartHeating()
    {
        if (State != TestState.Idle) return;
        _simulator.Phase = SimulationPhase.Heating;
        State = TestState.Preparing;
        AddMessage("开始升温，系统升温中");
    }

    public void StopHeating()
    {
        if (State != TestState.Preparing && State != TestState.Ready && State != TestState.Complete) return;
        _simulator.Phase = SimulationPhase.Cooling;
        State = TestState.Idle;
    }

    public void StartRecording()
    {
        if (State != TestState.Ready) return;
        _simulator.Phase = SimulationPhase.Recording;
        State = TestState.Recording;
        RecordElapsedSeconds = 0;
        _lastCheckMinute = 0;
        AddMessage("开始记录，计时开始");
    }

    public void StopRecording()
    {
        if (State != TestState.Recording) return;
        CompleteTest("用户手动停止记录");
    }

    private void CompleteTest(string reason)
    {
        State = TestState.Complete;
        if (CurrentTest != null)
        {
            CurrentTest.TotalTestTime = RecordElapsedSeconds;
            CurrentTest.FinalTf1 = _simulator.Tf1;
            CurrentTest.FinalTf2 = _simulator.Tf2;
            CurrentTest.FinalTs = _simulator.Ts;
            CurrentTest.FinalTc = _simulator.Tc;
        }
        AddMessage(reason);
    }

    public void FinalizeTest()
    {
        if (CurrentTest == null) return;
        CurrentTest.Flag = "10000000";
        _db.InsertTestMaster(CurrentTest);
        State = TestState.Preparing;
    }

    public void DoWork()
    {
        // 无活动试验时不做任何事
        if (CurrentTest == null || State == TestState.Idle)
            return;

        switch (State)
        {
            case TestState.Preparing:
                if (_simulator.CheckStartCriteria())
                {
                    State = TestState.Ready;
                    AddMessage("温度已稳定，可以开始记录");
                }
                break;

            case TestState.Ready:
                if (_simulator.Tf1 < 745 || _simulator.Tf1 > 755)
                    State = TestState.Preparing;
                break;

            case TestState.Recording:
                RecordElapsedSeconds++;
                var temps = _simulator.GetCurrentTemperatures();
                CsvLines.Add($"{RecordElapsedSeconds},{temps["TF1"]:F1},{temps["TF2"]:F1},{temps["TS"]:F1},{temps["TC"]:F1},{temps["TCal"]:F1}");
                CheckTerminationConditions();
                break;
        }

        _tf1Recent.Enqueue(_simulator.Tf1);
        _tf2Recent.Enqueue(_simulator.Tf2);
        while (_tf1Recent.Count > DriftWindowSeconds) _tf1Recent.Dequeue();
        while (_tf2Recent.Count > DriftWindowSeconds) _tf2Recent.Dequeue();

        double drift = 0;
        if (_tf1Recent.Count >= 60)
        {
            var xData = Enumerable.Range(0, _tf1Recent.Count).Select(i => (double)i).ToArray();
            var yData = _tf1Recent.ToArray();
            try
            {
                var (intercept, slope) = Fit.Line(xData, yData);
                drift = slope * 600;
            }
            catch { drift = 0; }
        }

        var args = new DataBroadcastEventArgs
        {
            Temperatures = _simulator.GetCurrentTemperatures(),
            StatusText = GetStatusText(),
            ElapsedSeconds = RecordElapsedSeconds,
            TemperatureDrift = drift,
            Tf1History = new List<double>(_simulator.Tf1History),
            Tf2History = new List<double>(_simulator.Tf2History),
            TsHistory = new List<double>(_simulator.TsHistory),
            TcHistory = new List<double>(_simulator.TcHistory),
            TimeAxis = Enumerable.Range(0, _simulator.Tf1History.Count).Select(i => i * 0.8).ToList(),
            Messages = new List<MasterMessage>(PendingMessages)
        };

        DataBroadcast?.Invoke(this, args);
        // 不在 DoWork 内清空消息——由调用方 (MainForm.OnTick) 负责显示后清空
    }

    private void CheckTerminationConditions()
    {
        if (CurrentTest == null) return;

        if (CurrentTest.DurationMode == "fixed")
        {
            if (RecordElapsedSeconds >= CurrentTest.TargetDurationSeconds)
                CompleteTest($"固定时长 {CurrentTest.TargetDurationSeconds} 秒到达，试验结束");
            return;
        }

        int currentMinute = RecordElapsedSeconds / 60;
        int[] checkMinutes = { 30, 35, 40, 45, 50, 55 };

        if (currentMinute >= 30 && currentMinute > _lastCheckMinute && checkMinutes.Contains(currentMinute))
        {
            _lastCheckMinute = currentMinute;
            if (CheckEarlyTermination())
            {
                CompleteTest("满足终止条件，试验结束");
                return;
            }
        }

        if (RecordElapsedSeconds >= 3600)
            CompleteTest("记录时间到达 3600 秒，试验自动结束");
    }

    private bool CheckEarlyTermination()
    {
        if (_tf1Recent.Count < DriftWindowSeconds || _tf2Recent.Count < DriftWindowSeconds)
            return false;

        var xData = Enumerable.Range(0, _tf1Recent.Count).Select(i => (double)i).ToArray();
        try
        {
            var drift1 = Fit.Line(xData, _tf1Recent.ToArray()).Item2 * 600;
            var drift2 = Fit.Line(xData, _tf2Recent.ToArray()).Item2 * 600;
            return Math.Abs(drift1) <= MaxTemperatureDrift && Math.Abs(drift2) <= MaxTemperatureDrift;
        }
        catch { return false; }
    }

    public string GetStatusText()
    {
        return State switch
        {
            TestState.Idle => "空闲",
            TestState.Preparing => "升温中",
            TestState.Ready => "就绪",
            TestState.Recording => "记录中",
            TestState.Complete => "完成",
            _ => "未知"
        };
    }

    private void AddMessage(string msg)
    {
        PendingMessages.Add(new MasterMessage(msg));
    }
}

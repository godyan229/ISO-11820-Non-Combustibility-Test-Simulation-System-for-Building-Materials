using ISO11820.Core;

namespace ISO11820.Services;

public class DaqWorker : IDisposable
{
    private readonly SensorSimulator _simulator;
    private readonly TestController _controller;
    private readonly System.Windows.Forms.Timer _timer;

    public DaqWorker(SensorSimulator simulator, TestController controller)
    {
        _simulator = simulator;
        _controller = controller;
        _timer = new System.Windows.Forms.Timer { Interval = 800 };
        _timer.Tick += OnTimerTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _simulator.Update();
        _controller.DoWork();
    }

    public void Dispose() => _timer.Dispose();
}

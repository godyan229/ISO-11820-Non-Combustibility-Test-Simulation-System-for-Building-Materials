using ISO11820.Global;
using ISO11820.Forms;
using Serilog;

namespace ISO11820;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("Logs/iso11820-.log", rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("ISO 11820 系统启动");
            AppGlobal.Instance.Initialize();
            Application.Run(new LoginForm());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "系统启动失败");
            MessageBox.Show($"系统启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

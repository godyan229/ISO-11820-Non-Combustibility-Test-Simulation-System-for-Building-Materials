using ISO11820.Data;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;

namespace ISO11820.Global;

public class AppGlobal
{
    private static AppGlobal? _instance;
    private static readonly object _lock = new();

    public static AppGlobal Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new AppGlobal();
                return _instance;
            }
        }
    }

    public IConfiguration Configuration { get; private set; } = null!;
    public DbHelper Db { get; private set; } = null!;
    public Operator? CurrentUser { get; set; }
    public List<SensorConfig> Sensors { get; set; } = new();
    public Apparatus? CurrentApparatus { get; set; }

    private AppGlobal() { }

    public void Initialize()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        Configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Db = new DbHelper(Configuration);
        Sensors = Db.GetSensors();
        CurrentApparatus = Db.GetApparatus();
    }
}

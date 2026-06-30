using ISO11820.Data;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;

namespace ISO11820.Global;

/// <summary>
/// 全局应用上下文类（单例模式）
/// 提供整个应用程序共享的配置、数据库连接、当前用户信息等资源
/// </summary>
public class AppGlobal
{
    // 单例实例
    private static AppGlobal? _instance;
    // 线程锁，确保单例的线程安全
    private static readonly object _lock = new();

    /// <summary>
    /// 获取全局单例实例
    /// </summary>
    public static AppGlobal Instance
    {
        get
        {
            lock (_lock)
            {
                // 若实例不存在则创建
                _instance ??= new AppGlobal();
                return _instance;
            }
        }
    }

    /// <summary>
    /// 应用配置对象
    /// </summary>
    public IConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// 数据库帮助类实例
    /// </summary>
    public DbHelper Db { get; private set; } = null!;

    /// <summary>
    /// 当前登录用户信息
    /// </summary>
    public Operator? CurrentUser { get; set; }

    /// <summary>
    /// 传感器配置列表
    /// </summary>
    public List<SensorConfig> Sensors { get; set; } = new();

    /// <summary>
    /// 当前使用的设备信息
    /// </summary>
    public Apparatus? CurrentApparatus { get; set; }

    /// <summary>
    /// 私有构造函数，防止外部实例化
    /// </summary>
    private AppGlobal() { }

    /// <summary>
    /// 初始化全局上下文：加载配置文件、初始化数据库、读取传感器和设备信息
    /// </summary>
    public void Initialize()
    {
        // 获取应用程序所在目录
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        // 构建配置对象，加载appsettings.json
        Configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 初始化数据库帮助类
        Db = new DbHelper(Configuration);
        // 加载传感器配置列表
        Sensors = Db.GetSensors();
        // 加载设备信息
        CurrentApparatus = Db.GetApparatus();
    }
}

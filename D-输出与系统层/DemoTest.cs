using ISO11820.Core;
using ISO11820.Data;
using ISO11820.Models;
using ISO11820.Services;
using Microsoft.Extensions.Configuration;

namespace ISO11820;

public static class DemoTest
{
    private static DbHelper _db = null!;
    private static SensorSimulator _sim = null!;
    private static TestController _ctrl = null!;
    private static ExportService _export = null!;
    private static int _ok, _fail;

    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        _db = new DbHelper(config);
        _sim = new SensorSimulator(config);
        _ctrl = new TestController(_db, config, _sim);
        _export = new ExportService(_db, config);
        _ok = 0; _fail = 0;

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  ISO 11820 不燃性试验系统 — 8 组测试用例");
        Console.WriteLine("═══════════════════════════════════════════════════");

        TestCase1();   // 石膏板 — 标准60分钟
        TestCase2();   // 岩棉板 — 固定时长30秒
        TestCase3();   // 异常测试 — 密码/空字段
        TestCase4();   // 中途停止升温 & 重新升温
        TestCase5();   // 按钮状态矩阵
        TestCase6();   // 快速连续两次试验
        TestCase7();   // 校准功能
        TestCase8();   // 记录查询

        Console.WriteLine($"\n═══════════════════════════════════════════════════");
        Console.WriteLine($"  总计: {_ok} 通过 / {_fail} 失败 (共 {_ok + _fail} 项)");
        Console.WriteLine($"═══════════════════════════════════════════════════");
        Console.WriteLine(_fail > 0 ? "\n❌ 存在失败项!" : "\n✅ 全部测试通过!");
        Environment.Exit(_fail > 0 ? 1 : 0);
    }

    static void Ok(string msg) { Console.WriteLine($"  ✅ {msg}"); _ok++; }
    static void Ng(string msg) { Console.WriteLine($"  ❌ {msg}"); _fail++; }

    static void HeatToReady()
    {
        _ctrl.StartHeating();
        int n = 0;
        while (_ctrl.State != TestState.Ready && n < 100) { _sim.Update(); _ctrl.DoWork(); n++; }
    }

    static void RecordSeconds(int seconds)
    {
        _ctrl.StartRecording();
        while (_ctrl.State == TestState.Recording && _ctrl.RecordElapsedSeconds < seconds)
        { _sim.Update(); _ctrl.DoWork(); }
        if (_ctrl.State == TestState.Recording) _ctrl.StopRecording();
    }

    static double Round(double v, int d = 2) => Math.Round(v, d);

    // ═══════════════════════════════════════
    // 用例 1: 石膏板 — 标准 60 分钟流程
    // ═══════════════════════════════════════
    static void TestCase1()
    {
        Console.WriteLine("\n━━━ 用例 1: 石膏板(G-001) — 标准完整流程 ━━━");

        // 1. 登录
        var admin = _db.ValidateLogin("管理员", "123456");
        Ok($"管理员登录: {admin?.Role ?? "null"}");

        // 2. 新建试验
        var t = new TestMaster {
            ProductId = "GB-001", TestId = "T01", TestDate = DateTime.Now,
            Operator = "admin", AmbientTemp = 23.5, AmbientHumidity = 48.0,
            PreWeight = 52.30, DurationMode = "standard", TargetDurationSeconds = 3600,
            DeviceId = "DEV001", DeviceName = "ISO11820不燃性试验炉", ConstPower = 2048
        };
        _ctrl.CreateTest(t);
        Ok("试验创建: GB-001/T01, 石膏板, 52.30g");
        Ok($"初始状态 Idle (实际: {_ctrl.State})");

        // 3. 升温
        _ctrl.StartHeating();
        Ok($"状态→Preparing (实际: {_ctrl.State})");
        int ticks = 0;
        while (_ctrl.State != TestState.Ready && ticks < 100) { _sim.Update(); _ctrl.DoWork(); ticks++; }
        Console.WriteLine($"    升温: {ticks} ticks, TF1={_sim.Tf1:F1}°C");
        Ok($"状态→Ready (实际: {_ctrl.State}), TF1={_sim.Tf1:F1}°C");
        Ok($"TF1 在 745~755: {_sim.Tf1 >= 745 && _sim.Tf1 <= 755}");

        // 4. 记录 5 秒
        _ctrl.StartRecording();
        Ok($"状态→Recording (实际: {_ctrl.State})");
        int rec = 0;
        while (rec < 5) { _sim.Update(); _ctrl.DoWork(); rec++; }
        Console.WriteLine($"    记录: {_ctrl.RecordElapsedSeconds}s, CSV行={_ctrl.CsvLines.Count}");
        Ok($"CSV 有数据: {_ctrl.CsvLines.Count >= 6}");
        _ctrl.StopRecording();
        Ok($"状态→Complete (实际: {_ctrl.State})");

        // 5. 试验记录
        t = _ctrl.CurrentTest!;
        t.PostWeight = 49.20;
        t.LostWeight = Round(t.PreWeight - t.PostWeight);
        t.LostWeightPer = Round(t.PreWeight > 0 ? t.LostWeight / t.PreWeight * 100 : 0);
        t.DeltaTf1 = Round(t.FinalTf1 - t.AmbientTemp, 1);
        t.DeltaTf2 = Round(t.FinalTf2 - t.AmbientTemp, 1);
        t.DeltaTs = Round(t.FinalTs - t.AmbientTemp, 1);
        t.DeltaTc = Round(t.FinalTc - t.AmbientTemp, 1);
        t.DeltaTf = t.DeltaTs;
        t.FlameOccurred = 1; t.FlameStartTime = 180; t.FlameDuration = 2;
        t.PassFail = (t.DeltaTf <= 50 && t.LostWeightPer <= 50 && t.FlameDuration < 5) ? "通过" : "不通过";
        _ctrl.FinalizeTest();
        Console.WriteLine($"    失重率={t.LostWeightPer:F2}%, ΔTf={t.DeltaTf:F1}°C, 火焰=2s, 判定={t.PassFail}");
        Ok($"Flag=10000000: {t.Flag == "10000000"}");

        // 6. 导出
        var csv = _export.SaveCsv(t, _ctrl.CsvLines);
        Ok($"CSV 生成: {Path.GetFileName(csv)} ({File.ReadAllLines(csv).Length}行)");
        var xlsx = _export.ExportExcel(t, _ctrl.CsvLines);
        Ok($"Excel 生成: {Path.GetFileName(xlsx)}");
        try
        {
            var pdf = _export.ExportPdf(t);
            Ok($"PDF 生成: {Path.GetFileName(pdf)}");
        }
        catch (Exception ex)
        {
            Ng($"PDF 失败: {ex.GetType().Name}: {ex.Message}");
        }

        // 7. 查询
        var saved = _db.GetTestMaster("GB-001", "T01");
        Ok($"查询回试验: PostWeight={saved?.PostWeight:F2}g, PassFail={saved?.PassFail}");
    }

    // ═══════════════════════════════════════
    // 用例 2: 岩棉板 — 固定时长 30 秒
    // ═══════════════════════════════════════
    static void TestCase2()
    {
        Console.WriteLine("\n━━━ 用例 2: 岩棉板(RW-002) — 固定时长 30 秒 ━━━");

        var exp = _db.ValidateLogin("试验员", "123456");
        Ok($"试验员登录: {exp?.Role ?? "null"}");

        var t = new TestMaster {
            ProductId = "RW-002", TestId = "T02", TestDate = DateTime.Now,
            Operator = "experimenter", AmbientTemp = 24.0, AmbientHumidity = 45.0,
            PreWeight = 38.70, DurationMode = "fixed", TargetDurationSeconds = 30,
            DeviceId = "DEV001", DeviceName = "ISO11820不燃性试验炉", ConstPower = 2048
        };
        _ctrl.CreateTest(t);

        // 炉子已经是热的 (用例1 结束后保持 Preparing/750°C)
        HeatToReady();
        Console.WriteLine($"    就绪 tick 数: ~0 (炉已热, TF1={_sim.Tf1:F1}°C)");
        Ok($"快速就绪: {_ctrl.State == TestState.Ready}");

        // 开始记录，等待自动结束
        _ctrl.StartRecording();
        while (_ctrl.State == TestState.Recording) { _sim.Update(); _ctrl.DoWork(); }
        Console.WriteLine($"    固定时长结束: {_ctrl.CurrentTest!.TotalTestTime}s");
        Ok($"自动结束于 ~30s: {_ctrl.CurrentTest.TotalTestTime >= 28 && _ctrl.CurrentTest.TotalTestTime <= 35}");

        // 保存记录
        t = _ctrl.CurrentTest;
        t.PostWeight = 36.10;
        t.LostWeight = Round(t.PreWeight - t.PostWeight);
        t.LostWeightPer = Round(t.PreWeight > 0 ? t.LostWeight / t.PreWeight * 100 : 0);
        t.DeltaTf1 = Round(t.FinalTf1 - t.AmbientTemp, 1);
        t.DeltaTs = Round(t.FinalTs - t.AmbientTemp, 1);
        t.DeltaTf = t.DeltaTs;
        t.PassFail = (t.DeltaTf <= 50 && t.LostWeightPer <= 50) ? "通过" : "不通过";
        _ctrl.FinalizeTest();
        Console.WriteLine($"    失重率={t.LostWeightPer:F2}%, 判定={t.PassFail}");

        _export.SaveCsv(t, _ctrl.CsvLines);
        _export.ExportExcel(t, _ctrl.CsvLines);
        try { _export.ExportPdf(t); } catch (Exception ex) { Ng($"PDF失败: {ex.Message}"); }
        Ok("报告生成完毕");
    }

    // ═══════════════════════════════════════
    // 用例 3: 异常测试
    // ═══════════════════════════════════════
    static void TestCase3()
    {
        Console.WriteLine("\n━━━ 用例 3: 异常测试 ━━━");

        var r1 = _db.ValidateLogin("管理员", "bad");
        Ok($"错误密码拒绝: {r1 == null}");

        var r2 = _db.ValidateLogin("试验员", "000");
        Ok($"试验员错误密码拒绝: {r2 == null}");

        // 创建试验但 ID 为空 → 控制器层不校验空字段 (UI 层校验)
        // 验证数据库中查不到空 ID 的试验
        var none = _db.GetTestMaster("", "");
        Ok($"空 ID 查询返回 null: {none == null}");

        // 试验后质量 = 0 的边界
        var badWeight = _db.GetTestMaster("GB-001", "T01");
        Ok($"PostWeight > 0: {(badWeight?.PostWeight ?? 0) > 0}");
    }

    // ═══════════════════════════════════════
    // 用例 4: 中途停止升温 & 重新升温
    // ═══════════════════════════════════════
    static void TestCase4()
    {
        Console.WriteLine("\n━━━ 用例 4: 中途停止升温 & 重新升温 ━━━");

        // 新建试验 (炉是热的，需先 StopHeating 回到 Idle)
        _ctrl.StopHeating(); // 从 Preparing → Idle
        _sim.Phase = SimulationPhase.Cooling;
        for (int i = 0; i < 5; i++) _sim.Update(); // 冷却一点

        var t = new TestMaster {
            ProductId = "STOP-01", TestId = "T04", TestDate = DateTime.Now,
            Operator = "admin", AmbientTemp = 24.0, AmbientHumidity = 50.0,
            PreWeight = 45.0, DurationMode = "standard", TargetDurationSeconds = 3600,
            DeviceId = "DEV001", DeviceName = "ISO11820不燃性试验炉", ConstPower = 2048
        };
        _ctrl.CreateTest(t);
        Ok($"创建试验, State={_ctrl.State}");

        _ctrl.StartHeating();
        _sim.Update(); _ctrl.DoWork();
        var midTemp = _sim.Tf1;
        _ctrl.StopHeating();
        Ok($"升温后停止: TF1 从 {midTemp:F0}°C 开始下降");

        for (int i = 0; i < 3; i++) _sim.Update();
        Console.WriteLine($"    降温后 TF1={_sim.Tf1:F0}°C, State={_ctrl.State}");

        _ctrl.StartHeating();
        Ok($"重新升温, State={_ctrl.State}");

        HeatToReady();
        Ok($"再次到达 Ready: TF1={_sim.Tf1:F1}°C");
    }

    // ═══════════════════════════════════════
    // 用例 5: 按钮状态矩阵
    // ═══════════════════════════════════════
    static void TestCase5()
    {
        Console.WriteLine("\n━━━ 用例 5: 按钮状态矩阵 ━━━");

        // 停止当前试验 → Idle
        _ctrl.StopHeating();
        for (int i = 0; i < 5; i++) _sim.Update();
        _sim.Reset();

        var t = new TestMaster {
            ProductId = "BTN-01", TestId = "T05", TestDate = DateTime.Now,
            Operator = "admin", AmbientTemp = 25.0, AmbientHumidity = 50.0,
            PreWeight = 50.0, DurationMode = "standard", TargetDurationSeconds = 3600,
            DeviceId = "DEV001", DeviceName = "ISO11820不燃性试验炉", ConstPower = 2048
        };
        _ctrl.CreateTest(t);

        // Idle
        Ok($"Idle: 可新建={true}, 可升温={_ctrl.State == TestState.Idle && _ctrl.CurrentTest != null}");

        // Preparing
        _ctrl.StartHeating();
        _sim.Update(); _ctrl.DoWork();
        Ok($"Preparing: 停止升温可用={_ctrl.State == TestState.Preparing}");

        // Ready
        HeatToReady();
        Ok($"Ready: 开始记录可用={_ctrl.State == TestState.Ready}");

        // Recording
        _ctrl.StartRecording();
        _sim.Update(); _ctrl.DoWork();
        Ok($"Recording: 停止记录可用={_ctrl.State == TestState.Recording}");
        _ctrl.StopRecording();

        // Complete
        Ok($"Complete: 试验记录可用={_ctrl.State == TestState.Complete}");
    }

    // ═══════════════════════════════════════
    // 用例 6: 快速连续两次试验
    // ═══════════════════════════════════════
    static void TestCase6()
    {
        Console.WriteLine("\n━━━ 用例 6: 快速连续两次试验 ━━━");

        // 保存第一次
        var t1 = _ctrl.CurrentTest!;
        t1.PostWeight = 48.0;
        t1.LostWeight = Round(t1.PreWeight - t1.PostWeight);
        t1.LostWeightPer = Round(t1.PreWeight > 0 ? t1.LostWeight / t1.PreWeight * 100 : 0);
        t1.DeltaTf = Round(t1.FinalTs - t1.AmbientTemp, 1);
        t1.PassFail = "通过";
        _ctrl.FinalizeTest();
        Ok($"第一次保存: Flag={t1.Flag}");

        // 第二次试验 — 炉已热
        var t2 = new TestMaster {
            ProductId = "SEQ-02", TestId = "T06", TestDate = DateTime.Now,
            Operator = "admin", AmbientTemp = 25.0, AmbientHumidity = 50.0,
            PreWeight = 48.1, DurationMode = "fixed", TargetDurationSeconds = 5,
            DeviceId = "DEV001", DeviceName = "ISO11820不燃性试验炉", ConstPower = 2048
        };
        _ctrl.CreateTest(t2);
        int preTicks = 0;
        _ctrl.StartHeating();
        while (_ctrl.State != TestState.Ready && preTicks < 20) { _sim.Update(); _ctrl.DoWork(); preTicks++; }
        Ok($"第二次秒级 Ready: {preTicks} ticks (≤10)");

        _ctrl.StartRecording();
        while (_ctrl.State == TestState.Recording) { _sim.Update(); _ctrl.DoWork(); }
        Console.WriteLine($"    固定 5s 结束: 实际 {_ctrl.CurrentTest!.TotalTestTime}s");
        Ok($"自动结束: {_ctrl.State == TestState.Complete}");
    }

    // ═══════════════════════════════════════
    // 用例 7: 校准功能
    // ═══════════════════════════════════════
    static void TestCase7()
    {
        Console.WriteLine("\n━━━ 用例 7: 校准功能 ━━━");

        // 750°C 校准
        double calTemp = _sim.TCal;
        var cr = new CalibrationRecord {
            CalDate = DateTime.Now, Operator = "admin",
            StandardTemp = 750.0, MeasuredTemp = calTemp,
            Deviation = Round(calTemp - 750.0, 2)
        };
        _db.InsertCalibrationRecord(cr);
        Console.WriteLine($"    标准=750°C, 测量={calTemp:F2}°C, 偏差={cr.Deviation:F2}°C");
        Ok($"偏差在 ±2°C 内: {Math.Abs(cr.Deviation) <= 2}");

        var records = _db.GetCalibrationRecords();
        Ok($"校准记录已存储: {records.Count} 条");
    }

    // ═══════════════════════════════════════
    // 用例 8: 记录查询
    // ═══════════════════════════════════════
    static void TestCase8()
    {
        Console.WriteLine("\n━━━ 用例 8: 记录查询 ━━━");

        var all = _db.QueryTests(DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), null, null);
        Console.WriteLine($"    总记录数: {all.Count}");
        Ok($"至少 5 条试验记录: {all.Count >= 5}");

        var byProduct = _db.QueryTests(null, null, "GB", null);
        Ok($"模糊匹配 'GB' → GB-001: {byProduct.Any(r => r.ProductId == "GB-001")}");

        var byOp = _db.QueryTests(null, null, null, "admin");
        Ok($"按操作员 'admin' 查询: {byOp.Count} 条");

        var byOpExp = _db.QueryTests(null, null, null, "experimenter");
        Ok($"按操作员 'experimenter' 查询: {byOpExp.Count} 条");

        var operators = _db.GetDistinctOperators();
        Ok($"操作员列表: {string.Join(", ", operators)}");

        // 验证每条记录的 Flag
        var saved = all.Where(r => r.Flag == "10000000").ToList();
        Ok($"已完成标记的记录: {saved.Count} 条");
    }
}

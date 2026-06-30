using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ISO11820.Models;

namespace ISO11820.Data;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(IConfiguration config)
    {
        var dbPath = config["Database:SqlitePath"] ?? "Data\\ISO11820.db";
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS operators (
                userid INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                pwd TEXT NOT NULL,
                role TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS productmaster (
                productid TEXT PRIMARY KEY,
                productname TEXT NOT NULL,
                specification TEXT,
                height REAL,
                diameter REAL
            );
            CREATE TABLE IF NOT EXISTS apparatus (
                deviceid TEXT PRIMARY KEY,
                devicename TEXT NOT NULL,
                calibrationdate TEXT,
                constpower REAL,
                serialport TEXT
            );
            CREATE TABLE IF NOT EXISTS sensors (
                channelid INTEGER PRIMARY KEY,
                channelname TEXT NOT NULL,
                rangemin REAL,
                rangemax REAL,
                unit TEXT
            );
            CREATE TABLE IF NOT EXISTS testmaster (
                productid TEXT NOT NULL,
                testid TEXT NOT NULL,
                testdate TEXT,
                operator_name TEXT,
                ambienttemp REAL,
                ambienthumidity REAL,
                preweight REAL,
                postweight REAL,
                lostweight REAL,
                lostweight_per REAL,
                deltatf REAL,
                deltats REAL,
                deltatc REAL,
                deltatf1 REAL,
                deltatf2 REAL,
                totaltesttime INTEGER,
                durationmode TEXT DEFAULT 'standard',
                targetdurationseconds INTEGER DEFAULT 0,
                flameoccurred INTEGER DEFAULT 0,
                flamestarttime INTEGER DEFAULT 0,
                flameduration INTEGER DEFAULT 0,
                remark TEXT,
                flag TEXT DEFAULT '',
                finaltf1 REAL,
                finaltf2 REAL,
                finalts REAL,
                finaltc REAL,
                deviceid TEXT,
                devicename TEXT,
                constpower REAL,
                passfail TEXT DEFAULT '',
                PRIMARY KEY (productid, testid)
            );
            CREATE TABLE IF NOT EXISTS calibrationrecords (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                caldate TEXT,
                operator_name TEXT,
                standardtemp REAL,
                measuredtemp REAL,
                deviation REAL,
                remark TEXT
            );
        ";
        cmd.ExecuteNonQuery();
        SeedDefaultData(conn);
    }

    private void SeedDefaultData(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO operators (username, pwd, role) VALUES ('admin', '123456', '管理员');
            INSERT OR IGNORE INTO operators (username, pwd, role) VALUES ('experimenter', '123456', '试验员');
            INSERT OR IGNORE INTO apparatus (deviceid, devicename, calibrationdate, constpower, serialport)
            VALUES ('DEV001', 'ISO11820不燃性试验炉', '2025-01-01', 2048, 'COM3');
            INSERT OR IGNORE INTO sensors (channelid, channelname, rangemin, rangemax, unit) VALUES (1, '炉温1(TF1)', 0, 1000, '°C');
            INSERT OR IGNORE INTO sensors (channelid, channelname, rangemin, rangemax, unit) VALUES (2, '炉温2(TF2)', 0, 1000, '°C');
            INSERT OR IGNORE INTO sensors (channelid, channelname, rangemin, rangemax, unit) VALUES (3, '表面温(TS)', 0, 1000, '°C');
            INSERT OR IGNORE INTO sensors (channelid, channelname, rangemin, rangemax, unit) VALUES (4, '中心温(TC)', 0, 1000, '°C');
            INSERT OR IGNORE INTO sensors (channelid, channelname, rangemin, rangemax, unit) VALUES (5, '校准温(TCal)', 0, 1000, '°C');
        ";
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public Operator? ValidateLogin(string role, string password)
    {
        var roleMap = new Dictionary<string, string>
        {
            { "管理员", "admin" },
            { "试验员", "experimenter" }
        };
        if (!roleMap.TryGetValue(role, out var username))
            return null;

        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT userid, username, pwd, role FROM operators WHERE username = @u AND pwd = @p";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", password);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Operator
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Pwd = reader.GetString(2),
                Role = reader.GetString(3)
            };
        }
        return null;
    }

    public void InsertProduct(ProductMaster p)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO productmaster (productid, productname, specification, height, diameter)
                            VALUES (@pid, @pn, @sp, @h, @d)";
        cmd.Parameters.AddWithValue("@pid", p.ProductId);
        cmd.Parameters.AddWithValue("@pn", p.ProductName);
        cmd.Parameters.AddWithValue("@sp", p.Specification);
        cmd.Parameters.AddWithValue("@h", p.Height);
        cmd.Parameters.AddWithValue("@d", p.Diameter);
        cmd.ExecuteNonQuery();
    }

    public Apparatus? GetApparatus()
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM apparatus LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Apparatus
            {
                DeviceId = reader.GetString(0),
                DeviceName = reader.GetString(1),
                CalibrationDate = DateTime.Parse(reader.GetString(2)),
                ConstPower = reader.GetDouble(3),
                SerialPort = reader.GetString(4)
            };
        }
        return null;
    }

    public List<SensorConfig> GetSensors()
    {
        var list = new List<SensorConfig>();
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sensors ORDER BY channelid";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SensorConfig
            {
                ChannelId = reader.GetInt32(0),
                ChannelName = reader.GetString(1),
                RangeMin = reader.GetDouble(2),
                RangeMax = reader.GetDouble(3),
                Unit = reader.GetString(4)
            });
        }
        return list;
    }

    public void InsertTestMaster(TestMaster t)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO testmaster
            (productid, testid, testdate, operator_name, ambienttemp, ambienthumidity,
             preweight, postweight, lostweight, lostweight_per, deltatf, deltats, deltatc,
             deltatf1, deltatf2, totaltesttime, durationmode, targetdurationseconds,
             flameoccurred, flamestarttime, flameduration, remark, flag,
             finaltf1, finaltf2, finalts, finaltc, deviceid, devicename, constpower, passfail)
            VALUES
            (@pid, @tid, @td, @op, @at, @ah, @pw, @pow, @lw, @lwp, @dtf, @dts, @dtc,
             @dtf1, @dtf2, @ttt, @dm, @tds, @fo, @fst, @fd, @rm, @fl,
             @ftf1, @ftf2, @fts, @ftc, @did, @dn, @cp, @pf)";
        cmd.Parameters.AddWithValue("@pid", t.ProductId);
        cmd.Parameters.AddWithValue("@tid", t.TestId);
        cmd.Parameters.AddWithValue("@td", t.TestDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@op", t.Operator);
        cmd.Parameters.AddWithValue("@at", t.AmbientTemp);
        cmd.Parameters.AddWithValue("@ah", t.AmbientHumidity);
        cmd.Parameters.AddWithValue("@pw", t.PreWeight);
        cmd.Parameters.AddWithValue("@pow", t.PostWeight);
        cmd.Parameters.AddWithValue("@lw", t.LostWeight);
        cmd.Parameters.AddWithValue("@lwp", t.LostWeightPer);
        cmd.Parameters.AddWithValue("@dtf", t.DeltaTf);
        cmd.Parameters.AddWithValue("@dts", t.DeltaTs);
        cmd.Parameters.AddWithValue("@dtc", t.DeltaTc);
        cmd.Parameters.AddWithValue("@dtf1", t.DeltaTf1);
        cmd.Parameters.AddWithValue("@dtf2", t.DeltaTf2);
        cmd.Parameters.AddWithValue("@ttt", t.TotalTestTime);
        cmd.Parameters.AddWithValue("@dm", t.DurationMode);
        cmd.Parameters.AddWithValue("@tds", t.TargetDurationSeconds);
        cmd.Parameters.AddWithValue("@fo", t.FlameOccurred);
        cmd.Parameters.AddWithValue("@fst", t.FlameStartTime);
        cmd.Parameters.AddWithValue("@fd", t.FlameDuration);
        cmd.Parameters.AddWithValue("@rm", t.Remark);
        cmd.Parameters.AddWithValue("@fl", t.Flag);
        cmd.Parameters.AddWithValue("@ftf1", t.FinalTf1);
        cmd.Parameters.AddWithValue("@ftf2", t.FinalTf2);
        cmd.Parameters.AddWithValue("@fts", t.FinalTs);
        cmd.Parameters.AddWithValue("@ftc", t.FinalTc);
        cmd.Parameters.AddWithValue("@did", t.DeviceId);
        cmd.Parameters.AddWithValue("@dn", t.DeviceName);
        cmd.Parameters.AddWithValue("@cp", t.ConstPower);
        cmd.Parameters.AddWithValue("@pf", t.PassFail);
        cmd.ExecuteNonQuery();
    }

    public TestMaster? GetTestMaster(string productId, string testId)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM testmaster WHERE productid = @pid AND testid = @tid";
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@tid", testId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadTestMaster(reader);
        return null;
    }

    public List<TestMaster> QueryTests(DateTime? startDate, DateTime? endDate, string? productId, string? operatorName)
    {
        var list = new List<TestMaster>();
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        var sql = "SELECT * FROM testmaster WHERE 1=1";
        if (startDate.HasValue) { sql += " AND testdate >= @sd"; cmd.Parameters.AddWithValue("@sd", startDate.Value.ToString("yyyy-MM-dd")); }
        if (endDate.HasValue) { sql += " AND testdate <= @ed"; cmd.Parameters.AddWithValue("@ed", endDate.Value.ToString("yyyy-MM-dd") + " 23:59:59"); }
        if (!string.IsNullOrEmpty(productId)) { sql += " AND productid LIKE @pid"; cmd.Parameters.AddWithValue("@pid", $"%{productId}%"); }
        if (!string.IsNullOrEmpty(operatorName)) { sql += " AND operator_name = @op"; cmd.Parameters.AddWithValue("@op", operatorName); }
        sql += " ORDER BY testdate DESC";
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadTestMaster(reader));
        return list;
    }

    public List<string> GetDistinctOperators()
    {
        var list = new List<string>();
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT operator_name FROM testmaster WHERE operator_name IS NOT NULL ORDER BY operator_name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    private TestMaster ReadTestMaster(SqliteDataReader reader)
    {
        return new TestMaster
        {
            ProductId = reader.GetString(0),
            TestId = reader.GetString(1),
            TestDate = DateTime.Parse(reader.GetString(2)),
            Operator = reader.GetString(3),
            AmbientTemp = reader.GetDouble(4),
            AmbientHumidity = reader.GetDouble(5),
            PreWeight = reader.GetDouble(6),
            PostWeight = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
            LostWeight = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
            LostWeightPer = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
            DeltaTf = reader.IsDBNull(10) ? 0 : reader.GetDouble(10),
            DeltaTs = reader.IsDBNull(11) ? 0 : reader.GetDouble(11),
            DeltaTc = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
            DeltaTf1 = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
            DeltaTf2 = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
            TotalTestTime = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
            DurationMode = reader.IsDBNull(16) ? "standard" : reader.GetString(16),
            TargetDurationSeconds = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
            FlameOccurred = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),
            FlameStartTime = reader.IsDBNull(19) ? 0 : reader.GetInt32(19),
            FlameDuration = reader.IsDBNull(20) ? 0 : reader.GetInt32(20),
            Remark = reader.IsDBNull(21) ? "" : reader.GetString(21),
            Flag = reader.IsDBNull(22) ? "" : reader.GetString(22),
            FinalTf1 = reader.IsDBNull(23) ? 0 : reader.GetDouble(23),
            FinalTf2 = reader.IsDBNull(24) ? 0 : reader.GetDouble(24),
            FinalTs = reader.IsDBNull(25) ? 0 : reader.GetDouble(25),
            FinalTc = reader.IsDBNull(26) ? 0 : reader.GetDouble(26),
            DeviceId = reader.IsDBNull(27) ? "" : reader.GetString(27),
            DeviceName = reader.IsDBNull(28) ? "" : reader.GetString(28),
            ConstPower = reader.IsDBNull(29) ? 0 : reader.GetDouble(29),
            PassFail = reader.IsDBNull(30) ? "" : reader.GetString(30)
        };
    }

    public void InsertCalibrationRecord(CalibrationRecord r)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO calibrationrecords (caldate, operator_name, standardtemp, measuredtemp, deviation, remark)
                            VALUES (@cd, @op, @st, @mt, @dv, @rm)";
        cmd.Parameters.AddWithValue("@cd", r.CalDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@op", r.Operator);
        cmd.Parameters.AddWithValue("@st", r.StandardTemp);
        cmd.Parameters.AddWithValue("@mt", r.MeasuredTemp);
        cmd.Parameters.AddWithValue("@dv", r.Deviation);
        cmd.Parameters.AddWithValue("@rm", r.Remark);
        cmd.ExecuteNonQuery();
    }

    public List<CalibrationRecord> GetCalibrationRecords()
    {
        var list = new List<CalibrationRecord>();
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM calibrationrecords ORDER BY caldate DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CalibrationRecord
            {
                Id = reader.GetInt32(0),
                CalDate = DateTime.Parse(reader.GetString(1)),
                Operator = reader.GetString(2),
                StandardTemp = reader.GetDouble(3),
                MeasuredTemp = reader.GetDouble(4),
                Deviation = reader.GetDouble(5),
                Remark = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }
}

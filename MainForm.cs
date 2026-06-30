using ISO11820.Core;
using ISO11820.Global;
using ISO11820.Models;
using ISO11820.Services;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using OxyPlot.Axes;
using OfficeOpenXml;

namespace ISO11820.Forms;

public class MainForm : Form
{
    private TestController _ctrl = null!;
    private ExportService _export = null!;

    private System.Windows.Forms.Timer _timer = null!;
    private DateTime _recStart;    // 记录开始的实际时间
    private int _tickCount;
    private bool _heartbeat; // ●/○ 闪烁
    private readonly Queue<double> _tf1Drift = new();

    // UI 控件
    private TabControl _tab;
    private Label _lblTf1 = null!, _lblTf2 = null!, _lblTs = null!, _lblTc = null!, _lblTCal = null!;
    private Label _lblStatus = null!, _lblTimer = null!, _lblDrift = null!, _lblSampleId = null!;
    private Button _btnNew = null!, _btnHeat = null!, _btnStopHeat = null!;
    private Button _btnRec = null!, _btnStopRec = null!;
    private Button _btnObs = null!, _btnXlsx = null!, _btnPdf = null!, _btnCfg = null!;
    private RichTextBox _rtbLog = null!;
    private PlotView _plot = null!;
    private PlotModel _plotModel = null!;
    private LineSeries _s1 = null!, _s2 = null!, _s3 = null!, _s4 = null!;
    private DataGridView _dgvTests = null!, _dgvCal = null!;
    private DateTimePicker _dtpS = null!, _dtpE = null!;
    private TextBox _txtPid = null!, _txtStdTemp = null!;
    private ComboBox _cboOp = null!;
    private Label _lblCalTemp = null!;

    public MainForm()
    {
        _ctrl = new TestController(AppGlobal.Instance.Db, AppGlobal.Instance.Configuration,
            new SensorSimulator(AppGlobal.Instance.Configuration));
        _export = new ExportService(AppGlobal.Instance.Db, AppGlobal.Instance.Configuration);

        _timer = new System.Windows.Forms.Timer { Interval = 800 };
        _timer.Tick += OnTick;

        BuildUI();
        LoadOps();
        LoadCal();
    }

    // ═══════════════ 定时器 Tick（UI 线程） ═══════════════
    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;
        _heartbeat = !_heartbeat;

        try { _ctrl.Sim.Update(); _ctrl.DoWork(); } catch { }

        try
        {
                var t = _ctrl.Sim.GetCurrentTemperatures();
                _lblTf1.Text = $"{t["TF1"]:F1} °C";
                _lblTf2.Text = $"{t["TF2"]:F1} °C";
                _lblTs.Text = $"{t["TS"]:F1} °C";
                _lblTc.Text = $"{t["TC"]:F1} °C";
                _lblTCal.Text = $"{t["TCal"]:F1} °C";
                _lblCalTemp.Text = $"校准温: {t["TCal"]:F1} °C";
                _lblTimer.Text = _ctrl.State == TestState.Recording
                    ? (DateTime.Now - _recStart).ToString(@"hh\:mm\:ss")
                    : "00:00:00";

                if (_ctrl.CurrentTest != null)
                    _lblSampleId.Text = $"样品: {_ctrl.CurrentTest.ProductId}";

                _tf1Drift.Enqueue(_ctrl.Sim.Tf1);
                while (_tf1Drift.Count > 600) _tf1Drift.Dequeue();
                // 温漂：手动线性回归
                string driftText = "计算中...";
                if (_tf1Drift.Count >= 5)
                {
                    var arr = _tf1Drift.ToArray();
                    int n = arr.Length;
                    double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        sumX += i; sumY += arr[i];
                        sumXY += i * arr[i]; sumX2 += i * (double)i;
                    }
                    double denom = n * sumX2 - sumX * sumX;
                    if (Math.Abs(denom) > 0.0001)
                    {
                        double slope = (n * sumXY - sumX * sumY) / denom;
                        driftText = $"{slope * 600:F2} °C/10min";
                    }
                }
                _lblStatus.Text = $"{(_heartbeat ? "●" : "○")} {_ctrl.GetStatusText()} | 温漂: {driftText}";
                _lblDrift.Text = $"温漂: {driftText}";

                _s1.Points.Clear(); _s2.Points.Clear(); _s3.Points.Clear(); _s4.Points.Clear();
                var h1 = _ctrl.Sim.Tf1History; var h2 = _ctrl.Sim.Tf2History;
                var h3 = _ctrl.Sim.TsHistory; var h4 = _ctrl.Sim.TcHistory;
                int cnt = Math.Min(Math.Min(h1.Count, h2.Count), Math.Min(h3.Count, h4.Count));
                int start = Math.Max(0, cnt - 750);
                double baseTime = start * 0.8;
                for (int i = start; i < cnt; i++)
                {
                    double tx = i * 0.8 - baseTime;
                    _s1.Points.Add(new DataPoint(tx, h1[i]));
                    _s2.Points.Add(new DataPoint(tx, h2[i]));
                    _s3.Points.Add(new DataPoint(tx, h3[i]));
                    _s4.Points.Add(new DataPoint(tx, h4[i]));
                }
                _plot.InvalidatePlot(true);

                foreach (var msg in _ctrl.PendingMessages)
                {
                    _rtbLog.SelectionColor = msg.Message.Contains("终止") ? Color.Yellow : Color.White;
                    _rtbLog.AppendText($"{msg.Time}  {msg.Message}\n");
                }
                _rtbLog.ScrollToCaret();
                _ctrl.PendingMessages.Clear();

                UpdateBtns();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"ERR: {ex.Message}";
        }
    }

    // ═══════════════ UI 构建 ═══════════════
    private void BuildUI()
    {
        Text = "ISO 11820 建筑材料不燃性试验仿真系统";
        Size = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1024, 600);

        _tab = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei", 9) };
        _tab.TabPages.Add(BuildMonitorTab());
        _tab.TabPages.Add(BuildQueryTab());
        _tab.TabPages.Add(BuildCalTab());
        Controls.Add(_tab);
        FormClosing += (s, e) => { _timer.Stop(); _timer.Dispose(); };
        UpdateBtns();
    }

    private TabPage BuildMonitorTab()
    {
        var tp = new TabPage("实时监控");
        var f = new Font("Microsoft YaHei", 9);

        // 温度面板
        var pan = new Panel { Location = new Point(10, 10), Size = new Size(800, 80), BackColor = Color.FromArgb(30, 30, 30) };
        _lblTf1 = MakeLed(pan, "炉温1(TF1)", 5); _lblTf2 = MakeLed(pan, "炉温2(TF2)", 160);
        _lblTs = MakeLed(pan, "表面温(TS)", 315); _lblTc = MakeLed(pan, "中心温(TC)", 470);
        _lblTCal = MakeLed(pan, "校准温(TCal)", 625);

        // 信息栏
        var ip = new Panel { Location = new Point(10, 100), Size = new Size(800, 30) };
        _lblStatus = new Label { Location = new Point(0, 3), Font = new Font(f.Name, 10, FontStyle.Bold), ForeColor = Color.FromArgb(0, 122, 204), Text = "空闲" };
        _lblTimer = new Label { Location = new Point(150, 3), Font = new Font("Consolas", 12), Text = "00:00:00" };
        _lblDrift = new Label { Location = new Point(350, 3), Size = new Size(250, 22), Font = f, Text = "温漂: -- °C/10min", ForeColor = Color.DarkBlue };
        _lblSampleId = new Label { Location = new Point(600, 3), Font = f, Text = "样品: --" };
        ip.Controls.AddRange(new Control[] { _lblStatus, _lblTimer, _lblDrift, _lblSampleId });

        // 按钮
        var bp = new Panel { Location = new Point(10, 140), Size = new Size(800, 40) };
        _btnNew = Btn("新建试验", 0, Color.FromArgb(76, 175, 80), (s, e) => { if (new NewTestForm(_ctrl).ShowDialog() == DialogResult.OK) UpdateBtns(); });
        _btnHeat = Btn("开始升温", 100, Color.FromArgb(255, 152, 0), (s, e) => { _ctrl.StartHeating(); _timer.Start(); UpdateBtns(); });
        _btnStopHeat = Btn("停止升温", 200, Color.FromArgb(244, 67, 54), (s, e) => { _ctrl.StopHeating(); _timer.Stop(); UpdateBtns(); });
        _btnRec = Btn("开始记录", 300, Color.FromArgb(0, 188, 212), (s, e) => { _ctrl.StartRecording(); _recStart = DateTime.Now; UpdateBtns(); });
        _btnStopRec = Btn("停止记录", 400, Color.FromArgb(156, 39, 176), (s, e) => { _ctrl.StopRecording(); UpdateBtns(); });
        _btnObs = Btn("试验记录", 500, Color.FromArgb(121, 85, 72), (s, e) => { if (_ctrl.CurrentTest != null) { if (new ObservationForm(_ctrl.CurrentTest, _ctrl, _export).ShowDialog() == DialogResult.OK) UpdateBtns(); } });
        _btnXlsx = Btn("导出Excel", 600, Color.FromArgb(63, 81, 181), (s, e) => { if (_ctrl.CurrentTest != null) { var p = _export.ExportExcel(_ctrl.CurrentTest, _ctrl.CsvLines); MessageBox.Show($"已导出:\n{p}"); } });
        _btnPdf = Btn("导出PDF", 700, Color.FromArgb(233, 30, 99), (s, e) => { if (_ctrl.CurrentTest != null) { var p = _export.ExportPdf(_ctrl.CurrentTest); MessageBox.Show($"已导出:\n{p}"); } });
        _btnCfg = Btn("参数设置", 800, Color.FromArgb(96, 125, 139), (s, e) => MessageBox.Show($"升温速率: {_ctrl.Sim.HeatingRatePerSecond}°C/s\n目标温度: {_ctrl.Sim.TargetTemp}°C", "参数"));
        bp.Controls.AddRange(new Control[] { _btnNew, _btnHeat, _btnStopHeat, _btnRec, _btnStopRec, _btnObs, _btnXlsx, _btnPdf, _btnCfg });

        // 曲线
        _plotModel = new PlotModel { Title = "温度曲线", TitleFontSize = 12 };
        _plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "时间 (秒)", Minimum = 0, Maximum = 600 });
        _plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "温度 (°C)", Minimum = 0, Maximum = 800 });
        _s1 = new LineSeries { Title = "炉温1", Color = OxyColor.FromRgb(255, 59, 48), StrokeThickness = 1.5 };
        _s2 = new LineSeries { Title = "炉温2", Color = OxyColor.FromRgb(0, 122, 255), StrokeThickness = 1.5 };
        _s3 = new LineSeries { Title = "表面温", Color = OxyColor.FromRgb(255, 149, 0), StrokeThickness = 1.5 };
        _s4 = new LineSeries { Title = "中心温", Color = OxyColor.FromRgb(76, 217, 100), StrokeThickness = 1.5 };
        _plotModel.Series.Add(_s1); _plotModel.Series.Add(_s2); _plotModel.Series.Add(_s3); _plotModel.Series.Add(_s4);
        _plot = new PlotView { Location = new Point(10, 190), Size = new Size(800, 320), Model = _plotModel, BackColor = Color.White };

        var ll = new Label { Text = "系统消息", Location = new Point(10, 520), Font = new Font(f.Name, 9, FontStyle.Bold) };
        _rtbLog = new RichTextBox { Location = new Point(10, 545), Size = new Size(800, 180), BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.White, Font = new Font("Consolas", 9), ReadOnly = true };

        tp.Controls.AddRange(new Control[] { pan, ip, bp, _plot, ll, _rtbLog });
        return tp;
    }

    private Label MakeLed(Panel parent, string name, int x)
    {
        var p = new Panel { Location = new Point(x, 5), Size = new Size(148, 70), BackColor = Color.FromArgb(45, 45, 45) };
        p.Controls.Add(new Label { Text = name, Location = new Point(2, 3), Size = new Size(144, 16), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei", 7), TextAlign = ContentAlignment.MiddleCenter });
        var v = new Label { Text = "--.- °C", Location = new Point(2, 22), Size = new Size(144, 45), ForeColor = Color.Lime, Font = new Font("Consolas", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        p.Controls.Add(v);
        parent.Controls.Add(p);
        return v;
    }

    private Button Btn(string text, int x, Color c, EventHandler h)
    {
        var b = new Button { Text = text, Location = new Point(x, 2), Size = new Size(90, 35), BackColor = c, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += h;
        return b;
    }

    private void UpdateBtns()
    {
        var s = _ctrl.State; var t = _ctrl.CurrentTest;
        bool uf = t != null && t.TotalTestTime > 0 && t.Flag != "10000000";
        _btnNew.Enabled = s == TestState.Idle || !uf;
        _btnHeat.Enabled = s == TestState.Idle && t != null;
        _btnStopHeat.Enabled = s == TestState.Preparing || s == TestState.Ready || s == TestState.Complete;
        _btnRec.Enabled = s == TestState.Ready;
        _btnStopRec.Enabled = s == TestState.Recording;
        _btnObs.Enabled = s == TestState.Complete || uf;
        _btnXlsx.Enabled = uf || t?.Flag == "10000000";
        _btnPdf.Enabled = uf || t?.Flag == "10000000";
        _btnCfg.Enabled = s != TestState.Recording;
    }

    // ═══════════════ 记录查询 Tab ═══════════════
    private TabPage BuildQueryTab()
    {
        var tp = new TabPage("记录查询");
        var p = new Panel { Location = new Point(10, 10), Size = new Size(1050, 40) };
        p.Controls.Add(new Label { Text = "开始:", Location = new Point(0, 10), Size = new Size(40, 20), Font = new Font("Microsoft YaHei", 9) });
        _dtpS = new DateTimePicker { Location = new Point(45, 8), Size = new Size(120, 22), Format = DateTimePickerFormat.Short };
        _dtpE = new DateTimePicker { Location = new Point(210, 8), Size = new Size(120, 22), Format = DateTimePickerFormat.Short };
        p.Controls.Add(new Label { Text = "结束:", Location = new Point(170, 10), Size = new Size(40, 20), Font = new Font("Microsoft YaHei", 9) });
        p.Controls.Add(new Label { Text = "样品:", Location = new Point(340, 10), Size = new Size(40, 20), Font = new Font("Microsoft YaHei", 9) });
        _txtPid = new TextBox { Location = new Point(380, 8), Size = new Size(90, 22) };
        p.Controls.Add(new Label { Text = "操作员:", Location = new Point(480, 10), Size = new Size(55, 20), Font = new Font("Microsoft YaHei", 9) });
        _cboOp = new ComboBox { Location = new Point(535, 8), Size = new Size(90, 22) };
        var bq = new Button { Text = "查询", Location = new Point(640, 6), Size = new Size(70, 28), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        bq.Click += (s, e) => DoQuery();
        var be = new Button { Text = "导出", Location = new Point(720, 6), Size = new Size(70, 28), BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        be.Click += (s, e) => ExportQuery();
        p.Controls.AddRange(new Control[] { _dtpS, _dtpE, _txtPid, _cboOp, bq, be });

        _dgvTests = new DataGridView { Location = new Point(10, 60), Size = new Size(1050, 480), AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Font = new Font("Microsoft YaHei", 9) };
        _dgvTests.DoubleClick += (s, e) => {
            if (_dgvTests.CurrentRow?.DataBoundItem == null) return;
            var r = _dgvTests.CurrentRow.DataBoundItem;
            var pid = r.GetType().GetProperty("ProductId")?.GetValue(r)?.ToString();
            var tid = r.GetType().GetProperty("TestId")?.GetValue(r)?.ToString();
            if (pid != null && tid != null) { var dt = AppGlobal.Instance.Db.GetTestMaster(pid, tid); if (dt != null) new TestDetailForm(dt).ShowDialog(); }
        };
        tp.Controls.AddRange(new Control[] { p, _dgvTests });
        _dtpS.Value = DateTime.Now.AddMonths(-1); _dtpE.Value = DateTime.Now;
        return tp;
    }

    private void DoQuery()
    {
        var r = AppGlobal.Instance.Db.QueryTests(_dtpS.Value.Date, _dtpE.Value.Date.AddDays(1),
            string.IsNullOrWhiteSpace(_txtPid.Text) ? null : _txtPid.Text.Trim(),
            _cboOp.SelectedIndex > 0 ? _cboOp.Text : null);
        _dgvTests.DataSource = r.Select(x => new { x.ProductId, x.TestId, 日期 = x.TestDate.ToString("yyyy-MM-dd HH:mm"), x.Operator, 失重率 = $"{x.LostWeightPer:F2}%", 温升 = $"{x.DeltaTf:F1}°C", 判定 = x.PassFail }).ToList();
        LoadOps();
    }

    private void ExportQuery()
    {
        if (_dgvTests.Rows.Count == 0) return;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var dir = AppGlobal.Instance.Configuration["Report:OutputDirectory"] ?? "D:\\ISO11820\\Reports";
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"查询_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        using var pkg = new ExcelPackage();
        var sh = pkg.Workbook.Worksheets.Add("结果");
        for (int c = 0; c < _dgvTests.Columns.Count; c++) sh.Cells[1, c + 1].Value = _dgvTests.Columns[c].HeaderText;
        for (int r = 0; r < _dgvTests.Rows.Count; r++)
            for (int c = 0; c < _dgvTests.Columns.Count; c++)
                sh.Cells[r + 2, c + 1].Value = _dgvTests.Rows[r].Cells[c].Value?.ToString();
        pkg.SaveAs(new FileInfo(path));
        MessageBox.Show($"已导出:\n{path}");
    }

    private void LoadOps()
    {
        var ops = AppGlobal.Instance.Db.GetDistinctOperators();
        _cboOp.Items.Clear(); _cboOp.Items.Add("(全部)"); _cboOp.SelectedIndex = 0;
        foreach (var o in ops) _cboOp.Items.Add(o);
    }

    // ═══════════════ 设备校准 Tab ═══════════════
    private TabPage BuildCalTab()
    {
        var tp = new TabPage("设备校准");
        var p = new Panel { Location = new Point(10, 10), Size = new Size(600, 50), BackColor = Color.FromArgb(30, 30, 30) };
        _lblCalTemp = new Label { Text = "校准温: --.- °C", Location = new Point(10, 10), Size = new Size(250, 30), ForeColor = Color.Lime, Font = new Font("Consolas", 16, FontStyle.Bold) };
        _txtStdTemp = new TextBox { Location = new Point(350, 12), Size = new Size(70, 22), Text = "750" };
        var br = new Button { Text = "记录校准", Location = new Point(430, 10), Size = new Size(90, 28), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        br.Click += (s, e) => {
            if (double.TryParse(_txtStdTemp.Text, out double st))
            {
                var cr = new CalibrationRecord { CalDate = DateTime.Now, Operator = AppGlobal.Instance.CurrentUser?.Username ?? "", StandardTemp = st, MeasuredTemp = _ctrl.Sim.TCal, Deviation = _ctrl.Sim.TCal - st };
                AppGlobal.Instance.Db.InsertCalibrationRecord(cr);
                LoadCal();
            }
        };
        p.Controls.AddRange(new Control[] { _lblCalTemp, new Label { Text = "标准温度:", Location = new Point(270, 15), Size = new Size(75, 20), ForeColor = Color.White, Font = new Font("Microsoft YaHei", 9) }, _txtStdTemp, br });

        _dgvCal = new DataGridView { Location = new Point(10, 70), Size = new Size(900, 400), AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Microsoft YaHei", 9) };
        tp.Controls.AddRange(new Control[] { p, _dgvCal });
        return tp;
    }

    private void LoadCal()
    {
        var rs = AppGlobal.Instance.Db.GetCalibrationRecords();
        _dgvCal.DataSource = rs.Select(r => new { 日期 = r.CalDate.ToString("yyyy-MM-dd HH:mm"), r.Operator, 标准 = r.StandardTemp, 测量 = r.MeasuredTemp, 偏差 = $"{r.Deviation:F2}" }).ToList();
    }
}

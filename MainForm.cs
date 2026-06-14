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
    private TestController _testController = null!;
    private DaqWorker _daqWorker = null!;
    private ExportService _exportService = null!;
    private SensorSimulator _simulator = null!;

    // Tab
    private TabControl _tabControl;

    // 温度标签
    private Label _lblTf1 = null!, _lblTf2 = null!, _lblTs = null!, _lblTc = null!, _lblTCal = null!;
    private Label _lblStatus = null!, _lblTimer = null!, _lblDrift = null!, _lblSampleId = null!;

    // 按钮
    private Button _btnNewTest = null!, _btnStartHeat = null!, _btnStopHeat = null!;
    private Button _btnStartRecord = null!, _btnStopRecord = null!;
    private Button _btnObservation = null!, _btnExportExcel = null!, _btnExportPdf = null!, _btnSettings = null!;

    // 日志与曲线
    private RichTextBox _rtbLog = null!;
    private PlotView _plotView = null!;
    private PlotModel _plotModel = null!;
    private LineSeries _seriesTf1 = null!, _seriesTf2 = null!, _seriesTs = null!, _seriesTc = null!;

    // 查询
    private DataGridView _dgvTests = null!;
    private DateTimePicker _dtpStart = null!, _dtpEnd = null!;
    private TextBox _txtSearchProduct = null!;
    private ComboBox _cboSearchOperator = null!;

    // 校准
    private Label _lblCalTemp = null!;
    private TextBox _txtStandardTemp = null!;
    private DataGridView _dgvCalRecords = null!;

    public MainForm()
    {
        InitServices();
        InitializeComponent();
        // 不在此处启动 DaqWorker——用户必须新建试验+点击"开始升温"后才开始仿真
        LoadOperators();
        LoadCalibrationRecords();
    }

    private void InitServices()
    {
        _simulator = new SensorSimulator(AppGlobal.Instance.Configuration);
        _testController = new TestController(AppGlobal.Instance.Db, AppGlobal.Instance.Configuration, _simulator);
        _daqWorker = new DaqWorker(_simulator, _testController);
        _exportService = new ExportService(AppGlobal.Instance.Db, AppGlobal.Instance.Configuration);
        _testController.DataBroadcast += OnDataBroadcast;
    }

    private void InitializeComponent()
    {
        this.Text = "ISO 11820 建筑材料不燃性试验仿真系统";
        this.Size = new Size(1280, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(1024, 600);

        _tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei", 9) };
        _tabControl.TabPages.Add(CreateMonitoringTab());
        _tabControl.TabPages.Add(CreateQueryTab());
        _tabControl.TabPages.Add(CreateCalibrationTab());
        this.Controls.Add(_tabControl);
        this.FormClosing += (s, e) => { _daqWorker.Stop(); _daqWorker.Dispose(); };
    }

    #region 实时监控 Tab
    private TabPage CreateMonitoringTab()
    {
        var tab = new TabPage("实时监控");

        var tempPanel = new Panel { Location = new Point(10, 10), Size = new Size(800, 80), BackColor = Color.FromArgb(30, 30, 30) };
        _lblTf1 = CreateTempChannel(tempPanel, "炉温1(TF1)", 5);
        _lblTf2 = CreateTempChannel(tempPanel, "炉温2(TF2)", 160);
        _lblTs = CreateTempChannel(tempPanel, "表面温(TS)", 315);
        _lblTc = CreateTempChannel(tempPanel, "中心温(TC)", 470);
        _lblTCal = CreateTempChannel(tempPanel, "校准温(TCal)", 625);

        var infoPanel = new Panel { Location = new Point(10, 100), Size = new Size(800, 30) };
        _lblStatus = new Label { Location = new Point(0, 3), Font = new Font("Microsoft YaHei", 10, FontStyle.Bold), ForeColor = Color.FromArgb(0, 122, 204), Text = "空闲" };
        _lblTimer = new Label { Location = new Point(150, 3), Font = new Font("Consolas", 12), Text = "00:00:00" };
        _lblDrift = new Label { Location = new Point(350, 3), Font = new Font("Microsoft YaHei", 9), Text = "温漂: -- °C/10min" };
        _lblSampleId = new Label { Location = new Point(600, 3), Font = new Font("Microsoft YaHei", 9), Text = "样品: --" };
        infoPanel.Controls.AddRange(new Control[] { _lblStatus, _lblTimer, _lblDrift, _lblSampleId });

        var btnPanel = new Panel { Location = new Point(10, 140), Size = new Size(800, 40) };
        _btnNewTest = CreateButton("新建试验", 0, Color.FromArgb(76, 175, 80), BtnNewTest_Click);
        _btnStartHeat = CreateButton("开始升温", 100, Color.FromArgb(255, 152, 0), BtnStartHeat_Click);
        _btnStopHeat = CreateButton("停止升温", 200, Color.FromArgb(244, 67, 54), BtnStopHeat_Click);
        _btnStartRecord = CreateButton("开始记录", 300, Color.FromArgb(0, 188, 212), BtnStartRecord_Click);
        _btnStopRecord = CreateButton("停止记录", 400, Color.FromArgb(156, 39, 176), BtnStopRecord_Click);
        _btnObservation = CreateButton("试验记录", 500, Color.FromArgb(121, 85, 72), BtnObservation_Click);
        _btnExportExcel = CreateButton("导出Excel", 600, Color.FromArgb(63, 81, 181), BtnExportExcel_Click);
        _btnExportPdf = CreateButton("导出PDF", 700, Color.FromArgb(233, 30, 99), BtnExportPdf_Click);
        _btnSettings = CreateButton("参数设置", 800, Color.FromArgb(96, 125, 139), BtnSettings_Click);
        btnPanel.Controls.AddRange(new Control[] { _btnNewTest, _btnStartHeat, _btnStopHeat, _btnStartRecord, _btnStopRecord, _btnObservation, _btnExportExcel, _btnExportPdf, _btnSettings });

        _plotModel = new PlotModel { Title = "温度曲线", TitleFontSize = 12 };
        _plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "时间 (秒)", Minimum = 0, Maximum = 600 });
        _plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "温度 (°C)", Minimum = 0, Maximum = 800 });
        _seriesTf1 = new LineSeries { Title = "炉温1", Color = OxyColor.FromRgb(255, 59, 48), StrokeThickness = 1.5 };
        _seriesTf2 = new LineSeries { Title = "炉温2", Color = OxyColor.FromRgb(0, 122, 255), StrokeThickness = 1.5 };
        _seriesTs = new LineSeries { Title = "表面温", Color = OxyColor.FromRgb(255, 149, 0), StrokeThickness = 1.5 };
        _seriesTc = new LineSeries { Title = "中心温", Color = OxyColor.FromRgb(76, 217, 100), StrokeThickness = 1.5 };
        _plotModel.Series.Add(_seriesTf1); _plotModel.Series.Add(_seriesTf2);
        _plotModel.Series.Add(_seriesTs); _plotModel.Series.Add(_seriesTc);
        _plotView = new PlotView { Location = new Point(10, 190), Size = new Size(800, 320), Model = _plotModel, BackColor = Color.White };

        var logLabel = new Label { Text = "系统消息", Location = new Point(10, 520), Font = new Font("Microsoft YaHei", 9, FontStyle.Bold) };
        _rtbLog = new RichTextBox { Location = new Point(10, 545), Size = new Size(800, 180), BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.White, Font = new Font("Consolas", 9), ReadOnly = true };

        tab.Controls.AddRange(new Control[] { tempPanel, infoPanel, btnPanel, _plotView, logLabel, _rtbLog });
        UpdateButtonStates();
        return tab;
    }

    private Label CreateTempChannel(Panel parent, string name, int x)
    {
        var panel = new Panel { Location = new Point(x, 5), Size = new Size(148, 70), BackColor = Color.FromArgb(45, 45, 45) };
        var nameLbl = new Label { Text = name, Location = new Point(2, 3), Size = new Size(144, 16), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei", 7), TextAlign = ContentAlignment.MiddleCenter };
        var valueLbl = new Label { Text = "--.- °C", Location = new Point(2, 22), Size = new Size(144, 45), ForeColor = Color.Lime, Font = new Font("Consolas", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        panel.Controls.Add(nameLbl);
        panel.Controls.Add(valueLbl);
        parent.Controls.Add(panel);
        return valueLbl;
    }

    private Button CreateButton(string text, int x, Color backColor, EventHandler handler)
    {
        var btn = new Button { Text = text, Location = new Point(x, 2), Size = new Size(90, 35), BackColor = backColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += handler;
        return btn;
    }
    #endregion

    #region 记录查询 Tab
    private TabPage CreateQueryTab()
    {
        var tab = new TabPage("记录查询");
        var filterPanel = new Panel { Location = new Point(10, 10), Size = new Size(1050, 40) };

        filterPanel.Controls.Add(new Label { Text = "开始日期:", Location = new Point(0, 10), Size = new Size(65, 20), Font = new Font("Microsoft YaHei", 9) });
        _dtpStart = new DateTimePicker { Location = new Point(70, 8), Size = new Size(130, 22), Format = DateTimePickerFormat.Short };
        filterPanel.Controls.Add(new Label { Text = "结束日期:", Location = new Point(210, 10), Size = new Size(65, 20), Font = new Font("Microsoft YaHei", 9) });
        _dtpEnd = new DateTimePicker { Location = new Point(280, 8), Size = new Size(130, 22), Format = DateTimePickerFormat.Short };
        filterPanel.Controls.Add(new Label { Text = "样品编号:", Location = new Point(420, 10), Size = new Size(65, 20), Font = new Font("Microsoft YaHei", 9) });
        _txtSearchProduct = new TextBox { Location = new Point(490, 8), Size = new Size(100, 22) };
        filterPanel.Controls.Add(new Label { Text = "操作员:", Location = new Point(600, 10), Size = new Size(55, 20), Font = new Font("Microsoft YaHei", 9) });
        _cboSearchOperator = new ComboBox { Location = new Point(660, 8), Size = new Size(100, 22) };

        var btnQuery = new Button { Text = "查询", Location = new Point(780, 6), Size = new Size(80, 28), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        btnQuery.Click += BtnQuery_Click;
        var btnQueryExport = new Button { Text = "导出结果", Location = new Point(870, 6), Size = new Size(80, 28), BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        btnQueryExport.Click += BtnQueryExport_Click;

        filterPanel.Controls.AddRange(new Control[] { _dtpStart, _dtpEnd, _txtSearchProduct, _cboSearchOperator, btnQuery, btnQueryExport });

        _dgvTests = new DataGridView { Location = new Point(10, 60), Size = new Size(1050, 480), AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Font = new Font("Microsoft YaHei", 9) };
        _dgvTests.DoubleClick += DgvTests_DoubleClick;

        tab.Controls.AddRange(new Control[] { filterPanel, _dgvTests });
        _dtpStart.Value = DateTime.Now.AddMonths(-1);
        _dtpEnd.Value = DateTime.Now;
        return tab;
    }
    #endregion

    #region 设备校准 Tab
    private TabPage CreateCalibrationTab()
    {
        var tab = new TabPage("设备校准");

        var calPanel = new Panel { Location = new Point(10, 10), Size = new Size(600, 50), BackColor = Color.FromArgb(30, 30, 30) };
        _lblCalTemp = new Label { Text = "校准温: --.- °C", Location = new Point(10, 10), Size = new Size(250, 30), ForeColor = Color.Lime, Font = new Font("Consolas", 16, FontStyle.Bold) };
        calPanel.Controls.Add(_lblCalTemp);
        calPanel.Controls.Add(new Label { Text = "标准温度:", Location = new Point(280, 15), Size = new Size(70, 20), ForeColor = Color.White, Font = new Font("Microsoft YaHei", 9) });
        _txtStandardTemp = new TextBox { Location = new Point(355, 12), Size = new Size(80, 22), Text = "750" };
        calPanel.Controls.Add(_txtStandardTemp);
        var btnRecordCal = new Button { Text = "记录校准", Location = new Point(450, 10), Size = new Size(100, 28), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9) };
        btnRecordCal.Click += BtnRecordCal_Click;
        calPanel.Controls.Add(btnRecordCal);

        _dgvCalRecords = new DataGridView { Location = new Point(10, 70), Size = new Size(900, 400), AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Microsoft YaHei", 9) };

        tab.Controls.AddRange(new Control[] { calPanel, _dgvCalRecords });
        return tab;
    }
    #endregion

    #region 事件处理
    private void OnDataBroadcast(object? sender, DataBroadcastEventArgs e)
    {
        if (this.InvokeRequired) { this.Invoke(() => OnDataBroadcast(sender, e)); return; }

        var t = e.Temperatures;
        _lblTf1.Text = $"{t["TF1"]:F1} °C";
        _lblTf2.Text = $"{t["TF2"]:F1} °C";
        _lblTs.Text = $"{t["TS"]:F1} °C";
        _lblTc.Text = $"{t["TC"]:F1} °C";
        _lblTCal.Text = $"{t["TCal"]:F1} °C";
        _lblCalTemp.Text = $"校准温: {t["TCal"]:F1} °C";

        _lblStatus.Text = e.StatusText;
        _lblTimer.Text = TimeSpan.FromSeconds(e.ElapsedSeconds).ToString(@"hh\:mm\:ss");
        _lblDrift.Text = $"温漂: {e.TemperatureDrift:F2} °C/10min";

        if (_testController.CurrentTest != null)
            _lblSampleId.Text = $"样品: {_testController.CurrentTest.ProductId}";

        _seriesTf1.Points.Clear(); _seriesTf2.Points.Clear();
        _seriesTs.Points.Clear(); _seriesTc.Points.Clear();

        int count = Math.Min(e.TimeAxis.Count, e.Tf1History.Count);
        int startIdx = Math.Max(0, count - 750);
        double minTime = startIdx < e.TimeAxis.Count ? e.TimeAxis[startIdx] : 0;

        for (int i = startIdx; i < count; i++)
        {
            double time = e.TimeAxis[i] - minTime;
            _seriesTf1.Points.Add(new DataPoint(time, e.Tf1History[i]));
            if (i < e.Tf2History.Count) _seriesTf2.Points.Add(new DataPoint(time, e.Tf2History[i]));
            if (i < e.TsHistory.Count) _seriesTs.Points.Add(new DataPoint(time, e.TsHistory[i]));
            if (i < e.TcHistory.Count) _seriesTc.Points.Add(new DataPoint(time, e.TcHistory[i]));
        }
        _plotView.InvalidatePlot(true);

        foreach (var msg in e.Messages)
        {
            var color = msg.Message.Contains("终止") ? Color.Yellow : Color.White;
            _rtbLog.SelectionColor = color;
            _rtbLog.AppendText($"{msg.Time}  {msg.Message}\n");
        }
        _rtbLog.ScrollToCaret();
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var state = _testController.State;
        var test = _testController.CurrentTest;
        bool hasUnfinalized = test != null && test.TotalTestTime > 0 && test.Flag != "10000000";

        _btnNewTest.Enabled = state == TestState.Idle || (state != TestState.Idle && !hasUnfinalized);
        _btnStartHeat.Enabled = state == TestState.Idle && test != null;
        _btnStopHeat.Enabled = state == TestState.Preparing || state == TestState.Ready;
        _btnStartRecord.Enabled = state == TestState.Ready;
        _btnStopRecord.Enabled = state == TestState.Recording;
        _btnObservation.Enabled = state == TestState.Complete || hasUnfinalized;
        _btnExportExcel.Enabled = hasUnfinalized || test?.Flag == "10000000";
        _btnExportPdf.Enabled = hasUnfinalized || test?.Flag == "10000000";
        _btnSettings.Enabled = state != TestState.Recording;
    }

    private void BtnNewTest_Click(object? sender, EventArgs e)
    {
        var form = new NewTestForm(_testController);
        if (form.ShowDialog() == DialogResult.OK) UpdateButtonStates();
    }

    private void BtnStartHeat_Click(object? sender, EventArgs e) { _testController.StartHeating(); _daqWorker.Start(); }
    private void BtnStopHeat_Click(object? sender, EventArgs e) { _testController.StopHeating(); _daqWorker.Stop(); }
    private void BtnStartRecord_Click(object? sender, EventArgs e) { _testController.StartRecording(); }
    private void BtnStopRecord_Click(object? sender, EventArgs e) { _testController.StopRecording(); }

    private void BtnObservation_Click(object? sender, EventArgs e)
    {
        if (_testController.CurrentTest == null) return;
        var form = new ObservationForm(_testController.CurrentTest, _testController, _exportService);
        if (form.ShowDialog() == DialogResult.OK) UpdateButtonStates();
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (_testController.CurrentTest == null) return;
        var path = _exportService.ExportExcel(_testController.CurrentTest, _testController.CsvLines);
        MessageBox.Show($"Excel 报告已导出到:\n{path}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnExportPdf_Click(object? sender, EventArgs e)
    {
        if (_testController.CurrentTest == null) return;
        var path = _exportService.ExportPdf(_testController.CurrentTest);
        MessageBox.Show($"PDF 报告已导出到:\n{path}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        MessageBox.Show($"当前升温速率: {_simulator.HeatingRatePerSecond}°C/s\n目标温度: {_simulator.TargetTemp}°C\n初始温度: {_simulator.InitialFurnaceTemp}°C", "参数信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnQuery_Click(object? sender, EventArgs e)
    {
        var results = AppGlobal.Instance.Db.QueryTests(_dtpStart.Value.Date, _dtpEnd.Value.Date.AddDays(1),
            string.IsNullOrWhiteSpace(_txtSearchProduct.Text) ? null : _txtSearchProduct.Text.Trim(),
            _cboSearchOperator.SelectedIndex > 0 ? _cboSearchOperator.Text : null);

        _dgvTests.DataSource = results.Select(r => new {
            r.ProductId, r.TestId, 试验日期 = r.TestDate.ToString("yyyy-MM-dd HH:mm"),
            r.Operator, 试验前质量 = r.PreWeight, 试验后质量 = r.PostWeight,
            失重率 = $"{r.LostWeightPer:F2}%", 温升 = $"{r.DeltaTf:F1}°C",
            时长秒 = r.TotalTestTime, 判定 = r.PassFail
        }).ToList();
        LoadOperators();
    }

    private void LoadOperators()
    {
        var ops = AppGlobal.Instance.Db.GetDistinctOperators();
        _cboSearchOperator.Items.Clear();
        _cboSearchOperator.Items.Add("(全部)");
        _cboSearchOperator.SelectedIndex = 0;
        foreach (var op in ops) _cboSearchOperator.Items.Add(op);
    }

    private void BtnQueryExport_Click(object? sender, EventArgs e)
    {
        if (_dgvTests.Rows.Count == 0) { MessageBox.Show("没有数据可导出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var dir = AppGlobal.Instance.Configuration["Report:OutputDirectory"] ?? "D:\\ISO11820\\Reports";
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"查询结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("查询结果");
        var headerFont = sheet.Cells[1, 1, 1, _dgvTests.Columns.Count].Style.Font;
        headerFont.Name = "Microsoft YaHei";
        headerFont.Bold = true;
        for (int col = 0; col < _dgvTests.Columns.Count; col++)
            sheet.Cells[1, col + 1].Value = _dgvTests.Columns[col].HeaderText;
        for (int row = 0; row < _dgvTests.Rows.Count; row++)
            for (int col = 0; col < _dgvTests.Columns.Count; col++)
                sheet.Cells[row + 2, col + 1].Value = _dgvTests.Rows[row].Cells[col].Value?.ToString();
        package.SaveAs(new FileInfo(path));
        MessageBox.Show($"查询结果已导出到:\n{path}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DgvTests_DoubleClick(object? sender, EventArgs e)
    {
        if (_dgvTests.CurrentRow?.DataBoundItem == null) return;
        var row = _dgvTests.CurrentRow.DataBoundItem;
        var productId = row.GetType().GetProperty("ProductId")?.GetValue(row)?.ToString();
        var testId = row.GetType().GetProperty("TestId")?.GetValue(row)?.ToString();
        if (productId == null || testId == null) return;
        var test = AppGlobal.Instance.Db.GetTestMaster(productId, testId);
        if (test != null) { var form = new TestDetailForm(test); form.ShowDialog(); }
    }

    private void BtnRecordCal_Click(object? sender, EventArgs e)
    {
        if (!double.TryParse(_txtStandardTemp.Text, out double stdTemp))
        { MessageBox.Show("请输入有效的标准温度值", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        var record = new CalibrationRecord
        {
            CalDate = DateTime.Now,
            Operator = AppGlobal.Instance.CurrentUser?.Username ?? "",
            StandardTemp = stdTemp,
            MeasuredTemp = _simulator.TCal,
            Deviation = _simulator.TCal - stdTemp
        };
        AppGlobal.Instance.Db.InsertCalibrationRecord(record);
        LoadCalibrationRecords();
        MessageBox.Show("校准记录已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LoadCalibrationRecords()
    {
        var records = AppGlobal.Instance.Db.GetCalibrationRecords();
        _dgvCalRecords.DataSource = records.Select(r => new {
            日期 = r.CalDate.ToString("yyyy-MM-dd HH:mm"), 操作员 = r.Operator,
            标准温度 = r.StandardTemp, 测量温度 = r.MeasuredTemp,
            偏差 = $"{r.Deviation:F2}", 备注 = r.Remark
        }).ToList();
    }
    #endregion
}

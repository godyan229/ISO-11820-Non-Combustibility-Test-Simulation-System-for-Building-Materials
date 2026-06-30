using ISO11820.Core;
using ISO11820.Models;
using ISO11820.Services;

namespace ISO11820.Forms;

public class ObservationForm : Form
{
    private readonly TestMaster _test;
    private readonly TestController _controller;
    private readonly ExportService _exportService;

    private CheckBox _chkFlame;
    private TextBox _txtFlameStart, _txtFlameDuration;
    private TextBox _txtPostWeight, _txtRemark;

    public ObservationForm(TestMaster test, TestController controller, ExportService exportService)
    {
        _test = test;
        _controller = controller;
        _exportService = exportService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "试验现象记录";
        this.Size = new Size(450, 430);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        var font = new Font("Microsoft YaHei", 9);
        int y = 15;

        var lblPre = new Label { Text = $"试验前质量: {_test.PreWeight:F2} g", Location = new Point(10, y), Size = new Size(400, 24), Font = font, ForeColor = Color.DarkBlue };
        this.Controls.Add(lblPre);
        y += 28;

        var lblPost = new Label { Text = "试验后质量 (g) *:", Location = new Point(10, y), Size = new Size(140, 24), Font = font };
        _txtPostWeight = new TextBox { Location = new Point(155, y), Size = new Size(200, 24), Font = font };
        this.Controls.AddRange(new Control[] { lblPost, _txtPostWeight });
        y += 32;

        _chkFlame = new CheckBox { Text = "是否出现持续火焰", Location = new Point(10, y), Size = new Size(200, 24), Font = font };
        _chkFlame.CheckedChanged += (s, e) => { _txtFlameStart.Enabled = _chkFlame.Checked; _txtFlameDuration.Enabled = _chkFlame.Checked; };
        this.Controls.Add(_chkFlame);
        y += 30;

        var lblStart = new Label { Text = "火焰发生时刻 (秒):", Location = new Point(30, y), Size = new Size(140, 24), Font = font };
        _txtFlameStart = new TextBox { Location = new Point(175, y), Size = new Size(180, 24), Font = font, Enabled = false };
        this.Controls.AddRange(new Control[] { lblStart, _txtFlameStart });
        y += 30;

        var lblDuration = new Label { Text = "火焰持续时间 (秒):", Location = new Point(30, y), Size = new Size(140, 24), Font = font };
        _txtFlameDuration = new TextBox { Location = new Point(175, y), Size = new Size(180, 24), Font = font, Enabled = false };
        this.Controls.AddRange(new Control[] { lblDuration, _txtFlameDuration });
        y += 30;

        var lblRemark = new Label { Text = "备注:", Location = new Point(10, y), Size = new Size(140, 24), Font = font };
        _txtRemark = new TextBox { Location = new Point(155, y), Size = new Size(200, 60), Font = font, Multiline = true };
        this.Controls.AddRange(new Control[] { lblRemark, _txtRemark });
        y += 70;

        y += 10;
        var btnSave = new Button
        {
            Text = "保存试验记录并生成报告",
            Location = new Point((this.ClientSize.Width - 220) / 2, y),
            Size = new Size(220, 35),
            BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
        };
        btnSave.Click += BtnSave_Click;
        this.Controls.Add(btnSave);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (!double.TryParse(_txtPostWeight.Text, out double postWeight))
        {
            MessageBox.Show("请输入有效的试验后质量", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _test.PostWeight = postWeight;
        _test.LostWeight = _test.PreWeight - postWeight;
        _test.LostWeightPer = _test.PreWeight > 0 ? _test.LostWeight / _test.PreWeight * 100 : 0;

        _test.DeltaTf1 = _test.FinalTf1 - _test.AmbientTemp;
        _test.DeltaTf2 = _test.FinalTf2 - _test.AmbientTemp;
        _test.DeltaTs = _test.FinalTs - _test.AmbientTemp;
        _test.DeltaTc = _test.FinalTc - _test.AmbientTemp;
        _test.DeltaTf = _test.DeltaTs;

        _test.FlameOccurred = _chkFlame.Checked ? 1 : 0;
        _test.FlameStartTime = int.TryParse(_txtFlameStart.Text, out int fs) ? fs : 0;
        _test.FlameDuration = int.TryParse(_txtFlameDuration.Text, out int fd) ? fd : 0;
        _test.Remark = _txtRemark.Text;

        bool pass = _test.DeltaTf <= 50 && _test.LostWeightPer <= 50 && _test.FlameDuration < 5;
        _test.PassFail = pass ? "通过" : "不通过";

        _controller.FinalizeTest();

        _exportService.SaveCsv(_test, _controller.CsvLines);
        _exportService.ExportExcel(_test, _controller.CsvLines);
        _exportService.ExportPdf(_test);

        MessageBox.Show($"试验记录已保存。\n失重率: {_test.LostWeightPer:F2}%\n温升: {_test.DeltaTf:F1}°C\n判定: {_test.PassFail}",
            "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}

using ISO11820.Core;
using ISO11820.Global;
using ISO11820.Models;

namespace ISO11820.Forms;

public class NewTestForm : Form
{
    private readonly TestController _controller;
    private TextBox _txtProductId, _txtTestId, _txtProductName, _txtSpec, _txtHeight, _txtDiameter;
    private TextBox _txtAmbientTemp, _txtAmbientHumidity, _txtPreWeight, _txtOperator;
    private ComboBox _cboDurationMode;
    private TextBox _txtTargetDuration;

    public NewTestForm(TestController controller)
    {
        _controller = controller;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "新建试验";
        this.Size = new Size(520, 600);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.AutoScroll = true;
        this.AutoScrollMinSize = new Size(480, 520);

        int y = 10, labelW = 120, ctrlW = 220, ctrlX = 140;
        var font = new Font("Microsoft YaHei", 9);

        _txtProductId = AddField("样品编号:", ref y, labelW, ctrlW, ctrlX, font);
        _txtTestId = AddField("试验标识:", ref y, labelW, ctrlW, ctrlX, font);
        _txtProductName = AddField("样品名称:", ref y, labelW, ctrlW, ctrlX, font);
        _txtSpec = AddField("规格:", ref y, labelW, ctrlW, ctrlX, font);
        _txtHeight = AddField("高度 (mm):", ref y, labelW, ctrlW, ctrlX, font);
        _txtDiameter = AddField("直径 (mm):", ref y, labelW, ctrlW, ctrlX, font);
        _txtAmbientTemp = AddField("环境温度 (°C):", ref y, labelW, ctrlW, ctrlX, font, "25.0");
        _txtAmbientHumidity = AddField("环境湿度 (%):", ref y, labelW, ctrlW, ctrlX, font, "50.0");
        _txtPreWeight = AddField("试验前质量 (g):", ref y, labelW, ctrlW, ctrlX, font, "50.0");
        _txtOperator = AddField("操作员:", ref y, labelW, ctrlW, ctrlX, font, AppGlobal.Instance.CurrentUser?.Username ?? "");

        var lblMode = new Label { Text = "试验时长模式:", Location = new Point(10, y), Size = new Size(labelW, 24), Font = font };
        _cboDurationMode = new ComboBox { Location = new Point(ctrlX, y), Size = new Size(ctrlW, 24), Font = font, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboDurationMode.Items.AddRange(new[] { "标准60分钟", "固定时长" });
        _cboDurationMode.SelectedIndex = 0;
        this.Controls.AddRange(new Control[] { lblMode, _cboDurationMode });
        y += 30;

        var lblDuration = new Label { Text = "目标时长 (秒):", Location = new Point(10, y), Size = new Size(labelW, 24), Font = font };
        _txtTargetDuration = new TextBox { Location = new Point(ctrlX, y), Size = new Size(ctrlW, 24), Font = font, Text = "3600", Enabled = false };
        this.Controls.AddRange(new Control[] { lblDuration, _txtTargetDuration });
        _cboDurationMode.SelectedIndexChanged += (s, e) => _txtTargetDuration.Enabled = _cboDurationMode.SelectedIndex == 1;
        y += 30;

        var lblDevGroup = new Label { Text = "设备信息（自动带入）:", Location = new Point(10, y), Size = new Size(360, 20), Font = new Font("Microsoft YaHei", 9, FontStyle.Bold) };
        this.Controls.Add(lblDevGroup);
        y += 25;

        var app = AppGlobal.Instance.CurrentApparatus;
        AddReadOnlyField("设备编号:", ref y, labelW, ctrlW, ctrlX, font, app?.DeviceId ?? "--");
        AddReadOnlyField("设备名称:", ref y, labelW, ctrlW, ctrlX, font, app?.DeviceName ?? "--");
        AddReadOnlyField("检定日期:", ref y, labelW, ctrlW, ctrlX, font, app?.CalibrationDate.ToString("yyyy-MM-dd") ?? "--");
        AddReadOnlyField("恒功率值:", ref y, labelW, ctrlW, ctrlX, font, app?.ConstPower.ToString() ?? "--");

        y += 15;
        var btnCreate = new Button
        {
            Text = "创建试验", Location = new Point((this.ClientSize.Width - 120) / 2, y),
            Size = new Size(120, 35), BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
        };
        btnCreate.Click += BtnCreate_Click;
        this.Controls.Add(btnCreate);
    }

    private TextBox AddField(string label, ref int y, int lw, int cw, int cx, Font font, string defaultVal = "")
    {
        var lbl = new Label { Text = label, Location = new Point(10, y), Size = new Size(lw, 24), Font = font };
        var txt = new TextBox { Location = new Point(cx, y), Size = new Size(cw, 24), Font = font, Text = defaultVal };
        this.Controls.AddRange(new Control[] { lbl, txt });
        y += 30;
        return txt;
    }

    private void AddReadOnlyField(string label, ref int y, int lw, int cw, int cx, Font font, string value)
    {
        var lbl = new Label { Text = label, Location = new Point(10, y), Size = new Size(lw, 24), Font = font, ForeColor = Color.Gray };
        var val = new Label { Text = value, Location = new Point(cx, y), Size = new Size(cw, 24), Font = font, ForeColor = Color.DarkBlue };
        this.Controls.AddRange(new Control[] { lbl, val });
        y += 30;
    }

    private void BtnCreate_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtProductId.Text) || string.IsNullOrWhiteSpace(_txtTestId.Text))
        {
            MessageBox.Show("样品编号和试验标识不能为空", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var app = AppGlobal.Instance.CurrentApparatus;
        var test = new TestMaster
        {
            ProductId = _txtProductId.Text.Trim(),
            TestId = _txtTestId.Text.Trim(),
            TestDate = DateTime.Now,
            Operator = _txtOperator.Text.Trim(),
            AmbientTemp = double.TryParse(_txtAmbientTemp.Text, out var at) ? at : 25.0,
            AmbientHumidity = double.TryParse(_txtAmbientHumidity.Text, out var ah) ? ah : 50.0,
            PreWeight = double.TryParse(_txtPreWeight.Text, out var pw) ? pw : 50.0,
            DurationMode = _cboDurationMode.SelectedIndex == 0 ? "standard" : "fixed",
            TargetDurationSeconds = _cboDurationMode.SelectedIndex == 1 && int.TryParse(_txtTargetDuration.Text, out var td) ? td : 3600,
            DeviceId = app?.DeviceId ?? "",
            DeviceName = app?.DeviceName ?? "",
            ConstPower = app?.ConstPower ?? 2048
        };

        _controller.CreateTest(test);

        AppGlobal.Instance.Db.InsertProduct(new ProductMaster
        {
            ProductId = test.ProductId,
            ProductName = _txtProductName.Text.Trim(),
            Specification = _txtSpec.Text.Trim(),
            Height = double.TryParse(_txtHeight.Text, out var h) ? h : 0,
            Diameter = double.TryParse(_txtDiameter.Text, out var d) ? d : 0
        });

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}

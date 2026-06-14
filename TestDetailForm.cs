using ISO11820.Models;

namespace ISO11820.Forms;

public class TestDetailForm : Form
{
    public TestDetailForm(TestMaster test)
    {
        this.Text = $"试验详情 - {test.ProductId}/{test.TestId}";
        this.Size = new Size(500, 520);
        this.StartPosition = FormStartPosition.CenterParent;
        var font = new Font("Microsoft YaHei", 9);
        int y = 10;

        AddRow("样品编号", test.ProductId, ref y, font);
        AddRow("试验标识", test.TestId, ref y, font);
        AddRow("试验日期", test.TestDate.ToString("yyyy-MM-dd HH:mm:ss"), ref y, font);
        AddRow("操作员", test.Operator, ref y, font);
        AddRow("环境温度", $"{test.AmbientTemp:F1} °C", ref y, font);
        AddRow("环境湿度", $"{test.AmbientHumidity:F1} %", ref y, font);
        AddRow("试验前质量", $"{test.PreWeight:F2} g", ref y, font);
        AddRow("试验后质量", $"{test.PostWeight:F2} g", ref y, font);
        AddRow("失重率", $"{test.LostWeightPer:F2} %", ref y, font);
        AddRow("炉温1温升", $"{test.DeltaTf1:F1} °C", ref y, font);
        AddRow("炉温2温升", $"{test.DeltaTf2:F1} °C", ref y, font);
        AddRow("表面温升", $"{test.DeltaTs:F1} °C", ref y, font);
        AddRow("中心温升", $"{test.DeltaTc:F1} °C", ref y, font);
        AddRow("样品温升 (deltaTf)", $"{test.DeltaTf:F1} °C", ref y, font);
        AddRow("试验时长", $"{test.TotalTestTime} 秒", ref y, font);
        AddRow("火焰时间", test.FlameOccurred == 1 ? $"{test.FlameDuration} 秒" : "无", ref y, font);
        AddRow("设备编号", test.DeviceId, ref y, font);
        AddRow("恒功率值", test.ConstPower.ToString(), ref y, font);
        AddRow("判定结果", test.PassFail, ref y, font, test.PassFail == "通过" ? Color.Green : Color.Red);
        AddRow("备注", test.Remark, ref y, font);
    }

    private void AddRow(string label, string value, ref int y, Font font, Color? valueColor = null)
    {
        var lbl = new Label { Text = label + ":", Location = new Point(15, y), Size = new Size(130, 22), Font = font, TextAlign = ContentAlignment.MiddleRight };
        var val = new Label { Text = value, Location = new Point(155, y), Size = new Size(310, 22), Font = font, ForeColor = valueColor ?? Color.Black };
        this.Controls.AddRange(new Control[] { lbl, val });
        y += 27;
    }
}

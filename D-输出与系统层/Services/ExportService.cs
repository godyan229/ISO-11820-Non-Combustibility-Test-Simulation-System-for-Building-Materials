using ISO11820.Data;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System.Runtime.InteropServices;

namespace ISO11820.Services;

/// <summary>
/// Windows 系统字体解析器，让 MigraDoc/PDFsharp 6.x 支持中文字体
/// </summary>
public class WindowsFontResolver : IFontResolver
{
    public string DefaultFontName => "SimHei";

    // 字体名 → 实际文件名映射
    private static readonly Dictionary<string, string> FontFileMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Microsoft YaHei",    "msyh.ttc" },
        { "Microsoft YaHei Bold","msyhbd.ttc" },
        { "SimHei",             "simhei.ttf" },
        { "SimSun",             "simsun.ttc" },
        { "KaiTi",              "simkai.ttf" },
        { "FangSong",           "simfang.ttf" },
    };

    public byte[]? GetFont(string faceName)
    {
        var fontDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        // 先按映射表查找，找不到直接按 faceName 拼接
        string fileName = FontFileMap.TryGetValue(faceName, out var mapped)
            ? mapped
            : faceName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || faceName.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                ? faceName : faceName + ".ttf";

        string path = Path.Combine(fontDir, fileName);
        // 指定的文件不存在就回退到 simhei.ttf
        if (!File.Exists(path))
            path = Path.Combine(fontDir, "simhei.ttf");
        if (!File.Exists(path))
            path = Path.Combine(fontDir, "simsun.ttc");

        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var name = familyName;
        // 尝试用映射表里的名称，找不到则用原名
        if (!FontFileMap.ContainsKey(familyName))
        {
            var fontDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var testPath = Path.Combine(fontDir, familyName);
            if (!File.Exists(testPath) && !File.Exists(Path.Combine(fontDir, familyName + ".ttf"))
                && !File.Exists(Path.Combine(fontDir, familyName + ".ttc")))
            {
                name = "SimHei"; // 最终回退
            }
        }
        return new FontResolverInfo(name);
    }
}

public class ExportService
{
    private readonly DbHelper _db;
    private readonly IConfiguration _config;
    private readonly string _baseDir;

    public ExportService(DbHelper db, IConfiguration config)
    {
        _db = db;
        _config = config;
        _baseDir = config["FileStorage:BaseDirectory"] ?? "D:\\ISO11820";
    }

    public string SaveCsv(TestMaster test, List<string> csvLines)
    {
        var dir = Path.Combine(_baseDir, "TestData", test.ProductId, test.TestId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "sensor_data.csv");
        // UTF-8 with BOM，否则 Excel 打开中文乱码
        var utf8Bom = new System.Text.UTF8Encoding(true);
        File.WriteAllText(path, string.Join(Environment.NewLine, csvLines), utf8Bom);
        return path;
    }

    public string ExportExcel(TestMaster test, List<string>? csvLines = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var dir = Path.Combine(_config["Report:OutputDirectory"] ?? Path.Combine(_baseDir, "Reports"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{test.TestId}_报告.xlsx");

        using var package = new ExcelPackage();

        // Sheet1: 试验信息
        var sheet1 = package.Workbook.Worksheets.Add("试验信息");
        sheet1.Cells["A1"].Value = "ISO 11820 不燃性试验报告";
        sheet1.Cells["A1"].Style.Font.Size = 16;
        sheet1.Cells["A1"].Style.Font.Bold = true;
        sheet1.Cells["A1"].Style.Font.Name = "SimHei";

        int row = 3;
        WriteInfoRow(sheet1, ref row, "样品编号", test.ProductId);
        WriteInfoRow(sheet1, ref row, "试验标识", test.TestId);
        WriteInfoRow(sheet1, ref row, "试验日期", test.TestDate.ToString("yyyy-MM-dd HH:mm:ss"));
        WriteInfoRow(sheet1, ref row, "操作员", test.Operator);
        WriteInfoRow(sheet1, ref row, "环境温度", $"{test.AmbientTemp:F1} °C");
        WriteInfoRow(sheet1, ref row, "环境湿度", $"{test.AmbientHumidity:F1} %");
        WriteInfoRow(sheet1, ref row, "试验前质量", $"{test.PreWeight:F2} g");
        WriteInfoRow(sheet1, ref row, "试验后质量", $"{test.PostWeight:F2} g");
        WriteInfoRow(sheet1, ref row, "失重率", $"{test.LostWeightPer:F2} %");
        WriteInfoRow(sheet1, ref row, "炉温1温升", $"{test.DeltaTf1:F1} °C");
        WriteInfoRow(sheet1, ref row, "炉温2温升", $"{test.DeltaTf2:F1} °C");
        WriteInfoRow(sheet1, ref row, "表面温升", $"{test.DeltaTs:F1} °C");
        WriteInfoRow(sheet1, ref row, "中心温升", $"{test.DeltaTc:F1} °C");
        WriteInfoRow(sheet1, ref row, "样品温升(deltaTf)", $"{test.DeltaTf:F1} °C");
        WriteInfoRow(sheet1, ref row, "试验时长", $"{test.TotalTestTime} 秒");
        WriteInfoRow(sheet1, ref row, "判定结果", test.PassFail);

        // Sheet2: 温度数据
        var sheet2 = package.Workbook.Worksheets.Add("温度数据");
        if (csvLines != null && csvLines.Count > 1)
        {
            for (int i = 0; i < csvLines.Count; i++)
            {
                var parts = csvLines[i].Split(',');
                for (int j = 0; j < parts.Length; j++)
                    sheet2.Cells[i + 1, j + 1].Value = parts[j];
            }
        }

        // Sheet3: 温度曲线图
        var sheet3 = package.Workbook.Worksheets.Add("温度曲线");
        if (csvLines != null && csvLines.Count > 2)
        {
            for (int i = 1; i < csvLines.Count; i++)
            {
                var parts = csvLines[i].Split(',');
                for (int j = 0; j < parts.Length; j++)
                    sheet3.Cells[i, j + 1].Value = double.Parse(parts[j]);
            }

            var chart = sheet3.Drawings.AddChart("TemperatureChart", eChartType.Line);
            chart.Title.Text = "温度曲线";
            chart.Title.Font.SetFromFont("SimHei", 12);
            chart.XAxis.Title.Text = "时间 (秒)";
            chart.XAxis.Title.Font.SetFromFont("SimHei", 10);
            chart.YAxis.Title.Text = "温度 (°C)";
            chart.YAxis.Title.Font.SetFromFont("SimHei", 10);
            chart.YAxis.MinValue = 0;
            chart.YAxis.MaxValue = 800;
            int dataRows = csvLines.Count - 1;
            var names = new[] { "炉温1", "炉温2", "表面温", "中心温" };
            for (int col = 1; col <= 4; col++)
            {
                var series = chart.Series.Add(sheet3.Cells[1, col + 1, dataRows, col + 1],
                                              sheet3.Cells[1, 1, dataRows, 1]);
                series.Header = names[col - 1];
            }
            chart.SetSize(800, 500);
            chart.SetPosition(2, 0, 0, 0);
        }

        package.SaveAs(new FileInfo(path));
        return path;
    }

    public string ExportPdf(TestMaster test, string? chartImagePath = null)
    {
        // 注册中文字体解析器（只需注册一次）
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new WindowsFontResolver();

        var dir = Path.Combine(_config["Report:OutputDirectory"] ?? Path.Combine(_baseDir, "Reports"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{test.TestId}_报告.pdf");

        var document = new MigraDoc.DocumentObjectModel.Document();

        // 设置默认中文字体，否则 PDF 中文乱码
        var style = document.Styles["Normal"];
        style.Font.Name = "SimHei";
        style.Font.Size = 11;
        style.ParagraphFormat.LineSpacing = 1.3;

        var section = document.AddSection();
        var titlePara = section.AddParagraph("ISO 11820 不燃性试验报告");
        titlePara.Format.Font.Name = "SimHei";
        titlePara.Format.Font.Size = 16;
        titlePara.Format.Font.Bold = true;
        AddPdfParagraph(section, $"样品编号: {test.ProductId}");
        AddPdfParagraph(section, $"试验标识: {test.TestId}");
        AddPdfParagraph(section, $"试验日期: {test.TestDate:yyyy-MM-dd HH:mm:ss}");
        AddPdfParagraph(section, $"操作员: {test.Operator}");
        AddPdfParagraph(section, $"试验前质量: {test.PreWeight:F2} g");
        AddPdfParagraph(section, $"试验后质量: {test.PostWeight:F2} g");
        AddPdfParagraph(section, $"失重率: {test.LostWeightPer:F2} %");
        AddPdfParagraph(section, $"样品温升: {test.DeltaTf:F1} °C");
        AddPdfParagraph(section, $"炉温1温升: {test.DeltaTf1:F1} °C");
        AddPdfParagraph(section, $"炉温2温升: {test.DeltaTf2:F1} °C");
        AddPdfParagraph(section, $"表面温升: {test.DeltaTs:F1} °C");
        AddPdfParagraph(section, $"中心温升: {test.DeltaTc:F1} °C");
        AddPdfParagraph(section, $"试验时长: {test.TotalTestTime} 秒");
        AddPdfParagraph(section, $"火焰时间: {(test.FlameOccurred == 1 ? $"{test.FlameDuration} 秒" : "无")}");
        AddPdfParagraph(section, $"判定结论: {test.PassFail}");

        var renderer = new PdfDocumentRenderer();
        renderer.Document = document;
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
        return path;
    }

    private void WriteInfoRow(ExcelWorksheet sheet, ref int row, string label, string value)
    {
        sheet.Cells[$"A{row}"].Value = label;
        sheet.Cells[$"A{row}"].Style.Font.Bold = true;
        sheet.Cells[$"A{row}"].Style.Font.Name = "SimHei";
        sheet.Cells[$"B{row}"].Value = value;
        sheet.Cells[$"B{row}"].Style.Font.Name = "SimHei";
        row++;
    }

    private void AddPdfParagraph(MigraDoc.DocumentObjectModel.Section section, string text)
    {
        var para = section.AddParagraph(text);
        para.Format.Font.Name = "SimHei";
    }
}

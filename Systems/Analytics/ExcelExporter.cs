using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;

namespace GenesisEngine.Systems.Analytics
{
    /// <summary>
    /// Собирает все CSV папки запуска в один report.xlsx (лист на файл).
    /// Вызывается ПОСЛЕ симуляции — не влияет на скорость тиков.
    /// </summary>
    public static class ExcelExporter
    {
        public static void ExportFolder(string folder)
        {
            try
            {
                var csvFiles = Directory.GetFiles(folder, "*.csv");
                if (csvFiles.Length == 0) return;

                string xlsxPath = Path.Combine(folder, "report.xlsx");
                using var workbook = new XLWorkbook();

                foreach (var csv in csvFiles)
                {
                    var ws = workbook.Worksheets.Add(SanitizeSheetName(Path.GetFileNameWithoutExtension(csv)));
                    int row = 1;

                    foreach (var line in File.ReadLines(csv))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (row > 1048576) break; // лимит Excel

                        var cells = ParseCsvLine(line);
                        for (int c = 0; c < cells.Count; c++)
                            SetCellValue(ws.Cell(row, c + 1), cells[c], row == 1);
                        row++;
                    }
                }

                workbook.SaveAs(xlsxPath);
                Console.WriteLine($"[ExcelExporter] Готово: {xlsxPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExcelExporter] Ошибка: {ex.Message}");
            }
        }

        private static void SetCellValue(IXLCell cell, string value, bool isHeader)
        {
            if (isHeader) { cell.Value = value; return; }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            { cell.Value = l; return; }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    cell.Value = 0d;
                    return;
                }
                cell.Value = d;
                return;
            }

            cell.Value = value;
        }

        private static string SanitizeSheetName(string name)
        {
            foreach (char c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}
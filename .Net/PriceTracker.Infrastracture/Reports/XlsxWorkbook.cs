using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PriceTracker.Infrastracture.Reports;

public sealed class XlsxWorkbook : IDisposable
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    private readonly SpreadsheetDocument _document;
    private readonly WorkbookPart _workbook;
    private readonly Sheets _sheets;
    private readonly DateTime _generatedAt;

    public XlsxWorkbook(Stream output, DateTime generatedAt)
    {
        _document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
        _workbook = _document.AddWorkbookPart();
        _sheets = new Sheets();
        _workbook.Workbook = new Workbook(_sheets);
        _workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();
        _generatedAt = generatedAt;
    }

    public WorksheetPart Register(string title)
    {
        var part = _workbook.AddNewPart<WorksheetPart>();
        _sheets.Append(new Sheet
        {
            Id = _workbook.GetIdOfPart(part), SheetId = (uint)_sheets.ChildElements.Count + 1, Name = title
        });
        return part;
    }

    public async Task<int> WriteSheetAsync(WorksheetPart part, string title, string[] headers, int[] widths,
        Func<Rows, Task> populate)
    {
        if (headers.Length != widths.Length)
            throw new ArgumentException("Every worksheet header requires a width.");
        using var writer = OpenXmlWriter.Create(part);
        writer.WriteStartElement(new Worksheet());
        var freezeColumns = headers.Length > 2 ? 2 : 0;
        writer.WriteElement(new SheetViews(new SheetView(new Pane
        {
            HorizontalSplit = freezeColumns, VerticalSplit = 4, TopLeftCell = freezeColumns == 0 ? "A5" : "C5",
            ActivePane = freezeColumns == 0 ? PaneValues.BottomLeft : PaneValues.BottomRight, State = PaneStateValues.Frozen
        }) { WorkbookViewId = 0, RightToLeft = true, ShowGridLines = false }));
        writer.WriteElement(new Columns(widths.Select((w, i) => new Column
        {
            Min = (uint)i + 1, Max = (uint)i + 1, Width = w, CustomWidth = true
        })));
        writer.WriteStartElement(new SheetData());

        var rows = new Rows(writer);
        rows.Write($"پارسا آل شریف | {title}");
        rows.Write($"تهیه در {Local(_generatedAt):yyyy-MM-dd HH:mm:ss} · ساعت تهران");
        rows.Write("قیمت‌ها به تومان و زمان‌ها بر اساس مشاهدهٔ ثبت‌شده هستند.");
        rows.WriteHeader(headers);
        await populate(rows);

        writer.WriteEndElement();
        writer.WriteElement(new AutoFilter { Reference = $"A4:{ColumnName(headers.Length)}{rows.Count}" });
        writer.WriteEndElement();
        return rows.Count - 4;
    }

    public static DateTime Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tehran);

    public void Dispose() => _document.Dispose();

    public sealed class Rows(OpenXmlWriter writer)
    {
        public int Count { get; private set; }

        public void Write(params object?[] values) => WriteRow(values, false);
        internal void WriteHeader(string[] values) => WriteRow(values, true);

        private void WriteRow(object?[] values, bool header)
        {
            writer.WriteStartElement(new Row { RowIndex = (uint)++Count });
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is not { } value)
                    continue;
                var cell = new Cell { CellReference = ColumnName(i + 1) + Count.ToString(CultureInfo.InvariantCulture) };
                switch (value)
                {
                    case DateTime utc:
                        cell.DataType = CellValues.Number;
                        cell.StyleIndex = 3;
                        cell.CellValue = new CellValue(Local(utc).ToOADate().ToString("R", CultureInfo.InvariantCulture));
                        break;
                    case int or long or decimal or double:
                        cell.DataType = CellValues.Number;
                        cell.StyleIndex = 2;
                        cell.CellValue = new CellValue(Convert.ToString(value, CultureInfo.InvariantCulture)!);
                        break;
                    default:
                        // Inline strings stay text even when a source title starts with an Excel formula prefix.
                        cell.DataType = CellValues.InlineString;
                        cell.StyleIndex = header ? 1U : 0U;
                        cell.InlineString = new InlineString(new Text(SafeText(value.ToString() ?? ""))
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        });
                        break;
                }
                writer.WriteElement(cell);
            }
            writer.WriteEndElement();
        }
    }

    private static string SafeText(string text)
    {
        var result = new StringBuilder(Math.Min(text.Length, 32767));
        foreach (var rune in text.EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > 32767)
                break;
            if (rune.Value is 9 or 10 or 13 || rune.Value is >= 32 and not (0xFFFE or 0xFFFF))
                result.Append(rune.ToString());
        }
        return result.ToString();
    }

    private static string ColumnName(int column)
    {
        var name = "";
        for (; column > 0; column = (column - 1) / 26)
            name = (char)('A' + (column - 1) % 26) + name;
        return name;
    }

    private static Stylesheet Styles() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0.##;[Red](#,##0.##)" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "yyyy-mm-dd hh:mm:ss" }) { Count = 2 },
        new Fonts(new Font(new FontName { Val = "Tahoma" }, new FontSize { Val = 10 }),
            new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontName { Val = "Tahoma" },
                new FontSize { Val = 11 })) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FF183C36" }, new BackgroundColor { Indexed = 64 })
            { PatternType = PatternValues.Solid })) { Count = 3 },
        new Borders(new Border()) { Count = 1 },
        new CellStyleFormats(new CellFormat()) { Count = 1 },
        new CellFormats(Format(0, 0, 0), Format(1, 2, 0), Format(0, 0, 164), Format(0, 0, 165)) { Count = 4 },
        new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });

    private static CellFormat Format(uint font, uint fill, uint number) => new(
        new Alignment { Horizontal = HorizontalAlignmentValues.Right, Vertical = VerticalAlignmentValues.Center })
    {
        FormatId = 0, FontId = font, FillId = fill, BorderId = 0, NumberFormatId = number,
        ApplyFont = true, ApplyFill = fill != 0, ApplyNumberFormat = number != 0, ApplyAlignment = true
    };
}

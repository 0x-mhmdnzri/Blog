using System.Globalization;
using System.Text;

namespace BlogApp.Services;

/// <summary>Build UTF-8 BOM CSV files for admin DataTable full exports.</summary>
public static class CsvExport
{
    public static FileContentResult File(string fileName, string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));

        // UTF-8 with BOM so Excel opens Persian correctly
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        var safe = SanitizeFileName(fileName);
        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = safe.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? safe : safe + ".csv"
        };
    }

    public static string Cell(object? value)
    {
        if (value is null) return "";
        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };
    }

    private static string Escape(string? value)
    {
        value ??= "";
        // neutralize formula injection in Excel
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "export.csv" : name.Trim();
    }
}

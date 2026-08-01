namespace AVICRM.Models.ViewModels;

/// <summary>DataTables 1.x/2.x server-side request (query-string bound).</summary>
public class DataTablesRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = 25;
    public string? SearchValue { get; set; }
    public int OrderColumn { get; set; }
    public string OrderDir { get; set; } = "desc";

    /// <summary>Per-column search values keyed by column index (columns[i][search][value]).</summary>
    public Dictionary<int, string> ColumnSearches { get; set; } = new();

    public static DataTablesRequest From(HttpRequest request)
    {
        var q = request.Query;
        int.TryParse(q["draw"], out var draw);
        int.TryParse(q["start"], out var start);
        int.TryParse(q["length"], out var length);
        if (length <= 0 || length > 200) length = 25;
        if (start < 0) start = 0;

        int.TryParse(q["order[0][column]"], out var orderCol);
        var orderDir = (q["order[0][dir]"].ToString() ?? "desc").ToLowerInvariant();
        if (orderDir is not ("asc" or "desc")) orderDir = "desc";

        var colSearches = new Dictionary<int, string>();
        // DataTables sends columns[0][search][value], columns[1][search][value], ...
        for (var i = 0; i < 40; i++)
        {
            var key = $"columns[{i}][search][value]";
            if (!q.ContainsKey(key)) continue;
            var val = q[key].ToString()?.Trim();
            if (string.IsNullOrEmpty(val)) continue;
            if (val.Length > 120) val = val[..120];
            colSearches[i] = val;
        }

        return new DataTablesRequest
        {
            Draw = draw,
            Start = start,
            Length = length,
            SearchValue = q["search[value]"].ToString()?.Trim(),
            OrderColumn = orderCol,
            OrderDir = orderDir,
            ColumnSearches = colSearches
        };
    }

    public bool Asc => OrderDir == "asc";

    public string? Col(int index) =>
        ColumnSearches.TryGetValue(index, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}

public class DataTablesResponse
{
    public int draw { get; set; }
    public int recordsTotal { get; set; }
    public int recordsFiltered { get; set; }
    public object data { get; set; } = Array.Empty<object>();

    public static DataTablesResponse Ok(int draw, int total, int filtered, object data) => new()
    {
        draw = draw,
        recordsTotal = total,
        recordsFiltered = filtered,
        data = data
    };
}

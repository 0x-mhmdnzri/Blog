using System.Globalization;

namespace BlogApp.Services;

/// <summary>
/// Formats UTC timestamps as Shamsi (Jalali) calendar dates in Asia/Tehran time.
/// Stored values stay UTC; only display is converted.
/// </summary>
public static class PersianDate
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly TimeZoneInfo Tehran = ResolveTehran();

    private static TimeZoneInfo ResolveTehran()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
        catch (TimeZoneNotFoundException) { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
        catch (TimeZoneNotFoundException) { }
        // Fallback: fixed UTC+03:30 (Iran no longer observes DST)
        return TimeZoneInfo.CreateCustomTimeZone("Tehran", TimeSpan.FromHours(3.5), "Tehran", "Tehran");
    }

    /// <summary>Convert a UTC (or unspecified-as-UTC) value to Tehran local time.</summary>
    public static DateTime ToTehran(DateTime utc)
    {
        var kind = utc.Kind == DateTimeKind.Local
            ? utc.ToUniversalTime()
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(kind, Tehran);
    }

    /// <summary>yyyy/MM/dd — e.g. 1405/05/07</summary>
    public static string Date(DateTime? utc)
    {
        if (utc is null) return "—";
        return Format(utc.Value, includeTime: false);
    }

    /// <summary>yyyy/MM/dd HH:mm — e.g. 1405/05/07 16:10</summary>
    public static string DateTime(DateTime? utc)
    {
        if (utc is null) return "—";
        return Format(utc.Value, includeTime: true);
    }

    /// <summary>ISO-like for &lt;time datetime&gt; attribute (still Gregorian UTC).</summary>
    public static string Iso(DateTime? utc)
    {
        if (utc is null) return string.Empty;
        var u = utc.Value.Kind == DateTimeKind.Utc
            ? utc.Value
            : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        return u.ToString("o", CultureInfo.InvariantCulture);
    }

    private static string Format(DateTime utc, bool includeTime)
    {
        var local = ToTehran(utc);
        var y = Calendar.GetYear(local);
        var m = Calendar.GetMonth(local);
        var d = Calendar.GetDayOfMonth(local);
        if (!includeTime)
            return $"{y:0000}/{m:00}/{d:00}";
        return $"{y:0000}/{m:00}/{d:00} {local.Hour:00}:{local.Minute:00}";
    }
}

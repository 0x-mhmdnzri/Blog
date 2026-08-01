namespace AVICRM.Services;

/// <summary>
/// Converts form datetime values. After client-side conversion, datetime-local posts
/// are UTC wall-clock (Unspecified). If a timezone offset is supplied instead,
/// treat Unspecified as the user's local wall-clock.
/// </summary>
public static class DateTimeUserLocal
{
    /// <param name="value">Bound datetime-local value (usually Unspecified).</param>
    /// <param name="timezoneOffsetMinutes">
    /// Optional JS <c>Date.getTimezoneOffset()</c> (minutes to add to local to reach UTC).
    /// When null, value is assumed already converted to UTC wall-clock by the client.
    /// </param>
    /// <param name="clientAlreadyConvertedToUtc">
    /// When true (default for posts that ran BlogDatePicker.prepareForm), treat Unspecified as UTC.
    /// </param>
    public static DateTime? ToUtc(
        DateTime? value,
        int? timezoneOffsetMinutes = null,
        bool clientAlreadyConvertedToUtc = true)
    {
        if (value is null) return null;
        var v = value.Value;
        if (v.Year < 2000) return null;

        if (v.Kind == DateTimeKind.Utc)
            return v;

        if (v.Kind == DateTimeKind.Local)
            return v.ToUniversalTime();

        // Unspecified
        if (clientAlreadyConvertedToUtc || timezoneOffsetMinutes is null)
            return DateTime.SpecifyKind(v, DateTimeKind.Utc);

        // Local wall-clock → UTC using browser offset (getTimezoneOffset)
        return DateTime.SpecifyKind(v.AddMinutes(timezoneOffsetMinutes.Value), DateTimeKind.Utc);
    }

    /// <summary>Format UTC instant for data-utc-iso attributes (round-trip to client).</summary>
    public static string? ToUtcIsoAttribute(DateTime? utc)
    {
        if (utc is null) return null;
        var u = utc.Value.Kind == DateTimeKind.Utc
            ? utc.Value
            : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        return u.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    }
}

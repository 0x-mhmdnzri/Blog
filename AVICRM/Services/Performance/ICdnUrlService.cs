namespace AVICRM.Services.Performance;

public interface ICdnUrlService
{
    string MediaUrl(int mediaId);
    string Resolve(string? relativeOrAbsolute);
    bool IsEnabled { get; }
}

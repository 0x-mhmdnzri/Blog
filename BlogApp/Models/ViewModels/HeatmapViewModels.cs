namespace BlogApp.Models.ViewModels;

public class HeatmapDetailViewModel
{
    public int PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int RangeDays { get; set; } = 30;
    public int TotalClicks { get; set; }
    public int UniqueCells { get; set; }
    public List<HeatmapPoint> Points { get; set; } = new();
}

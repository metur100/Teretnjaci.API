namespace TeretnjaciBa.Models;

public class Image
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Article Article { get; set; } = null!;
}

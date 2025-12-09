namespace TeretnjaciBa.Models;

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int CategoryId { get; set; }
    public int AuthorId { get; set; }
    public int ViewCount { get; set; } = 0;
    public bool IsPublished { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Category Category { get; set; } = null!;
    public User Author { get; set; } = null!;
    public ICollection<Image> Images { get; set; } = new List<Image>();
}

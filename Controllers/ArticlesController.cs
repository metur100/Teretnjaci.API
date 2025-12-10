using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TeretnjaciBa.Data;
using TeretnjaciBa.DTOs;
using TeretnjaciBa.Models;

namespace TeretnjaciBa.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ArticlesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/articles
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ArticleListDto>>> GetArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null)
    {
        var query = _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .Where(a => a.IsPublished) 
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(a => a.Category.Slug == category);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.Title.Contains(search) || a.Content.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var articles = await query
            .OrderByDescending(a => a.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArticleListDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                CategoryName = a.Category.Name,
                CategorySlug = a.Category.Slug,
                AuthorName = a.Author.FullName,
                ViewCount = a.ViewCount,
                PublishedAt = a.PublishedAt,
                PrimaryImageUrl = a.Images
                    .Where(i => i.IsPrimary)
                    .Select(i => i.FilePath)
                    .FirstOrDefault() ?? a.Images.Select(i => i.FilePath).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new PagedResponse<ArticleListDto>
        {
            Data = articles,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    // GET: api/articles/admin
    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("admin")]
    public async Task<ActionResult<PagedResponse<ArticleListDto>>> GetAdminArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isPublished = null)
    {
        var query = _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .AsQueryable();

        // For admin users, allow filtering by status
        if (isPublished.HasValue)
        {
            query = query.Where(a => a.IsPublished == isPublished.Value);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(a => a.Category.Slug == category);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.Title.Contains(search) || a.Content.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var articles = await query
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArticleListDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                CategoryName = a.Category.Name,
                CategorySlug = a.Category.Slug,
                AuthorName = a.Author.FullName,
                ViewCount = a.ViewCount,
                PublishedAt = a.PublishedAt,
                IsPublished = a.IsPublished,
                CreatedAt = a.CreatedAt,
                PrimaryImageUrl = a.Images
                    .Where(i => i.IsPrimary)
                    .Select(i => i.FilePath)
                    .FirstOrDefault() ?? a.Images.Select(i => i.FilePath).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new PagedResponse<ArticleListDto>
        {
            Data = articles,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    // GET: api/articles/slug/{slug}
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<ApiResponse<ArticleDetailDto>>> GetArticleBySlug(string slug)
    {
        var article = await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished);

        if (article == null)
        {
            return NotFound(new ApiResponse<ArticleDetailDto>
            {
                Success = false,
                Message = "Članak nije pronađen"
            });
        }

        // Increment view count
        article.ViewCount++;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<ArticleDetailDto>
        {
            Success = true,
            Data = new ArticleDetailDto
            {
                Id = article.Id,
                Title = article.Title,
                Slug = article.Slug,
                Content = article.Content,
                CategoryName = article.Category.Name,
                CategorySlug = article.Category.Slug,
                AuthorName = article.Author.FullName,
                ViewCount = article.ViewCount,
                PublishedAt = article.PublishedAt,
                Images = article.Images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    FileName = i.FileName,
                    Url = i.FilePath,
                    IsPrimary = i.IsPrimary
                }).ToList()
            }
        });
    }

    // POST: api/articles
    [Authorize(Roles = "Owner,Admin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ArticleDetailDto>>> CreateArticle([FromBody] CreateArticleRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var slug = GenerateSlug(request.Title);
        var existingSlug = await _context.Articles.AnyAsync(a => a.Slug == slug);
        if (existingSlug)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        var article = new Article
        {
            Title = request.Title,
            Slug = slug,
            Content = request.Content,
            CategoryId = request.CategoryId,
            AuthorId = userId,
            IsPublished = request.IsPublished,
            PublishedAt = request.IsPublished ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Articles.Add(article);
        await _context.SaveChangesAsync();

        var createdArticle = await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .FirstAsync(a => a.Id == article.Id);

        return Ok(new ApiResponse<ArticleDetailDto>
        {
            Success = true,
            Message = "Članak je uspješno kreiran",
            Data = new ArticleDetailDto
            {
                Id = createdArticle.Id,
                Title = createdArticle.Title,
                Slug = createdArticle.Slug,
                Content = createdArticle.Content,
                CategoryName = createdArticle.Category.Name,
                CategorySlug = createdArticle.Category.Slug,
                AuthorName = createdArticle.Author.FullName,
                ViewCount = createdArticle.ViewCount,
                PublishedAt = createdArticle.PublishedAt,
                Images = new List<ImageDto>()
            }
        });
    }

    [Authorize(Roles = "Owner,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ArticleDetailDto>>> UpdateArticle(int id, [FromBody] UpdateArticleRequest request)
    {
        var article = await _context.Articles
            .Include(a => a.Author)
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null)
        {
            return NotFound(new ApiResponse<ArticleDetailDto>
            {
                Success = false,
                Message = "Članak nije pronađen"
            });
        }

        // Validate category exists
        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
        {
            return BadRequest(new ApiResponse<ArticleDetailDto>
            {
                Success = false,
                Message = "Kategorija nije pronađena"
            });
        }

        article.Title = request.Title;
        article.Content = request.Content;
        article.CategoryId = request.CategoryId;
        article.IsPublished = request.IsPublished;

        if (request.IsPublished && article.PublishedAt == null)
        {
            article.PublishedAt = DateTime.UtcNow;
        }

        article.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload article with updated relationships
        var updatedArticle = await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .FirstAsync(a => a.Id == id);

        return Ok(new ApiResponse<ArticleDetailDto>
        {
            Success = true,
            Message = "Članak je uspješno ažuriran",
            Data = new ArticleDetailDto
            {
                Id = updatedArticle.Id,
                Title = updatedArticle.Title,
                Slug = updatedArticle.Slug,
                Content = updatedArticle.Content,
                CategoryName = updatedArticle.Category.Name,
                CategorySlug = updatedArticle.Category.Slug,
                AuthorName = updatedArticle.Author.FullName,
                ViewCount = updatedArticle.ViewCount,
                PublishedAt = updatedArticle.PublishedAt,
                Images = updatedArticle.Images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    FileName = i.FileName,
                    Url = i.FilePath,
                    IsPrimary = i.IsPrimary
                }).ToList()
            }
        });
    }

    // DELETE: api/articles/{id}
    [Authorize(Roles = "Owner,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteArticle(int id)
    {
        var article = await _context.Articles
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Članak nije pronađen"
            });
        }

        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Članak je uspješno obrisan"
        });
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        
        // Replace special Bosnian characters
        slug = slug.Replace('č', 'c').Replace('ć', 'c').Replace('đ', 'd')
                   .Replace('š', 's').Replace('ž', 'z');
        
        // Remove invalid characters
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        
        // Replace multiple spaces or hyphens with single hyphen
        slug = Regex.Replace(slug, @"[\s-]+", " ").Trim();
        slug = Regex.Replace(slug, @"\s", "-");
        
        return slug;
    }

    // GET: api/articles/{id}
    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ArticleDetailDto>>> GetArticleById(int id)
    {
        var article = await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Author)
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null)
        {
            return NotFound(new ApiResponse<ArticleDetailDto>
            {
                Success = false,
                Message = "Članak nije pronađen"
            });
        }

        return Ok(new ApiResponse<ArticleDetailDto>
        {
            Success = true,
            Data = new ArticleDetailDto
            {
                Id = article.Id,
                Title = article.Title,
                Slug = article.Slug,
                Content = article.Content,
                CategoryName = article.Category.Name,
                CategorySlug = article.Category.Slug,
                CategoryId = article.CategoryId, 
                AuthorName = article.Author.FullName,
                ViewCount = article.ViewCount,
                PublishedAt = article.PublishedAt,
                IsPublished = article.IsPublished,
                Images = article.Images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    FileName = i.FileName,
                    Url = i.FilePath,
                    IsPrimary = i.IsPrimary
                }).ToList()
            }
        });
    }
}

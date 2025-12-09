using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeretnjaciBa.Data;
using TeretnjaciBa.DTOs;

namespace TeretnjaciBa.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/categories
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories()
    {
        var categories = await _context.Categories
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ArticleCount = c.Articles.Count(a => a.IsPublished)
            })
            .ToListAsync();

        return Ok(new ApiResponse<List<CategoryDto>>
        {
            Success = true,
            Data = categories
        });
    }

    // GET: api/categories/{slug}
    [HttpGet("{slug}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetCategoryBySlug(string slug)
    {
        var category = await _context.Categories
            .Where(c => c.Slug == slug)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ArticleCount = c.Articles.Count(a => a.IsPublished)
            })
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Kategorija nije pronađena"
            });
        }

        return Ok(new ApiResponse<CategoryDto>
        {
            Success = true,
            Data = category
        });
    }
}

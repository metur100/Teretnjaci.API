using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeretnjaciBa.Data;
using TeretnjaciBa.DTOs;
using TeretnjaciBa.Models;
using TeretnjaciBa.Services;

namespace TeretnjaciBa.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin")]
public class ImagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;

    public ImagesController(ApplicationDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    // POST: api/images/upload/{articleId}
    [HttpPost("upload/{articleId}")]
    public async Task<ActionResult<ApiResponse<ImageDto>>> UploadImage(int articleId, IFormFile file)
    {
        var article = await _context.Articles.FindAsync(articleId);
        if (article == null)
        {
            return NotFound(new ApiResponse<ImageDto>
            {
                Success = false,
                Message = "Članak nije pronađen"
            });
        }

        var result = await _fileService.SaveFileAsync(file);
        if (!result.Success)
        {
            return BadRequest(new ApiResponse<ImageDto>
            {
                Success = false,
                Message = result.Error
            });
        }

        // If this is the first image, make it primary
        var isFirstImage = !await _context.Images.AnyAsync(i => i.ArticleId == articleId);

        var image = new Image
        {
            ArticleId = articleId,
            FileName = result.FileName,
            FilePath = result.FilePath,
            FileSize = result.FileSize,
            IsPrimary = isFirstImage,
            CreatedAt = DateTime.UtcNow
        };

        _context.Images.Add(image);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<ImageDto>
        {
            Success = true,
            Message = "Slika je uspješno učitana",
            Data = new ImageDto
            {
                Id = image.Id,
                FileName = image.FileName,
                Url = image.FilePath,
                IsPrimary = image.IsPrimary
            }
        });
    }

    // PUT: api/images/{id}/set-primary
    [HttpPut("{id}/set-primary")]
    public async Task<ActionResult<ApiResponse<object>>> SetPrimaryImage(int id)
    {
        var image = await _context.Images.FindAsync(id);
        if (image == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Slika nije pronađena"
            });
        }

        // Remove primary flag from other images
        var otherImages = await _context.Images
            .Where(i => i.ArticleId == image.ArticleId && i.Id != id)
            .ToListAsync();

        foreach (var otherImage in otherImages)
        {
            otherImage.IsPrimary = false;
        }

        image.IsPrimary = true;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Glavna slika je postavljena"
        });
    }

    // DELETE: api/images/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteImage(int id)
    {
        var image = await _context.Images.FindAsync(id);
        if (image == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Slika nije pronađena"
            });
        }

        // Delete file from disk
        await _fileService.DeleteFileAsync(image.FilePath);

        _context.Images.Remove(image);
        await _context.SaveChangesAsync();

        // If deleted image was primary, set another image as primary
        if (image.IsPrimary)
        {
            var nextImage = await _context.Images
                .Where(i => i.ArticleId == image.ArticleId)
                .FirstOrDefaultAsync();

            if (nextImage != null)
            {
                nextImage.IsPrimary = true;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Slika je uspješno obrisana"
        });
    }
}

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
    private readonly IImgBBService _imgBBService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        ApplicationDbContext context,
        IImgBBService imgBBService,
        ILogger<ImagesController> logger)
    {
        _context = context;
        _imgBBService = imgBBService;
        _logger = logger;
    }

    // POST: api/images/upload/{articleId}
    [HttpPost("upload/{articleId}")]
    public async Task<ActionResult<ApiResponse<ImageDto>>> UploadImage(int articleId, IFormFile file)
    {
        try
        {
            _logger.LogInformation($"Starting image upload for article {articleId}");

            var article = await _context.Articles.FindAsync(articleId);
            if (article == null)
            {
                _logger.LogWarning($"Article {articleId} not found");
                return NotFound(new ApiResponse<ImageDto>
                {
                    Success = false,
                    Message = "Članak nije pronađen"
                });
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file provided");
                return BadRequest(new ApiResponse<ImageDto>
                {
                    Success = false,
                    Message = "Molimo odaberite datoteku"
                });
            }

            _logger.LogInformation($"File received: {file.FileName}, Size: {file.Length} bytes");

            // Upload to ImgBB
            _logger.LogInformation("Uploading to ImgBB...");
            var result = await _imgBBService.UploadImageAsync(file);

            if (!result.Success)
            {
                _logger.LogError($"ImgBB upload failed: {result.Error}");
                return BadRequest(new ApiResponse<ImageDto>
                {
                    Success = false,
                    Message = result.Error
                });
            }

            _logger.LogInformation($"ImgBB upload successful: {result.Url}");

            // If this is the first image, make it primary
            var isFirstImage = !await _context.Images.AnyAsync(i => i.ArticleId == articleId);
            _logger.LogInformation($"Is first image: {isFirstImage}");

            var image = new Image
            {
                ArticleId = articleId,
                FileName = result.FileName,
                FilePath = result.Url, // Full ImgBB URL
                FileSize = result.FileSize,
                IsPrimary = isFirstImage,
                CreatedAt = DateTime.UtcNow
            };

            _context.Images.Add(image);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Image saved to database with ID: {image.Id}");

            return Ok(new ApiResponse<ImageDto>
            {
                Success = true,
                Message = "Slika je uspješno učitana na ImgBB",
                Data = new ImageDto
                {
                    Id = image.Id,
                    FileName = image.FileName,
                    Url = image.FilePath,
                    IsPrimary = image.IsPrimary
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new ApiResponse<ImageDto>
            {
                Success = false,
                Message = $"Greška pri učitavanju slike: {ex.Message}\n\nStack trace: {ex.StackTrace}"
            });
        }
    }

    // POST: api/images/upload-inline
    [HttpPost("upload-inline")]
    public async Task<ActionResult<ApiResponse<InlineImageDto>>> UploadInlineImage(IFormFile file)
    {
        try
        {
            _logger.LogInformation("Starting inline image upload");

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file provided for inline upload");
                return BadRequest(new ApiResponse<InlineImageDto>
                {
                    Success = false,
                    Message = "Molimo odaberite datoteku"
                });
            }

            _logger.LogInformation($"Inline file received: {file.FileName}, Size: {file.Length} bytes");

            // Upload to ImgBB
            _logger.LogInformation("Uploading inline image to ImgBB...");
            var result = await _imgBBService.UploadImageAsync(file);

            if (!result.Success)
            {
                _logger.LogError($"Inline ImgBB upload failed: {result.Error}");
                return BadRequest(new ApiResponse<InlineImageDto>
                {
                    Success = false,
                    Message = result.Error
                });
            }

            _logger.LogInformation($"Inline ImgBB upload successful: {result.Url}");

            return Ok(new ApiResponse<InlineImageDto>
            {
                Success = true,
                Message = "Slika je uspješno učitana na ImgBB",
                Data = new InlineImageDto
                {
                    Url = result.Url,
                    FileName = result.FileName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading inline image");
            return StatusCode(500, new ApiResponse<InlineImageDto>
            {
                Success = false,
                Message = $"Greška pri učitavanju slike: {ex.Message}\n\nStack trace: {ex.StackTrace}"
            });
        }
    }

    // PUT: api/images/{id}/set-primary
    [HttpPut("{id}/set-primary")]
    public async Task<ActionResult<ApiResponse<object>>> SetPrimaryImage(int id)
    {
        try
        {
            _logger.LogInformation($"Setting image {id} as primary");

            var image = await _context.Images.FindAsync(id);
            if (image == null)
            {
                _logger.LogWarning($"Image {id} not found");
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

            _logger.LogInformation($"Removing primary flag from {otherImages.Count} other images");

            foreach (var otherImage in otherImages)
            {
                otherImage.IsPrimary = false;
            }

            image.IsPrimary = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Image {id} set as primary successfully");

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Glavna slika je postavljena"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary image");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Greška: {ex.Message}"
            });
        }
    }

    // DELETE: api/images/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteImage(int id)
    {
        try
        {
            _logger.LogInformation($"Deleting image {id}");

            var image = await _context.Images.FindAsync(id);
            if (image == null)
            {
                _logger.LogWarning($"Image {id} not found");
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Slika nije pronađena"
                });
            }

            // Try to delete from ImgBB (won't work in free tier, but attempt anyway)
            _logger.LogInformation($"Attempting to delete from ImgBB: {image.FilePath}");
            await _imgBBService.DeleteImageAsync(image.FilePath);

            // Remove from database
            _context.Images.Remove(image);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Image {id} deleted from database");

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
                    _logger.LogInformation($"Set image {nextImage.Id} as new primary");
                }
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Slika je uklonjena iz baze podataka (ImgBB slike su trajne u free tieru)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Greška: {ex.Message}"
            });
        }
    }
}
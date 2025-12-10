using Microsoft.AspNetCore.Mvc;
using TeretnjaciBa.Services;

namespace TeretnjaciBa.Controllers;

/// <summary>
/// Test controller to verify ImgBB service is working
/// Access at: https://localhost:YOUR_PORT/api/test/imgbb
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IImgBBService _imgBBService;
    private readonly IConfiguration _configuration;

    public TestController(IImgBBService imgBBService, IConfiguration configuration)
    {
        _imgBBService = imgBBService;
        _configuration = configuration;
    }

    // GET: api/test/imgbb
    [HttpGet("imgbb")]
    public IActionResult TestImgBBConfiguration()
    {
        try
        {
            var apiKey = _configuration["ImgBB:ApiKey"];

            return Ok(new
            {
                success = true,
                message = "ImgBB service is configured",
                hasApiKey = !string.IsNullOrEmpty(apiKey),
                apiKeyLength = apiKey?.Length ?? 0,
                apiKeyPreview = apiKey?.Substring(0, Math.Min(10, apiKey.Length)) + "...",
                serviceRegistered = _imgBBService != null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "ImgBB service test failed",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    // POST: api/test/upload
    // Test actual upload
    [HttpPost("upload")]
    public async Task<IActionResult> TestUpload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided" });
            }

            var result = await _imgBBService.UploadImageAsync(file);

            return Ok(new
            {
                success = result.Success,
                url = result.Url,
                fileName = result.FileName,
                fileSize = result.FileSize,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Upload test failed",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
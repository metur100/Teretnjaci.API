namespace TeretnjaciBa.Services;

public interface IFileService
{
    Task<(bool Success, string FileName, string FilePath, long FileSize, string Error)> SaveFileAsync(IFormFile file);
    Task<bool> DeleteFileAsync(string filePath);
}

public class FileService : IFileService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly string _uploadPath;
    private readonly long _maxFileSize;
    private readonly string[] _allowedExtensions;

    public FileService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;

        var fileUploadSettings = _configuration.GetSection("FileUpload");
        _uploadPath = fileUploadSettings["UploadPath"] ?? "wwwroot/uploads";
        _maxFileSize = fileUploadSettings.GetValue<long>("MaxFileSize", 5242880); // 5MB default
        _allowedExtensions = fileUploadSettings.GetSection("AllowedExtensions").Get<string[]>() 
            ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    }

    public async Task<(bool Success, string FileName, string FilePath, long FileSize, string Error)> SaveFileAsync(IFormFile file)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return (false, string.Empty, string.Empty, 0, "Nema datoteke ili je datoteka prazna");
            }

            if (file.Length > _maxFileSize)
            {
                return (false, string.Empty, string.Empty, 0, $"Datoteka je prevelika. Maksimalna veličina je {_maxFileSize / 1024 / 1024}MB");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                return (false, string.Empty, string.Empty, 0, $"Tip datoteke nije dozvoljen. Dozvoljeni tipovi: {string.Join(", ", _allowedExtensions)}");
            }

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var uploadDir = Path.Combine(_environment.ContentRootPath, _uploadPath);

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var filePath = Path.Combine(uploadDir, uniqueFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/{uniqueFileName}";
            return (true, file.FileName, relativePath, file.Length, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, string.Empty, 0, $"Greška pri čuvanju datoteke: {ex.Message}");
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fullPath = Path.Combine(_environment.ContentRootPath, "wwwroot", filePath.TrimStart('/'));
            
            if (File.Exists(fullPath))
            {
                await Task.Run(() => File.Delete(fullPath));
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

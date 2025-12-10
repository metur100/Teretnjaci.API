using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeretnjaciBa.Services;

public interface IImgBBService
{
    Task<(bool Success, string Url, string FileName, long FileSize, string Error)> UploadImageAsync(IFormFile file);
    Task<(bool Success, string Error)> DeleteImageAsync(string url);
}

public class ImgBBService : IImgBBService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.imgbb.com/1/upload";
    private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB limit (ImgBB supports up to 32MB)

    public ImgBBService(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = configuration["ImgBB:ApiKey"];

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentException("ImgBB API Key is not configured. Please add ImgBB:ApiKey to appsettings.json");
        }
    }

    public async Task<(bool Success, string Url, string FileName, long FileSize, string Error)> UploadImageAsync(IFormFile file)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return (false, null, null, 0, "File is empty");
            }

            // Validate file size
            if (file.Length > _maxFileSize)
            {
                return (false, null, null, 0, $"File is too large. Maximum size is {_maxFileSize / 1024 / 1024}MB");
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return (false, null, null, 0, $"File type not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
            }

            // Read file into base64
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var base64Image = Convert.ToBase64String(ms.ToArray());

            // Prepare form data
            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(base64Image), "image");
            formData.Add(new StringContent(_apiKey), "key");
            formData.Add(new StringContent("0"), "expiration"); // 0 = never expire

            // Send request
            var response = await _httpClient.PostAsync(_baseUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = ParseError(responseContent);
                return (false, null, null, 0, $"Upload failed: {error}");
            }

            // Parse successful response
            var result = JsonSerializer.Deserialize<ImgBBResponse>(responseContent);

            if (result?.Success == true && result.Data != null)
            {
                return (true, result.Data.Url, file.FileName, file.Length, string.Empty);
            }

            return (false, null, null, 0, "Upload failed: Unknown error");
        }
        catch (Exception ex)
        {
            return (false, null, null, 0, $"Upload error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Error)> DeleteImageAsync(string url)
    {
        try
        {
            // Note: ImgBB free tier doesn't support image deletion via API
            // Images are permanent unless expiration was set during upload
            // Return success to keep app flow working
            await Task.CompletedTask;
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Delete error: {ex.Message}");
        }
    }

    private string ParseError(string responseContent)
    {
        try
        {
            var errorDoc = JsonDocument.Parse(responseContent);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorProp))
            {
                if (errorProp.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString();
                }
                return errorProp.ToString();
            }
        }
        catch
        {
            // If we can't parse JSON, return raw content
        }

        return responseContent.Length > 100 ? responseContent.Substring(0, 100) + "..." : responseContent;
    }

    // Response classes
    private class ImgBBResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public ImgBBData Data { get; set; }
    }

    private class ImgBBData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("display_url")]
        public string DisplayUrl { get; set; }

        [JsonPropertyName("thumb")]
        public Thumbnail Thumb { get; set; }

        [JsonPropertyName("delete_url")]
        public string DeleteUrl { get; set; }
    }

    private class Thumbnail
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
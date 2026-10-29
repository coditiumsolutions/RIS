using Microsoft.Extensions.Options;

namespace ResidentPortal.Services;

public class VisitorPhotoStorageOptions
{
    public const string SectionName = "VisitorPhotos";

    /// <summary>Absolute or content-root-relative folder for visitor photos. Outside wwwroot.</summary>
    public string StoragePath { get; set; } = "App_Data/visitor-photos";

    public int MaxBytes { get; set; } = 2 * 1024 * 1024;
}

public class VisitorPhotoStorage
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    private readonly IWebHostEnvironment _env;
    private readonly VisitorPhotoStorageOptions _options;
    private readonly ILogger<VisitorPhotoStorage> _logger;

    public VisitorPhotoStorage(
        IWebHostEnvironment env,
        IOptions<VisitorPhotoStorageOptions> options,
        ILogger<VisitorPhotoStorage> logger)
    {
        _env = env;
        _options = options.Value;
        _logger = logger;
    }

    public int MaxBytes => Math.Max(64 * 1024, _options.MaxBytes);

    public string ResolveRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.StoragePath)
            ? "App_Data/visitor-photos"
            : _options.StoragePath.Trim();

        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));

        Directory.CreateDirectory(root);
        return root;
    }

    public bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType.Trim());

    public string NormalizeContentType(string contentType)
    {
        if (contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            return "image/jpeg";
        return contentType.Trim().ToLowerInvariant();
    }

    public async Task<(bool Ok, string? RelativeFileName, string? Error)> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedContentType(contentType))
            return (false, null, "Only JPEG or PNG images are allowed.");

        if (content.CanSeek)
            content.Position = 0;

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0)
            return (false, null, "The photo file is empty.");

        if (buffer.Length > MaxBytes)
            return (false, null, $"Photo must be {MaxBytes / (1024 * 1024)} MB or smaller.");

        buffer.Position = 0;
        if (!LooksLikeImage(buffer, contentType))
            return (false, null, "The uploaded file is not a valid image.");

        var normalized = NormalizeContentType(contentType);
        var extension = normalized == "image/png" ? ".png" : ".jpg";
        var relativeName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(ResolveRoot(), relativeName);

        buffer.Position = 0;
        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await buffer.CopyToAsync(file, cancellationToken);
        }

        return (true, relativeName, null);
    }

    public Stream? OpenRead(string relativeFileName)
    {
        if (!IsSafeRelativeFileName(relativeFileName))
            return null;

        var fullPath = Path.Combine(ResolveRoot(), relativeFileName);
        if (!File.Exists(fullPath))
            return null;

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void TryDelete(string? relativeFileName)
    {
        if (!IsSafeRelativeFileName(relativeFileName))
            return;

        try
        {
            var fullPath = Path.Combine(ResolveRoot(), relativeFileName!);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete visitor photo {File}.", relativeFileName);
        }
    }

    private static bool IsSafeRelativeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (name.Contains("..", StringComparison.Ordinal) || name.Contains('/') || name.Contains('\\'))
            return false;
        return name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeImage(Stream stream, string contentType)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        stream.Position = 0;
        if (read < 3)
            return false;

        var isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

        if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase))
            return isPng;
        return isJpeg;
    }
}

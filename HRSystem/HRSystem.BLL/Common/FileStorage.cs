namespace HRSystem.BLL.Common;

// An uploaded file without any ASP.NET types, so services stay testable
public record FileUpload(Stream Content, string FileName, long Length);

// Implemented in the Web layer (saves under App_Data, outside wwwroot)
public interface IFileStorage
{
    // Returns the stored path relative to the storage root
    Task<string> SaveAsync(FileUpload file, string folder);
    Stream? Open(string relativePath);
    void Delete(string relativePath);
}

public static class FileRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly string[] DocumentExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    public static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png"];

    public static string? Validate(FileUpload file, string[] allowedExtensions)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return "نوع الملف مش مسموح. المسموح: " + string.Join("، ", allowedExtensions);
        if (file.Length == 0)
            return "الملف فاضي";
        if (file.Length > MaxBytes)
            return "حجم الملف أكبر من 5 ميجا";
        return null;
    }

    public static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}

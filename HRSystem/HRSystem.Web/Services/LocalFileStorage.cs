using HRSystem.BLL.Common;

namespace HRSystem.Web.Services;

// Files live under App_Data/uploads, outside wwwroot, so they are only reachable through
// controller actions that check permissions first
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "uploads"));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(FileUpload file, string folder)
    {
        var safeName = string.Concat(Path.GetFileName(file.FileName).Split(Path.GetInvalidFileNameChars()));
        var relativePath = Path.Combine(folder, $"{Guid.NewGuid():N}_{safeName}").Replace('\\', '/');
        var fullPath = Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = File.Create(fullPath);
        await file.Content.CopyToAsync(output);

        return relativePath;
    }

    public Stream? Open(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    public void Delete(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    // Refuses any path that would escape the uploads folder (e.g. "../")
    private string Resolve(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path.");
        return fullPath;
    }
}

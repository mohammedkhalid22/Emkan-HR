using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public record DocumentFile(Stream Content, string FileName, string ContentType);

public interface IEmployeeDocumentService
{
    // Returns the new document id
    Task<ServiceResult<int>> UploadAsync(UploadDocumentDto dto, FileUpload file);
    Task<DocumentFile?> OpenAsync(int documentId, EmployeeAccessScope scope);
    Task<ServiceResult<int>> DeleteAsync(int documentId);
}

public class EmployeeDocumentService : IEmployeeDocumentService
{
    public const string CvDocumentType = "السيرة الذاتية (CV)";

    public static readonly string[] DocumentTypes = [CvDocumentType, "بطاقة الرقم القومي", "شهادة المؤهل", "عقد العمل", "شهادة الميلاد", "صحيفة الحالة الجنائية", "شهادة التجنيد", "أخرى"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<EmployeeDocument> _documents;
    private readonly IFileStorage _fileStorage;

    public EmployeeDocumentService(IUnitOfWork unitOfWork, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _documents = unitOfWork.Repository<EmployeeDocument>();
        _fileStorage = fileStorage;
    }

    public async Task<ServiceResult<int>> UploadAsync(UploadDocumentDto dto, FileUpload file)
    {
        if (!await _unitOfWork.Repository<Employee>().QueryNoTracking().AnyAsync(e => e.Id == dto.EmployeeId))
            return ServiceResult<int>.Failure("الموظف مش موجود");

        if (FileRules.Validate(file, FileRules.DocumentExtensions) is string error)
            return ServiceResult<int>.Failure(error);

        var path = await _fileStorage.SaveAsync(file, $"employees/{dto.EmployeeId}/documents");
        var document = new EmployeeDocument
        {
            EmployeeId = dto.EmployeeId,
            DocumentType = dto.DocumentType.Trim(),
            ExpiryDate = dto.ExpiryDate,
            FilePath = path
        };
        await _documents.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<int>.Success(document.Id);
    }

    public async Task<DocumentFile?> OpenAsync(int documentId, EmployeeAccessScope scope)
    {
        var document = await _documents.QueryNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.FilePath, d.EmployeeId, d.Employee.ManagerId })
            .FirstOrDefaultAsync();

        if (document is null || !scope.CanSee(document.EmployeeId, document.ManagerId))
            return null;

        var stream = _fileStorage.Open(document.FilePath);
        return stream is null
            ? null
            : new DocumentFile(stream, OriginalFileName(document.FilePath), FileRules.ContentTypeFor(document.FilePath));
    }

    // Returns the employee id so the caller can go back to the profile
    public async Task<ServiceResult<int>> DeleteAsync(int documentId)
    {
        var document = await _documents.GetByIdAsync(documentId);
        if (document is null)
            return ServiceResult<int>.Failure("المستند مش موجود");

        // The row is soft-deleted; the file stays on disk so the audit trail still points to something
        _documents.Remove(document);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<int>.Success(document.EmployeeId);
    }

    // Stored names are "{guid}_{original name}"
    public static string OriginalFileName(string storedPath)
    {
        var name = Path.GetFileName(storedPath);
        var separator = name.IndexOf('_');
        return separator > 0 ? name[(separator + 1)..] : name;
    }
}

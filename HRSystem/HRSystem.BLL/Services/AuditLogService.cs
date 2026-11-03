using System.Reflection;
using System.Text.Json;
using HRSystem.BLL.Common;
using HRSystem.DAL.Data;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// One changed field; values come back typed (enum, bool, date...) so the view can name them in Arabic
public record AuditChange(string Field, object? Old, object? New);

public class AuditLogDto
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = null!;
    public string EntityName { get; init; } = null!;
    public string EntityId { get; init; } = null!;

    // Who or what the row is about, when we can tell (employee name, account name, setting key)
    public string? RecordName { get; init; }
    public List<AuditChange> Changes { get; init; } = [];
}

public class AuditLogFilter
{
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    // Insert / Update / Delete ("Action" would clash with the MVC route value)
    public string? Op { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}

// "Who changed what": the read side of the audit trail HRDbContext writes on every save
public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogFilter filter);
    Task<List<string>> EntityNamesAsync();

    // Everyone who appears in the log, for the filter
    Task<List<(string Id, string Name)>> ActorsAsync();
}

public class AuditLogService : IAuditLogService
{
    private readonly HRDbContext _context;

    public AuditLogService(HRDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogFilter filter)
    {
        var query = _context.AuditLogs.AsNoTracking();
        if (!string.IsNullOrEmpty(filter.Entity))
            query = query.Where(a => a.EntityName == filter.Entity);
        if (!string.IsNullOrEmpty(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId);
        if (!string.IsNullOrEmpty(filter.UserId))
            query = filter.UserId == "-" ? query.Where(a => a.UserId == null) : query.Where(a => a.UserId == filter.UserId);
        if (!string.IsNullOrEmpty(filter.Op))
            query = query.Where(a => a.Action == filter.Op);
        // Dates are picked in local time, stored in UTC
        if (filter.From is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.Timestamp >= start);
        }
        if (filter.To is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.Timestamp < end);
        }

        var page = Math.Max(1, filter.Page);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Skip((page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync();

        var names = await RecordNamesAsync(rows);
        // Accounts are few: one lookup serves both "who" and the *By fields inside the changes
        var actors = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName);
        var roles = await _context.Roles.ToDictionaryAsync(r => r.Id, r => r.Name);

        return new PagedResult<AuditLogDto>
        {
            Items = rows.Select(r => new AuditLogDto
            {
                Id = r.Id,
                Timestamp = r.Timestamp,
                UserId = r.UserId,
                UserName = r.UserId is null ? null : actors.GetValueOrDefault(r.UserId) ?? "حساب اتمسح",
                Action = r.Action,
                EntityName = r.EntityName,
                EntityId = r.EntityId,
                RecordName = names.GetValueOrDefault((r.EntityName, r.EntityId)),
                Changes = Changes(r, roles, actors)
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = filter.PageSize
        };
    }

    public Task<List<string>> EntityNamesAsync()
        => _context.AuditLogs.AsNoTracking().Select(a => a.EntityName).Distinct().OrderBy(n => n).ToListAsync();

    public async Task<List<(string Id, string Name)>> ActorsAsync()
    {
        var ids = await _context.AuditLogs.AsNoTracking().Where(a => a.UserId != null).Select(a => a.UserId!).Distinct().ToListAsync();
        var users = await _context.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName }).ToListAsync();
        return users.Select(u => (u.Id, u.FullName)).ToList();
    }

    // ---------- Helpers ----------

    // Names for the rows on this page, one query per kind
    private async Task<Dictionary<(string, string), string>> RecordNamesAsync(List<AuditLog> rows)
    {
        var result = new Dictionary<(string, string), string>();
        List<string> Ids(params string[] entities) => rows.Where(r => entities.Contains(r.EntityName)).Select(r => r.EntityId).Distinct().ToList();

        var employeeIds = Ids(nameof(Employee)).Select(id => int.TryParse(id, out var n) ? n : 0).ToList();
        if (employeeIds.Count > 0)
        {
            foreach (var e in await _context.Employees.IgnoreQueryFilters().AsNoTracking()
                         .Where(e => employeeIds.Contains(e.Id)).Select(e => new { e.Id, e.FullNameAr }).ToListAsync())
                result[(nameof(Employee), e.Id.ToString())] = e.FullNameAr;
        }

        var userIds = Ids(nameof(ApplicationUser), "UserRole");
        if (userIds.Count > 0)
        {
            foreach (var u in await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName }).ToListAsync())
            {
                result[(nameof(ApplicationUser), u.Id)] = u.FullName;
                result[("UserRole", u.Id)] = u.FullName;
            }
        }

        var settingIds = Ids(nameof(SystemSetting)).Select(id => int.TryParse(id, out var n) ? n : 0).ToList();
        if (settingIds.Count > 0)
        {
            foreach (var s in await _context.SystemSettings.AsNoTracking().Where(s => settingIds.Contains(s.Id)).ToListAsync())
                result[(nameof(SystemSetting), s.Id.ToString())] = s.Key;
        }

        return result;
    }

    private static List<AuditChange> Changes(AuditLog row, Dictionary<string, string?> roles, Dictionary<string, string> users)
    {
        var oldValues = Parse(row.OldValues);
        var newValues = Parse(row.NewValues);
        var type = EntityType(row.EntityName);

        object? Typed(string field, JsonElement? value)
        {
            if (value is not JsonElement json || json.ValueKind == JsonValueKind.Null)
                return null;
            if (field == "RoleId")
                return roles.GetValueOrDefault(json.GetString() ?? "") ?? json.GetString();
            // Account ids (UserId, ApprovedBy, HrActionBy...) read as the person's name
            if ((field == "UserId" || field.EndsWith("By")) && json.ValueKind == JsonValueKind.String)
                return users.GetValueOrDefault(json.GetString()!) ?? json.GetString();

            // Read the value back as the entity's own property type, so enums and dates come out right
            var property = type?.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
            {
                try { return json.Deserialize(property.PropertyType); }
                catch (Exception ex) when (ex is JsonException or NotSupportedException) { }
            }
            return json.ValueKind switch
            {
                JsonValueKind.String => json.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => json.GetRawText()
            };
        }

        // The row's own key is already in the "on what" column
        return oldValues.Keys.Union(newValues.Keys).Where(field => field != "Id")
            .Select(field => new AuditChange(field,
                Typed(field, oldValues.TryGetValue(field, out var o) ? o : null),
                Typed(field, newValues.TryGetValue(field, out var n) ? n : null)))
            .ToList();
    }

    private static Dictionary<string, JsonElement> Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static Type? EntityType(string name) => name switch
    {
        nameof(ApplicationUser) => typeof(ApplicationUser),
        _ => typeof(Employee).Assembly.GetType($"{typeof(Employee).Namespace}.{name}")
    };
}

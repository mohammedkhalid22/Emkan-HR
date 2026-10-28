using System.Globalization;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// Reads and edits values in SystemSettings (seeded in DbInitializer)
public interface ISettingsService
{
    Task<string?> GetAsync(string key);
    Task<int> GetIntAsync(string key, int fallback);
    Task<decimal> GetDecimalAsync(string key, decimal fallback);

    Task<List<SettingDto>> GetAllAsync();
    Task<ServiceResult> UpdateAsync(string key, string value);
}

public class SettingsService : ISettingsService
{
    public const string WeekendDays = "WeekendDays";

    private readonly HRDbContext _context;

    public SettingsService(HRDbContext context)
    {
        _context = context;
    }

    public Task<string?> GetAsync(string key)
        => _context.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

    public async Task<int> GetIntAsync(string key, int fallback)
        => int.TryParse(await GetAsync(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback)
        => decimal.TryParse(await GetAsync(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public Task<List<SettingDto>> GetAllAsync()
        => _context.SystemSettings.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new SettingDto { Key = s.Key, Value = s.Value, Description = s.Description })
            .ToListAsync();

    public async Task<ServiceResult> UpdateAsync(string key, string value)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
            return ServiceResult.Failure("الإعداد مش موجود");

        value = (value ?? "").Trim();
        if (key == WeekendDays)
        {
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != LeaveCalculator.ParseWeekend(value).Count)
                return ServiceResult.Failure("أيام الإجازة لازم تتكتب بالإنجليزي مفصولة بفاصلة، زي: Friday,Saturday");
        }
        else if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < 0)
        {
            return ServiceResult.Failure("القيمة لازم تكون رقم موجب (استخدم . للكسور، زي 0.11)");
        }

        setting.Value = value;
        await _context.SaveChangesAsync();
        return ServiceResult.Success();
    }
}

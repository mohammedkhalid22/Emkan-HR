using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.Common;

public record CvAnalysisResult(
    string Summary,
    decimal? ExperienceYears,
    string? Education,
    int MatchPercent,
    string? MatchReason,
    List<string> Skills,
    List<string> Strengths,
    List<string> Gaps);

// Personal data read from a CV to pre-fill the new employee form; anything unclear stays null
public record CvProfile(
    string? FullNameAr,
    string? FullNameEn,
    string? Email,
    string? Phone,
    string? Address,
    DateOnly? BirthDate,
    Gender? Gender,
    MaritalStatus? MaritalStatus);

// Turns the AI's JSON into checked values (pure, so it can be unit tested).
// The AI may wrap the JSON in text or ``` fences, skip fields or return odd values.
public static partial class CvParser
{
    private const int MaxListItems = 15;

    public static CvAnalysisResult? ParseAnalysis(string text)
    {
        using var doc = TryParse(text);
        if (doc is null)
            return null;

        var root = doc.RootElement;
        var summary = Str(root, "summary", 2000);
        if (summary is null)
            return null;

        var years = Num(root, "experienceYears");
        return new CvAnalysisResult(
            summary,
            years is >= 0 and <= 60 ? Math.Round(years.Value, 1) : null,
            Str(root, "education", 500),
            (int)Math.Clamp(Math.Round(Num(root, "matchPercent") ?? 0), 0, 100),
            Str(root, "matchReason", 1000),
            List(root, "skills"),
            List(root, "strengths"),
            List(root, "gaps"));
    }

    public static CvProfile? ParseProfile(string text)
    {
        using var doc = TryParse(text);
        if (doc is null)
            return null;

        var root = doc.RootElement;
        var email = Str(root, "email", 150);
        return new CvProfile(
            Str(root, "fullNameAr", 150),
            Str(root, "fullNameEn", 150),
            email is not null && EmailPattern().IsMatch(email) ? email : null,
            EgyptianMobile(Str(root, "phone", 40)),
            Str(root, "address", 250),
            Date(Str(root, "birthDate", 20)),
            Str(root, "gender", 20)?.ToLowerInvariant() switch
            {
                "male" => Gender.Male,
                "female" => Gender.Female,
                _ => null
            },
            Str(root, "maritalStatus", 20)?.ToLowerInvariant() switch
            {
                "single" => MaritalStatus.Single,
                "married" => MaritalStatus.Married,
                "divorced" => MaritalStatus.Divorced,
                "widowed" => MaritalStatus.Widowed,
                _ => null
            });
    }

    // "+20 100 123 4567", "00201001234567", "٠١٠٠١٢٣٤٥٦٧" -> "01001234567"; anything else -> null
    public static string? EgyptianMobile(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Select(c => char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c)
            .Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("0020")) digits = "0" + digits[4..];
        else if (digits.StartsWith("20") && digits.Length == 12) digits = "0" + digits[2..];
        else if (digits.Length == 10 && digits.StartsWith('1')) digits = "0" + digits;

        return MobilePattern().IsMatch(digits) ? digits : null;
    }

    // The JSON object inside the reply, ignoring any text or fences around it
    private static JsonDocument? TryParse(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            var doc = JsonDocument.Parse(text[start..(end + 1)]);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                return doc;
            doc.Dispose();
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static string? Str(JsonElement root, string name, int maxLength)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var text = value.GetString()!.Trim();
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        return text.Length > maxLength ? text[..maxLength] : text;
    }

    private static decimal? Num(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString()?.TrimEnd('%', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            return number;
        return null;
    }

    private static List<string> List(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!.Replace('\n', ' ').Trim())
            .Where(s => s.Length > 0)
            .Select(s => s.Length > 200 ? s[..200] : s)
            .Distinct()
            .Take(MaxListItems)
            .ToList();
    }

    private static DateOnly? Date(string? text)
        => DateOnly.TryParseExact(text, ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
           && date.Year is > 1940 and < 2015
            ? date
            : null;

    [GeneratedRegex(@"^01[0125]\d{8}$")]
    private static partial Regex MobilePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

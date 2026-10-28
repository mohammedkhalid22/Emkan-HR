using HRSystem.Domain.Enums;

namespace HRSystem.BLL.Common;

// Egyptian national ID: C YYMMDD GG SSS G X (14 digits)
//   C  = century (2 = 1900s, 3 = 2000s)
//   YYMMDD = birth date, GG = governorate, 13th digit odd = male / even = female
public static class NationalId
{
    public static bool TryParse(string? value, out DateOnly birthDate, out Gender gender)
    {
        birthDate = default;
        gender = default;

        if (value is null || value.Length != 14 || !value.All(char.IsAsciiDigit))
            return false;

        var century = value[0] switch { '2' => 1900, '3' => 2000, _ => 0 };
        if (century == 0)
            return false;

        var year = century + int.Parse(value[1..3]);
        var month = int.Parse(value[3..5]);
        var day = int.Parse(value[5..7]);

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        birthDate = new DateOnly(year, month, day);
        gender = (value[12] - '0') % 2 == 1 ? Gender.Male : Gender.Female;
        return true;
    }
}

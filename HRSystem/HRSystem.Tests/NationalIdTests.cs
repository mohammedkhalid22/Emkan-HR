using HRSystem.BLL.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Tests;

public class NationalIdTests
{
    [Fact]
    public void Born_in_the_1900s_male()
    {
        Assert.True(NationalId.TryParse("29005150112351", out var birthDate, out var gender));
        Assert.Equal(new DateOnly(1990, 5, 15), birthDate);
        Assert.Equal(Gender.Male, gender);
    }

    [Fact]
    public void Born_in_the_2000s_female()
    {
        Assert.True(NationalId.TryParse("30001010112342", out var birthDate, out var gender));
        Assert.Equal(new DateOnly(2000, 1, 1), birthDate);
        Assert.Equal(Gender.Female, gender);
    }

    [Theory]
    [InlineData("2900515011235")]   // 13 digits
    [InlineData("19005150112351")]  // century digit 1
    [InlineData("29013150112351")]  // month 13
    [InlineData("29002300112351")]  // 30 February
    [InlineData("2900515011235A")]  // not all digits
    [InlineData(null)]
    public void Invalid_ids_are_rejected(string? value)
        => Assert.False(NationalId.TryParse(value, out _, out _));
}

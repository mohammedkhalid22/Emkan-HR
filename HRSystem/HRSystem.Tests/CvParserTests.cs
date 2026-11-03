using HRSystem.BLL.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Tests;

public class CvParserTests
{
    private const string Analysis = """
        {
          "summary": "محاسب بخبرة 4 سنين في الحسابات العامة.",
          "experienceYears": 4.25,
          "education": "بكالوريوس تجارة - جامعة القاهرة",
          "skills": ["Excel", "Odoo", "Excel", "  "],
          "matchPercent": 82,
          "matchReason": "بيغطي أغلب المتطلبات.",
          "strengths": ["خبرة في الحسابات العامة"],
          "gaps": ["مفيش شهادة CMA"]
        }
        """;

    [Fact]
    public void Reads_a_full_analysis()
    {
        var result = CvParser.ParseAnalysis(Analysis)!;

        Assert.Equal(82, result.MatchPercent);
        Assert.Equal(4.2m, result.ExperienceYears);
        Assert.Equal(["Excel", "Odoo"], result.Skills);
        Assert.Single(result.Gaps);
    }

    [Fact]
    public void Ignores_text_and_fences_around_the_json()
        => Assert.NotNull(CvParser.ParseAnalysis("Here you go:\n```json\n" + Analysis + "\n```"));

    [Theory]
    [InlineData("150", 100)]
    [InlineData("-5", 0)]
    [InlineData("\"70%\"", 70)]
    [InlineData("null", 0)]
    public void Match_percent_is_kept_between_0_and_100(string value, int expected)
        => Assert.Equal(expected, CvParser.ParseAnalysis($$"""{"summary":"x","matchPercent":{{value}}}""")!.MatchPercent);

    [Fact]
    public void An_answer_without_a_summary_is_rejected()
        => Assert.Null(CvParser.ParseAnalysis("""{"matchPercent": 90}"""));

    [Fact]
    public void Broken_json_is_rejected()
        => Assert.Null(CvParser.ParseAnalysis("""{"summary": "x", """));

    [Fact]
    public void Reads_a_profile_and_drops_bad_values()
    {
        var profile = CvParser.ParseProfile("""
            {"fullNameAr":"منى سامي","fullNameEn":null,"email":"not-an-email","phone":"+20 100 123 4567",
             "birthDate":"1995-03-14","gender":"female","maritalStatus":"engaged"}
            """)!;

        Assert.Equal("منى سامي", profile.FullNameAr);
        Assert.Null(profile.FullNameEn);
        Assert.Null(profile.Email);
        Assert.Equal("01001234567", profile.Phone);
        Assert.Equal(new DateOnly(1995, 3, 14), profile.BirthDate);
        Assert.Equal(Gender.Female, profile.Gender);
        Assert.Null(profile.MaritalStatus);
    }

    [Theory]
    [InlineData("01001234567", "01001234567")]
    [InlineData("+20 100 123 4567", "01001234567")]
    [InlineData("00201221234567", "01221234567")]
    [InlineData("1551234567", "01551234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "01001234567")]
    [InlineData("0223456789", null)]
    [InlineData("+971 50 123 4567", null)]
    public void Normalises_egyptian_mobiles(string input, string? expected)
        => Assert.Equal(expected, CvParser.EgyptianMobile(input));
}

using HRSystem.BLL.Common;

namespace HRSystem.Tests;

public class GeoCalculatorTests
{
    // Tahrir Square, Cairo
    private const double BranchLat = 30.04442;
    private const double BranchLng = 31.23571;
    private const int Radius = 200;

    // Roughly 0.001 degree of latitude = 111 meters
    private static GeoPoint North(double meters, double? accuracy = 10)
        => new(BranchLat + meters / 111_195.0, BranchLng, accuracy);

    [Fact]
    public void Distance_to_the_same_point_is_zero()
        => Assert.Equal(0, GeoCalculator.DistanceMeters(BranchLat, BranchLng, BranchLat, BranchLng), 3);

    [Fact]
    public void Distance_from_Cairo_to_Alexandria_is_about_180_km()
    {
        var meters = GeoCalculator.DistanceMeters(30.0444, 31.2357, 31.2001, 29.9187);
        Assert.InRange(meters, 178_000, 182_000);
    }

    [Fact]
    public void Inside_the_radius_is_allowed()
        => Assert.Equal(LocationCheck.Allowed, GeoCalculator.Check(North(150), BranchLat, BranchLng, Radius));

    [Fact]
    public void Far_from_the_branch_is_refused()
        => Assert.Equal(LocationCheck.OutsideBranch, GeoCalculator.Check(North(1_000), BranchLat, BranchLng, Radius));

    [Fact]
    public void Gps_error_is_forgiven_just_outside_the_radius()
        => Assert.Equal(LocationCheck.Allowed, GeoCalculator.Check(North(250, accuracy: 60), BranchLat, BranchLng, Radius));

    [Fact]
    public void Gps_error_allowance_is_capped()
        => Assert.Equal(LocationCheck.OutsideBranch, GeoCalculator.Check(North(450, accuracy: 400), BranchLat, BranchLng, Radius));

    [Fact]
    public void A_very_weak_fix_is_not_trusted_even_at_the_branch()
        => Assert.Equal(LocationCheck.TooInaccurate, GeoCalculator.Check(North(0, accuracy: 2_000), BranchLat, BranchLng, Radius));

    [Theory]
    [InlineData(91, 31)]
    [InlineData(30, 181)]
    public void Out_of_range_coordinates_are_invalid(double lat, double lng)
        => Assert.False(GeoCalculator.IsValid(new GeoPoint(lat, lng)));
}

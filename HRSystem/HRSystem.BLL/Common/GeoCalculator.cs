namespace HRSystem.BLL.Common;

// A position reported by the browser; accuracy is the radius (meters) it may be off by
public record GeoPoint(double Latitude, double Longitude, double? AccuracyMeters = null);

public enum LocationCheck
{
    Allowed,
    OutsideBranch,
    TooInaccurate
}

// Pure calculations (no database) so they can be unit tested
public static class GeoCalculator
{
    private const double EarthRadiusMeters = 6_371_000;

    // A weak GPS fix can be off by this much at most and still be trusted
    public const double MaxAccuracyMeters = 500;

    // GPS error is forgiven up to this much on top of the branch radius
    public const double MaxAccuracyAllowance = 100;

    public static bool IsValid(GeoPoint point)
        => point.Latitude is >= -90 and <= 90 && point.Longitude is >= -180 and <= 180
           && (point.AccuracyMeters is null or >= 0);

    // Haversine distance between two points on the earth
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // Inside the radius, allowing for some GPS error, but never trusting a very weak fix
    public static LocationCheck Check(GeoPoint point, double branchLat, double branchLng, int radiusMeters)
    {
        var accuracy = point.AccuracyMeters ?? 0;
        if (accuracy > MaxAccuracyMeters)
            return LocationCheck.TooInaccurate;

        var distance = DistanceMeters(point.Latitude, point.Longitude, branchLat, branchLng);
        return distance <= radiusMeters + Math.Min(accuracy, MaxAccuracyAllowance)
            ? LocationCheck.Allowed
            : LocationCheck.OutsideBranch;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

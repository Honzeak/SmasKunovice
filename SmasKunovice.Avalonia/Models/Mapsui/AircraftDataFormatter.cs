namespace SmasKunovice.Avalonia.Models.Mapsui;

public static class AircraftDataFormatter
{
    public static string GetSpeedString(ScoutData scoutData)
    {
        const double mpsToKnotFactor = 1.94384;
        var speedKnots = scoutData.Odid?.Location?.SpeedHorizontal;
        return speedKnots is null ? "?" : $"{speedKnots * mpsToKnotFactor:F0}";
    }

    public static string GetHeightString(ScoutData scoutData)
    {
        const double meterToFeetConvertFactor = 3.28084;
        var verticalSpeed = scoutData.Odid?.Location?.SpeedVertical;
        var verticalSpeedSymbol = verticalSpeed switch
        {
            > 0 and <= 62 => "↑",
            < 0 and >= -62 => "↓",
            _ => string.Empty
            // null or 0 or <= -63 or >= 63 => string.Empty,
        };
        var scoutDataAltitude = scoutData.Odid?.Location?.AltitudeGeo;
        string heightValue;
        if (scoutDataAltitude is null)
        {
            heightValue = "?";
        }
        else
        {
            var feet = (int)(scoutDataAltitude.Value * meterToFeetConvertFactor);
            heightValue = feet < 5000 ? feet.ToString() : $"FL{feet / 100f:F0}";
        }

        return heightValue + verticalSpeedSymbol;
    }
}
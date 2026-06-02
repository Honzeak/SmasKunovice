namespace SmasKunovice.Avalonia.Extensions;

public static class NumberExtensions
{
    private const double MeterToFeetConvertFactor = 3.28084;
    public static double MeterToFeet(this double meter)
    {
        return MeterToFeetConvertFactor * meter;
    }
    
    public static double MeterToFeet(this float meter)
    {
        return MeterToFeetConvertFactor * meter;
    }
    
    public static double MeterToFeet(this int meter)
    {
        return MeterToFeetConvertFactor * meter;
    }
    
}
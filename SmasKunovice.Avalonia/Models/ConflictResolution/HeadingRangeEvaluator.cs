using System;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class HeadingRangeEvaluator
{
    public int HeadingLowerBound { get; }
    public int HeadingUpperBound { get; }

    public HeadingRangeEvaluator(int runwayDirectionDegrees, int headingThresholdOffset)
    {
        if (headingThresholdOffset is < 0 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(headingThresholdOffset),
                headingThresholdOffset,
                "The heading offset must be between 0 and 180 degrees.");
        }

        var normalizedRunwayDirection = Normalize(runwayDirectionDegrees);

        HeadingLowerBound = Normalize(normalizedRunwayDirection - headingThresholdOffset);
        HeadingUpperBound = Normalize(normalizedRunwayDirection + headingThresholdOffset);
    }

    public bool IsWithinBounds(int headingDegrees)
    {
        var heading = Normalize(headingDegrees);

        if (HeadingLowerBound <= HeadingUpperBound)
        {
            return heading >= HeadingLowerBound &&
                   heading <= HeadingUpperBound;
        }

        // The range crosses 0°, for example 350° through 30°.
        return heading >= HeadingLowerBound ||
               heading <= HeadingUpperBound;
    }

    private static int Normalize(int degrees)
    {
        return ((degrees % 360) + 360) % 360;
    }
}

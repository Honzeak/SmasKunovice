using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests.TestUtils;

public class DummyTransformator : IScoutDataCoordTransformation
{
    public ScoutData TransformScoutDataCoords(ScoutData scoutData)
    {
        return scoutData;
    }
}
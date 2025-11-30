using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests.TestUtils;

public class DummyAircraftDatabase : IAircraftDatabase
{
    public AircraftRecord? GetByIcao24(string icao24)
    {
        return null;
    }
}
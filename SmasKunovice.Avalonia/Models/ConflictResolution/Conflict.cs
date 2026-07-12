namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public enum ConflictType
{
    DroneAboveLimit,
    RpaPresence,
    RunwayApproach
}

public enum ConflictLevel
{
    None,
    Warning,
    Alarm
}
namespace SmasKunovice.Avalonia.Models;

/// <summary>
/// The Data Transfer Object representing a single row in the CSV.
/// Properties use CamelCase as requested.
/// </summary>
public record AircraftRecord
{
    public required string Icao24 { get; init; }
    public string? Timestamp { get; set; } // Kept as string to preserve format, can be DateTime
    public string? Acars { get; set; }
    public string? Adsb { get; set; }
    public string? Built { get; set; }
    public string? CategoryDescription { get; set; }
    public string? Country { get; set; }
    public string? Engines { get; set; }
    public string? FirstFlightDate { get; set; }
    public string? FirstSeen { get; set; }
    public string? IcaoAircraftClass { get; set; }
    public string? LineNumber { get; set; }
    public string? ManufacturerIcao { get; set; }
    public string? ManufacturerName { get; set; }
    public string? Model { get; set; }
    public string? Modes { get; set; }
    public string? NextReg { get; set; }
    public string? Notes { get; set; }
    public string? Operator { get; set; }
    public string? OperatorCallsign { get; set; }
    public string? OperatorIata { get; set; }
    public string? OperatorIcao { get; set; }
    public string? Owner { get; set; }
    public string? PrevReg { get; set; }
    public string? RegUntil { get; set; }
    public string? Registered { get; set; }
    public string? Registration { get; set; }
    public string? SelCal { get; set; }
    public string? SerialNumber { get; set; }
    public string? Status { get; set; }
    public string? Typecode { get; set; }
    public string? Vdl { get; set; }
}
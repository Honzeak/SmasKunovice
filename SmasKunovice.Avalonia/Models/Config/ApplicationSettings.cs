using System.ComponentModel.DataAnnotations;

namespace SmasKunovice.Avalonia.Models.Config;

public class ApplicationSettings
{
    public string LoggingLevel { get; set; } = "Information";
    public int MaxConnections { get; set; }
    [Required] public string AircraftDatabasePath { get; set; } = string.Empty;

    [Required] public string GeoJsonsBasePath { get; set; } = string.Empty;
    [Required] public string SvgBasePath { get; set; } = string.Empty;
    
}
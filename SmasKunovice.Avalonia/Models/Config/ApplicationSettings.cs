using System.ComponentModel.DataAnnotations;

namespace SmasKunovice.Avalonia.Models.Config;

public class ApplicationSettings
{
    public string LoggingLevel { get; set; } = "Information";
    public int MaxConnections { get; set; }
}
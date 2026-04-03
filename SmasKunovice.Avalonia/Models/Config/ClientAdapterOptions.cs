using System.ComponentModel.DataAnnotations;

namespace SmasKunovice.Avalonia.Models.Config;

public class ClientAdapterOptions
{
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    public string? Username { get; set; }
    public string? Password { get; set; }

    [Required]
    public string HeartbeatTopic { get; set; } = string.Empty;

    [Required]
    public string OdidTopic { get; set; } = string.Empty;
    
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    public bool LogReceivedMessages { get; set; } = false;
    public string ClientSourceLogFilePath { get; set; } = string.Empty;
}

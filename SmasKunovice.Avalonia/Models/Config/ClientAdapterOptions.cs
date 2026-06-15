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
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    [Required]
    public string HeartbeatTopic { get; set; } = string.Empty;
    [Required]
    public string OdidTopic { get; set; } = string.Empty;
    
    public bool IsBatchedData { get; set; }
    public bool IsCompressedData { get; set; }

    public bool LogReceivedMessages { get; set; }
    public string ClientSourceLogFilePath { get; set; } = string.Empty;
}

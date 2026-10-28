using System.Text;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace SmasKunovice.BrokerService;

public class MqttBrokerService : BackgroundService
{
    private readonly MqttServer _mqttServer;
    private readonly ILogger<MqttBrokerService> _logger;
    private readonly IConfiguration _configuration;

    public MqttBrokerService(ILogger<MqttBrokerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var mqttFactory = new MqttServerFactory();

        // 1. Configure Server Options (TCP Port 1883)
        var mqttServerOptions = mqttFactory.CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(1883) // Standard MQTT port
            .WithConnectionBacklog(100)
            .Build();

        _mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

        // 2. Register Event Handlers
        _mqttServer.ValidatingConnectionAsync += OnValidatingConnectionAsync;
        _mqttServer.ClientConnectedAsync += OnClientConnectedAsync;
        _mqttServer.ClientDisconnectedAsync += OnClientDisconnectedAsync;
        _mqttServer.InterceptingPublishAsync += OnInterceptingPublishAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting embedded MQTT Broker on port 1883...");
        await _mqttServer.StartAsync();

        try
        {
            // Keep the background task alive until host shutdown is signaled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected exception during graceful shutdown
        }
        finally
        {
            _logger.LogInformation("Stopping MQTT Broker...");
            await _mqttServer.StopAsync();
            _logger.LogInformation("MQTT Broker stopped successfully.");
        }
    }

    // Client Authentication Validation
    private Task OnValidatingConnectionAsync(ValidatingConnectionEventArgs e)
    {
        // Example authentication check
        var username = _configuration["MqttBroker:Username"];
        var password = _configuration["MqttBroker:Password"];
        var isValidUser = e.UserName == username && e.Password == password;
        _logger.LogInformation("broker username: {0} broker password: {1}", username, password);
        _logger.LogInformation("client username: {0} client password: {1}", e.UserName, e.Password);

        if (!isValidUser)
        {
            e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            _logger.LogWarning("Unauthorized connection attempt from Client ID: {ClientId}, IP: {Endpoint}",
                e.ClientId, e.RemoteEndPoint);
        }
        else
        {
            e.ReasonCode = MqttConnectReasonCode.Success;
            _logger.LogInformation("Client authenticated: {ClientId}", e.ClientId);
        }

        return Task.CompletedTask;
    }

    private Task OnClientConnectedAsync(ClientConnectedEventArgs e)
    {
        _logger.LogInformation("Client Connected: {ClientId}", e.ClientId);
        return Task.CompletedTask;
    }

    private Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs e)
    {
        _logger.LogInformation("Client Disconnected: {ClientId} (Type: {DisconnectType})",
            e.ClientId, e.DisconnectType);
        return Task.CompletedTask;
    }

    // Message Interception / Logging / In-Memory Inspection
    private Task OnInterceptingPublishAsync(InterceptingPublishEventArgs e)
    {
        string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        _logger.LogInformation("Topic: {Topic} | Payload: {Payload}", e.ApplicationMessage.Topic, payload);

        return Task.CompletedTask;
    }
}
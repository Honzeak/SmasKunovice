using System;
using System.IO;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MQTTnet;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Config;

namespace SmasKunovice.Avalonia.Models;

public class ScoutDataMqttClientAdapter : IDronetagClient
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _connectOptions;
    private readonly IScoutDataCoordTransformation _transformation;
    private readonly bool _logReceivedMessages;
    private readonly string _messagesLogPath = string.Empty;
    private readonly Channel<string> _loggingChannel = Channel.CreateUnbounded<string>();
    private readonly Task? _loggingTask;

    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;
    public event EventHandler<string>? HeartbeatReceived;

    public ScoutDataMqttClientAdapter(IScoutDataCoordTransformation transformation, IOptions<ClientAdapterOptions> options)
    {
        _transformation = transformation;
        var adapterOptions = options.Value;
        _logReceivedMessages = adapterOptions.LogReceivedMessages;
        if (_logReceivedMessages)
        {
            _loggingTask = Task.Run(ProcessLogQueue);
            Directory.CreateDirectory("ScoutMessages");
            _messagesLogPath = Path.Combine("ScoutMessages", $"log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        }

        _client = new MqttClientFactory().CreateMqttClient();
        var connectionBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(adapterOptions.Host, adapterOptions.Port)
            .WithTlsOptions(builder =>
                builder.UseTls()
                    .WithAllowUntrustedCertificates()
                    .WithCertificateValidationHandler(_ => true)
                    .WithSslProtocols(SslProtocols.None)
            )
            .WithCleanSession();
        if (adapterOptions.HasCredentials)
            connectionBuilder.WithCredentials(adapterOptions.Username, adapterOptions.Password);

        _connectOptions = connectionBuilder.Build();

        SetupConnectionEvents(adapterOptions);
        LogExtensions.LogInfo("Dronetag MQTT client adapter initialized.", this);
    }

    private async Task ProcessLogQueue()
    {
        await using var streamWriter = new StreamWriter(_messagesLogPath, true);
        var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        await foreach (var log in _loggingChannel.Reader.ReadAllAsync())
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(log);
                var prettyJson = JsonSerializer.Serialize(jsonDoc, jsonSerializerOptions);
                await streamWriter.WriteLineAsync(prettyJson + ",");
            }
            catch (Exception ex)
            {
                LogExtensions.LogError(ex, "Failed to log JSON message", this);
            }
        }

        await streamWriter.FlushAsync();
    }

    private void SetupConnectionEvents(ClientAdapterOptions adapterOptions)
    {
        _client.ConnectedAsync += async e =>
        {
            LogExtensions.LogInfo("Connected to MQTT broker.", this);
            await _client.SubscribeAsync(adapterOptions.HeartbeatTopic);
            LogExtensions.LogInfo("Subscribed to heartbeat topic: {0}", this, adapterOptions.HeartbeatTopic);
            await _client.SubscribeAsync(adapterOptions.OdidTopic);
            LogExtensions.LogInfo("Subscribed to ODID topic: {0}", this, adapterOptions.OdidTopic);
        };

        _client.DisconnectedAsync += e =>
        {
            LogExtensions.LogInfo("Disconnected from MQTT broker.", this);
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            if (e.ApplicationMessage.Topic.Equals(adapterOptions.HeartbeatTopic))
            {
                // LogExtensions.LogDebug("Received message on Heartbeat topic.", this);
                ProcessHeartbeatMessage(e);
            }
            else if (e.ApplicationMessage.Topic.Equals(adapterOptions.OdidTopic))
            {
                // LogExtensions.LogDebug("Received message on ODID topic.", this);
                await ProcessOdidMessage(e);
            }
            else
            {
                LogExtensions.LogWarning("Received message on unknown topic.", this);
            }
        };
    }

    private void ProcessHeartbeatMessage(
        MqttApplicationMessageReceivedEventArgs mqttApplicationMessageReceivedEventArgs)
    {
        // TODO implement heartbeat processing
        HeartbeatReceived?.Invoke(this, Encoding.UTF8.GetString(mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload));
        LogExtensions.LogDebug("Received Heartbeat message: {0}", this, mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload);
    }

    private async Task ProcessOdidMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            var message = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
            if (_logReceivedMessages)
                await LogJsonMessage(message);

            // Deserialize the JSON payload into the ScoutData object
            var scoutData = JsonSerializer.Deserialize<ScoutData>(message, ScoutData.SerializerOptions);

            if (scoutData != null)
            {
                scoutData = _transformation.TransformScoutDataCoords(scoutData);
                
                // if (scoutData.HasLocation)
                //     LogExtensions.LogDebug("ODID coords after transform: {0}, {1}", this, scoutData.Odid.Location!.Longitude!, scoutData.Odid.Location.Latitude!);
                // else
                // {
                //     LogExtensions.LogDebug("ODID message didn't have coordinates.", this);
                // }
                MessageReceived?.Invoke(this, new ScoutDataReceivedEventArgs { Messages = [scoutData] });
            }
            else
            {
                LogExtensions.LogError("Failed to deserialize scout data.", this);
                eventArgs.ProcessingFailed = true;
            }
        }
        catch (Exception ex)
        {
            LogExtensions.LogError(ex, "Processing of ODID message has failed", this);
        }
    }

    private Task LogJsonMessage(string message)
    {
        _loggingChannel.Writer.TryWrite(message);
        return Task.CompletedTask;
    }

    public async Task ConnectAsync()
    {
        if (_client.IsConnected)
            return;
        
        MqttClientConnectResult result;
        try
        {
            result = await _client.ConnectAsync(_connectOptions);
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "MQTT client connection failed", this);
            throw;
        }

        LogExtensions.LogInfo("MQTT client connection result code: {0}", this, result.ResultCode);
    }

    public void Dispose()
    {
        _loggingChannel.Writer.Complete();
        _loggingTask?.GetAwaiter().GetResult();
        _client.Dispose();
    }
}
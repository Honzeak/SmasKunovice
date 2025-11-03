using System;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MQTTnet;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models;

public class ScoutDataMqttClientAdapter : IDronetagClient
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _connectOptions;
    private readonly IScoutDataCoordTransformation _transformation;
    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;
    public event EventHandler<string>? HeartbeatReceived;

    public ScoutDataMqttClientAdapter(IScoutDataCoordTransformation transformation, IOptions<ClientAdapterOptions> options)
    {
        _transformation = transformation;
        var adapterOptions = options.Value;
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

        _client.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Topic.Equals(adapterOptions.HeartbeatTopic))
            {
                LogExtensions.LogDebug("Received message on Heartbeat topic.", this);
                ProcessHeartbeatMessage(e);
            }
            else if (e.ApplicationMessage.Topic.Equals(adapterOptions.OdidTopic))
            {
                LogExtensions.LogDebug("Received message on ODID topic.", this);
                ProcessOdidMessage(e);
            }
            else
            {
                LogExtensions.LogWarning("Received message on unknown topic.", this);
            }

            return Task.CompletedTask;
        };
    }

    private void ProcessHeartbeatMessage(
        MqttApplicationMessageReceivedEventArgs mqttApplicationMessageReceivedEventArgs)
    {
        // TODO implement heartbeat processing
        HeartbeatReceived?.Invoke(this, Encoding.UTF8.GetString(mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload));
        LogExtensions.LogDebug("Received Heartbeat message: {0}", this, mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload);
    }

    private void ProcessOdidMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            // TODO is it encoded?
            var message = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
            LogExtensions.LogDebug("Received ODID message", this);

            // Deserialize the JSON payload into the ScoutData object
            var scoutData = JsonSerializer.Deserialize<ScoutData>(message, ScoutData.SerializerOptions);

            if (scoutData != null)
            {
                scoutData = _transformation.TransformScoutDataCoords(scoutData);
                
                if (scoutData.HasLocation)
                    LogExtensions.LogDebug("ODID coords after transform: {0}, {1}", this, scoutData.Odid.Location!.Longitude!, scoutData.Odid.Location.Latitude!);
                else
                {
                    LogExtensions.LogDebug("ODID message didn't have coordinates.", this);
                }
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
            LogExtensions.LogError(ex,"Processing of ODID message has failed", this);
        }
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
        _client.Dispose();
    }
}
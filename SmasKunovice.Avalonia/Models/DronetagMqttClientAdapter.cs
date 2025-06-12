using System;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace SmasKunovice.Avalonia.Models;

public class DronetagMqttClientAdapter : IDronetagClient
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _connectOptions;
    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;
    public event EventHandler<string>? HeartbeatReceived;

    public DronetagMqttClientAdapter(IOptions<ClientAdapterOptions> options)
    {
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
        Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Dronetag MQTT client adapter initialized.");
    }

    private void SetupConnectionEvents(ClientAdapterOptions adapterOptions)
    {
        _client.ConnectedAsync += async e =>
        {
            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Connected to MQTT broker.");
            await _client.SubscribeAsync(adapterOptions.HeartbeatTopic);
            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Subscribed to heartbeat topic: {0}",
                adapterOptions.HeartbeatTopic);
            await _client.SubscribeAsync(adapterOptions.OdidTopic);
            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Subscribed to ODID topic: {0}",
                adapterOptions.OdidTopic);
        };

        _client.DisconnectedAsync += e =>
        {
            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Disconnected from MQTT broker.");
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Topic.Equals(adapterOptions.HeartbeatTopic))
            {
                Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this, "Received message on Heartbeat topic.");
                ProcessHeartbeatMessage(e);
            }
            else if (e.ApplicationMessage.Topic.Equals(adapterOptions.OdidTopic))
            {
                Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this, "Received message on ODID topic.");
                ProcessOdidMessage(e);
            }
            else
            {
                Logger.Sink?.Log(LogEventLevel.Warning, LogArea.Control, this, "Received message on unknown topic.");
            }

            return Task.CompletedTask;
        };
    }

    private void ProcessHeartbeatMessage(
        MqttApplicationMessageReceivedEventArgs mqttApplicationMessageReceivedEventArgs)
    {
        // TODO implement heartbeat processing
        HeartbeatReceived?.Invoke(this, Encoding.UTF8.GetString(mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload));
        Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this, "Received Heartbeat message: {0}",
            mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload);
    }

    private void ProcessOdidMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            // TODO is it encoded?
            var message = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
            Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this, "Received ODID message");

            // Deserialize the JSON payload into the ScoutData object
            var scoutData = JsonSerializer.Deserialize<ScoutData>(message, ScoutData.SerializerOptions);

            if (scoutData != null)
            {
                MessageReceived?.Invoke(this, new ScoutDataReceivedEventArgs { Messages = [scoutData] });
            }
            else
            {
                Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, this, "Failed to deserialize scout data.");
                eventArgs.ProcessingFailed = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, this, "Processing of ODID message has failed: {0}",
                ex.Message);
        }
    }

    public async Task ConnectAsync()
    {
        MqttClientConnectResult result;
        try
        {
            result = await _client.ConnectAsync(_connectOptions);
        }
        catch (Exception e)
        {
            Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, this, "MQTT client connection failed: {0}",
                e.ToString());
            throw;
        }

        Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "MQTT client connection result: {0}",
            result);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
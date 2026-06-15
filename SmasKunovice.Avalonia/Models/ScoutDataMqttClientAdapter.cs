using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    private readonly string _messagesLogPath = string.Empty;
    private readonly Channel<byte[]> _loggingChannel = Channel.CreateUnbounded<byte[]>();
    private readonly Task? _loggingTask;
    private readonly ClientAdapterOptions _adapterOptions;
    private bool _disposed;

    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;
    public event EventHandler<string>? HeartbeatReceived;

    public ScoutDataMqttClientAdapter(IScoutDataCoordTransformation transformation, IOptions<ClientAdapterOptions> options)
    {
        _transformation = transformation;
        _adapterOptions = options.Value;
        if (_adapterOptions.LogReceivedMessages)
        {
            Directory.CreateDirectory("ScoutMessages");
            _messagesLogPath = Path.Combine("ScoutMessages", $"log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            _loggingTask = Task.Run(ProcessLogQueue);
        }

        _client = new MqttClientFactory().CreateMqttClient();
        var connectionBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_adapterOptions.Host, _adapterOptions.Port)
            .WithTlsOptions(builder =>
                builder.UseTls()
                    .WithAllowUntrustedCertificates()
                    .WithCertificateValidationHandler(_ => true)
                    .WithSslProtocols(SslProtocols.None)
            )
            .WithCleanSession();
        if (_adapterOptions.HasCredentials)
            connectionBuilder.WithCredentials(_adapterOptions.Username, _adapterOptions.Password);

        _connectOptions = connectionBuilder.Build();

        SetupConnectionEvents(_adapterOptions);
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

        _client.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                if (e.ApplicationMessage.Topic.Equals(adapterOptions.HeartbeatTopic))
                {
                    // LogExtensions.LogDebug("Received message on Heartbeat topic.", this);
                    ProcessHeartbeatMessage(e);
                }
                else if (e.ApplicationMessage.Topic.Equals(adapterOptions.OdidTopic))
                {
                    // LogExtensions.LogDebug("Received message on ODID topic.", this);
                    ProcessOdidMessage(e);
                }
                else
                {
                    LogExtensions.LogWarning("Received message on unknown topic.", this);
                }

                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        };
    }

    private void ProcessHeartbeatMessage(MqttApplicationMessageReceivedEventArgs mqttApplicationMessageReceivedEventArgs)
    {
        // TODO implement heartbeat processing
        HeartbeatReceived?.Invoke(this, Encoding.UTF8.GetString(mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload));
        LogExtensions.LogDebug("Received Heartbeat message: {0}", this, mqttApplicationMessageReceivedEventArgs.ApplicationMessage.Payload);
    }

    private void ProcessOdidMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            var payload = eventArgs.ApplicationMessage.Payload;
            if (payload.IsEmpty)
                return;

            if (_adapterOptions.IsCompressedData)
            {
                payload = DecompressPayload(payload);
            }

            if (_adapterOptions.LogReceivedMessages)
                LogJsonMessage(payload.ToArray());

            var scoutDataList = DeserializeScoutData(payload);
            MessageReceived?.Invoke(this, new ScoutDataReceivedEventArgs { Messages = scoutDataList });
        }
        catch (Exception ex)
        {
            LogExtensions.LogError(ex, "Processing of ODID message has failed", this);
        }
    }

    private List<ScoutData> DeserializeScoutData(ReadOnlySequence<byte> payload)
    {
        var reader = new Utf8JsonReader(payload);
        List<ScoutData>? scoutDataList;
        if (_adapterOptions.IsBatchedData)
        {
            scoutDataList = JsonSerializer.Deserialize<List<ScoutData>>(ref reader, ScoutData.SerializerOptions);
            if (scoutDataList is not null && scoutDataList.Count > 0)
            {
                scoutDataList = scoutDataList.Select(scoutData => _transformation.TransformScoutDataCoords(scoutData)).ToList();
            }
            else
            {
                throw new Exception("Received ODID message with no data.");
            }
        }
        else
        {
            var scoutData = JsonSerializer.Deserialize<ScoutData>(ref reader, ScoutData.SerializerOptions);
            if (scoutData is not null)
            {
                scoutDataList = [_transformation.TransformScoutDataCoords(scoutData)];
            }
            else
            {
                throw new Exception("Received ODID message with no data.");
            }
        }

        return scoutDataList;
    }

    private static ReadOnlySequence<byte> DecompressPayload(ReadOnlySequence<byte> payload)
    {
        var compressedLength = (int)payload.Length;
        var compressedBuffer = ArrayPool<byte>.Shared.Rent(compressedLength);
        var decompressedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(compressedLength * 4, 4096));
        try
        {
            payload.CopyTo(compressedBuffer);
            using var memoryStream = new MemoryStream(compressedBuffer, 0, compressedLength);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            var totalRead = 0;
            while (true)
            {
                var remaining = decompressedBuffer.Length - totalRead;
                if (remaining == 0)
                {
                    // Dynamic growth: Double the buffer size if the JSON is larger than anticipated
                    var newBuffer = ArrayPool<byte>.Shared.Rent(decompressedBuffer.Length * 2);
                    Buffer.BlockCopy(decompressedBuffer, 0, newBuffer, 0, totalRead);
                    ArrayPool<byte>.Shared.Return(decompressedBuffer);
                    decompressedBuffer = newBuffer;
                    remaining = decompressedBuffer.Length - totalRead;
                }

                var read = gzipStream.Read(decompressedBuffer, totalRead, remaining);
                if (read == 0)
                    break; // Fully decompressed

                totalRead += read;
            }

            return new ReadOnlySequence<byte>(decompressedBuffer, 0, totalRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressedBuffer);
            ArrayPool<byte>.Shared.Return(decompressedBuffer);
        }
    }

    private void LogJsonMessage(byte[] message)
    {
        _loggingChannel.Writer.TryWrite(message);
    }

    public async Task ConnectAsync()
    {
        if (_client.IsConnected)
            return;

        var result = await _client.ConnectAsync(_connectOptions);
        switch (result.ResultCode)
        {
            case MqttClientConnectResultCode.Success:
                LogExtensions.LogInfo("MQTT client connected.", this);
                break;
            case MqttClientConnectResultCode.NotAuthorized:
                LogExtensions.LogError("Invalid credentials provided for MQTT client connection.", this);
                throw new InvalidOperationException("Invalid credentials provided for MQTT client connection.");
            default:
                LogExtensions.LogError("MQTT client connection failed with result code: {0}", this, result.ResultCode);
                throw new InvalidOperationException($"MQTT client connection failed with result code: {result.ResultCode}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_client.IsConnected)
        {
            _client.DisconnectAsync().GetAwaiter().GetResult();
        }

        _loggingChannel.Writer.Complete();
        _loggingTask?.GetAwaiter().GetResult();
        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
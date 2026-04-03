using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Config;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public class LogfileDronetagClient : FakeDronetagClient
{
    private readonly IScoutDataCoordTransformation _transformation;
    private readonly string _sourceLogFilePath;

    private Task? _publishTask;
    private JsonArrayWrapperStream? _stream;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public LogfileDronetagClient(IOptions<ClientAdapterOptions> options, IScoutDataCoordTransformation transformation)
    {
        _transformation = transformation;
        var adapterOptions = options.Value;
        if (string.IsNullOrEmpty(adapterOptions.ClientSourceLogFilePath))
            throw new ArgumentException("Client source log file path is not set.", nameof(options));

        _sourceLogFilePath = adapterOptions.ClientSourceLogFilePath;
        if (!File.Exists(_sourceLogFilePath))
            throw new FileNotFoundException($"Log file '{_sourceLogFilePath}' not found.");
    }

    public override async Task ConnectAsync()
    {
        _stream = new JsonArrayWrapperStream(File.OpenRead(_sourceLogFilePath));
        _publishTask = PublishMessagesAsync(_stream);
        await base.ConnectAsync();
    }

    private async Task PublishMessagesAsync(JsonArrayWrapperStream stream)
    {
        try
        {
            var messages = JsonSerializer.DeserializeAsyncEnumerable<ScoutData>(stream, ScoutData.SerializerOptions, _cancellationTokenSource.Token);

            DateTime? startTime = null;
            Stopwatch? clock = null;

            await foreach (var message in messages)
            {
                var messageTimestamp = message?.GetTimestamp();
                if (message is null || messageTimestamp is null) continue;
                if (startTime is null)
                {
                    startTime = messageTimestamp;
                    clock = Stopwatch.StartNew();
                }

                var simTimeNow = startTime.Value + clock!.Elapsed;
                var delay = messageTimestamp.Value - simTimeNow;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, _cancellationTokenSource.Token);

                message.Odid?.Location?.SetTimestamp(DateTime.UtcNow);
                _transformation.TransformScoutDataCoords(message); // message is immutable (?)
                SendMessageReceived(new ScoutDataReceivedEventArgs { Messages = [message] });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Message replay logging failed.", this);
        }
        LogExtensions.LogWarning("Message replay logging finished.", this);
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource.Cancel();
            _stream?.Dispose();
            _cancellationTokenSource.Dispose();
        }

        base.Dispose(disposing);
    }
}
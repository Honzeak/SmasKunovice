using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Config;
using SmasKunovice.Avalonia.Models.Dronetag;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public class LogfileDronetagClient : FakeDronetagClient
{
    private readonly IScoutDataCoordTransformation _transformation;
    private readonly string _sourceLogFilePath;
    private readonly bool _isBatchedData;

    private Stream _stream;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;
    private DateTime? _startTime = null;
    private Stopwatch _clock = new ();

    public LogfileDronetagClient(IOptions<ClientAdapterOptions> options, IScoutDataCoordTransformation transformation)
    {
        _transformation = transformation;
        var adapterOptions = options.Value;
        if (string.IsNullOrEmpty(adapterOptions.ClientSourceLogFilePath))
            throw new ArgumentException("Client source log file path is not set.", nameof(options));

        _sourceLogFilePath = adapterOptions.ClientSourceLogFilePath;
        if (!File.Exists(_sourceLogFilePath))
            throw new FileNotFoundException($"Log file '{_sourceLogFilePath}' not found.");
        
        _isBatchedData = adapterOptions.IsBatchedData;
    }

    public override async Task ConnectAsync()
    {
        _stream = new JsonArrayWrapperStream(File.OpenRead(_sourceLogFilePath)); // One big array of messages
        _ = PublishMessagesAsync(_stream);
        await base.ConnectAsync();
    }

    private async Task PublishMessagesAsync(Stream stream)
    {
        try
        {
            if (_isBatchedData)
            {
                var messages = JsonSerializer.DeserializeAsyncEnumerable<ScoutData[]>(stream, ScoutData.SerializerOptions, _cancellationTokenSource.Token);
                await ProcessMessagesArrays(messages);
            }
            else
            {
                var messages = JsonSerializer.DeserializeAsyncEnumerable<ScoutData>(stream, ScoutData.SerializerOptions, _cancellationTokenSource.Token);
                await ProcessMessages(messages);
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

    private async Task ProcessMessagesArrays(IAsyncEnumerable<ScoutData[]?> messages)
    {
        await foreach (var messageArray in messages)
        {
            if (messageArray is null) continue;

            List<ScoutData> validMessages = [];
            DateTime? batchTimestamp = null;
            foreach (var message in messageArray)
            {
                var messageTimestamp = message?.Odid.Location?.GetTimestamp();
                if (message is null || messageTimestamp is null) continue;

                validMessages.Add(message);
                if (batchTimestamp is null || messageTimestamp < batchTimestamp)
                    batchTimestamp = messageTimestamp;
            }

            if (validMessages.Count == 0 || batchTimestamp is null) continue;
            await ReplayMessages(validMessages, batchTimestamp.Value);
        }
    }

    private async Task ProcessMessages(IAsyncEnumerable<ScoutData?> messages)
    {
        await foreach (var message in messages)
        {
            var messageTimestamp = message?.Odid.Location?.GetTimestamp();
            if (message is null || messageTimestamp is null) continue;
            await ReplayMessages([message], messageTimestamp.Value);
        }
    }

    private async Task ReplayMessages(List<ScoutData> messages, DateTime sourceTimestamp)
    {
        if (_startTime is null)
        {
            _startTime = sourceTimestamp;
            _clock.Restart();
        }

        var simTimeNow = _startTime.Value + _clock.Elapsed;
        var delay = sourceTimestamp - simTimeNow;

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, _cancellationTokenSource.Token);

        foreach (var message in messages)
        {
            message.Odid?.Location?.SetTimestamp(DateTime.UtcNow);
            _transformation.TransformScoutDataCoords(message);
        }

        SendMessageReceived(new ScoutDataReceivedEventArgs { Messages = messages });
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            _cancellationTokenSource.Cancel();
            _stream?.Dispose();
            _cancellationTokenSource.Dispose();
        }

        base.Dispose(disposing);
    }
}
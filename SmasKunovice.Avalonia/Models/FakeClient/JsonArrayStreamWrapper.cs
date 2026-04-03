using System;
using System.IO;
using System.Threading.Tasks;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public sealed class JsonArrayWrapperStream(Stream innerStream) : Stream
{
    private int _phase = 0; // 0: '[', 1: data, 2: ']'

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_phase == 0)
        {
            buffer[offset] = (byte)'[';
            _phase = 1;
            return 1;
        }
        
        var bytesRead = innerStream.Read(buffer, offset, count);
        
        if (bytesRead == 0 && _phase == 1)
        {
            buffer[offset] = (byte)']';
            _phase = 2;
            return 1;
        }
        
        return bytesRead;
    }

    // Boilerplate: Forward all other methods (Seek, Length, Position, etc.) to innerStream
    public override bool CanRead => true;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => innerStream.Length + 2;
    public override long Position { get => innerStream.Position; set => innerStream.Position = value; }
    public override void Flush() => innerStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private async ValueTask DisposeAsyncCore()
    {
        await innerStream.DisposeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
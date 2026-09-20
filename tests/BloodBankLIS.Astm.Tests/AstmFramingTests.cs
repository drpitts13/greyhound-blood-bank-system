using BloodBankLIS.Astm.Framing;

namespace BloodBankLIS.Astm.Tests;

public class AstmFramingTests
{
    [Fact]
    public void Checksum_MatchesKnownFrame()
    {
        var checksum = AstmChecksum.Compute('1', "H|\\^&|", AstmControlChars.Etx);
        Assert.Equal(2, checksum.Length);
        Assert.True(AstmChecksum.TryValidate('1', "H|\\^&|", AstmControlChars.Etx, checksum));
    }

    [Fact]
    public void SplitAndEncode_RoundTripsMessage()
    {
        const string message = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219164429\rP|1|1171984\rL|1|N\r";
        var frames = AstmFraming.Split(message);
        Assert.NotEmpty(frames);
        Assert.True(frames[^1].IsLast);

        var buffer = new List<byte>();
        foreach (var frame in frames)
        {
            buffer.AddRange(AstmFraming.Encode(frame));
        }

        Assert.True(AstmFraming.TryExtract(buffer, out var parsed, out var error));
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Contains("H|\\^&|", parsed!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_SendsAndReceivesOverMemoryStream()
    {
        var pipe = new DuplexMemory();
        var server = new AstmSession(pipe.Server, isServer: true);
        var client = new AstmSession(pipe.Client, isServer: false);
        const string payload = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219164429\rL|1|N\r";

        var receive = server.ReceiveAsync(CancellationToken.None);
        var send = client.SendAsync(payload, CancellationToken.None);
        var sent = await send;
        var received = await receive;

        Assert.True(sent.Completed, sent.Error);
        Assert.True(received.Completed, received.Error);
        Assert.Contains("ImmuLINK", received.Message);
    }

    private sealed class DuplexMemory
    {
        public Stream Server { get; }
        public Stream Client { get; }

        public DuplexMemory()
        {
            var toServer = new ProducerConsumerStream();
            var toClient = new ProducerConsumerStream();
            Server = new CoupledStream(toServer, toClient);
            Client = new CoupledStream(toClient, toServer);
        }
    }

    private sealed class CoupledStream(ProducerConsumerStream read, ProducerConsumerStream write) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => read.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            read.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            read.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => write.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            write.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            write.WriteAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class ProducerConsumerStream : Stream
    {
        private readonly SemaphoreSlim _data = new(0);
        private readonly List<byte> _buffer = [];

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _data.WaitAsync(cancellationToken);
            lock (_buffer)
            {
                var take = Math.Min(count, _buffer.Count);
                _buffer.CopyTo(0, buffer, offset, take);
                _buffer.RemoveRange(0, take);
                if (_buffer.Count > 0)
                {
                    _data.Release();
                }

                return take;
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var rented = new byte[buffer.Length];
            var read = await ReadAsync(rented, 0, rented.Length, cancellationToken);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_thisLock())
            {
                _buffer.AddRange(buffer.AsSpan(offset, count).ToArray());
            }

            _data.Release();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.ToArray(), 0, buffer.Length);
            return ValueTask.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private object _thisLock() => _buffer;
    }
}

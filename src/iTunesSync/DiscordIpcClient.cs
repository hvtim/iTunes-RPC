using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace iTunesSync;

// Minimal client for Discord's local Rich Presence IPC protocol (named pipe,
// documented by the archived discord-rpc project). Implemented by hand instead
// of using a wrapper library because most .NET wrapper libraries hardcode
// activity.type to 0 (Playing) - we need type 2 (Listening) for "Listening to iTunes".
public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;

    private readonly string _clientId;
    private readonly int _pid = Environment.ProcessId;
    private NamedPipeClientStream? _pipe;

    public bool IsConnected => _pipe?.IsConnected == true;

    public DiscordIpcClient(string clientId)
    {
        _clientId = clientId;
    }

    public bool Connect()
    {
        if (IsConnected) return true;

        for (int i = 0; i < 10; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(200);
                _pipe = pipe;

                WriteFrame(OpHandshake, JsonSerializer.Serialize(new { v = 1, client_id = _clientId }));
                ReadFrame(); // discard the READY dispatch event

                return true;
            }
            catch
            {
                _pipe?.Dispose();
                _pipe = null;
            }
        }

        return false;
    }

    public void SetActivity(string name, string details, string state, DateTimeOffset start, DateTimeOffset? end, string largeImageKey, string largeImageText)
    {
        if (!IsConnected && !Connect()) return;

        object timestamps = end.HasValue
            ? new { start = start.ToUnixTimeSeconds(), end = end.Value.ToUnixTimeSeconds() }
            : new { start = start.ToUnixTimeSeconds() };

        var payload = new
        {
            cmd = "SET_ACTIVITY",
            args = new
            {
                pid = _pid,
                activity = new
                {
                    name,
                    type = 2, // Listening - renders as "Listening to <name>"
                    details,
                    state,
                    timestamps,
                    assets = new { large_image = largeImageKey, large_text = largeImageText }
                }
            },
            nonce = Guid.NewGuid().ToString()
        };

        TrySend(payload);
    }

    public void ClearActivity()
    {
        if (!IsConnected) return;

        var payload = new
        {
            cmd = "SET_ACTIVITY",
            args = new { pid = _pid },
            nonce = Guid.NewGuid().ToString()
        };

        TrySend(payload);
    }

    private void TrySend(object payload)
    {
        try
        {
            WriteFrame(OpFrame, JsonSerializer.Serialize(payload));
            ReadFrame();
        }
        catch
        {
            // Discord likely closed/restarted - drop the pipe so the next call reconnects.
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    private void WriteFrame(int opcode, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(body.Length).CopyTo(header, 4);

        _pipe!.Write(header, 0, header.Length);
        _pipe.Write(body, 0, body.Length);
        _pipe.Flush();
    }

    private string ReadFrame()
    {
        var header = ReadExact(8);
        int length = BitConverter.ToInt32(header, 4);
        var body = ReadExact(length);
        return Encoding.UTF8.GetString(body);
    }

    private byte[] ReadExact(int count)
    {
        var buffer = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = _pipe!.Read(buffer, read, count - read);
            if (n == 0) throw new IOException("Discord IPC pipe closed");
            read += n;
        }
        return buffer;
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _pipe = null;
    }
}

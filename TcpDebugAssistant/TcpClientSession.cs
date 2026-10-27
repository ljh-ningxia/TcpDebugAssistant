using System.Net;
using System.Net.Sockets;

namespace TcpDebugAssistant;

/// <summary>
/// 客户端模式下封装一个 TCP 连接。
/// </summary>
public sealed class TcpClientSession : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource _cts = new();

    /// <summary>连接成功建立。</summary>
    public event Action? Connected;

    /// <summary>连接断开（主动或异常）。</summary>
    public event Action<Exception?>? Disconnected;

    /// <summary>收到数据。注意回调在后台线程触发。</summary>
    public event Action<byte[]>? DataReceived;

    /// <summary>是否已连接。</summary>
    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>连接远端。</summary>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        Disconnect();
        _cts = new CancellationTokenSource();

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _stream = _client.GetStream();
            Connected?.Invoke();
            _ = ReceiveLoopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Disconnect();
            Disconnected?.Invoke(ex);
            throw;
        }
    }

    /// <summary>发送数据。</summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("未连接到服务器。");
        }

        await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var read = await _stream!.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var payload = new byte[read];
                Array.Copy(buffer, payload, read);
                DataReceived?.Invoke(payload);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke(ex);
        }
        finally
        {
            Disconnect();
            Disconnected?.Invoke(null);
        }
    }

    /// <summary>主动断开。</summary>
    public void Disconnect()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // 忽略
        }

        try
        {
            _stream?.Close();
        }
        catch
        {
            // 忽略
        }

        try
        {
            _client?.Close();
        }
        catch
        {
            // 忽略
        }

        _stream = null;
        _client = null;
    }

    /// <summary>本地终结点。</summary>
    public string LocalEndPoint => _client?.Client.LocalEndPoint?.ToString() ?? "-";

    /// <summary>远端终结点。</summary>
    public string RemoteEndPoint
    {
        get
        {
            if (_client?.Connected != true)
            {
                return "-";
            }

            var remote = _client.Client.RemoteEndPoint;
            return remote is IPEndPoint ep ? $"{ep.Address}:{ep.Port}" : remote?.ToString() ?? "-";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Disconnect();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

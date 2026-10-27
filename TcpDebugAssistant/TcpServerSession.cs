using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TcpDebugAssistant;

/// <summary>
/// 服务端模式下管理监听与多个客户端连接。
/// </summary>
public sealed class TcpServerSession : IDisposable
{
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new();
    private CancellationTokenSource _cts = new();
    private int _clientSeq;

    /// <summary>开始监听。</summary>
    public event Action? Started;

    /// <summary>停止监听。</summary>
    public event Action? Stopped;

    /// <summary>有新客户端接入。</summary>
    public event Action<ClientInfo>? ClientConnected;

    /// <summary>客户端断开。</summary>
    public event Action<ClientInfo>? ClientDisconnected;

    /// <summary>收到数据。回调在后台线程触发。</summary>
    public event Action<ClientInfo, byte[]>? DataReceived;

    /// <summary>是否正在监听。</summary>
    public bool IsListening => _listener?.Server.IsBound ?? false;

    /// <summary>当前客户端列表。</summary>
    public IReadOnlyCollection<ClientInfo> Clients => _clients.Values.ToList();

    /// <summary>开始监听指定地址与端口。</summary>
    public Task StartAsync(string listenIp, int port, CancellationToken cancellationToken = default)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var listener = new TcpListener(IPAddress.Parse(listenIp), port);
        listener.Start();
        _listener = listener;
        Started?.Invoke();

        // 接受连接循环放到后台任务，调用方 await 后即可继续（自测等流程依赖立即返回）
        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    var info = new ClientInfo($"客户端{Interlocked.Increment(ref _clientSeq)}", tcpClient);
                    _clients[info.Id] = info;
                    ClientConnected?.Invoke(info);
                    _ = ClientLoopAsync(info).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            catch (Exception)
            {
                // 监听异常时静默停止
            }
            finally
            {
                Stop();
                Stopped?.Invoke();
            }
        });

        return Task.CompletedTask;
    }

    private async Task ClientLoopAsync(ClientInfo info)
    {
        var buffer = new byte[8192];
        try
        {
            var stream = info.Client.GetStream();
            while (!_cts.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var payload = new byte[read];
                Array.Copy(buffer, payload, read);
                DataReceived?.Invoke(info, payload);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception)
        {
            // 客户端异常断开
        }
        finally
        {
            _clients.TryRemove(info.Id, out _);
            try
            {
                info.Client.Close();
            }
            catch
            {
                // 忽略
            }

            ClientDisconnected?.Invoke(info);
        }
    }

    /// <summary>发送给指定客户端。</summary>
    public async Task SendAsync(ClientInfo info, byte[] data, CancellationToken cancellationToken = default)
    {
        if (!_clients.ContainsKey(info.Id))
        {
            throw new InvalidOperationException("客户端已断开。");
        }

        var stream = info.Client.GetStream();
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>发送给所有已连接客户端。</summary>
    public async Task SendToAllAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        foreach (var info in _clients.Values)
        {
            try
            {
                await SendAsync(info, data, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 单个客户端失败不影响其他客户端
            }
        }
    }

    /// <summary>断开指定客户端。</summary>
    public void DisconnectClient(ClientInfo info)
    {
        try
        {
            info.Client.Close();
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>停止监听并断开全部客户端。</summary>
    public void Stop()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // 忽略
        }

        foreach (var info in _clients.Values)
        {
            try
            {
                info.Client.Close();
            }
            catch
            {
                // 忽略
            }
        }

        _clients.Clear();

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // 忽略
        }

        _listener = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>服务端的一个客户端连接。</summary>
public sealed class ClientInfo
{
    public ClientInfo(string name, TcpClient client)
    {
        Name = name;
        Client = client;
    }

    /// <summary>内部唯一 Id。</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>显示名称，如“客户端1”。</summary>
    public string Name { get; set; }

    /// <summary>底层 TCP 客户端。</summary>
    public TcpClient Client { get; }

    /// <summary>远端终结点。</summary>
    public string EndPoint =>
        Client.Client.RemoteEndPoint is IPEndPoint ep ? $"{ep.Address}:{ep.Port}" : "-";

    /// <summary>下拉框显示文本。</summary>
    public string DisplayText => $"{Name} [{EndPoint}]";
}

using System.Net;
using System.Net.Sockets;

namespace TcpDebugAssistant;

/// <summary>
/// 主窗体：TCP 调试助手。
/// 支持客户端 / 服务端两种模式，ASCII / HEX 收发、自动发送、日志保存等。
/// </summary>
public partial class MainForm : Form
{
    private TcpClientSession _clientSession = new();
    private TcpServerSession _serverSession = new();

    private long _sentBytes;
    private long _receivedBytes;
    private bool _pauseDisplay;

    // 顶部连接区
    private RadioButton _radClient = null!;
    private RadioButton _radServer = null!;

    private Label _lblRemoteIp = null!;
    private TextBox _txtRemoteIp = null!;
    private Label _lblRemotePort = null!;
    private TextBox _txtRemotePort = null!;
    private Button _btnConnect = null!;

    private Label _lblListenIp = null!;
    private TextBox _txtListenIp = null!;
    private Label _lblListenPort = null!;
    private TextBox _txtListenPort = null!;
    private Button _btnListen = null!;

    private Label _lblClients = null!;
    private ComboBox _cmbClients = null!;
    private Button _btnKick = null!;
    private CheckBox _chkBroadcast = null!;

    // 发送区
    private RadioButton _radSendAscii = null!;
    private RadioButton _radSendHex = null!;
    private TextBox _txtSend = null!;
    private CheckBox _chkAppendNewline = null!;
    private CheckBox _chkAutoSend = null!;
    private TextBox _txtAutoInterval = null!;
    private Label _lblMs = null!;
    private Button _btnSend = null!;
    private System.Windows.Forms.Timer _autoSendTimer = null!;

    // 接收区
    private RadioButton _radRecvAscii = null!;
    private RadioButton _radRecvHex = null!;
    private CheckBox _chkTimestamp = null!;
    private CheckBox _chkPause = null!;
    private Button _btnClear = null!;
    private Button _btnSaveLog = null!;
    private TextBox _txtLog = null!;

    // 状态栏
    private ToolStripStatusLabel _lblStatus = null!;
    private ToolStripStatusLabel _lblSent = null!;
    private ToolStripStatusLabel _lblRecv = null!;
    private ToolStripStatusLabel _lblEndpoints = null!;

    /// <summary>初始化窗体。</summary>
    public MainForm()
    {
        Text = "TCP 调试助手";
        MinimumSize = new Size(880, 620);
        Size = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.WhiteSmoke;
        Font = new Font("Microsoft YaHei UI", 9f);

        BuildConnectionGroup();
        BuildSendGroup();
        BuildReceiveGroup();
        BuildStatusStrip();

        WireEvents();
        ApplyMode();
        UpdateStats();
        UpdateStatus();
    }

    #region 界面构建

    private void BuildConnectionGroup()
    {
        var gb = new GroupBox { Text = "连接设置", Location = new Point(12, 12), Size = new Size(900, 118), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _radClient = new RadioButton { Text = "客户端模式", Location = new Point(16, 26), AutoSize = true, Checked = true };
        _radServer = new RadioButton { Text = "服务端模式", Location = new Point(16, 56), AutoSize = true };

        // 客户端行
        _lblRemoteIp = new Label { Text = "远程 IP：", Location = new Point(150, 29), AutoSize = true };
        _txtRemoteIp = new TextBox { Text = "127.0.0.1", Location = new Point(212, 25), Width = 140 };
        _lblRemotePort = new Label { Text = "端口：", Location = new Point(368, 29), AutoSize = true };
        _txtRemotePort = new TextBox { Text = "8080", Location = new Point(416, 25), Width = 70 };
        _btnConnect = new Button { Text = "连接", Location = new Point(510, 24), Width = 90, Height = 28 };

        // 服务端行
        _lblListenIp = new Label { Text = "监听 IP：", Location = new Point(150, 59), AutoSize = true };
        _txtListenIp = new TextBox { Text = "0.0.0.0", Location = new Point(212, 55), Width = 140 };
        _lblListenPort = new Label { Text = "端口：", Location = new Point(368, 59), AutoSize = true };
        _txtListenPort = new TextBox { Text = "8080", Location = new Point(416, 55), Width = 70 };
        _btnListen = new Button { Text = "开始监听", Location = new Point(510, 54), Width = 90, Height = 28 };

        // 客户端列表行
        _lblClients = new Label { Text = "客户端列表：", Location = new Point(150, 89), AutoSize = true };
        _cmbClients = new ComboBox { Location = new Point(240, 85), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
        _btnKick = new Button { Text = "断开选中", Location = new Point(494, 84), Width = 90, Height = 28 };
        _chkBroadcast = new CheckBox { Text = "广播发送（发给所有客户端）", Location = new Point(620, 26), AutoSize = true };

        gb.Controls.AddRange(new Control[]
        {
            _radClient, _radServer,
            _lblRemoteIp, _txtRemoteIp, _lblRemotePort, _txtRemotePort, _btnConnect,
            _lblListenIp, _txtListenIp, _lblListenPort, _txtListenPort, _btnListen,
            _lblClients, _cmbClients, _btnKick, _chkBroadcast
        });
        Controls.Add(gb);
    }

    private void BuildSendGroup()
    {
        var gb = new GroupBox { Text = "发送", Location = new Point(12, 140), Size = new Size(900, 150), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _txtSend = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(16, 24),
            Size = new Size(860, 66),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 10f),
            PlaceholderText = "输入要发送的内容（ASCII 文本或十六进制字节，如 41 42 43）"
        };

        _radSendAscii = new RadioButton { Text = "ASCII", Location = new Point(16, 100), AutoSize = true, Checked = true };
        _radSendHex = new RadioButton { Text = "HEX", Location = new Point(82, 100), AutoSize = true };
        _chkAppendNewline = new CheckBox { Text = "自动追加 \\r\\n", Location = new Point(150, 100), AutoSize = true };
        _chkAutoSend = new CheckBox { Text = "自动发送，间隔", Location = new Point(270, 100), AutoSize = true };
        _txtAutoInterval = new TextBox { Text = "1000", Location = new Point(380, 98), Width = 55, TextAlign = HorizontalAlignment.Center };
        _lblMs = new Label { Text = "ms", Location = new Point(440, 100), AutoSize = true };
        _btnSend = new Button { Text = "发送", Location = new Point(784, 96), Width = 92, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnSend.Click += async (_, _) => await SendOnceAsync();

        gb.Controls.AddRange(new Control[]
        {
            _txtSend, _radSendAscii, _radSendHex, _chkAppendNewline, _chkAutoSend,
            _txtAutoInterval, _lblMs, _btnSend
        });
        Controls.Add(gb);

        _autoSendTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _autoSendTimer.Tick += async (_, _) => await SendOnceAsync(silent: true);
    }

    private void BuildReceiveGroup()
    {
        var gb = new GroupBox { Text = "接收日志", Location = new Point(12, 298), Size = new Size(900, 270), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        _radRecvAscii = new RadioButton { Text = "ASCII", Location = new Point(16, 24), AutoSize = true, Checked = true };
        _radRecvHex = new RadioButton { Text = "HEX", Location = new Point(82, 24), AutoSize = true };
        _chkTimestamp = new CheckBox { Text = "显示时间戳", Location = new Point(150, 24), AutoSize = true, Checked = true };
        _chkPause = new CheckBox { Text = "暂停显示", Location = new Point(252, 24), AutoSize = true };
        _btnClear = new Button { Text = "清空", Location = new Point(660, 21), Width = 75, Height = 26, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnSaveLog = new Button { Text = "保存日志", Location = new Point(745, 21), Width = 75, Height = 26, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnClear.Click += (_, _) => _txtLog.Clear();
        _btnSaveLog.Click += (_, _) => SaveLog();

        _txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Location = new Point(16, 54),
            Size = new Size(860, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 10f),
            WordWrap = false,
            BackColor = Color.White
        };

        gb.Controls.AddRange(new Control[]
        {
            _radRecvAscii, _radRecvHex, _chkTimestamp, _chkPause, _btnClear, _btnSaveLog, _txtLog
        });
        Controls.Add(gb);
    }

    private void BuildStatusStrip()
    {
        var strip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("状态：未连接") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblEndpoints = new ToolStripStatusLabel("本地：- 远程：-") { AutoSize = false, Width = 260 };
        _lblSent = new ToolStripStatusLabel("发送：0 B");
        _lblRecv = new ToolStripStatusLabel("接收：0 B");
        strip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblEndpoints, _lblSent, _lblRecv });
        Controls.Add(strip);
    }

    #endregion

    #region 事件绑定与模式切换

    private void WireEvents()
    {
        _radClient.CheckedChanged += (_, _) => ApplyMode();
        _radServer.CheckedChanged += (_, _) => ApplyMode();

        _btnConnect.Click += async (_, _) =>
        {
            if (_clientSession.IsConnected)
            {
                _clientSession.Disconnect();
            }
            else
            {
                await ConnectClientAsync();
            }
        };

        _btnListen.Click += async (_, _) =>
        {
            if (_serverSession.IsListening)
            {
                _serverSession.Stop();
            }
            else
            {
                await StartServerAsync();
            }
        };

        _btnKick.Click += (_, _) =>
        {
            if (_cmbClients.SelectedItem is ClientInfo info)
            {
                _serverSession.DisconnectClient(info);
            }
        };

        _chkAutoSend.CheckedChanged += (_, _) => ApplyAutoSend();

        _chkPause.CheckedChanged += (_, _) => _pauseDisplay = _chkPause.Checked;

        _clientSession.Connected += () => OnUiThread(() =>
        {
            UpdateStatus();
            AppendLog("系统", $"已连接服务器  本地：{_clientSession.LocalEndPoint}  远程：{_clientSession.RemoteEndPoint}");
            _btnConnect.Text = "断开";
            _btnConnect.Enabled = true;
        });

        _clientSession.Disconnected += ex => OnUiThread(() =>
        {
            UpdateStatus();
            _btnConnect.Text = "连接";
            _btnConnect.Enabled = true;
            AppendLog("系统", ex is null ? "连接已断开" : $"连接断开：{ex.Message}");
        });

        _clientSession.DataReceived += data => OnUiThread(() =>
        {
            _receivedBytes += data.Length;
            UpdateStats();
            AppendPacket("接收", data);
        });

        _serverSession.Started += () => OnUiThread(() =>
        {
            UpdateStatus();
            _btnListen.Text = "停止监听";
            AppendLog("系统", $"服务端已开始监听 {_txtListenIp.Text}:{_txtListenPort.Text}");
        });

        _serverSession.Stopped += () => OnUiThread(() =>
        {
            UpdateStatus();
            _btnListen.Text = "开始监听";
            _cmbClients.Items.Clear();
            AppendLog("系统", "服务端已停止监听");
        });

        _serverSession.ClientConnected += info => OnUiThread(() =>
        {
            _cmbClients.Items.Add(info);
            if (_cmbClients.SelectedItem is null)
            {
                _cmbClients.SelectedItem = info;
            }

            AppendLog("系统", $"{info.Name} 已接入（{info.EndPoint}），当前连接数：{_serverSession.Clients.Count}");
        });

        _serverSession.ClientDisconnected += info => OnUiThread(() =>
        {
            _cmbClients.Items.Remove(info);
            AppendLog("系统", $"{info.Name} 已断开（{info.EndPoint}），当前连接数：{_serverSession.Clients.Count}");
        });

        _serverSession.DataReceived += (info, data) => OnUiThread(() =>
        {
            _receivedBytes += data.Length;
            UpdateStats();
            AppendPacket($"接收[{info.Name}]", data);
        });

        FormClosing += (_, _) =>
        {
            _autoSendTimer.Stop();
            _clientSession.Dispose();
            _serverSession.Dispose();
        };
    }

    private void ApplyMode()
    {
        var clientMode = _radClient.Checked;

        _lblRemoteIp.Visible = clientMode;
        _txtRemoteIp.Visible = clientMode;
        _lblRemotePort.Visible = clientMode;
        _txtRemotePort.Visible = clientMode;
        _btnConnect.Visible = clientMode;

        _lblListenIp.Visible = !clientMode;
        _txtListenIp.Visible = !clientMode;
        _lblListenPort.Visible = !clientMode;
        _txtListenPort.Visible = !clientMode;
        _btnListen.Visible = !clientMode;
        _lblClients.Visible = !clientMode;
        _cmbClients.Visible = !clientMode;
        _btnKick.Visible = !clientMode;
        _chkBroadcast.Visible = !clientMode;

        // 切换模式时自动断开当前会话，避免状态混乱
        if (clientMode)
        {
            if (_serverSession.IsListening)
            {
                _serverSession.Stop();
            }
        }
        else
        {
            if (_clientSession.IsConnected)
            {
                _clientSession.Disconnect();
            }
        }

        UpdateStatus();
    }

    private void ApplyAutoSend()
    {
        if (_chkAutoSend.Checked)
        {
            if (!int.TryParse(_txtAutoInterval.Text, out var ms) || ms < 50 || ms > 600000)
            {
                MessageBox.Show(this, "自动发送间隔需为 50 ~ 600000 之间的毫秒数。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _chkAutoSend.Checked = false;
                return;
            }

            _autoSendTimer.Interval = ms;
            _autoSendTimer.Start();
        }
        else
        {
            _autoSendTimer.Stop();
        }
    }

    #endregion

    #region 连接 / 收发逻辑

    private async Task ConnectClientAsync()
    {
        var host = _txtRemoteIp.Text.Trim();
        if (!int.TryParse(_txtRemotePort.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(this, "端口需为 1 ~ 65535 之间的整数。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnConnect.Enabled = false;
        AppendLog("系统", $"正在连接 {host}:{port} ...");
        try
        {
            await _clientSession.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            _btnConnect.Enabled = true;
            AppendLog("系统", $"连接失败：{ex.Message}");
        }
    }

    private async Task StartServerAsync()
    {
        var ipText = _txtListenIp.Text.Trim();
        if (!int.TryParse(_txtListenPort.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(this, "端口需为 1 ~ 65535 之间的整数。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!IPAddress.TryParse(ipText, out _))
        {
            MessageBox.Show(this, "监听 IP 格式不正确，可使用 0.0.0.0 监听所有网卡。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _serverSession.StartAsync(ipText, port);
        }
        catch (Exception ex)
        {
            AppendLog("系统", $"监听失败：{ex.Message}");
        }
    }

    private async Task SendOnceAsync(bool silent = false)
    {
        var format = _radSendHex.Checked ? DisplayFormat.Hex : DisplayFormat.Ascii;
        if (!DataConvert.TryParseInput(_txtSend.Text, format, out var payload) || payload.Length == 0)
        {
            if (!silent)
            {
                MessageBox.Show(this, format == DisplayFormat.Hex
                    ? "HEX 内容无效：需为偶数个十六进制字符（可带空格分隔）。"
                    : "请输入要发送的内容。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (_chkAppendNewline.Checked)
        {
            var withCrlf = new byte[payload.Length + 2];
            Array.Copy(payload, withCrlf, payload.Length);
            withCrlf[^2] = 0x0D;
            withCrlf[^1] = 0x0A;
            payload = withCrlf;
        }

        try
        {
            if (_radClient.Checked)
            {
                if (!_clientSession.IsConnected)
                {
                    if (!silent)
                    {
                        MessageBox.Show(this, "尚未连接到服务器。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    return;
                }

                await _clientSession.SendAsync(payload);
            }
            else
            {
                if (_serverSession.Clients.Count == 0)
                {
                    if (!silent)
                    {
                        MessageBox.Show(this, "当前没有已连接的客户端。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    return;
                }

                if (_chkBroadcast.Checked)
                {
                    await _serverSession.SendToAllAsync(payload);
                }
                else if (_cmbClients.SelectedItem is ClientInfo info)
                {
                    await _serverSession.SendAsync(info, payload);
                }
                else
                {
                    if (!silent)
                    {
                        MessageBox.Show(this, "请先在客户端列表中选择一个客户端，或勾选广播发送。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    return;
                }
            }

            _sentBytes += payload.Length;
            UpdateStats();
            AppendPacket("发送", payload);
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(this, $"发送失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #endregion

    #region 日志与状态

    private void AppendPacket(string direction, byte[] data)
    {
        var format = _radRecvHex.Checked ? DisplayFormat.Hex : DisplayFormat.Ascii;
        var content = format == DisplayFormat.Hex
            ? DataConvert.BytesToHex(data)
            : DataConvert.BytesToAscii(data);

        AppendLog(direction, content);
    }

    private void AppendLog(string direction, string content)
    {
        if (_pauseDisplay)
        {
            return;
        }

        var prefix = _chkTimestamp.Checked ? $"[{DateTime.Now:HH:mm:ss.fff}] " : string.Empty;
        var line = $"{prefix}[{direction}] {content}{Environment.NewLine}";
        _txtLog.AppendText(line);
    }

    private void SaveLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"tcp-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, _txtLog.Text);
            AppendLog("系统", $"日志已保存：{dialog.FileName}");
        }
    }

    private void UpdateStats()
    {
        _lblSent.Text = $"发送：{FormatBytes(_sentBytes)}";
        _lblRecv.Text = $"接收：{FormatBytes(_receivedBytes)}";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / 1024.0 / 1024.0:F2} MB"
    };

    private void UpdateStatus()
    {
        if (_radClient.Checked)
        {
            _lblStatus.Text = _clientSession.IsConnected ? "状态：客户端已连接" : "状态：客户端未连接";
            _lblEndpoints.Text = $"本地：{_clientSession.LocalEndPoint}  远程：{_clientSession.RemoteEndPoint}";
        }
        else
        {
            _lblStatus.Text = _serverSession.IsListening
                ? $"状态：服务端监听中（{_serverSession.Clients.Count} 个客户端）"
                : "状态：服务端未监听";
            _lblEndpoints.Text = $"监听：{_txtListenIp.Text}:{_txtListenPort.Text}";
        }
    }

    private void OnUiThread(Action action)
    {
        if (IsHandleCreated && InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // 窗体已关闭
            }
        }
        else
        {
            action();
        }
    }

    #endregion
}

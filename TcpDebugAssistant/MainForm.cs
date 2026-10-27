using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpDebugAssistant;

/// <summary>
/// 主窗体：TCP 调试助手。
/// 支持客户端 / 服务端两种模式，ASCII / HEX 收发、自动发送、日志保存等。
/// </summary>
public partial class MainForm : Form
{
    // 主题色
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);      // 蓝（客户端）
    private static readonly Color ServerAccent = Color.FromArgb(5, 150, 105); // 绿（服务端）
    private static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);     // 深 slate
    private static readonly Color FormBg = Color.FromArgb(241, 245, 249);    // 浅 slate
    private static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
    private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
    private static readonly Color BorderColor = Color.FromArgb(226, 232, 240);
    private static readonly Color SuccessColor = Color.FromArgb(22, 163, 74);
    private static readonly Color DangerColor = Color.FromArgb(220, 38, 38);
    private static readonly Color DisabledGray = Color.FromArgb(148, 163, 184);

    private static readonly Font UiFont = new("Microsoft YaHei UI", 9.5f);
    private static readonly Font InputFont = new("Consolas", 11f);
    private static readonly Font LogFont = new("Consolas", 10.5f);

    private TcpClientSession _clientSession = new();
    private TcpServerSession _serverSession = new();
    private int _serverListenPort = 8080;

    private long _sentBytes;
    private long _receivedBytes;
    private bool _pauseDisplay;

    // 顶部模式切换
    private RadioButton _radClient = null!;
    private RadioButton _radServer = null!;

    // 连接区
    private Label _lblConnCardTitle = null!;
    private Label _lblRemoteIp = null!;
    private TextBox _txtRemoteIp = null!;
    private Label _lblRemotePort = null!;
    private TextBox _txtRemotePort = null!;
    private Button _btnConnect = null!;
    private Label _lblTargetHint = null!;

    private Label _lblListenIp = null!;
    private TextBox _txtListenIp = null!;
    private Label _lblListenPort = null!;
    private TextBox _txtListenPort = null!;
    private Button _btnListen = null!;

    private Label _lblClients = null!;
    private ComboBox _cmbClients = null!;
    private Button _btnKick = null!;
    private CheckBox _chkBroadcast = null!;
    private Button _btnSelfTest = null!;

    // 发送区
    private RadioButton _radSendAscii = null!;
    private RadioButton _radSendHex = null!;
    private Label _lblSendEncoding = null!;
    private ComboBox _cmbSendEncoding = null!;
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
        MinimumSize = new Size(980, 780);
        Size = new Size(1040, 860);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = FormBg;
        Font = UiFont;
        ForeColor = TextPrimary;
        DoubleBuffered = true;

        BuildHeader();
        BuildConnectionCard();
        BuildSendCard();
        BuildReceiveCard();
        BuildStatusStrip();

        WireEvents();
        ApplyMode();
        StyleFormatToggle(_radSendAscii, true);
        StyleFormatToggle(_radSendHex, false);
        StyleFormatToggle(_radRecvAscii, true);
        StyleFormatToggle(_radRecvHex, false);
        Load += (_, _) => EnsureOnScreen();
        UpdateStats();
        UpdateStatus();
    }

    /// <summary>
    /// 确保窗口打开时正常显示在桌面可见区域内：
    /// 处于最小化则恢复正常；落在所有屏幕之外则回到主屏中心。
    /// </summary>
    private void EnsureOnScreen()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        var intersects = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(Bounds));
        if (!intersects)
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(
                area.Left + Math.Max(0, (area.Width - Width) / 2),
                area.Top + Math.Max(0, (area.Height - Height) / 2));
        }
    }

    #region 界面构建

    private void BuildHeader()
    {
        // 注意：顶栏面板停靠前默认宽度只有 200px，子控件若用右锚定会以 200px 为基准、
        // 停靠拉伸后被推到屏幕外。因此这里用固定定位（不加 Anchor）。
        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = HeaderBg };

        var title = new Label
        {
            Text = "TCP 调试助手",
            Font = new(UiFont.FontFamily, 15f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 10),
            AutoSize = true
        };
        var subtitle = new Label
        {
            Text = "TCP 客户端 / 服务端联调工具",
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(21, 38),
            AutoSize = true
        };
        var modeHint = new Label
        {
            Text = "工作模式 →",
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(590, 23),
            AutoSize = true
        };

        _radClient = new RadioButton
        {
            Text = "客户端模式",
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(130, 40),
            Location = new Point(672, 12),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Checked = true
        };
        _radServer = new RadioButton
        {
            Text = "服务端模式",
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(130, 40),
            Location = new Point(810, 12),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };

        header.Controls.AddRange(new Control[] { title, subtitle, modeHint, _radClient, _radServer });
        Controls.Add(header);
    }

    private void BuildConnectionCard()
    {
        var card = new CardPanel { Location = new Point(12, 76), Size = new Size(976, 164), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _lblConnCardTitle = NewCardTitle("连接设置", new Point(28, 12));

        // 客户端行
        _lblRemoteIp = new Label { Text = "远程 IP", Location = new Point(28, 52), AutoSize = true, ForeColor = TextSecondary };
        _txtRemoteIp = new TextBox { Text = "127.0.0.1", Location = new Point(100, 46), Width = 260, Height = 30, Font = InputFont, BorderStyle = BorderStyle.FixedSingle };
        _lblRemotePort = new Label { Text = "端口", Location = new Point(382, 52), AutoSize = true, ForeColor = TextSecondary };
        _txtRemotePort = new TextBox { Text = "8080", Location = new Point(432, 46), Width = 100, Height = 30, Font = InputFont, TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle };
        _btnConnect = new Button { Text = "连 接", Location = new Point(556, 44), Size = new Size(116, 34) };
        StylePrimaryButton(_btnConnect, Accent);
        _lblTargetHint = new Label { Location = new Point(690, 52), Width = 250, ForeColor = TextSecondary, Font = new(UiFont.FontFamily, 8.5f), TextAlign = ContentAlignment.MiddleRight };

        // 服务端行
        _lblListenIp = new Label { Text = "监听 IP", Location = new Point(28, 92), AutoSize = true, ForeColor = TextSecondary };
        _txtListenIp = new TextBox { Text = "0.0.0.0", Location = new Point(100, 86), Width = 260, Height = 30, Font = InputFont, BorderStyle = BorderStyle.FixedSingle };
        _lblListenPort = new Label { Text = "端口", Location = new Point(382, 92), AutoSize = true, ForeColor = TextSecondary };
        _txtListenPort = new TextBox { Text = "8080", Location = new Point(432, 86), Width = 100, Height = 30, Font = InputFont, TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle };
        _btnListen = new Button { Text = "开始监听", Location = new Point(556, 84), Size = new Size(116, 34) };
        StylePrimaryButton(_btnListen, ServerAccent);

        // 客户端列表行
        _lblClients = new Label { Text = "已接客户端", Location = new Point(28, 130), AutoSize = true, ForeColor = TextSecondary };
        _cmbClients = new ComboBox { Location = new Point(120, 126), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = InputFont };
        _btnKick = new Button { Text = "断开选中", Location = new Point(400, 124), Size = new Size(104, 32) };
        StyleSecondaryButton(_btnKick, DangerColor);
        _chkBroadcast = new CheckBox { Text = "广播发送（发给所有客户端）", Location = new Point(524, 129), AutoSize = true, ForeColor = TextSecondary };

        _btnSelfTest = new Button { Text = "⚡ 一键自测", Location = new Point(820, 10), Size = new Size(132, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        StyleSecondaryButton(_btnSelfTest, Accent);

        card.Controls.AddRange(new Control[]
        {
            _lblConnCardTitle,
            _lblRemoteIp, _txtRemoteIp, _lblRemotePort, _txtRemotePort, _btnConnect, _lblTargetHint,
            _lblListenIp, _txtListenIp, _lblListenPort, _txtListenPort, _btnListen,
            _lblClients, _cmbClients, _btnKick, _chkBroadcast, _btnSelfTest
        });
        Controls.Add(card);
    }

    private void BuildSendCard()
    {
        var card = new CardPanel { Location = new Point(12, 248), Size = new Size(976, 184), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var cardTitle = NewCardTitle("发送", new Point(28, 12));

        _radSendAscii = new RadioButton { Text = "ASCII", Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Size = new Size(64, 30), Location = new Point(28, 44), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Checked = true };
        _radSendHex = new RadioButton { Text = "HEX", Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Size = new Size(64, 30), Location = new Point(96, 44), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };

        _lblSendEncoding = new Label { Text = "编码", Location = new Point(174, 49), AutoSize = true, ForeColor = TextSecondary };
        _cmbSendEncoding = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(210, 45), Size = new Size(76, 28), Font = InputFont, FlatStyle = FlatStyle.Flat };
        _cmbSendEncoding.Items.AddRange(new object[] { "UTF-8", "GBK" });
        _cmbSendEncoding.SelectedIndex = 0;

        _chkAppendNewline = new CheckBox { Text = "自动追加 \\r\\n", Location = new Point(302, 49), AutoSize = true, ForeColor = TextSecondary };
        _chkAutoSend = new CheckBox { Text = "自动发送，间隔", Location = new Point(404, 49), AutoSize = true, ForeColor = TextSecondary };
        _txtAutoInterval = new TextBox { Text = "1000", Location = new Point(516, 45), Width = 60, Height = 28, Font = InputFont, TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle };
        _lblMs = new Label { Text = "ms", Location = new Point(582, 49), AutoSize = true, ForeColor = TextSecondary };

        _btnSend = new Button { Text = "发 送", Location = new Point(856, 40), Size = new Size(116, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        StylePrimaryButton(_btnSend, Accent);

        _txtSend = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(28, 86),
            Size = new Size(920, 82),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = InputFont,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            PlaceholderText = "输入要发送的内容（ASCII 文本可选 UTF-8 / GBK 编码，或 HEX 字节如 41 42 43）"
        };

        card.Controls.AddRange(new Control[]
        {
            cardTitle, _radSendAscii, _radSendHex, _lblSendEncoding, _cmbSendEncoding,
            _chkAppendNewline, _chkAutoSend, _txtAutoInterval, _lblMs, _btnSend, _txtSend
        });
        Controls.Add(card);

        _autoSendTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _autoSendTimer.Tick += async (_, _) => await SendOnceAsync(silent: true);

        // HEX 模式下编码选择无意义
        _radSendAscii.CheckedChanged += (_, _) => _cmbSendEncoding.Enabled = _radSendAscii.Checked;
        _radSendHex.CheckedChanged += (_, _) => _cmbSendEncoding.Enabled = _radSendAscii.Checked;
    }

    private void BuildReceiveCard()
    {
        var card = new CardPanel
        {
            Location = new Point(12, 444),
            Size = new Size(976, 280),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var cardTitle = NewCardTitle("接收日志", new Point(28, 12));

        _radRecvAscii = new RadioButton { Text = "ASCII", Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Size = new Size(64, 30), Location = new Point(28, 44), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Checked = true };
        _radRecvHex = new RadioButton { Text = "HEX", Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Size = new Size(64, 30), Location = new Point(96, 44), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };

        _chkTimestamp = new CheckBox { Text = "显示时间戳", Location = new Point(180, 49), AutoSize = true, Checked = true, ForeColor = TextSecondary };
        _chkPause = new CheckBox { Text = "暂停显示", Location = new Point(282, 49), AutoSize = true, ForeColor = TextSecondary };

        _btnClear = new Button { Text = "清空", Location = new Point(740, 44), Size = new Size(96, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        StyleSecondaryButton(_btnClear, TextPrimary);
        _btnSaveLog = new Button { Text = "保存日志", Location = new Point(848, 44), Size = new Size(96, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        StyleSecondaryButton(_btnSaveLog, TextPrimary);

        _txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Location = new Point(28, 86),
            Size = new Size(920, 170),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font = LogFont,
            WordWrap = false,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        card.Controls.AddRange(new Control[]
        {
            cardTitle, _radRecvAscii, _radRecvHex, _chkTimestamp, _chkPause, _btnClear, _btnSaveLog, _txtLog
        });
        Controls.Add(card);
    }

    private void BuildStatusStrip()
    {
        var strip = new StatusStrip { BackColor = Color.White };
        _lblStatus = new ToolStripStatusLabel("● 未连接") { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextSecondary };
        _lblEndpoints = new ToolStripStatusLabel("本地：-  远程：-") { AutoSize = false, Width = 300, TextAlign = ContentAlignment.MiddleLeft };
        _lblSent = new ToolStripStatusLabel("发送：0 B");
        _lblRecv = new ToolStripStatusLabel("接收：0 B");
        strip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblEndpoints, _lblSent, _lblRecv });
        Controls.Add(strip);
    }

    #endregion

    #region 样式辅助

    private static Label NewCardTitle(string text, Point location) => new()
    {
        Text = text,
        Font = new(UiFont.FontFamily, 11f, FontStyle.Bold),
        ForeColor = TextPrimary,
        Location = location,
        AutoSize = true
    };

    private static void StylePrimaryButton(Button b, Color color)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = color;
        b.ForeColor = Color.White;
        b.FlatAppearance.BorderSize = 0;
        b.Cursor = Cursors.Hand;
        b.Font = new(UiFont, FontStyle.Bold);
        b.EnabledChanged += (_, _) => b.BackColor = b.Enabled ? color : DisabledGray;
    }

    private static void StyleSecondaryButton(Button b, Color textColor)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Color.White;
        b.ForeColor = textColor;
        b.FlatAppearance.BorderColor = BorderColor;
        b.FlatAppearance.BorderSize = 1;
        b.Cursor = Cursors.Hand;
        b.EnabledChanged += (_, _) =>
        {
            b.ForeColor = b.Enabled ? textColor : DisabledGray;
            b.FlatAppearance.BorderColor = b.Enabled ? BorderColor : Color.FromArgb(241, 245, 249);
        };
    }

    private void StyleFormatToggle(RadioButton rad, bool checkedState)
    {
        rad.FlatAppearance.BorderSize = 1;
        rad.FlatAppearance.BorderColor = BorderColor;
        rad.FlatAppearance.CheckedBackColor = Accent;
        rad.FlatAppearance.MouseOverBackColor = checkedState ? Accent : Color.FromArgb(248, 250, 252);
        rad.BackColor = checkedState ? Accent : Color.White;
        rad.ForeColor = checkedState ? Color.White : TextSecondary;
        rad.Font = new(UiFont, FontStyle.Bold);
    }

    private void StyleModeToggle(RadioButton rad, bool checkedState, Color accentColor)
    {
        rad.FlatAppearance.BorderSize = checkedState ? 0 : 1;
        rad.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
        rad.FlatAppearance.CheckedBackColor = accentColor;
        rad.FlatAppearance.MouseOverBackColor = checkedState ? accentColor : Color.FromArgb(30, 41, 59);
        rad.BackColor = checkedState ? accentColor : Color.FromArgb(30, 41, 59);
        rad.ForeColor = Color.White;
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

        _btnSelfTest.Click += async (_, _) => await RunSelfTestAsync();

        _btnSend.Click += async (_, _) => await SendOnceAsync();

        _chkAutoSend.CheckedChanged += (_, _) => ApplyAutoSend();

        _chkPause.CheckedChanged += (_, _) => _pauseDisplay = _chkPause.Checked;

        _radSendAscii.CheckedChanged += (_, _) => StyleFormatToggle(_radSendAscii, _radSendAscii.Checked);
        _radSendHex.CheckedChanged += (_, _) => StyleFormatToggle(_radSendHex, _radSendHex.Checked);
        _radRecvAscii.CheckedChanged += (_, _) => StyleFormatToggle(_radRecvAscii, _radRecvAscii.Checked);
        _radRecvHex.CheckedChanged += (_, _) => StyleFormatToggle(_radRecvHex, _radRecvHex.Checked);

        _btnClear.Click += (_, _) => _txtLog.Clear();
        _btnSaveLog.Click += (_, _) => SaveLog();

        _clientSession.Connected += () => OnUiThread(() =>
        {
            UpdateStatus();
            AppendLog("系统", $"已连接服务器  本地：{_clientSession.LocalEndPoint}  远程：{_clientSession.RemoteEndPoint}");
            _btnConnect.Text = "断 开";
            _btnConnect.Enabled = true;
        });

        _clientSession.Disconnected += ex => OnUiThread(() =>
        {
            UpdateStatus();
            _btnConnect.Text = "连 接";
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

        StyleModeToggle(_radClient, clientMode, Accent);
        StyleModeToggle(_radServer, !clientMode, ServerAccent);
        _lblConnCardTitle.Text = clientMode ? "连接设置 · 客户端模式" : "连接设置 · 服务端模式";
        _lblConnCardTitle.ForeColor = clientMode ? Accent : ServerAccent;

        _lblRemoteIp.Visible = clientMode;
        _txtRemoteIp.Visible = clientMode;
        _lblRemotePort.Visible = clientMode;
        _txtRemotePort.Visible = clientMode;
        _btnConnect.Visible = clientMode;
        _lblTargetHint.Visible = clientMode;

        _lblListenIp.Visible = !clientMode;
        _txtListenIp.Visible = !clientMode;
        _lblListenPort.Visible = !clientMode;
        _txtListenPort.Visible = !clientMode;
        _btnListen.Visible = !clientMode;
        _lblClients.Visible = !clientMode;
        _cmbClients.Visible = !clientMode;
        _btnKick.Visible = !clientMode;
        _chkBroadcast.Visible = !clientMode;

        // 注意：切换模式时不再自动停止另一个会话，
        // 这样可以在同一窗口内自测（服务端保持监听，切到客户端连 127.0.0.1 验证）。
        // 需要断开时请点击“断开”/“停止监听”按钮，或关闭程序。

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
            AppendLog("提示", "请检查：① IP 是否写对（本机回环是 127.0.0.1）② 目标程序是否已启动并在该端口监听 ③ 防火墙是否拦截。可点右上角“一键自测”验证软件本身。");
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

        _serverListenPort = port;
        try
        {
            await _serverSession.StartAsync(ipText, port);
        }
        catch (Exception ex)
        {
            AppendLog("系统", $"监听失败：{ex.Message}");
            AppendLog("提示", "该端口可能已被其他程序占用，可换一个端口再试。");
        }
    }

    private async Task SendOnceAsync(bool silent = false)
    {
        byte[] payload;
        if (_radSendHex.Checked)
        {
            if (!DataConvert.TryParseHex(_txtSend.Text, out payload) || payload.Length == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "HEX 内容无效：需为偶数个十六进制字符（可带空格分隔）。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }
        }
        else
        {
            var gbk = _cmbSendEncoding.SelectedItem is "GBK";
            payload = DataConvert.EncodeText(_txtSend.Text, gbk);
            if (payload.Length == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "请输入要发送的内容。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }
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
                        MessageBox.Show(this, "尚未连接到服务器，请先点击“连接”。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    /// <summary>
    /// 本地回环自测：自动在 127.0.0.1 起监听、客户端连接、收发 PING/PONG，
    /// 用于验证软件本身的收发功能是否正常。
    /// </summary>
    private async Task RunSelfTestAsync()
    {
        _btnSelfTest.Enabled = false;
        var startedHere = false;
        TcpClientSession? testClient = null;
        Action<ClientInfo, byte[]>? replyHandler = null;

        try
        {
            // 1. 确保本机有监听（优先复用已有服务端）
            var port = _serverSession.IsListening
                ? _serverListenPort
                : (int.TryParse(_txtListenPort.Text, out var p) && p is >= 1 and <= 65535 ? p : 8080);

            if (!_serverSession.IsListening)
            {
                try
                {
                    await _serverSession.StartAsync("127.0.0.1", port);
                    _serverListenPort = port;
                    startedHere = true;
                }
                catch (Exception ex)
                {
                    AppendLog("自测", $"无法在本机 127.0.0.1:{port} 启动监听：{ex.Message}");
                    MessageBox.Show(this, $"自测失败：无法在 127.0.0.1:{port} 启动监听。\n{ex.Message}\n\n该端口可能被其他程序占用。", "自测失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // 2. 切到客户端模式并连接本机
            _radClient.Checked = true;
            _txtRemoteIp.Text = "127.0.0.1";
            _txtRemotePort.Text = port.ToString();

            testClient = new TcpClientSession();
            var gotPong = new TaskCompletionSource<bool>();

            testClient.DataReceived += d =>
            {
                if (DataConvert.BytesToAscii(d).Contains("PONG", StringComparison.OrdinalIgnoreCase))
                {
                    gotPong.TrySetResult(true);
                }
            };

            replyHandler = (info, d) =>
            {
                if (DataConvert.BytesToAscii(d).Contains("PING", StringComparison.OrdinalIgnoreCase))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _serverSession.SendAsync(info, Encoding.ASCII.GetBytes("PONG"));
                        }
                        catch
                        {
                            // 客户端可能已断开
                        }
                    });
                }
            };
            _serverSession.DataReceived += replyHandler;

            try
            {
                await testClient.ConnectAsync("127.0.0.1", port);
                AppendLog("自测", $"已连接本机 127.0.0.1:{port}，正在发送测试数据 ...");
                await testClient.SendAsync(Encoding.ASCII.GetBytes("PING"));

                var ok = await gotPong.Task.WaitAsync(TimeSpan.FromSeconds(3));
                if (ok)
                {
                    AppendLog("自测", "收到服务端回应 PONG —— 程序收发功能完全正常！");
                    MessageBox.Show(this,
                        "自测通过：本机回环连接、发送、接收均正常。\n\n这说明软件本身没有问题。\n如果连接外部目标失败，请检查目标 IP/端口是否正确、\n目标程序是否已启动并监听、网络是否互通。",
                        "自测通过", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppendLog("自测", "连接成功，但 3 秒内未收到回应");
                    MessageBox.Show(this, "自测未完全通过：可以连接本机，但未收到回应数据。", "自测异常", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog("自测", $"连接失败：{ex.Message}");
                MessageBox.Show(this, $"自测失败：{ex.Message}\n\n本机回环都无法连接时，可能是防火墙拦截或端口被占用。", "自测失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (replyHandler is not null)
                {
                    _serverSession.DataReceived -= replyHandler;
                }

                testClient?.Dispose();

                // 只有自测自己启动的监听才关掉，用户原有的监听保持不变
                if (startedHere)
                {
                    _serverSession.Stop();
                }

                UpdateStatus();
            }
        }
        finally
        {
            _btnSelfTest.Enabled = true;
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
            var serverHint = _serverSession.IsListening ? "  （本程序服务端仍在监听，可切回服务端模式查看）" : string.Empty;
            _lblStatus.Text = (_clientSession.IsConnected ? "● 客户端已连接" : "● 客户端未连接") + serverHint;
            _lblStatus.ForeColor = _clientSession.IsConnected ? SuccessColor : TextSecondary;
            _lblEndpoints.Text = $"本地：{_clientSession.LocalEndPoint}  远程：{_clientSession.RemoteEndPoint}";
            _lblTargetHint.Text = $"当前目标：{_txtRemoteIp.Text}:{_txtRemotePort.Text}";
        }
        else
        {
            _lblStatus.Text = _serverSession.IsListening
                ? $"● 服务端监听中（{_serverSession.Clients.Count} 个客户端）"
                : "● 服务端未监听";
            _lblStatus.ForeColor = _serverSession.IsListening ? SuccessColor : TextSecondary;
            _lblEndpoints.Text = $"监听地址：{_txtListenIp.Text}:{_txtListenPort.Text}";
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

/// <summary>
/// 圆角卡片面板，用于承载各个功能区。
/// </summary>
internal sealed class CardPanel : Panel
{
    private static readonly Color CardBorder = Color.FromArgb(226, 232, 240);

    public CardPanel()
    {
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        const int radius = 10;
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(CardBorder, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

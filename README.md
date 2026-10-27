# TCP 调试助手（TcpDebugAssistant）

一个基于 C# / .NET 8 + WinForms 的 TCP 调试工具，支持 **客户端** 与 **服务端** 两种模式，适用于串口转以太网、物联网设备、自研 TCP 协议等场景的联调测试。

## 下载即用（推荐）

不想装开发环境？直接下载打好的 Windows 单文件版，解压后双击 `TcpDebugAssistant.exe` 即可运行，无需安装 .NET：

👉 **[Releases 页面下载 TcpDebugAssistant.exe](https://github.com/ljh-ningxia/TcpDebugAssistant/releases)**

- 系统要求：Windows 10/11（x64）
- 文件约 160 MB（自包含单文件，含 .NET 运行时）

## 功能特性

### 连接
- **客户端模式**：填写远程 IP / 端口，一键连接 / 断开
- **服务端模式**：监听指定 IP（支持 `0.0.0.0` 监听全部网卡），实时显示已接入客户端列表，可断开指定客户端
- 状态栏实时显示本地 / 远端终结点、连接状态、收发字节数

### 收发
- 发送内容支持 **ASCII** 与 **HEX** 两种格式，HEX 可带空格 / `0x` 前缀混排输入
- 发送编码可选 **UTF-8 / GBK**（GBK 用于只认国标码的国产设备）
- 接收显示**自动识别 UTF-8 / GBK**，中文内容直接可读，不会变成一串点
- 可勾选**自动追加 `\r\n`**
- **自动发送**：按设定间隔循环发送（50ms ~ 10min）
- 服务端模式支持**广播发送**（发给所有客户端）
- 接收日志支持 ASCII / HEX 切换、时间戳、暂停显示、一键清空、保存为文本文件

## 环境要求

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 编译与运行

```bash
cd TcpDebugAssistant
dotnet run --project TcpDebugAssistant
```

或发布为独立 exe：

```bash
dotnet publish TcpDebugAssistant -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## 使用说明

1. **作为客户端测试服务端**：选择「客户端模式」，输入目标 IP 与端口，点击连接，即可发送 / 查看返回数据。
2. **作为服务端测试客户端**：选择「服务端模式」，设置监听端口（IP 填 `0.0.0.0` 即可），点击开始监听；客户端接入后可在列表中选择目标单独发送，或勾选「广播发送」群发。

## 项目结构

```
TcpDebugAssistant/
├── TcpDebugAssistant.sln
├── .gitignore
├── README.md
└── TcpDebugAssistant/
    ├── Program.cs             # 程序入口
    ├── MainForm.cs            # 主窗体：界面与交互逻辑
    ├── TcpClientSession.cs    # 客户端模式连接封装
    ├── TcpServerSession.cs    # 服务端模式监听与多客户端管理
    ├── DataConvert.cs         # ASCII / HEX 转换与解析
    └── TcpDebugAssistant.csproj
```

## 技术要点

- 全异步 Socket 模型（`ReadAsync` / `WriteAsync`），UI 线程不阻塞
- 后台线程回调通过 `BeginInvoke` 安全切回 UI 线程
- `CancellationTokenSource` 管理连接生命周期，关闭窗体时自动释放资源

## License

MIT

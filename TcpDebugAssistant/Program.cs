using System.Text;

namespace TcpDebugAssistant;

internal static class Program
{
    /// <summary>
    /// 应用程序入口。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // 注册代码页编码提供器，使 GBK（936）等国标编码可用
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

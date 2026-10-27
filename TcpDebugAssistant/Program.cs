namespace TcpDebugAssistant;

internal static class Program
{
    /// <summary>
    /// 应用程序入口。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

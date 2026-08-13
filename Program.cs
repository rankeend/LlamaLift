using System;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("LlamaLift")]
[assembly: AssemblyDescription("本地模型，一键起飞。")]
[assembly: AssemblyCompany("RankeeNd-Masen Hu")]
[assembly: AssemblyProduct("LlamaLift")]
[assembly: AssemblyCopyright("Copyright © 2026 RankeeNd-Masen Hu")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0-preview")]

namespace LlamaServerManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                ShowFatalError(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ShowFatalError(e.ExceptionObject as Exception);
            };

            Application.Run(new MainFormV2());
        }

        private static void ShowFatalError(Exception exception)
        {
            string message = exception == null ? "未知错误" : exception.ToString();
            MessageBox.Show(
                "程序遇到未处理错误：\r\n\r\n" + message,
                "LlamaLift " + AppVersion.DisplayVersion,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

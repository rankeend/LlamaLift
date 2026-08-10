using System;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Llama Server Manager")]
[assembly: AssemblyDescription("通用 Windows llama.cpp 服务管理器与模型启动器")]
[assembly: AssemblyCompany("Llama Server Manager Community")]
[assembly: AssemblyProduct("LlamaServerManager")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

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
                "Llama Server Manager " + AppVersion.DisplayVersion,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

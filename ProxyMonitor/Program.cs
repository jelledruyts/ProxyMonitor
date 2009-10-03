using System;
using System.Globalization;
using System.Windows.Forms;

namespace ProxyMonitor
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                bool detectAndQuit = false;
                if (args.Length > 0)
                {
                    if (string.Equals(args[0], "/detect", StringComparison.OrdinalIgnoreCase))
                    {
                        detectAndQuit = true;
                    }
                    else
                    {
                        TrayMonitor.ShowAbout();
                        return;
                    }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (TrayMonitor monitor = new TrayMonitor(detectAndQuit))
                {
                    monitor.DetectProxyServers();
                    Application.Run();
                }
            }
            catch (Exception exc)
            {
                string message = string.Format(CultureInfo.CurrentCulture, "An unexpected exception occurred, please forgive the machine and blame the authors.{0}{0}Exception message: {1}", Environment.NewLine, exc.Message);
                MessageBox.Show(message, "An exception occurred...", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
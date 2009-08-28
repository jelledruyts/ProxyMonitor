using System;
using System.Globalization;
using System.Windows.Forms;

namespace JelleDruyts.ProxyMonitor
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            TrayMonitor monitor = null;
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
                monitor = new TrayMonitor(detectAndQuit);
                monitor.DetectProxy();
                Application.Run();
            }
            catch (Exception exc)
            {
                string message = string.Format(CultureInfo.CurrentCulture, "An unexpected exception occurred, please forgive the machine and blame the author.{0}{0}Exception message: {1}", Environment.NewLine, exc.Message);
                MessageBox.Show(message, "An exception occurred...", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (monitor != null)
                {
                    monitor.Dispose();
                }
            }
        }
    }
}
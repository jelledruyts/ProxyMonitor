using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace ProxyMonitor
{
    internal static class Program
    {
        #region AppVersion

        private static Version applicationVersion = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// Gets the application version.
        /// </summary>
        public static Version ApplicationVersion
        {
            get
            {
                return applicationVersion;
            }
        }

        #endregion

        #region Main

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Application started (v{0}).", Program.ApplicationVersion.ToString()), TraceEventType.Information);
                bool detectAndQuit = false;
                if (args.Length > 0)
                {
                    if (string.Equals(args[0], "/detect", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogMessage("The /detect command line was present: performing auto-detection and exiting immediately.", TraceEventType.Information);
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
                    monitor.DetectProxyServers(true);
                    Application.Run();
                }
            }
            catch (Exception exc)
            {
                Logger.LogException(exc);
                string message = string.Format(CultureInfo.CurrentCulture, "An unexpected exception occurred, please forgive the machine and blame the authors.{0}{0}Exception message: {1}", Environment.NewLine, exc.Message);
                MessageBox.Show(message, "An exception occurred...", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Logger.LogMessage("Application exited.", TraceEventType.Information);
        }

        #endregion
    }
}
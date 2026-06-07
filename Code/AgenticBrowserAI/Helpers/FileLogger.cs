namespace AgenticBrowserAI.Helpers
{
    using System;
    using System.IO;
    using System.Text;

    public static class FileLogger
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string ReportDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Report");
        private static readonly object LockObject = new object();

        /// <summary>
        /// Logs a message to a daily log file. Appends if the file already exists.
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info, bool IsLogForReport = false)
        {
            try
            {
                string directory = IsLogForReport ? ReportDirectory : LogDirectory;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string fileName = $"log_{DateTime.Now:yyyy_MM_dd}.txt";
                string filePath = Path.Combine(directory, fileName);

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message}{Environment.NewLine}";

                // Thread-safe file writing
                lock (LockObject)
                {
                    // FileMode.Append ensures data is added to the end of the file and handles app restarts perfectly
                    using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        writer.Write(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }
}

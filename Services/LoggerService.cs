using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AutoInventario.Services
{
    public static class LoggerService
    {
        private static readonly string LogDir = @"C:\ProgramData\AutoInventario";
        private static readonly string LogFile = Path.Combine(LogDir, "logs.txt");
        private static readonly object _lock = new object();

        public static void Log(string message, string category = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);

                    // Rotar si el archivo pesa más de 1 MB
                    if (File.Exists(LogFile))
                    {
                        var info = new FileInfo(LogFile);
                        if (info.Length > 1_000_000)
                        {
                            string archiveName = Path.Combine(LogDir, $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                            File.Move(LogFile, archiveName);
                        }
                    }

                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFile, logEntry, Encoding.UTF8);
                }
            }
            catch
            {
                // Evitar romper la app si el log falla
            }
        }

        public static void LogError(Exception ex, string context = "")
        {
            string msg = $"{context}: {ex.Message}\n{ex.StackTrace}";
            Log(msg, "ERROR");
        }
    }
}

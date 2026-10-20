using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

namespace AutoInventario.Services
{
    public static class LoggerService
    {
        private static readonly string LogDir = @"C:\ProgramData\AutoInventario";
        private static readonly string LogFile = Path.Combine(LogDir, "logs.txt");
        private static readonly object _lock = new object();
        private static readonly Regex PemBlockRegex = new(
            "-----BEGIN [^-]+-----.*?-----END [^-]+-----",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex JsonSecretRegex = new(
            "(\"(?:api[_-]?key|authtoken|token|password|secret|connectionstring|ciphertext|encrypted_key|nonce|iv|tag|data|payload|privatekeypath|private_key|bitlocker_recovery_key)\"\\s*:\\s*\")([^\"]*)(\")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NamedSecretRegex = new(
            @"((?:api[_ -]?key|authtoken|token|password|secret|connectionstring|ciphertext|encrypted_key|nonce|iv|tag|data|payload|private[_ -]?key|bitlocker(?:[_ -]?recovery[_ -]?key)?)\s*[:=]\s*)([^\s,;]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringRegex = new(
            @"(?i)\b(?:Host|Server|Data Source)=[^;\r\n]+;[^\r\n]*(?:Password|Pwd)=[^;\r\n]+",
            RegexOptions.Compiled);
        private static readonly Regex BitLockerRecoveryKeyRegex = new(
            @"\b\d{6}(?:-\d{6}){7}\b",
            RegexOptions.Compiled);

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

                    string sanitizedMessage = RedactSensitiveData(message);
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {sanitizedMessage}{Environment.NewLine}";
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
            var contextPrefix = string.IsNullOrWhiteSpace(context)
                ? "Unhandled exception"
                : context;
            string msg = $"{contextPrefix}. ErrorType={ex.GetType().Name}. Detail={RedactSensitiveData(ex.Message)}";
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                msg += $"{Environment.NewLine}{RedactSensitiveData(ex.StackTrace)}";
            }

            Log(msg, "ERROR");
        }

        public static string RedactSensitiveData(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string sanitized = value;
            sanitized = PemBlockRegex.Replace(sanitized, "[REDACTED_PEM_BLOCK]");
            sanitized = ConnectionStringRegex.Replace(sanitized, "[REDACTED_CONNECTION_STRING]");
            sanitized = JsonSecretRegex.Replace(sanitized, "$1[REDACTED]$3");
            sanitized = NamedSecretRegex.Replace(sanitized, "$1[REDACTED]");
            sanitized = BitLockerRecoveryKeyRegex.Replace(sanitized, "[REDACTED_BITLOCKER_RECOVERY_KEY]");
            return sanitized;
        }
    }
}

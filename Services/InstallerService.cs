using Microsoft.Win32;

using System.Diagnostics;
using System.Runtime.Versioning;

namespace AutoInventario.Services
{
    public static class InstallerService
    {
        [SupportedOSPlatform("windows")]
        public static void Install(string appName, string currentPath, string targetPath)
        {
            string? directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Ruta de instalación inválida.");

            Directory.CreateDirectory(directory);
            File.Copy(currentPath, targetPath, true);
            RegisterInstallation(appName, targetPath);
            Console.WriteLine("✅ Instalado correctamente.");
        }

        [SupportedOSPlatform("windows")]
        public static void Uninstall(string appName, string exePath)
        {
            try
            {
                try
                {
                    Helpers.TaskSchedulerHelper.DeleteTask(appName);
                    Console.WriteLine("🧹 Tarea programada eliminada.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia al eliminar tarea: {ex.Message}");
                }

                if (File.Exists(exePath)) File.Delete(exePath);
                string? folder = Path.GetDirectoryName(exePath);
                if (Directory.Exists(folder)) Directory.Delete(folder!, true);

                Console.WriteLine("✅ Desinstalación completada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al desinstalar: {ex.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void RegisterInstallation(string appName, string exePath)
        {
            string version = FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "1.0.0";
            string keyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{appName}";

            using var key = Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("DisplayName", appName);
            key?.SetValue("DisplayVersion", version);
            key?.SetValue("Publisher", "mrHouston");
            key?.SetValue("InstallLocation", Path.GetDirectoryName(exePath) ?? "");
            key?.SetValue("DisplayIcon", exePath);
            key?.SetValue("UninstallString", $"\"{exePath}\" /uninstall");
            key?.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        }

        public static (string clientId, string webhookUrl) ReadParameters(string[] args, string defaultClientId, string defaultUrl)
        {
            string clientId = defaultClientId;
            string url = defaultUrl;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-client_id":
                        if (i + 1 < args.Length) clientId = args[i + 1];
                        break;
                    case "-url":
                        if (i + 1 < args.Length) url = args[i + 1];
                        break;
                }
            }

            return (clientId, url);
        }
    }
}

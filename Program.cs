using AutoInventario.Services;
using AutoInventario.Helpers;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace AutoInventario
{
    static class Program
    {
        public static string Url = "https://ciconia.mrhouston.net";
        public static string WebhookUrl => $"{Url}/webhooks";

        [SupportedOSPlatform("windows")]
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ConfigForm());
                return;
            }

            string clientID = "1";
            string installPath = @"C:\ProgramData\AutoInventario";
            string exeName = "AutoInventario.exe";
            string targetPath = Path.Combine(installPath, exeName);
            string currentPath = Path.Combine(AppContext.BaseDirectory, exeName);

            if (args.Contains("-del"))
            {
                InstallerService.Uninstall("AutoInventario", targetPath);
                return;
            }

            if (!File.Exists(targetPath))
                InstallerService.Install("AutoInventario", currentPath, targetPath);
            else
                await UpdateService.CheckAndUpdateAsync(Url, "AutoInventario", currentPath, targetPath);

            string passedArgs = string.Join(" ", args);
            TaskSchedulerHelper.CreateStartupTask("AutoInventario", targetPath, passedArgs);

            (clientID, _) = InstallerService.ReadParameters(args, clientID, WebhookUrl);

            await InventoryService.ExecuteAsync(clientID, WebhookUrl);
        }
    }
}


//using System.Diagnostics;
//using System.Reflection;
//using System.Runtime.Versioning;
//using System.Security.Cryptography;
//using System.Text;
//using System.Text.Json;
//using System.Windows.Forms;

//using AutoInventario.Helpers;

//using Microsoft.Win32;



//namespace AutoInventario
//{
//    static class Program
//    {
//        public static string Url = "https://ciconia.mrhouston.net";
//        public static string webhookUrl = $"{Program.webhookUrl}/webhooks";

//        [SupportedOSPlatform("windows")]
//        static async Task Main(string[] args)
//        {
//            if (args.Length == 0)
//            {
//                Application.EnableVisualStyles();
//                Application.SetCompatibleTextRenderingDefault(false);
//                Application.Run(new ConfigForm());
//                return;
//            }

//            string clientID = "1";
//            string installPath = @"C:\ProgramData\AutoInventario";
//            string exeName = "AutoInventario.exe";
//            string targetPath = Path.Combine(installPath, exeName);
//            string currentPath = Path.Combine(AppContext.BaseDirectory, exeName);

//            if (args.Contains("-del"))
//            {
//                EliminarInstalacion("AutoInventario", targetPath);
//                return;
//            }

//            if (!File.Exists(targetPath))
//            {
//                try
//                {
//                    InstalarAplicacion("AutoInventario", currentPath, targetPath);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine("Error al instalar: " + ex.Message);
//                    return;
//                }
//            }
//            else
//            {
//                try
//                {
//                    VerificarYActualizar("AutoInventario", currentPath, targetPath);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine("Error al verificar o actualizar la versión: " + ex.Message);
//                }
//            }

//            try
//            {
//                string passedArgs = string.Join(" ", args);
//                TaskSchedulerHelper.CreateStartupTask("AutoInventario", targetPath, passedArgs);
//                Console.WriteLine("Tarea Programada Creada");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine(ex.Message);
//            }

//            (clientID, webhookUrl) = LeerParametros(args, clientID, webhookUrl);

//            await EjecutarInventarioAsync(clientID, webhookUrl);

//        }





//        [SupportedOSPlatform("windows")]
//        public static void RegistrarInstalacion(string nombreApp, string rutaExe)
//        {
//            string version = FileVersionInfo.GetVersionInfo(rutaExe).FileVersion ?? "1.0.0";
//            string claveRegistro = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + nombreApp;

//            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(claveRegistro))
//            {
//                if (key == null)
//                {
//                    Console.WriteLine("No se pudo crear la clave de registro.");
//                    return;
//                }

//                string? installLocation = Path.GetDirectoryName(rutaExe);

//                key.SetValue("DisplayName", nombreApp);
//                key.SetValue("DisplayVersion", version);
//                key.SetValue("Publisher", "mrHouston");
//                key.SetValue("InstallLocation", installLocation ?? "");
//                key.SetValue("DisplayIcon", rutaExe);
//                key.SetValue("UninstallString", "\"" + rutaExe + "\" /uninstall");
//                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

//                Console.WriteLine("Registro de instalación añadido.");
//            }
//        }

//        [SupportedOSPlatform("windows")]
//        public static void InstalarAplicacion(string nombre, string currentPath, string targetPath)
//        {
//            string? directory = Path.GetDirectoryName(targetPath);
//            if (!string.IsNullOrEmpty(directory))
//            {
//                Directory.CreateDirectory(directory);
//            }
//            else
//            {
//                throw new InvalidOperationException("La ruta de instalación no contiene un directorio válido.");
//            }

//            Directory.CreateDirectory(directory);
//            File.Copy(currentPath, targetPath, true);
//            RegistrarInstalacion(nombre, targetPath);
//            Console.WriteLine("Instalado correctamente.");
//        }

//        [SupportedOSPlatform("windows")]
//        public static async Task VerificarYActualizar(string nombreApp, string currentPath, string targetPath)
//        {
//            string manifestUrl = $"{Program.Url}/updates/latest.json";

//            try
//            {
//                using var http = new HttpClient();
//                var json = await http.GetStringAsync(manifestUrl);
//                var update = JsonSerializer.Deserialize<UpdateInfo>(json);

//                if (update == null || string.IsNullOrWhiteSpace(update.file))
//                {
//                    Console.WriteLine("❌ No se pudo leer el manifiesto de actualización.");
//                    return;
//                }

//                var localVersion = FileVersionInfo.GetVersionInfo(currentPath).FileVersion;
//                Console.WriteLine($"Versión local: {localVersion}, Versión remota: {update.version}");

//                if (Version.TryParse(localVersion, out var vLocal) && Version.TryParse(update.version, out var vRemote))
//                {
//                    if (vRemote > vLocal)
//                    {
//                        Console.WriteLine("🔄 Nueva versión disponible. Descargando actualización...");

//                        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(update.file));

//                        using (var stream = await http.GetStreamAsync(update.file))
//                        using (var file = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
//                        {
//                            await stream.CopyToAsync(file);
//                        }

//                        Console.WriteLine("Descarga completada. Verificando integridad...");

//                        string computedHash = CalcularSHA256(tempFile);

//                        if (!computedHash.Equals(update.sha256, StringComparison.OrdinalIgnoreCase))
//                        {
//                            Console.WriteLine("⚠️ Error: El hash del archivo descargado no coincide con el manifiesto.");
//                            File.Delete(tempFile);
//                            return;
//                        }

//                        Console.WriteLine("✅ Integridad verificada correctamente.");

//                        File.Copy(tempFile, targetPath, true);
//                        RegistrarInstalacion(nombreApp, targetPath);

//                        Console.WriteLine($"✅ Actualización completada a la versión {update.version}.");

//                        Console.WriteLine("Reiniciando aplicación actualizada...");

//                        string currentArgs = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));

//                        Process.Start(new ProcessStartInfo
//                        {
//                            FileName = targetPath,
//                            Arguments = currentArgs,
//                            WorkingDirectory = Path.GetDirectoryName(targetPath),
//                            UseShellExecute = true
//                        });

//                        Environment.Exit(0);
//                    }
//                    else
//                    {
//                        Console.WriteLine("✔️ No hay actualizaciones disponibles.");
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Error durante la verificación online: {ex.Message}");
//            }
//        }

//        private static string CalcularSHA256(string filePath)
//        {
//            using var sha256 = SHA256.Create();
//            using var stream = File.OpenRead(filePath);
//            var hash = sha256.ComputeHash(stream);
//            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
//        }

//        private class UpdateInfo
//        {
//            public string version { get; set; } = "";
//            public string file { get; set; } = "";
//            public string sha256 { get; set; } = "";
//        }


//        [SupportedOSPlatform("windows")]
//        static (string clientId, string webhookUrl) LeerParametros(string[] args, string defaultClientId, string defaultUrl)
//        {
//            string clientId = defaultClientId;
//            string url = defaultUrl;

//            for (int i = 0; i < args.Length; i++)
//            {
//                switch (args[i])
//                {
//                    case "-client_id":
//                        if (i + 1 < args.Length)
//                            clientId = args[i + 1];
//                        break;
//                    case "-url":
//                        if (i + 1 < args.Length)
//                            url = args[i + 1];
//                        break;
//                }
//            }

//            return (clientId, url);
//        }

//        [SupportedOSPlatform("windows")]
//        public static void EliminarInstalacion(string nombreApp, string rutaExe)
//        {
//            try
//            {
//                try
//                {
//                    TaskSchedulerHelper.DeleteTask(nombreApp);
//                    Console.WriteLine("Tarea programada eliminada correctamente.");
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"No se pudo eliminar la tarea programada: {ex.Message}");
//                }

//                if (File.Exists(rutaExe))
//                {
//                    File.Delete(rutaExe);
//                    Console.WriteLine("Ejecutable eliminado.");
//                }

//                string carpeta = Path.GetDirectoryName(rutaExe)!;
//                if (Directory.Exists(carpeta))
//                {
//                    Directory.Delete(carpeta, true);
//                    Console.WriteLine("Carpeta de instalación eliminada.");
//                }

//                Console.WriteLine("Desinstalación completa.");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error al eliminar la instalación: {ex.Message}");
//            }
//        }

//        [SupportedOSPlatform("windows")]
//        public static async Task EjecutarInventarioAsync(string clientID, string webhookUrl)
//        {
//            var systemInfo = Systeminfo.GenerateWorkstationJson(clientID);

//            using var doc = JsonDocument.Parse(systemInfo);
//            var options = new JsonSerializerOptions { WriteIndented = true };
//            string properJson = JsonSerializer.Serialize(doc.RootElement, options);

//            string publicKey = LoadPublicKey();
//            var (encryptedJson, encryptedKey, iv) = EncryptData(properJson, publicKey);
//            await SendDataToWebhook(clientID, encryptedJson, encryptedKey, iv, webhookUrl);
//        }


//    }
//}

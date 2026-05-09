using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.Versioning;

namespace AutoInventario.Services
{
    public static class UpdateService
    {
        [SupportedOSPlatform("windows")]
        public static async Task CheckAndUpdateAsync(string baseUrl, string appName, string currentPath, string targetPath)
        {
            string manifestUrl = $"{baseUrl}/updates/latest.json";

            try
            {
                LoggerService.Log($"🔍 Verificando actualizaciones en: {manifestUrl}");

                using var http = new HttpClient();
                var json = await http.GetStringAsync(manifestUrl);
                var update = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (update == null || update.files == null)
                {
                    LoggerService.Log("❌ No se pudo leer el manifiesto de actualización (estructura inválida).");
                    return;
                }

                LoggerService.Log($"📦 Versión remota: {update.version}");
                var localVersion = FileVersionInfo.GetVersionInfo(currentPath).FileVersion ?? "0.0.0.0";
                LoggerService.Log($"💾 Versión local: {localVersion}");

                if (!Version.TryParse(localVersion, out var vLocal) || !Version.TryParse(update.version, out var vRemote))
                {
                    LoggerService.Log("⚠️ No se pudieron analizar las versiones correctamente.");
                    return;
                }

                if (vRemote <= vLocal)
                {
                    LoggerService.Log("✔️ No hay actualizaciones disponibles.");
                    return;
                }

                LoggerService.Log($"🔄 Nueva versión detectada: {vRemote}. Iniciando descarga...");

                // Rutas temporales
                string tempMain = Path.Combine(Path.GetTempPath(), "AutoInventario.new.exe");
                string tempUpdater = Path.Combine(Path.GetTempPath(), "AutoInventario.Updater.exe");

                // 1️⃣ Descargar nuevo ejecutable principal
                await DownloadFileAsync(http, update.files.main, tempMain);
                if (!ValidateHash(tempMain, update.sha256.main))
                {
                    LoggerService.Log($"⚠️ Hash del archivo principal no coincide. Abortando.");
                    File.Delete(tempMain);
                    return;
                }

                // 2️⃣ Descargar Updater solo si hay nueva versión
                await DownloadFileAsync(http, update.files.updater, tempUpdater);
                if (!ValidateHash(tempUpdater, update.sha256.updater))
                {
                    LoggerService.Log($"⚠️ Hash del Updater no coincide. Abortando.");
                    File.Delete(tempUpdater);
                    return;
                }

                LoggerService.Log("✅ Descarga e integridad verificadas correctamente.");

                // 3️⃣ Ejecutar el Updater para aplicar la actualización
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempUpdater,
                    Arguments = $"\"{tempMain}\" \"{targetPath}\"",
                    UseShellExecute = true
                });

                LoggerService.Log("🚀 Lanzando Updater y cerrando la aplicación actual.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex);
            }
        }

        // 🔧 Descarga de archivos
        private static async Task DownloadFileAsync(HttpClient http, string url, string destination)
        {
            try
            {
                LoggerService.Log($"⬇️ Descargando: {url}");
                using var stream = await http.GetStreamAsync(url);
                using var file = new FileStream(destination, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(file);
                LoggerService.Log($"📁 Archivo descargado en: {destination}");
            }
            catch (Exception ex)
            {
                LoggerService.Log($"❌ Error al descargar {url}: {ex.Message}");
                throw;
            }
        }

        // 🔐 Validación SHA256
        private static bool ValidateHash(string filePath, string expectedHash)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            bool valid = hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
            LoggerService.Log(valid ? "✅ Hash válido." : $"❌ Hash inválido ({hash} ≠ {expectedHash})");
            return valid;
        }

        // 📦 Modelo para latest.json
        private class UpdateInfo
        {
            public string version { get; set; } = "";
            public FileLinks files { get; set; } = new();
            public HashLinks sha256 { get; set; } = new();
        }

        private class FileLinks
        {
            public string main { get; set; } = "";
            public string updater { get; set; } = "";
        }

        private class HashLinks
        {
            public string main { get; set; } = "";
            public string updater { get; set; } = "";
        }
    }
}

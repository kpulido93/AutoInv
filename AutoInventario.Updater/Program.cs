using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace AutoInventario.Updater
{
    [SupportedOSPlatform("windows")]
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2) return 2;

            string source = args[0];
            string target = args[1];
            int pid = (args.Length >= 3 && int.TryParse(args[2], out var p)) ? p : -1;

            try
            {
                if (!File.Exists(source))
                {
                    LoggerService.Log($"❌ Source no existe: {source}");
                    return 3;
                }

                // Si target es carpeta, construir ruta al exe
                if (Directory.Exists(target))
                    target = Path.Combine(target, "AutoInventario.exe");

                LoggerService.Log($"🧩 Updater | source={source}");
                LoggerService.Log($"🧩 Updater | target={target}");
                LoggerService.Log($"🧩 Updater | pid={pid}");

                // 1) Esperar cierre del PID principal (si existe)
                if (pid > 0)
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);

                        if (!proc.WaitForExit(60000))
                        {
                            LoggerService.Log("⚠️ El proceso no cerró a tiempo. Intentando matar...");
                            try { proc.Kill(true); } catch { }
                            proc.WaitForExit(15000);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Log($"⚠️ No se pudo esperar/matar PID {pid}: {ex.Message}");
                    }
                }

                // 2) Copiar con reintentos, asegurando que no haya instancias usando el target
                bool copied = TryCopyWithRetries(source, target, retries: 25, delayMs: 500, out var lastError);

                if (!copied)
                {
                    LoggerService.Log($"❌ No se pudo copiar después de reintentos. Último error: {lastError}");
                    return 4;
                }

                LoggerService.Log("✅ Copia completada. Limpiando registro (si aplica)...");
                DeleteUninstallEntry("Autoinventario");

                // 3) Leer argumentos desde la tarea programada
                string taskName = "AutoInventario";
                string? taskArgs = TryGetTaskArguments(taskName);

                LoggerService.Log(taskArgs == null
                    ? $"⚠️ No pude leer args de la tarea '{taskName}'. Arrancando sin args."
                    : $"✅ Args de tarea '{taskName}': {taskArgs}");

                // 4) Arrancar el exe actualizado con args
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = taskArgs ?? "",
                    UseShellExecute = true
                };

                LoggerService.Log($"🚀 Iniciando actualizado: {psi.FileName} {psi.Arguments}");
                Process.Start(psi);

                // 5) Borrar el exe temporal descargado
                try { File.Delete(source); }
                catch (Exception ex) { LoggerService.Log($"⚠️ No se pudo borrar source: {ex.Message}"); }

                // 6) Autolimpieza del updater (el propio exe)
                TrySelfDelete();

                return 0;
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Error Updater");
                return 1;
            }
        }

        static string? TryGetTaskArguments(string taskName)
        {
            try
            {
                using var ts = new TaskService();
                var task = ts.GetTask(taskName);
                if (task == null) return null;

                var exec = task.Definition.Actions.OfType<ExecAction>().FirstOrDefault();
                return exec?.Arguments;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryCopyWithRetries(string source, string target, int retries, int delayMs, out string lastError)
        {
            lastError = "";

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrWhiteSpace(dir))
                        Directory.CreateDirectory(dir);

                    // Espera/termina instancias que estén bloqueando el target
                    EnsureTargetNotRunning(target, timeoutMs: 6000);

                    File.Copy(source, target, true);

                    var len = new FileInfo(target).Length;
                    if (len <= 0) throw new IOException("Archivo copiado con tamaño 0.");

                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Thread.Sleep(delayMs);
                }
            }

            return false;
        }

        static void EnsureTargetNotRunning(string targetPath, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            string procName = Path.GetFileNameWithoutExtension(targetPath);

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var lockers = Process.GetProcessesByName(procName)
                    .Where(p =>
                    {
                        try
                        {
                            if (p.HasExited) return false;
                            var pPath = p.MainModule?.FileName;
                            return !string.IsNullOrEmpty(pPath) &&
                                   string.Equals(Path.GetFullPath(pPath), Path.GetFullPath(targetPath),
                                       StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            // si no puedo leer MainModule (permisos), no confirmo que sea locker aquí
                            return false;
                        }
                    })
                    .ToList();

                if (lockers.Count == 0) return;

                foreach (var p in lockers)
                {
                    try { p.CloseMainWindow(); } catch { }
                }

                Thread.Sleep(500);
            }

            // último recurso: matar por nombre (puede matar otras instancias legítimas, pero para update es OK)
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try
                {
                    if (!p.HasExited) p.Kill(true);
                }
                catch { }
            }

            Thread.Sleep(500);
        }

        static void TrySelfDelete()
        {
            try
            {
                string me = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string cmdPath = Path.Combine(systemDir, "cmd.exe");

                Process.Start(new ProcessStartInfo
                {
                    FileName = cmdPath,
                    Arguments = $"/c timeout /t 2 /nobreak >nul & del /f /q \"{me}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = systemDir
                });
            }
            catch (Exception ex)
            {
                LoggerService.Log($"⚠️ No se pudo autolimpiarse: {ex.Message}");
            }
        }

        public static void DeleteUninstallEntry(string subkeyName = "Autoinventario")
        {
            const string baseKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            try
            {
                using var baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var uninstall64 = baseKey64.OpenSubKey(baseKeyPath, writable: true);

                if (uninstall64?.OpenSubKey(subkeyName) != null)
                {
                    uninstall64.DeleteSubKeyTree(subkeyName, throwOnMissingSubKey: false);
                    LoggerService.Log($"✅ Entrada '{subkeyName}' borrada de HKLM:\\{baseKeyPath} (64-bit).");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log($"⚠️ No se pudo borrar '{subkeyName}' (64-bit): {ex.Message}");
            }
        }
    }
}

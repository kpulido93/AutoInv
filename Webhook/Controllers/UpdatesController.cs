using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Webhook.Controllers
{
    [ApiController]
    [Route("updates")]
    public class UpdatesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public UpdatesController(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        [HttpGet("latest.json")]
        public IActionResult GetLatest()
        {
            try
            {
                string baseUrl = _config["Kestrel:EndPoints:Http:Url"] ?? throw new Exception("BaseUrl no configurada.");
                string updatesRoot = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "updates");

                if (!Directory.Exists(updatesRoot))
                    return NotFound(new { error = "Directorio de actualizaciones no encontrado." });

                // 🔍 Buscar carpetas de versiones (ejemplo: 1.2.0.0, 1.3.0.0)
                var versionDirs = Directory.GetDirectories(updatesRoot)
                    .Select(d => new { Path = d, Name = new DirectoryInfo(d).Name })
                    .Where(v => Version.TryParse(v.Name, out _))
                    .OrderByDescending(v => Version.Parse(v.Name))
                    .ToList();

                if (versionDirs.Count == 0)
                    return NotFound(new { error = "No se encontraron versiones publicadas." });

                var latestDir = versionDirs.First();
                string version = latestDir.Name;

                string mainFile = Path.Combine(latestDir.Path, "AutoInventario.exe");
                string updaterFile = Path.Combine(updatesRoot, "AutoInventario.Updater.exe");

                if (!System.IO.File.Exists(mainFile))
                    return NotFound(new { error = $"Falta el ejecutable principal en {latestDir.Path}" });

                if (!System.IO.File.Exists(updaterFile))
                    return NotFound(new { error = "No se encontró el AutoInventario.Updater.exe en la raíz de /updates" });

                // 🔐 Calcular hashes
                string mainHash = CalcularSHA256(mainFile);
                string updaterHash = CalcularSHA256(updaterFile);

                // 🌐 Construir URLs públicas
                string mainUrl = $"{baseUrl}/updates/{version}/AutoInventario.exe";
                string updaterUrl = $"{baseUrl}/updates/AutoInventario.Updater.exe";

                var response = new
                {
                    version,
                    files = new
                    {
                        main = mainUrl,
                        updater = updaterUrl
                    },
                    sha256 = new
                    {
                        main = mainHash,
                        updater = updaterHash
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error generando el manifiesto de actualización",
                    details = ex.Message
                });
            }
        }

        private static string CalcularSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = System.IO.File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}

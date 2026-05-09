using System;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using AutoInventario.Helpers;
using AutoInventario.Services;

namespace AutoInventario
{
    public partial class ConfigForm : Form
    {
        private readonly HttpClient _http = new HttpClient();

        public ConfigForm()
        {
            InitializeComponent();
        }

        private async void ConfigForm_Load(object sender, EventArgs e)
        {
            await CargarClientesAsync();
        }

        private async Task CargarClientesAsync()
        {
            const int maxIntentos = 3;
            int intentos = 0;
            bool exito = false;

            while (intentos < maxIntentos && !exito)
            {
                try
                {
                    intentos++;
                    string url = $"{Program.Url}/id-clients";
                    LoggerService.Log($"Cargando clientes desde {url} (intento {intentos})");

                    string json = await _http.GetStringAsync(url);
                    var clients = JsonSerializer.Deserialize<ClientInfo[]>(json) ?? [];

                    comboClients.DataSource = clients;
                    comboClients.DisplayMember = "name";
                    comboClients.ValueMember = "id";
                    exito = true;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError(ex, "Error al cargar clientes");
                    if (intentos < maxIntentos)
                    {
                        var retry = MessageBox.Show(
                            "No se pudo conectar al servidor para obtener los clientes.\n¿Desea reintentar?",
                            "Error de conexión",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );
                        if (retry == DialogResult.No)
                            break;
                    }
                    else
                    {
                        MessageBox.Show("No se pudieron cargar los clientes después de varios intentos.",
                                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string clientId = comboClients.SelectedValue?.ToString() ?? "1";
                string installPath = @"C:\ProgramData\AutoInventario";
                string exeName = "AutoInventario.exe";
                string targetPath = System.IO.Path.Combine(installPath, exeName);
                string currentPath = System.IO.Path.Combine(AppContext.BaseDirectory, exeName);

                await UpdateService.CheckAndUpdateAsync(Program.Url, "AutoInventario", currentPath, targetPath);
                // Instalar si no existe
                if (!System.IO.File.Exists(targetPath))
                    InstallerService.Install("AutoInventario", currentPath, targetPath);

                // Crear tarea programada
                TaskSchedulerHelper.CreateStartupTask("AutoInventario", targetPath, $"-client_id {clientId}");
                LoggerService.Log($"Configuración guardada para client_id={clientId}");

                // Ejecutar inventario inicial
                await InventoryService.ExecuteAsync(clientId, Program.WebhookUrl);
                LoggerService.Log($"Inventario ejecutado para client_id={clientId}");

                MessageBox.Show("✅ Configuración guardada correctamente.", "AutoInventario",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Error al guardar configuración");
                MessageBox.Show($"Error: {ex.Message}", "AutoInventario", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private class ClientInfo
        {
            public string id { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
        }
    }
}

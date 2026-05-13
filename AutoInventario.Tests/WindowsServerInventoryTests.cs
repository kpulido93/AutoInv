using AutoInventario.Models;
using AutoInventario.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoInventario.Tests;

public class WindowsServerInventoryTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void IsServerProductType_detects_windows_server(uint productType, bool expected)
    {
        Assert.Equal(expected, WindowsServerInventoryService.IsServerProductType(productType));
    }

    [Fact]
    public void GetConfiguredServiceNames_splits_and_deduplicates_values()
    {
        var services = WindowsServerInventoryService.GetConfiguredServiceNames("W3SVC; MSSQLSERVER, w3svc ; DNS");

        Assert.Equal(new[] { "W3SVC", "MSSQLSERVER", "DNS" }, services);
    }

    [Fact]
    public void Workstation_serialization_includes_server_inventory_when_server()
    {
        var payload = new WorkstationInfo
        {
            workstation = new Workstation
            {
                name = "SRV-APP-01",
                is_server = true,
                operating_system = new OperatingSystemInfo
                {
                    os = "Microsoft Windows Server 2022 Standard",
                    version = "10.0.20348",
                    build_number = "20348"
                },
                server_inventory = new ServerInventory
                {
                    os_caption = "Microsoft Windows Server 2022 Standard",
                    os_version = "10.0.20348",
                    os_build = "20348",
                    part_of_domain = true,
                    domain = "corp.example",
                    uptime_seconds = 3600,
                    roles_features = new List<ServerRoleFeature>
                    {
                        new() { id = "Web-Server", name = "Web Server (IIS)" }
                    },
                    tracked_services = new List<TrackedWindowsService>
                    {
                        new()
                        {
                            name = "W3SVC",
                            display_name = "World Wide Web Publishing Service",
                            state = "Running",
                            start_mode = "Auto"
                        }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var root = JObject.Parse(json);
        var workstation = root["workstation"]!;

        Assert.True(workstation.Value<bool>("is_server"));
        Assert.Equal("Microsoft Windows Server 2022 Standard", workstation["operating_system"]!.Value<string>("os"));
        Assert.Equal("20348", workstation["server_inventory"]!.Value<string>("os_build"));
        Assert.Equal("corp.example", workstation["server_inventory"]!.Value<string>("domain"));
        Assert.Equal("W3SVC", workstation["server_inventory"]!["tracked_services"]![0]!.Value<string>("name"));
    }

    [Fact]
    public void Sensitive_license_and_recovery_helpers_return_non_secret_status()
    {
        Assert.Equal("No recopilado", AutoInventario.Systeminfo.GetRecoveryPassword());
        Assert.Equal("Detectado sin recopilar clave", AutoInventario.Systeminfo.GetWindowsLicense());
        Assert.Equal("No recopilado", AutoInventario.Helpers.Systeminfo.GetRecoveryPassword());
        Assert.Equal("Detectado sin recopilar clave", AutoInventario.Helpers.Systeminfo.GetWindowsLicense());
    }
}

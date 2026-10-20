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

    [Theory]
    [InlineData(false, "windows_workstation")]
    [InlineData(true, "windows_server")]
    public void GetOperatingSystemFamily_returns_expected_values(bool isServer, string expected)
    {
        Assert.Equal(expected, WindowsServerInventoryService.GetOperatingSystemFamily(isServer));
    }

    [Fact]
    public void GetConfiguredServiceNames_splits_and_deduplicates_values()
    {
        var services = WindowsServerInventoryService.GetConfiguredServiceNames("W3SVC; MSSQLSERVER, w3svc ; DNS");

        Assert.Equal(new[] { "W3SVC", "MSSQLSERVER", "DNS" }, services);
    }

    [Fact]
    public void GetServerInventory_returns_null_for_workstation_payloads()
    {
        Assert.Null(WindowsServerInventoryService.GetServerInventory(false));
    }

    [Fact]
    public void CreateInstalledRoleFeature_returns_name_and_installed_state()
    {
        var feature = WindowsServerInventoryService.CreateInstalledRoleFeature("Web Server (IIS)");

        Assert.NotNull(feature);
        Assert.Equal("Web Server (IIS)", feature!.name);
        Assert.Equal("Installed", feature.state);
        Assert.Null(feature.id);
    }

    [Fact]
    public void CreateInstalledRoleFeature_ignores_blank_names()
    {
        Assert.Null(WindowsServerInventoryService.CreateInstalledRoleFeature("  "));
    }

    [Fact]
    public void Workstation_serialization_includes_server_inventory_when_server()
    {
        var payload = new WorkstationInfo
        {
            workstation = new Workstation
            {
                name = "SRV-APP-01",
                os_family = "windows_server",
                os_caption = "Microsoft Windows Server 2022 Standard",
                os_version = "10.0.20348",
                os_build = "20348",
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
                        new() { name = "Web Server (IIS)", state = "Installed" }
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
        Assert.Equal("windows_server", workstation.Value<string>("os_family"));
        Assert.Equal("Microsoft Windows Server 2022 Standard", workstation.Value<string>("os_caption"));
        Assert.Equal("10.0.20348", workstation.Value<string>("os_version"));
        Assert.Equal("20348", workstation.Value<string>("os_build"));
        Assert.Equal("Microsoft Windows Server 2022 Standard", workstation["operating_system"]!.Value<string>("os"));
        Assert.Equal("20348", workstation["server_inventory"]!.Value<string>("os_build"));
        Assert.Equal("corp.example", workstation["server_inventory"]!.Value<string>("domain"));
        Assert.Equal("Web Server (IIS)", workstation["server_inventory"]!["roles_features"]![0]!.Value<string>("name"));
        Assert.Equal("Installed", workstation["server_inventory"]!["roles_features"]![0]!.Value<string>("state"));
        Assert.Equal("W3SVC", workstation["server_inventory"]!["tracked_services"]![0]!.Value<string>("name"));
    }

    [Fact]
    public void Workstation_serialization_marks_workstation_operating_systems_as_non_server()
    {
        var payload = new WorkstationInfo
        {
            workstation = new Workstation
            {
                name = "PC-001",
                os_family = "windows_workstation",
                os_caption = "Microsoft Windows 11 Pro",
                os_version = "10.0.22631",
                os_build = "22631",
                is_server = false
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var root = JObject.Parse(json);
        var workstation = root["workstation"]!;

        Assert.False(workstation.Value<bool>("is_server"));
        Assert.Equal("windows_workstation", workstation.Value<string>("os_family"));
        Assert.Equal("Microsoft Windows 11 Pro", workstation.Value<string>("os_caption"));
        Assert.Equal("10.0.22631", workstation.Value<string>("os_version"));
        Assert.Equal("22631", workstation.Value<string>("os_build"));
        Assert.Null(workstation["server_inventory"]);
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

using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using AutoInventario.Models;

namespace AutoInventario.Services
{
    [SupportedOSPlatform("windows")]
    public static class WindowsServerInventoryService
    {
        private static readonly string[] DefaultTrackedServices =
        {
            "W3SVC",
            "MSSQLSERVER",
            "SQLSERVERAGENT",
            "DNS",
            "DHCPServer",
            "NTDS",
            "ADWS",
            "TermService"
        };

        public static bool IsWindowsServer()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProductType FROM Win32_OperatingSystem");
                var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
                if (os?["ProductType"] is null)
                {
                    return false;
                }

                return IsServerProductType(Convert.ToUInt32(os["ProductType"]));
            }
            catch (Exception ex) when (IsRecoverableWmiFailure(ex))
            {
                return false;
            }
        }

        public static bool IsServerProductType(uint productType) => productType is 2 or 3;

        public static ServerInventory? GetServerInventory(bool isServer, string? configuredServiceNames = null)
        {
            if (!isServer)
            {
                return null;
            }

            var inventory = new ServerInventory();
            PopulateOperatingSystem(inventory);
            PopulateDomainOrWorkgroup(inventory);
            inventory.roles_features = GetServerRolesAndFeatures();
            inventory.tracked_services = GetTrackedServices(configuredServiceNames);

            return inventory;
        }

        private static void PopulateOperatingSystem(ServerInventory inventory)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Version, BuildNumber, LastBootUpTime FROM Win32_OperatingSystem");
                var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
                if (os is null)
                {
                    return;
                }

                inventory.os_caption = os["Caption"]?.ToString();
                inventory.os_version = os["Version"]?.ToString();
                inventory.os_build = os["BuildNumber"]?.ToString();

                var bootTime = TryParseWmiDateTime(os["LastBootUpTime"]?.ToString());
                if (bootTime is not null)
                {
                    var bootTimeUtc = bootTime.Value.ToUniversalTime();
                    inventory.last_boot_time_utc = bootTimeUtc.ToString("o");
                    inventory.uptime_seconds = Math.Max(0, Convert.ToInt64((DateTime.UtcNow - bootTimeUtc).TotalSeconds));
                }
            }
            catch (Exception ex) when (IsRecoverableWmiFailure(ex))
            {
            }
        }

        private static void PopulateDomainOrWorkgroup(ServerInventory inventory)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Domain, PartOfDomain, Workgroup FROM Win32_ComputerSystem");
                var computer = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
                if (computer is null)
                {
                    return;
                }

                bool? partOfDomain = computer["PartOfDomain"] is bool value ? value : null;
                inventory.part_of_domain = partOfDomain;

                if (partOfDomain == true)
                {
                    inventory.domain = computer["Domain"]?.ToString();
                }
                else
                {
                    inventory.workgroup = computer["Workgroup"]?.ToString() ?? computer["Domain"]?.ToString();
                }
            }
            catch (Exception ex) when (IsRecoverableWmiFailure(ex))
            {
            }
        }

        private static List<ServerRoleFeature> GetServerRolesAndFeatures()
        {
            var roles = new List<ServerRoleFeature>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ID, Name FROM Win32_ServerFeature");
                foreach (var feature in searcher.Get().OfType<ManagementObject>())
                {
                    var name = feature["Name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    roles.Add(new ServerRoleFeature
                    {
                        id = feature["ID"]?.ToString(),
                        name = name
                    });
                }
            }
            catch (Exception ex) when (IsRecoverableWmiFailure(ex))
            {
            }

            return roles;
        }

        private static List<TrackedWindowsService> GetTrackedServices(string? configuredServiceNames)
        {
            var trackedServices = new List<TrackedWindowsService>();
            foreach (var serviceName in GetConfiguredServiceNames(configuredServiceNames))
            {
                try
                {
                    var escapedName = serviceName.Replace("'", "''");
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT Name, DisplayName, State, StartMode FROM Win32_Service WHERE Name = '{escapedName}'");

                    foreach (var service in searcher.Get().OfType<ManagementObject>())
                    {
                        trackedServices.Add(new TrackedWindowsService
                        {
                            name = service["Name"]?.ToString(),
                            display_name = service["DisplayName"]?.ToString(),
                            state = service["State"]?.ToString(),
                            start_mode = service["StartMode"]?.ToString()
                        });
                    }
                }
                catch (Exception ex) when (IsRecoverableWmiFailure(ex))
                {
                }
            }

            return trackedServices;
        }

        public static IReadOnlyList<string> GetConfiguredServiceNames(string? configuredServiceNames)
        {
            var source = string.IsNullOrWhiteSpace(configuredServiceNames)
                ? DefaultTrackedServices
                : configuredServiceNames
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return source
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static DateTime? TryParseWmiDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return ManagementDateTimeConverter.ToDateTime(value);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        private static bool IsRecoverableWmiFailure(Exception ex)
            => ex is ManagementException or UnauthorizedAccessException or COMException or InvalidOperationException;
    }
}

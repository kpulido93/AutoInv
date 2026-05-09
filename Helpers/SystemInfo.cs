using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Autoinventario.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace AutoInventario.Helpers
{
    [SupportedOSPlatform("windows")]
    public static class Systeminfo
    {
        [SupportedOSPlatform("windows")]
        public static string GenerateWorkstationJson(string clientId)
        {
            var workstation = new Workstation
            {
                name = Environment.MachineName,
                last_logged_user = Environment.UserName,
                org_serial_number = GetBiosSerialNumber(),
                manufacturer = GetSystemManufacturer(),
                is_remote_control_prompt_enabled = IsRemoteControlPromptEnabled(),
                is_server = IsServer(),
                operating_system = GetOperatingSystemInfo(),
                computer_system = GetComputerSystemInfo(),
                product = new Product
                {
                    part_no = GetBiosSerialNumber(),
                    manufacturer = GetSystemManufacturer(),
                    product_type = new ProductType { id = GetDeviceTypeCode() },
                    id = 3306,
                },
                domain = new Domain
                {
                    name = GetDomain()
                },
                acquisition_date = new AcquisitionDate
                {
                    display_value = DateTime.Now.ToString("yyyy-MM-dd"),
                    value = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds().ToString()
                },
                user_accounts = GetUserAccounts(),
                physical_drives = GetDiskDrives(),
                processors = GetProcessors(),
                ip_addresses = string.Join(", ", GetIpAddresses()),
                primary_ip = GetIpAddresses().FirstOrDefault(),
                workstation_udf_fields = GetUdfFields(clientId),
                udf_fields = new UdfFields
                {
                    udf_sline_8401 = GetResponsible(),
                    udf_sline_9901 = GetWmiProperty("Win32_ComputerSystemProduct", "Version")
                },
                memory = new MemoryInfo
                {
                    virtual_memory = GetVirtualMemory(),
                    physical_memory = GetPhysicalMemory()
                },
                sound_card = new SoundCard { sound_card_name = GetSoundCardName() },
                logical_cpu_count = Environment.ProcessorCount.ToString(),
                monitors = GetMonitors(),
                usb_controllers = GetUSBControllers(),
                network_adapters = GetNetworkAdapters(),
                memory_modules = GetMemoryModules(),
                motherboards = GetMotherboards(),
                hard_disks = GetHardDisks(),
                logical_drives = GetLogicalDrives(),
                mouse = GetMice(),
                keyboard = GetKeyboard(),
                ports = GetPorts()
            };

            var root = new { workstation };
            return JsonConvert.SerializeObject(root, Formatting.Indented);
        }

        private static string GetBiosSerialNumber()
        {
            var serial = GetWmiProperty("Win32_BIOS", "SerialNumber");
            return string.IsNullOrWhiteSpace(serial) ? "UNKNOWN" : serial.Replace(" ", "");
        }
        private static string GetSystemManufacturer() => GetWmiProperty("Win32_ComputerSystem", "Manufacturer");
        private static string GetModel() => GetWmiProperty("Win32_ComputerSystem", "Model");

        private static bool IsRemoteControlPromptEnabled()
        {
            try
            {

                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("RemoteControlPrompt");
                        if (value != null && int.TryParse(value.ToString(), out var result))
                        {
                            return result == 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al verificar RemoteControlPrompt: " + ex.Message);
            }
            return false;

        }

        private static bool IsServer()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT ProductType FROM Win32_OperatingSystem"))
            {
                var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
                if (os != null)
                {
                    var productType = (uint)os["ProductType"];
                    return productType != 1; // 1 = Workstation, 2 = Domain Controller, 3 = Server
                }
            }
            return false;
        }

        private static OperatingSystemInfo GetOperatingSystemInfo() => new OperatingSystemInfo
        {
            os = GetWmiProperty("Win32_OperatingSystem", "Caption"),
            version = Environment.OSVersion.Version.ToString(),
            build_number = GetWmiProperty("Win32_OperatingSystem", "BuildNumber"),
            product_id = GetWmiProperty("Win32_OperatingSystem", "SerialNumber"),
            os_system_drive = Path.GetPathRoot(Environment.SystemDirectory)
        };

        private static ComputerSystem GetComputerSystemInfo() => new ComputerSystem
        {
            model = GetModel(),
            bios_version = GetWmiProperty("Win32_BIOS", "SMBIOSBIOSVersion"),
            bios_date = GetWmiProperty("Win32_BIOS", "ReleaseDate"),
            system_manufacturer = GetSystemManufacturer(),
            service_tag = GetBiosSerialNumber()
        };

        private static List<UserAccount> GetUserAccounts()
        {
            var accounts = new List<UserAccount>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_UserAccount WHERE LocalAccount=True");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                accounts.Add(new UserAccount
                {
                    name = obj["Name"]?.ToString(),
                    domain = obj["Domain"]?.ToString(),
                    full_name = obj["Name"]?.ToString(),
                    description = string.IsNullOrWhiteSpace(obj["Description"]?.ToString()) ? "Sin descripcion" : obj["Description"].ToString(),
                    status = obj["Status"]?.ToString(),
                    sid = obj["SID"]?.ToString()
                });
            }
            return accounts;
        }


        private static List<PhysicalDrive> GetDiskDrives()
        {
            var drives = new List<PhysicalDrive>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                drives.Add(new PhysicalDrive
                {
                    drive_type = "Disk drive",
                    name = obj["Caption"]?.ToString(),
                    manufacturer = obj["Manufacturer"]?.ToString(),
                    provider = obj["PNPDeviceID"]?.ToString(),
                    version = obj["FirmwareRevision"]?.ToString()
                });
            }
            return drives;
        }

        [SupportedOSPlatform("windows")]
        public static string? GetResponsible()
        {
            const string registryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI";
            const string valueName = "LastLoggedOnDisplayName";

            try
            {
                using var registryKey = Registry.LocalMachine.OpenSubKey(registryKeyPath);
                if (registryKey == null) return null;

                var value = registryKey.GetValue(valueName);
                return value?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accessing the registry: " + ex.Message);
                return null;
            }
        }

        private static List<Processor> GetProcessors()
        {
            var list = new List<Processor>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                list.Add(new Processor
                {
                    name = obj["Name"]?.ToString(),
                    core_count = obj["NumberOfCores"]?.ToString(),
                    number_of_cores = obj["NumberOfCores"]?.ToString(),
                    speed = obj["MaxClockSpeed"]?.ToString(),
                    manufacturer = obj["Manufacturer"]?.ToString(),
                    cpu_manufacturer = obj["Manufacturer"]?.ToString(),
                    family = obj["Family"]?.ToString(),
                    model = obj["ProcessorId"]?.ToString(),
                    serial_number = obj["ProcessorId"]?.ToString()
                });
            }
            return list;
        }

        private static List<string> GetIpAddresses() => Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(ip => ip.ToString()).ToList();

        private static string GetPhysicalMemory()
        {
            ulong totalRam = 0;
            var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                totalRam += Convert.ToUInt64(obj["Capacity"]);
            }
            var totalRamGB = (int)Math.Round(totalRam / (1024.0 * 1024 * 1024));
            return totalRamGB.ToString();
        }

        private static string GetVirtualMemory()
        {
            var searcher = new ManagementObjectSearcher("SELECT TotalVirtualMemorySize FROM Win32_OperatingSystem");
            var obj = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (obj != null)
            {
                var totalVirtualMemoryKB = Convert.ToUInt64(obj["TotalVirtualMemorySize"]);
                var totalVirtualMemoryGB = (int)Math.Round(totalVirtualMemoryKB / 1024.0 / 1024);
                return totalVirtualMemoryGB.ToString();
            }
            return "0";
        }


        [SupportedOSPlatform("windows")]
        private static string GetSoundCardName()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return obj?["ProductName"]?.ToString() ?? "No disponible";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la tarjeta de sonido: {ex.Message}");
                return "No disponible";
            }
        }

        [SupportedOSPlatform("windows")]
        private static WorkstationUdfFields GetUdfFields(string clientId)
        {
            return new WorkstationUdfFields
            {
                udf_sline_6913 = GetSystemManufacturer(),
                udf_sline_6924 = GetEncryptionStatus(),
                udf_sline_6912 = GetBiosSerialNumber(),
                udf_sline_6915 = GetWmiProperty("Win32_Processor", "Name"),
                udf_sline_3003 = Environment.MachineName,
                udf_sline_6920 = GetDomain(),
                udf_sline_6911 = GetAntivirus(),
                udf_sline_6922 = GetPurchaseDate(),
                udf_sline_7802 = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                udf_sline_6910 = GetWmiProperty("Win32_OperatingSystem", "Caption"),
                udf_sline_7801 = GetRecoveryPassword(),
                udf_sline_6909 = GetDeviceType(),
                udf_sline_6917 = GetRAM(),
                udf_sline_6916 = GetStorage(),
                udf_sline_6919 = GetOfficeLicense(),
                udf_sline_6918 = GetWindowsLicense()
            };
        }

        [SupportedOSPlatform("windows")]
        private static string GetDomain()
        {
            return string.IsNullOrWhiteSpace(IPGlobalProperties.GetIPGlobalProperties().DomainName)
                ? Environment.MachineName
                : IPGlobalProperties.GetIPGlobalProperties().DomainName;
        }

        [SupportedOSPlatform("windows")]
        private static string GetWmiProperty(string wmiClass, string property)
        {
            try
            {
                var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return obj?[property]?.ToString() ?? "No disponible";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener propiedad WMI {property} de {wmiClass}: {ex.Message}");
                return "No disponible";
            }
        }


        [SupportedOSPlatform("windows")]
        public static string GetEncryptionStatus()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                    "SELECT ProtectionStatus FROM Win32_EncryptableVolume");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (obj != null && obj["ProtectionStatus"] != null)
                {
                    return Convert.ToInt32(obj["ProtectionStatus"]) == 1 ? "Encriptado" : "No Encriptado";
                }

                return "No disponible";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el estado de cifrado: {ex.Message}");
                return "No disponible";
            }
        }

        [SupportedOSPlatform("windows")]
        public static string GetAntivirus()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "root\\SecurityCenter2",
                    "SELECT displayName FROM AntivirusProduct");

                var antivirusName = searcher
                    .Get()
                    .Cast<ManagementObject>()
                    .Select(mo => mo["displayName"]?.ToString())
                    .FirstOrDefault(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        !name.Equals("Windows Defender", StringComparison.OrdinalIgnoreCase));

                return antivirusName ?? "No instalado";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el antivirus: {ex.Message}");
                return "No instalado";
            }
        }

        [SupportedOSPlatform("windows")]
        public static string GetPurchaseDate()
        {
            try
            {
                return Directory.GetCreationTime("C:\\Windows").ToString("yyyy-MM-dd HH:mm");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener la fecha de instalación: " + ex.Message);
            }
            return "Desconocido";
        }


        [SupportedOSPlatform("windows")]
        public static string GetRecoveryPassword()
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption");
                scope.Connect();

                var searcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));
                var results = searcher.Get().Cast<ManagementObject>(); // ✔️ Corrige S3217

                foreach (var volume in results)
                {
                    var outParams = volume.InvokeMethod("GetKeyProtectors", null, null);
                    if (outParams != null && outParams["KeyProtectorIDs"] is string[] protectorIds)
                    {
                        foreach (var protectorId in protectorIds)
                        {
                            var inParams = volume.GetMethodParameters("GetKeyProtectorRecoveryPassword");
                            inParams["KeyProtectorID"] = protectorId;

                            var recoveryResult = volume.InvokeMethod("GetKeyProtectorRecoveryPassword", inParams, null);

                            var recoveryPassword = recoveryResult?["RecoveryPassword"]?.ToString();
                            if (!string.IsNullOrEmpty(recoveryPassword))
                            {
                                return recoveryPassword;
                            }
                        }
                    }
                }

                return "No disponible";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la contraseña de recuperación: {ex.Message}");
                return "No disponible";
            }
        }

        [SupportedOSPlatform("windows")]
        public static int GetDeviceTypeCode()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT PCSystemType FROM Win32_ComputerSystem");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (obj != null && obj["PCSystemType"] != null)
                {
                    return Convert.ToInt32(obj["PCSystemType"]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el código del tipo de dispositivo: {ex.Message}");
            }
            return -1; // Código para "Desconocido" o error
        }

        [SupportedOSPlatform("windows")]
        public static string GetDeviceType()
        {
            var type = GetDeviceTypeCode();
            return type switch
            {
                0 => "Unspecified",
                1 => "Desktop",
                2 => "Laptop",
                3 => "Workstation",
                4 => "EnterpriseServer",
                5 => "SOHOServer",
                6 => "AppliancePC",
                7 => "PerformanceServer",
                _ => "Maximum"
            };
        }

        [SupportedOSPlatform("windows")]
        public static string GetRAM()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                double totalRAM = 0;
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    totalRAM += Convert.ToDouble(obj["Capacity"] ?? 0);
                }
                return Math.Round(totalRAM / 1e9, 2) + " GB";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la RAM: {ex.Message}");
                return "Desconocido";
            }
        }


        [SupportedOSPlatform("windows")]
        public static string GetStorage()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Size FROM Win32_DiskDrive");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (obj != null && obj["Size"] != null)
                {
                    var sizeGB = Convert.ToDouble(obj["Size"]) / 1e9;
                    return Math.Round(sizeGB, 2) + " GB";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el almacenamiento: {ex.Message}");
            }
            return "Desconocido";
        }


        [SupportedOSPlatform("windows")]
        public static string GetOfficeLicense()
        {
            try
            {
                string[] officeVersions = { "16.0", "15.0", "14.0", "12.0" };
                var basePath = @"SOFTWARE\Microsoft\Office\";

                foreach (var version in officeVersions)
                {
                    var path = basePath + version + @"\Registration";
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            var productKey = subKey?.GetValue("DigitalProductID");
                            if (productKey != null)
                            {
                                return $"Office {version} detectado";
                            }
                        }
                    }
                }

                return "No se encontró una licencia de Office";
            }
            catch (Exception ex)
            {
                return $"Error al obtener la licencia de Office: {ex.Message}";
            }
        }

        [SupportedOSPlatform("windows")]
        public static string GetWindowsLicense()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return obj?["OA3xOriginalProductKey"]?.ToString() ?? "No disponible";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la licencia de Windows: {ex.Message}");
                return "No disponible";
            }
        }

        [SupportedOSPlatform("windows")]
        private static List<Monitor_pc> GetMonitors()
        {
            var monitors = new List<Monitor_pc>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity  WHERE PNPClass = 'Monitor'");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                monitors.Add(new Monitor_pc
                {
                    serial_number = obj["PNPDeviceID"]?.ToString(),
                    monitor_type = obj["Name"]?.ToString(),
                    manufacturer = (string)(obj["Manufacturer"] ?? "(Desconocido)")
                });
            }

            return monitors;
        }

        [SupportedOSPlatform("windows")]
        private static List<UsbController> GetUSBControllers()
        {
            var controlers = new List<UsbController>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBController");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                controlers.Add(new UsbController
                {
                    name = obj["Name"]?.ToString(),
                });
            }
            return controlers;
        }

        [SupportedOSPlatform("windows")]
        static List<NetworkAdapter> GetNetworkAdapters()
        {
            var NetworkAdapters = new List<NetworkAdapter>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var ipAddresses = (string[])obj["IPAddress"];
                var subnets = (string[])obj["IPSubnet"];

                NetworkAdapters.Add(new NetworkAdapter
                {
                    description = obj["Description"]?.ToString(),
                    name = obj["Description"]?.ToString(),
                    ip_address = ipAddresses?[0],
                    mac_address = obj["MACAddress"]?.ToString(),
                    ipnet_mask = subnets?[0],
                    network = ipAddresses != null ? ipAddresses[0].Substring(0, ipAddresses[0].LastIndexOf('.') + 1) + "0" : null,
                    gateway = ((string[])obj["DefaultIPGateway"])?[0],
                    dhcp = (bool?)obj["DHCPEnabled"] ?? false,
                    dhcp_server = obj["DHCPServer"]?.ToString(),
                    nic_lease = DateTime.UtcNow.ToString("o")
                });
            }
            return NetworkAdapters;
        }

        [SupportedOSPlatform("windows")]
        public static string GetMemoryType()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory"))
                {
                    foreach (var mo in searcher.Get().Cast<ManagementObject>())
                    {
                        if (mo["SMBIOSMemoryType"] != null)
                        {
                            var smbiosType = Convert.ToUInt16(mo["SMBIOSMemoryType"]);
                            return TranslateMemoryType(smbiosType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error obteniendo tipo de memoria: " + ex.Message);
            }
            return "Unknown";
        }

        private static string TranslateMemoryType(ushort type)
        {
            return type switch
            {
                20 => "DDR",
                21 => "DDR2",
                22 => "DDR2 FB-DIMM",
                24 => "DDR3",
                26 => "DDR4",
                27 => "LPDDR",
                28 => "LPDDR2",
                29 => "LPDDR3",
                30 => "LPDDR4",
                31 => "Logical non-volatile device",
                32 => "HBM",
                33 => "HBM2",
                34 => "DDR5",
                _ => $"Other ({type})"
            };
        }

        [SupportedOSPlatform("windows")]
        static List<MemoryModule> GetMemoryModules()
        {
            var memoryModules = new List<MemoryModule>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                memoryModules.Add(new MemoryModule
                {
                    module_tag = obj["Tag"]?.ToString(),
                    capacity = obj["Capacity"]?.ToString(),
                    socket = obj["DeviceLocator"]?.ToString(),
                    bank_label = string.IsNullOrWhiteSpace(obj["BankLabel"]?.ToString()) ? "-" : obj["BankLabel"].ToString(),
                    memory_type = GetMemoryType(),
                    frequency = obj["Speed"]?.ToString()
                });
            }

            return memoryModules;
        }

        [SupportedOSPlatform("windows")]
        static List<Motherboard> GetMotherboards()
        {
            var motherb = new List<Motherboard>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                motherb.Add(new Motherboard
                {
                    manufacturer = obj["Manufacturer"]?.ToString(),
                    model = obj["Product"]?.ToString(),
                    serial_number = GetBiosSerialNumber(),
                    product = obj["Product"]?.ToString(),
                    version = obj["Version"]?.ToString(),
                });
            }

            return motherb;
        }

        [SupportedOSPlatform("windows")]
        static List<HardDisk> GetHardDisks()
        {
            var result = new List<HardDisk>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    result.Add(new HardDisk
                    {
                        model = obj["Model"]?.ToString(),
                        capacity = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : null,
                        serial_number = obj["SerialNumber"]?.ToString()
                    });
                }

            return result;
        }

        [SupportedOSPlatform("windows")]
        static List<LogicalDrive> GetLogicalDrives()
        {
            var result = new List<LogicalDrive>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                result.Add(new LogicalDrive
                {
                    name = obj["DeviceID"]?.ToString(),
                    file_system = obj["FileSystem"]?.ToString(),
                    capacity = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : null,
                    free_space = obj["FreeSpace"] != null ? Convert.ToInt64(obj["FreeSpace"]) : null
                });
            }

            return result;
        }

        [SupportedOSPlatform("windows")]
        static List<Mouse> GetMice()
        {
            var result = new List<Mouse>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PointingDevice");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var manufacturer = obj["Manufacturer"]?.ToString() ?? "N/A";
                var deviceId = obj["DeviceID"]?.ToString() ?? "N/A";

                var vid = Regex.Match(deviceId, @"VID[_&]?([0-9A-Fa-f]+)").Groups[1].Value;
                var pid = Regex.Match(deviceId, @"PID[_&]?([0-9A-Fa-f]+)").Groups[1].Value;

                var connectionType = GetConnectionType(deviceId);

                try
                {
                    result.Add(new Mouse
                    {
                        mouse_manufacturer = manufacturer,
                        mouse_type = connectionType,
                        mouse_serial_number = "N/A"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error procesando dispositivo de entrada: " + ex.Message);
                }
            }

            return result;
        }

        [SupportedOSPlatform("windows")]
        static Keyboard GetKeyboard()
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Keyboard");
            var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (obj != null)
            {
                var manufacturer = obj["Description"]?.ToString() ?? "N/A";
                var deviceId = obj["DeviceID"]?.ToString() ?? "N/A";

                var vid = Regex.Match(deviceId, @"VID[_&]?([0-9A-Fa-f]+)").Groups[1].Value;
                var pid = Regex.Match(deviceId, @"PID[_&]?([0-9A-Fa-f]+)").Groups[1].Value;

                var connectionType = GetConnectionType(deviceId);

                return new Keyboard
                {
                    keyboard_manufacturer = manufacturer,
                    keyboard_type = connectionType,
                    keyboard_serial_number = "N/A"
                };
            }

            return new Keyboard
            {
                keyboard_manufacturer = "Desconocido",
                keyboard_type = "Desconocido",
                keyboard_serial_number = "N/A"
            };
        }

        [SupportedOSPlatform("windows")]
        static List<Port> GetPorts()
        {
            var ports = new List<Port>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB%'");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                if (obj["Name"] != null)
                {
                    ports.Add(new Port
                    {
                        name = obj["Name"]?.ToString(),
                        status = obj["Status"]?.ToString()
                    });
                }
            }

            return ports;
        }

        static string GetConnectionType(string deviceId)
        {
            if (deviceId.Contains("BTH") || deviceId.Contains("00001812") || deviceId.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                return "Bluetooth";
            else if (deviceId.Contains("USB", StringComparison.OrdinalIgnoreCase) || deviceId.Contains("VID"))
                return "USB";
            else if (deviceId.Contains("PS2") || deviceId.Contains("i8042"))
                return "PS/2";
            else
                return "Desconocido";
        }
    }
}

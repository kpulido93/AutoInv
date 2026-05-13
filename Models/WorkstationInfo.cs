using System.Runtime.Versioning;

using Newtonsoft.Json;

namespace AutoInventario.Models
{
    [SupportedOSPlatform("windows")]
    public class WorkstationInfo
    {
        public Workstation? workstation { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class Workstation
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? org_serial_number { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Processor>? processors { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<PhysicalDrive>? physical_drives { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Domain? domain { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public UdfFields? udf_fields { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? is_remote_control_prompt_enabled { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? manufacturer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? is_server { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ServerInventory? server_inventory { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OperatingSystemInfo? operating_system { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Product? product { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<UserAccount>? user_accounts { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? primary_ip { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public WorkstationUdfFields? workstation_udf_fields { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? asset_tag { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? free_slots { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? allowed_vms { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? vm_host { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? chassis_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public MemoryInfo? memory { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? last_logged_user { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ip_addresses { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SoundCard? sound_card { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public AcquisitionDate? acquisition_date { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? logical_cpu_count { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ComputerSystem? computer_system { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Monitor_pc>? monitors { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<UsbController>? usb_controllers { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<NetworkAdapter>? network_adapters { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<MemoryModule>? memory_modules { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Motherboard>? motherboards { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<HardDisk>? hard_disks { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<LogicalDrive>? logical_drives { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Mouse>? mouse { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Keyboard? keyboard { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Port>? ports { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class Processor
    {
        public string? number_of_cores { get; set; }
        public string? name { get; set; }
        public string? cpu_manufacturer { get; set; }
        public string? model { get; set; }
        public string? serial_number { get; set; }
        public string? family { get; set; }
        public string? core_count { get; set; }
        public string? speed { get; set; }
        public string? manufacturer { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class PhysicalDrive
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? drive_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? provider { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? version { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? manufacturer { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class Domain {
        public string? name { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class UdfFields
    {
        public string? udf_sline_8401 { get; set; }
        public string? udf_sline_9901 { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class OperatingSystemInfo
    {
        public string? os_license_type { get; set; }
        public string? os { get; set; }
        public string? service_pack { get; set; }
        public string? product_id { get; set; }
        public string? build_number { get; set; }
        public string? os_license_status { get; set; }
        public string? version { get; set; }
        public string? os_system_drive { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class Product
    {
        public string? part_no { get; set; }
        public ProductType? product_type { get; set; }
        public string? manufacturer { get; set; }
        public int? id { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class ProductType { public int? id { get; set; } }
    [SupportedOSPlatform("windows")]
    public class UserAccount
    {
        public string? full_name { get; set; }
        public string? domain { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? status { get; set; }
        public string? sid { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class WorkstationUdfFields
    {
        public string? udf_sline_6913 { get; set; }
        public string? udf_sline_6924 { get; set; }
        public string? udf_sline_6912 { get; set; }
        public string? udf_sline_6915 { get; set; }
        public string? udf_sline_3003 { get; set; }
        public string? udf_sline_6920 { get; set; }
        public string? udf_sline_6911 { get; set; }
        public string? udf_sline_6922 { get; set; }
        public string? udf_sline_7802 { get; set; }
        public string? udf_sline_6910 { get; set; }
        public string? udf_sline_7801 { get; set; }
        public string? udf_sline_6909 { get; set; }
        public string? udf_sline_6917 { get; set; }
        public string? udf_sline_6916 { get; set; }
        public string? udf_sline_6919 { get; set; }
        public string? udf_sline_6918 { get; set; }
        public string? udf_pick_3002 { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class MemoryInfo
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? virtual_memory { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? physical_memory { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class SoundCard {
        public string? sound_card_name { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class AcquisitionDate {
        public string? display_value { get; set; } public string? value { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class CiDefaultFields {
        public string? udf_pickref_108 { get; set; }
    }
    [SupportedOSPlatform("windows")]
    public class ComputerSystem
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? system_manufacturer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? service_tag { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? model { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? bios_date { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? bios_version { get; set; }
    }
    public class Monitor_pc
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? serial_number { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? monitor_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? manufacturer { get; set; }
    }

    public class UsbController
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
    }

    public class NetworkAdapter
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? description { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
        public string? ip_address { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? mac_address { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ipnet_mask { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? network { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? gateway { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? dhcp { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? dhcp_server { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? nic_lease { get; set; }
    }

    public class MemoryModule
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? module_tag { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? capacity { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? socket { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? bank_label { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? memory_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? frequency { get; set; }
    }

    public class Motherboard
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? manufacturer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? model { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? serial_number { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? product { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? version { get; set; }
    }

    public class HardDisk
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? model { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? capacity { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? serial_number { get; set; }
    }

    public class LogicalDrive
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? file_system { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? capacity { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? free_space { get; set; }
    }

    public class Mouse
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? mouse_manufacturer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? mouse_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? mouse_serial_number { get; set; }
    }

    public class Keyboard
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? keyboard_manufacturer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? keyboard_type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? keyboard_serial_number { get; set; }
    }

    public class Port
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? status { get; set; }
    }
}

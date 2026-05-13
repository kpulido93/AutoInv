using System.Runtime.Versioning;

using Newtonsoft.Json;

namespace AutoInventario.Models
{
    [SupportedOSPlatform("windows")]
    public class ServerInventory
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? os_caption { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? os_version { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? os_build { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? part_of_domain { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? domain { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? workgroup { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? last_boot_time_utc { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? uptime_seconds { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ServerRoleFeature>? roles_features { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<TrackedWindowsService>? tracked_services { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public class ServerRoleFeature
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public class TrackedWindowsService
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? display_name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? state { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? start_mode { get; set; }
    }
}

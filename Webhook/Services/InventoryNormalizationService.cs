using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Webhook.Models;

namespace Webhook.Services;

public sealed class InventoryNormalizationService : IInventoryNormalizer
{
    private static readonly Regex NonPrintableCharacters = new("[^\\x20-\\x7E]", RegexOptions.Compiled);

    public InventoryNormalizationResult Normalize(string decryptedJson, string clientId)
    {
        if (!int.TryParse(clientId, out var parsedClientId))
        {
            return InventoryNormalizationResult.Failure("InvalidClientId", "clientID must be a numeric value.");
        }

        if (string.IsNullOrWhiteSpace(decryptedJson))
        {
            return InventoryNormalizationResult.Failure("EmptyPayload", "Decrypted payload is empty.");
        }

        JsonNode rootNode;
        try
        {
            rootNode = JsonNode.Parse(decryptedJson);
        }
        catch (JsonException)
        {
            return InventoryNormalizationResult.Failure("InvalidJson", "Decrypted payload is not valid JSON.");
        }

        if (rootNode is not JsonObject root)
        {
            return InventoryNormalizationResult.Failure("InvalidPayload", "Decrypted payload root must be an object.");
        }

        RemoveNullValues(root);

        if (!root.TryGetPropertyValue("workstation", out var workstationNode) ||
            workstationNode is not JsonObject workstation)
        {
            return InventoryNormalizationResult.Failure("MissingWorkstation", "Payload does not include a workstation object.");
        }

        var hostname = GetString(workstation, "name");
        ReplaceDefaultStrings(root, hostname);
        NormalizeWorkstation(workstation);

        var serialNumber = GetString(workstation, "org_serial_number");
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return InventoryNormalizationResult.Failure("MissingSerialNumber", "Workstation serial number is required.");
        }

        var connectorPayload = BuildCreateWorkstationPayload(workstation, parsedClientId, serialNumber, hostname);
        return InventoryNormalizationResult.Success(
            new NormalizedInventory(parsedClientId, serialNumber, hostname, root, workstation, connectorPayload));
    }

    private static void NormalizeWorkstation(JsonObject workstation)
    {
        NormalizePorts(workstation);
        NormalizeHardDisks(workstation);
        NormalizePhysicalDrives(workstation);
    }

    private static void NormalizePorts(JsonObject workstation)
    {
        if (!workstation.TryGetPropertyValue("ports", out var portsNode) || portsNode is not JsonArray ports)
        {
            return;
        }

        for (var i = ports.Count - 1; i >= 0; i--)
        {
            if (ports[i] is JsonObject port && !string.IsNullOrWhiteSpace(GetString(port, "name")))
            {
                continue;
            }

            ports.RemoveAt(i);
        }
    }

    private static void NormalizeHardDisks(JsonObject workstation)
    {
        if (!workstation.TryGetPropertyValue("hard_disks", out var disksNode) || disksNode is not JsonArray disks)
        {
            return;
        }

        foreach (var diskNode in disks)
        {
            if (diskNode is not JsonObject disk)
            {
                continue;
            }

            if (!disk.ContainsKey("capacity"))
            {
                disk["capacity"] = 0;
            }

            var serialNumber = GetString(disk, "serial_number");
            disk["serial_number"] = string.IsNullOrWhiteSpace(serialNumber) ? "UNKNOWN" : serialNumber;
        }
    }

    private static void NormalizePhysicalDrives(JsonObject workstation)
    {
        if (!workstation.TryGetPropertyValue("physical_drives", out var drivesNode) ||
            drivesNode is not JsonArray drives)
        {
            return;
        }

        foreach (var driveNode in drives)
        {
            if (driveNode is not JsonObject drive)
            {
                continue;
            }

            if (drive.TryGetPropertyValue("version", out var versionNode) &&
                versionNode is JsonValue versionValue &&
                versionValue.TryGetValue<string>(out var versionText))
            {
                var cleanVersion = NonPrintableCharacters.Replace(versionText, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cleanVersion))
                {
                    drive.Remove("version");
                }
                else
                {
                    drive["version"] = cleanVersion;
                }
            }
            else
            {
                drive.Remove("version");
            }

            var provider = GetString(drive, "provider");
            if (provider.Length > 99)
            {
                drive["provider"] = provider[..99];
            }
        }
    }

    private static JsonObject BuildCreateWorkstationPayload(
        JsonObject workstation,
        int clientId,
        string serialNumber,
        string hostname)
    {
        var payloadWorkstation = new JsonObject
        {
            ["name"] = string.IsNullOrWhiteSpace(hostname) ? serialNumber : hostname,
            ["org_serial_number"] = serialNumber,
            ["account"] = new JsonObject
            {
                ["id"] = clientId
            },
            ["state"] = new JsonObject
            {
                ["name"] = "Pending",
                ["id"] = "601"
            }
        };

        CopyComputerSystem(workstation, payloadWorkstation);

        foreach (var propertyName in new[]
                 {
                     "product",
                     "processors",
                     "physical_drives",
                     "operating_system",
                     "user_accounts",
                     "workstation_udf_fields",
                     "asset_tag",
                     "allowed_vms",
                     "vm_host",
                     "memory",
                     "last_logged_user",
                     "sound_card",
                     "acquisition_date",
                     "logical_cpu_count",
                     "is_remote_control_prompt_enabled",
                     "is_server"
                 })
        {
            CopyIfExists(workstation, payloadWorkstation, propertyName);
        }

        return new JsonObject
        {
            ["workstation"] = payloadWorkstation
        };
    }

    private static void CopyComputerSystem(JsonObject source, JsonObject target)
    {
        if (!source.TryGetPropertyValue("computer_system", out var node) || node is null)
        {
            return;
        }

        var cloned = node.DeepClone();
        if (cloned is JsonObject computerSystem)
        {
            computerSystem.Remove("bios_date");
        }

        target["computer_system"] = cloned;
    }

    private static void CopyIfExists(JsonObject source, JsonObject target, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var node) && node is not null)
        {
            target[propertyName] = node.DeepClone();
        }
    }

    private static void RemoveNullValues(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is null)
                {
                    obj.Remove(property.Key);
                    continue;
                }

                RemoveNullValues(property.Value);
            }

            return;
        }

        if (node is JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (array[i] is null)
                {
                    array.RemoveAt(i);
                    continue;
                }

                RemoveNullValues(array[i]);
            }
        }
    }

    private static void ReplaceDefaultStrings(JsonNode node, string nameValue)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    obj[property.Key] = ReplaceDefaultString(text, nameValue);
                    continue;
                }

                if (property.Value is not null)
                {
                    ReplaceDefaultStrings(property.Value, nameValue);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    array[i] = ReplaceDefaultString(text, nameValue);
                    continue;
                }

                if (array[i] is not null)
                {
                    ReplaceDefaultStrings(array[i], nameValue);
                }
            }
        }
    }

    private static string ReplaceDefaultString(string value, string nameValue)
    {
        var suffix = nameValue ?? string.Empty;
        var replaced = Regex.Replace(value, "defaultstring", $"DEFAULTSTRING-{suffix}", RegexOptions.IgnoreCase);
        replaced = Regex.Replace(replaced, "SystemSerialNumber", $"SystemSerialNumber-{suffix}", RegexOptions.IgnoreCase);
        return Regex.Replace(replaced, "TobefilledbyO\\.E\\.M", $"TobefilledbyO.E.M-{suffix}", RegexOptions.IgnoreCase);
    }

    private static string GetString(JsonObject source, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var node) &&
            node is JsonValue value &&
            value.TryGetValue<string>(out var text))
        {
            return text?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }
}

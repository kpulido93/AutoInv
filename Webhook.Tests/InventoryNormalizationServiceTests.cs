using Microsoft.Extensions.Logging.Abstractions;
using Webhook.Models;
using Webhook.Services;

namespace Webhook.Tests;

public class InventoryNormalizationServiceTests
{
    [Fact]
    public void Normalize_valid_payload_applies_lambda_compatible_rules()
    {
        var normalizer = new InventoryNormalizationService();
        var providerName = new string('P', 120);

        var result = normalizer.Normalize(
            $$"""
            {
              "workstation": {
                "name": "PC-001",
                "org_serial_number": "SER-001",
                "asset_tag": null,
                "ports": [
                  { "name": "COM1" },
                  { "name": "" },
                  { "description": "missing name" }
                ],
                "hard_disks": [
                  { "serial_number": "   " }
                ],
                "physical_drives": [
                  { "version": "FW\u0001\n1", "provider": "{{providerName}}" }
                ],
                "computer_system": {
                  "bios_date": "2024-01-01",
                  "model": "defaultstring"
                },
                "product": {
                  "name": "SystemSerialNumber"
                }
              }
            }
            """,
            "42");

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Inventory.ClientId);
        Assert.Equal("SER-001", result.Inventory.SerialNumber);

        var ports = result.Inventory.Workstation["ports"]!.AsArray();
        Assert.Single(ports);

        var disk = result.Inventory.Workstation["hard_disks"]![0]!.AsObject();
        Assert.Equal(0, disk["capacity"]!.GetValue<int>());
        Assert.Equal("UNKNOWN", disk["serial_number"]!.GetValue<string>());

        var drive = result.Inventory.Workstation["physical_drives"]![0]!.AsObject();
        Assert.Equal("FW1", drive["version"]!.GetValue<string>());
        Assert.Equal(99, drive["provider"]!.GetValue<string>().Length);

        var connectorWorkstation = result.Inventory.ConnectorPayload["workstation"]!.AsObject();
        Assert.False(connectorWorkstation["computer_system"]!.AsObject().ContainsKey("bios_date"));
        Assert.Equal(42, connectorWorkstation["account"]!["id"]!.GetValue<int>());
        Assert.Equal("DEFAULTSTRING-PC-001", connectorWorkstation["computer_system"]!["model"]!.GetValue<string>());
        Assert.Equal("SystemSerialNumber-PC-001", connectorWorkstation["product"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Local_processor_submits_normalized_payload_without_aws()
    {
        var connector = new RecordingAssetConnector(new AssetConnectorResult("test-submit", 201, true));
        var processor = new LocalInventoryProcessor(
            new InventoryNormalizationService(),
            connector,
            NullLogger<LocalInventoryProcessor>.Instance);

        var result = await processor.ProcessAsync(new InventoryProcessingRequest(
            "42",
            "legacy",
            "encrypted-data",
            "encrypted-key",
            "iv",
            "",
            "{\"workstation\":{\"name\":\"PC-001\",\"org_serial_number\":\"SER-001\"}}",
            "cid-test-001"));

        Assert.Equal("Evento procesado correctamente.", result.ResponseMessage);
        Assert.Equal(1, connector.Calls);
        Assert.Equal("SER-001", connector.LastInventory.SerialNumber);
    }

    [Fact]
    public async Task Local_processor_rejects_invalid_payload_without_calling_connector()
    {
        var connector = new RecordingAssetConnector(new AssetConnectorResult("test-submit", 201, true));
        var processor = new LocalInventoryProcessor(
            new InventoryNormalizationService(),
            connector,
            NullLogger<LocalInventoryProcessor>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(new InventoryProcessingRequest(
            "42",
            "legacy",
            "encrypted-data",
            "encrypted-key",
            "iv",
            "",
            "{\"workstation\":{\"name\":\"PC-001\"}}",
            "cid-test-001")));

        Assert.Equal(0, connector.Calls);
    }

    private sealed class RecordingAssetConnector : IAssetConnector
    {
        private readonly AssetConnectorResult _result;

        public RecordingAssetConnector(AssetConnectorResult result)
        {
            _result = result;
        }

        public int Calls { get; private set; }

        public NormalizedInventory LastInventory { get; private set; } = null!;

        public Task<AssetConnectorResult> SubmitAsync(
            NormalizedInventory inventory,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastInventory = inventory;
            return Task.FromResult(_result);
        }
    }
}

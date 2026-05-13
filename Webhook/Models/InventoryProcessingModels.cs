using System.Text.Json.Nodes;

namespace Webhook.Models;

public sealed class NormalizedInventory
{
    public NormalizedInventory(
        int clientId,
        string serialNumber,
        string hostname,
        JsonObject sourcePayload,
        JsonObject workstation,
        JsonObject connectorPayload)
    {
        ClientId = clientId;
        SerialNumber = serialNumber;
        Hostname = hostname;
        SourcePayload = sourcePayload;
        Workstation = workstation;
        ConnectorPayload = connectorPayload;
    }

    public int ClientId { get; }

    public string SerialNumber { get; }

    public string Hostname { get; }

    public JsonObject SourcePayload { get; }

    public JsonObject Workstation { get; }

    public JsonObject ConnectorPayload { get; }
}

public sealed class InventoryNormalizationResult
{
    private InventoryNormalizationResult(bool succeeded, NormalizedInventory inventory, string errorCode, string errorMessage)
    {
        Succeeded = succeeded;
        Inventory = inventory;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public NormalizedInventory Inventory { get; }

    public string ErrorCode { get; }

    public string ErrorMessage { get; }

    public static InventoryNormalizationResult Success(NormalizedInventory inventory)
        => new(true, inventory, string.Empty, string.Empty);

    public static InventoryNormalizationResult Failure(string errorCode, string errorMessage)
        => new(false, null, errorCode, errorMessage);
}

public sealed class AssetConnectorResult
{
    public AssetConnectorResult(string operation, int statusCode, bool succeeded)
    {
        Operation = operation;
        StatusCode = statusCode;
        Succeeded = succeeded;
    }

    public string Operation { get; }

    public int StatusCode { get; }

    public bool Succeeded { get; }
}

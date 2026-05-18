using Microsoft.Extensions.Logging.Abstractions;
using Webhook.Services;

namespace Webhook.Tests;

public class LocalInventoryProcessorTests
{
    [Fact]
    public async Task ProcessAsync_returns_stub_message_without_external_sync()
    {
        var processor = new LocalInventoryProcessor(NullLogger<LocalInventoryProcessor>.Instance);

        var result = await processor.ProcessAsync(new InventoryProcessingRequest(
            "42",
            "legacy",
            "encrypted-data",
            "encrypted-key",
            "iv",
            "",
            "{\"workstation\":{\"name\":\"PC-001\"}}",
            "cid-test-001"));

        Assert.Equal(
            "Procesado localmente en modo stub. No sincronizado aun con ManageEngine.",
            result.ResponseMessage);
    }
}

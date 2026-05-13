using System.Threading;
using System.Threading.Tasks;
using Webhook.Models;

namespace Webhook.Services;

public interface IAssetConnector
{
    Task<AssetConnectorResult> SubmitAsync(NormalizedInventory inventory, CancellationToken cancellationToken = default);
}

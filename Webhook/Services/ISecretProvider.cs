using System.Threading;
using System.Threading.Tasks;

namespace Webhook.Services;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}

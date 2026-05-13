using System.Threading;
using System.Threading.Tasks;

using Webhook.Models;

namespace Webhook.Services;

public interface ILicenseValidator
{
    ValueTask<LicenseValidationResult> ValidateAsync(
        LicenseValidationContext context,
        CancellationToken cancellationToken = default);
}

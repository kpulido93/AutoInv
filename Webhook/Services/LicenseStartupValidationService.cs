using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Webhook.Models;
using Webhook.Options;

namespace Webhook.Services;

public sealed class LicenseStartupValidationService : IHostedService
{
    private readonly ILicenseValidator _licenseValidator;
    private readonly IOptions<LicenseOptions> _options;
    private readonly ILogger<LicenseStartupValidationService> _logger;

    public LicenseStartupValidationService(
        ILicenseValidator licenseValidator,
        IOptions<LicenseOptions> options,
        ILogger<LicenseStartupValidationService> logger)
    {
        _licenseValidator = licenseValidator;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.ValidateOnStartup)
        {
            _logger.LogInformation("License startup validation is disabled.");
            return;
        }

        var result = await _licenseValidator.ValidateAsync(new LicenseValidationContext(), cancellationToken);
        if (result.IsValid)
        {
            _logger.LogInformation(
                "License validation succeeded. Edition: {Edition}. MaxEndpoints: {MaxEndpoints}.",
                result.Details.Edition,
                result.Details.MaxEndpoints);
            return;
        }

        if (ProductEditions.RequiresSignedLicense(options.Edition) ||
            !ProductEditions.IsKnown(options.Edition))
        {
            throw new InvalidOperationException(
                $"License validation failed for commercial or invalid edition. ErrorCode: {result.ErrorCode}.");
        }

        _logger.LogWarning(
            "Development license validation failed but startup remains allowed. ErrorCode: {ErrorCode}.",
            result.ErrorCode);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}

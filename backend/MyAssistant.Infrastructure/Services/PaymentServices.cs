using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Services;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger) => _logger = logger;

    public Task<PaymentResult> CreateCheckoutAsync(Guid userId, Guid planId, string billingPeriod, string platform)
    {
        _logger.LogInformation("Mock checkout created for user {UserId} plan {PlanId} period {Period} platform {Platform}", userId, planId, billingPeriod, platform);
        var reference = $"mock_{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentResult(true, "Checkout ready", reference, reference, null));
    }

    public Task<PaymentResult> VerifyAsync(Guid userId, string providerReference, string receipt)
    {
        _logger.LogInformation("Mock payment verified for user {UserId} ref {Reference}", userId, providerReference);
        return Task.FromResult(new PaymentResult(true, "Payment verified", providerReference, receipt, null));
    }

    public Task<PaymentResult> RestoreAsync(Guid userId, string platform)
    {
        _logger.LogInformation("Mock restore for user {UserId} platform {Platform}", userId, platform);
        return Task.FromResult(new PaymentResult(true, "No active purchase to restore", null, null, null));
    }
}

public class GooglePlayPaymentService : IPaymentService
{
    private readonly ILogger<GooglePlayPaymentService> _logger;
    public GooglePlayPaymentService(ILogger<GooglePlayPaymentService> logger) => _logger = logger;

    public Task<PaymentResult> CreateCheckoutAsync(Guid userId, Guid planId, string billingPeriod, string platform)
        => throw new NotSupportedException("Google Play Billing requires native app component; use server-side verification in VerifyAsync.");

    public Task<PaymentResult> VerifyAsync(Guid userId, string providerReference, string receipt)
    {
        _logger.LogWarning("Google Play verification requested without service account credentials");
        return Task.FromResult(new PaymentResult(false, "Google Play verification requires service account credentials. Configure GOOGLE_PLAY_SERVICE_ACCOUNT_JSON.", null, null, "GOOGLE_PLAY_CREDENTIALS_MISSING"));
    }

    public Task<PaymentResult> RestoreAsync(Guid userId, string platform)
        => Task.FromResult(new PaymentResult(false, "Restore handled client-side via Play Billing", null, null, null));
}

public class AppleStoreKitPaymentService : IPaymentService
{
    private readonly ILogger<AppleStoreKitPaymentService> _logger;
    public AppleStoreKitPaymentService(ILogger<AppleStoreKitPaymentService> logger) => _logger = logger;

    public Task<PaymentResult> VerifyAsync(Guid userId, string providerReference, string receipt)
    {
        _logger.LogWarning("App Store verification requested without credentials");
        return Task.FromResult(new PaymentResult(false, "App Store verification requires Apple App Store Server API credentials. Configure APPLE_BUNDLE_ID and APPLE_ISSUER_ID.", null, null, "APPLE_CREDENTIALS_MISSING"));
    }

    public Task<PaymentResult> CreateCheckoutAsync(Guid userId, Guid planId, string billingPeriod, string platform)
        => Task.FromResult(new PaymentResult(false, "Apple purchases handled by StoreKit on device", null, null, null));

    public Task<PaymentResult> RestoreAsync(Guid userId, string platform)
        => Task.FromResult(new PaymentResult(false, "Restore handled client-side via StoreKit", null, null, null));
}
using Amazon.S3;
using Amazon.S3.Model;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure.Services.Auth;
using FeedbackHub.Infrastructure.Services.RateLimiting;

namespace FeedbackHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSecurity();
        services.AddContext();
        services.AddCaching(configuration);
        services.AddRateLimiterStore(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddStorage(configuration);
        services.AddEmail(configuration);
        services.AddPush(configuration);
        services.AddExternalAuth(configuration);
        services.AddBackgroundJobs(configuration);
        services.AddNotificationChannels();
        services.AddRealtime(configuration);
        return services;
    }

    // Recurring jobs must be scheduled against a resolved IRecurringJobManager, not the static
    // RecurringJob facade — JobStorage.Current isn't populated until the DI container is built,
    // so this can only run after app.Build(), not inside AddInfrastructure.
    public static void UseBackgroundJobs(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IRecurringJobManager recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        RefreshTokenCleanupSettings cleanupSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<RefreshTokenCleanupSettings>>().Value;

        // Cronos rejects a step >= the field's range (e.g. "*/24" for hours, 0-23), so a 24h
        // interval — the default — is expressed as a fixed daily run instead of a step.
        string cronExpression = cleanupSettings.IntervalHours >= 24
            ? "0 0 * * *"
            : $"0 */{cleanupSettings.IntervalHours} * * *";

        recurringJobManager.AddOrUpdate<RefreshTokenCleanupJob>(
            "refresh-token-cleanup",
            job => job.RunAsync(CancellationToken.None),
            cronExpression);
    }

    // The MinIO bucket must exist before the first upload: its S3 API does not create one on PutObject.
    // No-op for Local storage.
    public static Task UseStorageAsync(this IApplicationBuilder app) =>
        EnsureStorageAsync(app.ApplicationServices, CancellationToken.None);

    /// <summary>Whether <c>StorageSettings:Provider</c> names Local storage, in any case; throws for an unknown provider.</summary>
    public static bool IsLocalStorage(string provider) =>
        StorageExtensions.ResolveProviderKey(provider) == StorageExtensions.LocalProvider;

    internal static async Task EnsureStorageAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        StorageSettings storageSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageSettings>>().Value;

        if (StorageExtensions.ResolveProviderKey(storageSettings.Provider) == StorageExtensions.MinioProvider)
        {
            await EnsureBucketAsync(
                scope.ServiceProvider.GetRequiredKeyedService<IAmazonS3>(StorageExtensions.MinioProvider),
                scope.ServiceProvider.GetRequiredService<IOptions<MinioSettings>>().Value.BucketName,
                cancellationToken);
        }
    }

    internal static async Task EnsureBucketAsync(IAmazonS3 s3Client, string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "BucketAlreadyOwnedByYou")
        {
            // Already ours: an earlier start, or another instance that won the race to create it.
        }

        // Only public/ is readable without credentials; every other key stays reachable through the API alone.
        await s3Client.PutBucketPolicyAsync(
            new PutBucketPolicyRequest
            {
                BucketName = bucketName,
                Policy = $$"""
                    {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucketName}}/public/*"]}]}
                    """
            },
            cancellationToken);
    }
}

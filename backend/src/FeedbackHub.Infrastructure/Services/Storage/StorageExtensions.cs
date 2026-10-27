using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure.Services.Storage;

namespace FeedbackHub.Infrastructure;

internal static class StorageExtensions
{
    internal const string LocalProvider = "Local";
    internal const string MinioProvider = "MinIO";

    internal static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        StorageSettings storageSettings = configuration
            .GetSection(nameof(StorageSettings))
            .Get<StorageSettings>() ?? new StorageSettings();
        ResolveProviderKey(storageSettings.Provider);

        services.AddSingleton<StoragePathGenerator>();
        services.Configure<StorageSettings>(configuration.GetSection(nameof(StorageSettings)));
        services.Configure<MinioSettings>(configuration.GetSection(nameof(MinioSettings)));

        services.AddKeyedScoped<IStorageProvider, LocalFileProvider>(LocalProvider);

        // ForcePathStyle is required for any non-AWS S3-compatible endpoint such as MinIO: without it the SDK
        // builds virtual-hosted-style URLs (bucket.host) that only real AWS resolves.
        services.AddKeyedSingleton<IAmazonS3>(MinioProvider, (serviceProvider, _) =>
        {
            MinioSettings minioSettings = serviceProvider
                .GetRequiredService<IOptions<MinioSettings>>()
                .Value;

            AmazonS3Config config = new()
            {
                ServiceURL = minioSettings.ServiceUrl,
                ForcePathStyle = true
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(minioSettings.AccessKey, minioSettings.SecretKey),
                config);
        });
        services.AddKeyedScoped<IStorageProvider>(MinioProvider, (serviceProvider, _) =>
            new MinioFileProvider(
                serviceProvider.GetRequiredKeyedService<IAmazonS3>(MinioProvider),
                serviceProvider.GetRequiredService<IOptions<MinioSettings>>(),
                serviceProvider.GetRequiredService<StoragePathGenerator>()));

        services.AddScoped<IStorageService>(serviceProvider =>
        {
            StorageSettings resolved = serviceProvider
                .GetRequiredService<IOptions<StorageSettings>>()
                .Value;
            IStorageProvider provider = serviceProvider.GetRequiredKeyedService<IStorageProvider>(
                ResolveProviderKey(resolved.Provider));

            return new StorageService(provider);
        });

        return services;
    }

    /// <summary>The canonical provider name for a configured value in any case; throws for any other value.</summary>
    internal static string ResolveProviderKey(string provider)
    {
        if (string.Equals(provider, LocalProvider, StringComparison.OrdinalIgnoreCase))
        {
            return LocalProvider;
        }

        if (string.Equals(provider, MinioProvider, StringComparison.OrdinalIgnoreCase))
        {
            return MinioProvider;
        }

        throw new InvalidOperationException(
            $"Unknown StorageSettings:Provider '{provider}'. Supported values: {LocalProvider}, {MinioProvider}.");
    }
}

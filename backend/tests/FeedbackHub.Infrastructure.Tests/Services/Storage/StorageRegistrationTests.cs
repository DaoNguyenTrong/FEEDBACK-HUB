using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Tests.Services.Storage;

public class StorageRegistrationTests
{
    private const string BucketName = "feedbackhub";

    private sealed record Fixture(ServiceProvider Services, IAmazonS3 S3Client) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    /// <summary>The real storage registration with only the MinIO S3 client replaced, so no request leaves the test.</summary>
    private static Fixture CreateFixture(string provider)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageSettings:Provider"] = provider,
                ["MinioSettings:BucketName"] = BucketName
            })
            .Build();
        IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        ServiceCollection services = new();
        services.AddStorage(configuration);
        services.AddKeyedSingleton(StorageExtensions.MinioProvider, s3Client);

        return new Fixture(services.BuildServiceProvider(), s3Client);
    }

    // ResolveProviderKey

    [Theory]
    [InlineData("Local", StorageExtensions.LocalProvider)]
    [InlineData("local", StorageExtensions.LocalProvider)]
    [InlineData("MinIO", StorageExtensions.MinioProvider)]
    [InlineData("minio", StorageExtensions.MinioProvider)]
    [InlineData("MINIO", StorageExtensions.MinioProvider)]
    public void ResolveProviderKey_KnownNameInAnyCase_ReturnsTheCanonicalName(string provider, string expected)
    {
        Assert.Equal(expected, StorageExtensions.ResolveProviderKey(provider));
    }

    [Theory]
    [InlineData("SeaweedFS")]
    [InlineData("S3")]
    [InlineData("")]
    [InlineData(" MinIO ")]
    public void ResolveProviderKey_UnknownName_Throws(string provider)
    {
        Assert.Throws<InvalidOperationException>(() => StorageExtensions.ResolveProviderKey(provider));
    }

    // AddStorage

    [Fact]
    public void AddStorage_UnknownProvider_ThrowsAtRegistration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["StorageSettings:Provider"] = "SeaweedFS" })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStorage(configuration));
    }

    [Fact]
    public async Task AddStorage_MinioInAnotherCase_StorageServiceUsesMinio()
    {
        await using Fixture f = CreateFixture("minio");
        await using AsyncServiceScope scope = f.Services.CreateAsyncScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        await storage.ExistsAsync("public/avatars/a.png", CancellationToken.None);

        await f.S3Client.Received(1).GetObjectMetadataAsync(BucketName, "public/avatars/a.png", Arg.Any<CancellationToken>());
    }

    // IsLocalStorage

    [Theory]
    [InlineData("Local", true)]
    [InlineData("local", true)]
    [InlineData("MinIO", false)]
    [InlineData("minio", false)]
    public void IsLocalStorage_Provider_Expected(string provider, bool expected)
    {
        Assert.Equal(expected, DependencyInjection.IsLocalStorage(provider));
    }

    // EnsureStorageAsync

    [Fact]
    public async Task EnsureStorageAsync_MinioInAnotherCase_PreparesTheBucket()
    {
        await using Fixture f = CreateFixture("minio");

        await DependencyInjection.EnsureStorageAsync(f.Services, CancellationToken.None);

        await f.S3Client.Received(1).PutBucketPolicyAsync(
            Arg.Is<PutBucketPolicyRequest>(r => r != null && r.BucketName == BucketName), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureStorageAsync_Local_TouchesNoS3()
    {
        await using Fixture f = CreateFixture("local");

        await DependencyInjection.EnsureStorageAsync(f.Services, CancellationToken.None);

        Assert.Empty(f.S3Client.ReceivedCalls());
    }

    // EnsureBucketAsync

    [Fact]
    public async Task EnsureBucketAsync_NewBucket_CreatesItAndAllowsAnonymousReadOfPublicOnly()
    {
        IAmazonS3 s3Client = Substitute.For<IAmazonS3>();

        await DependencyInjection.EnsureBucketAsync(s3Client, BucketName, CancellationToken.None);

        await s3Client.Received(1).PutBucketAsync(
            Arg.Is<PutBucketRequest>(r => r != null && r.BucketName == BucketName), Arg.Any<CancellationToken>());
        await s3Client.Received(1).PutBucketPolicyAsync(
            Arg.Is<PutBucketPolicyRequest>(r => r != null && r.BucketName == BucketName
                && r.Policy != null && r.Policy.Contains("\"s3:GetObject\"")
                && r.Policy.Contains($"arn:aws:s3:::{BucketName}/public/*")),
            Arg.Any<CancellationToken>());
    }

    // Several API instances start together; the one that loses the create race must still start.
    [Fact]
    public async Task EnsureBucketAsync_BucketAlreadyOwnedByYou_StillSetsThePolicy()
    {
        IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("owned") { ErrorCode = "BucketAlreadyOwnedByYou", StatusCode = HttpStatusCode.Conflict });

        await DependencyInjection.EnsureBucketAsync(s3Client, BucketName, CancellationToken.None);

        await s3Client.Received(1).PutBucketPolicyAsync(Arg.Any<PutBucketPolicyRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("AccessDenied", HttpStatusCode.Forbidden)]
    [InlineData("BucketAlreadyExists", HttpStatusCode.Conflict)]
    public async Task EnsureBucketAsync_OtherCreateError_Propagates(string errorCode, HttpStatusCode status)
    {
        IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("failed") { ErrorCode = errorCode, StatusCode = status });

        await Assert.ThrowsAsync<AmazonS3Exception>(
            () => DependencyInjection.EnsureBucketAsync(s3Client, BucketName, CancellationToken.None));
        await s3Client.DidNotReceive().PutBucketPolicyAsync(Arg.Any<PutBucketPolicyRequest>(), Arg.Any<CancellationToken>());
    }
}

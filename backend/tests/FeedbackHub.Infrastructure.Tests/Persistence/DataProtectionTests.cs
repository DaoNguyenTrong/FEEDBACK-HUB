using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollection))]
public sealed class DataProtectionTests(PostgresContainerFixture fixture)
{
    private sealed class FakeHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "FeedbackHub.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private ServiceProvider BuildProvider(string contentRoot)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(contentRoot));
        services.AddPersistence(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DataProtection_TwoProvidersSameDatabase_UnprotectEachOthersPayload()
    {
        using ServiceProvider providerA = BuildProvider("/srv/instance-a");
        using ServiceProvider providerB = BuildProvider("/srv/instance-b");

        string payload = providerA.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("FeedbackHub.Secrets")
            .Protect("secret-value");

        string plain = providerB.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("FeedbackHub.Secrets")
            .Unprotect(payload);

        Assert.Equal("secret-value", plain);
    }
}

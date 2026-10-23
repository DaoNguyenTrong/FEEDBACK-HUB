using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using FeedbackHub.API.Common;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure;
using FeedbackHub.Infrastructure.Services.Email;
using FeedbackHub.Infrastructure.Services.Notifications;
using FeedbackHub.Infrastructure.Services.RateLimiting;
using YamlDotNet.Serialization;

namespace FeedbackHub.API.Tests.Common;

public class ProductionTopologyValidatorTests
{
    private static Dictionary<string, string?> Complete() => new()
    {
        ["CacheSettings:Provider"] = "Redis",
        ["RealtimeSettings:Backplane"] = "Redis",
        ["ConnectionStrings:Redis"] = "redis:6379,password=secret",
        ["StorageSettings:Provider"] = "SeaweedFS",
        ["RateLimiterSettings:Store"] = "Redis",
        ["DatabaseSettings:MigrateOnStartup"] = "false",
        ["ForwardedHeadersSettings:KnownProxies:0"] = "10.0.0.1",
        ["AuthCookieSettings:Secure"] = "true",
        ["EmailSettings:SecureSocket"] = "StartTls"
    };

    private static IReadOnlyList<string> Run(Dictionary<string, string?> values)
    {
        return ProductionTopologyValidator.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    [Fact]
    public void Validate_CompleteProductionConfig_ReturnsNoErrors()
    {
        Assert.Empty(Run(Complete()));
    }

    [Theory]
    [InlineData("CacheSettings:Provider", "Memory", "CacheSettings:Provider")]
    [InlineData("RealtimeSettings:Backplane", "None", "RealtimeSettings:Backplane")]
    [InlineData("ConnectionStrings:Redis", "", "ConnectionStrings:Redis")]
    [InlineData("StorageSettings:Provider", "Local", "StorageSettings:Provider")]
    [InlineData("RateLimiterSettings:Store", "Memory", "RateLimiterSettings:Store")]
    [InlineData("DatabaseSettings:MigrateOnStartup", "true", "DatabaseSettings:MigrateOnStartup")]
    [InlineData("ForwardedHeadersSettings:KnownProxies:0", " ", "ForwardedHeadersSettings")]
    [InlineData("AuthCookieSettings:Secure", "false", "AuthCookieSettings:Secure")]
    [InlineData("EmailSettings:SecureSocket", "", "EmailSettings:SecureSocket")]
    public void Validate_EachConditionBroken_ReportsExactlyThatKey(string key, string value, string expectedKey)
    {
        Dictionary<string, string?> values = Complete();
        values[key] = value;

        IReadOnlyList<string> errors = Run(values);

        string error = Assert.Single(errors);
        Assert.Contains(expectedKey, error);
        Assert.DoesNotContain("secret", error);
    }

    [Fact]
    public void Validate_EmailSecureSocketStartTlsWhenAvailable_IsError()
    {
        Dictionary<string, string?> values = Complete();
        values["EmailSettings:SecureSocket"] = "StartTlsWhenAvailable";

        Assert.Single(Run(values));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_StorageProviderMissingOrBlank_IsError(string? value)
    {
        Dictionary<string, string?> values = Complete();
        if (value is null)
        {
            values.Remove("StorageSettings:Provider");
        }
        else
        {
            values["StorageSettings:Provider"] = value;
        }

        Assert.Contains("StorageSettings:Provider", Assert.Single(Run(values)));
    }

    [Theory]
    [InlineData("CacheSettings:Provider")]
    [InlineData("RealtimeSettings:Backplane")]
    [InlineData("RateLimiterSettings:Store")]
    [InlineData("EmailSettings:SecureSocket")]
    public void Validate_PaddedValue_IsNotTrimmed(string key)
    {
        Dictionary<string, string?> values = Complete();
        values[key] = " " + values[key] + " ";

        Assert.Contains(key, Assert.Single(Run(values)));
    }

    [Fact]
    public void Validate_BothProxyArraysAbsent_IsError()
    {
        Dictionary<string, string?> values = Complete();
        values.Remove("ForwardedHeadersSettings:KnownProxies:0");

        Assert.Contains("ForwardedHeadersSettings", Assert.Single(Run(values)));
    }

    // The validator cannot reference Infrastructure (architecture rule), so these pin its literals to the
    // names Infrastructure and Application actually accept; a rename there must fail here, not fail open.
    [Fact]
    public void Constants_MatchInfrastructureAndApplicationNames()
    {
        Assert.Equal(CachingExtensions.RedisProvider, ProductionTopologyValidator.Redis);
        Assert.Equal(RealtimeExtensions.RedisBackplane, ProductionTopologyValidator.Redis);
        Assert.Equal(RateLimiterStore.Redis, ProductionTopologyValidator.Redis);
        Assert.Equal(EmailSecureSocket.SslOnConnect, ProductionTopologyValidator.SslOnConnect);
        Assert.Equal(EmailSecureSocket.StartTls, ProductionTopologyValidator.StartTls);
        Assert.Equal(new StorageSettings().Provider, ProductionTopologyValidator.LocalStorage);
    }

    [Fact]
    public void Validate_MigrateOnStartupMissing_IsError()
    {
        Dictionary<string, string?> values = Complete();
        values.Remove("DatabaseSettings:MigrateOnStartup");

        Assert.Contains("DatabaseSettings:MigrateOnStartup", Assert.Single(Run(values)));
    }

    [Fact]
    public void Validate_ValuesIgnoringCase_AreAccepted()
    {
        Dictionary<string, string?> values = Complete();
        values["CacheSettings:Provider"] = "redis";
        values["RealtimeSettings:Backplane"] = "REDIS";
        values["StorageSettings:Provider"] = "seaweedfs";
        values["RateLimiterSettings:Store"] = "redis";
        values["EmailSettings:SecureSocket"] = "starttls";
        values["AuthCookieSettings:Secure"] = "True";
        values["DatabaseSettings:MigrateOnStartup"] = "False";

        Assert.Empty(Run(values));
    }

    [Fact]
    public void Validate_StorageLocalLowercase_IsError()
    {
        Dictionary<string, string?> values = Complete();
        values["StorageSettings:Provider"] = "local";

        Assert.Contains("StorageSettings:Provider", Assert.Single(Run(values)));
    }

    [Fact]
    public void Validate_KnownNetworksOnly_IsAccepted()
    {
        Dictionary<string, string?> values = Complete();
        values.Remove("ForwardedHeadersSettings:KnownProxies:0");
        values["ForwardedHeadersSettings:KnownNetworks:0"] = "10.0.0.0/8";

        Assert.Empty(Run(values));
    }

    // The shipped compose file plus the example appsettings must satisfy the validator except for what only the
    // operator can choose, so a compose edit that drops a required key fails here instead of at deploy time.
    [Fact]
    public void Validate_DockerComposeProdEnvironment_OnlyMissesOperatorSecrets()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "docker-compose.prod.yml")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);

        Dictionary<string, object> compose = new DeserializerBuilder().Build()
            .Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(directory, "docker-compose.prod.yml")));
        Dictionary<object, object> services = (Dictionary<object, object>)compose["services"];
        Dictionary<object, object> api = (Dictionary<object, object>)services["api"];
        Dictionary<object, object> environment = (Dictionary<object, object>)api["environment"];

        Dictionary<string, string?> fromCompose = environment.ToDictionary(
            pair => ((string)pair.Key).Replace("__", ":"),
            pair => (string?)pair.Value);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Example.json"), optional: false)
            .AddInMemoryCollection(fromCompose)
            .Build();

        string error = Assert.Single(ProductionTopologyValidator.Validate(configuration));
        Assert.Contains("EmailSettings:SecureSocket", error);
    }

    [Theory]
    [InlineData("Production", false, false, true)]
    [InlineData("Production", true, false, false)]
    [InlineData("Production", false, true, false)]
    [InlineData("Development", false, false, false)]
    [InlineData("Staging", false, false, false)]
    public void ShouldValidate_Scenario_Expected(string environment, bool isOpenApiExport, bool isCliCommand, bool expected)
    {
        TestEnv env = new(environment);

        Assert.Equal(expected, ProductionTopologyValidator.ShouldValidate(isOpenApiExport, isCliCommand, env));
    }

    private sealed class TestEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

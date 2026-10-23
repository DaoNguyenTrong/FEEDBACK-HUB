using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using FeedbackHub.API.Common;

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

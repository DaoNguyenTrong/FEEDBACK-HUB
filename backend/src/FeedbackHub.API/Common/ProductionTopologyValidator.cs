using Microsoft.Extensions.Hosting;

namespace FeedbackHub.API.Common;

/// <summary>
/// Lists every production configuration condition that is not met for running several API instances securely
/// (docs/tech/00-nen-tang.md §10.3). Pure: reads <see cref="IConfiguration"/> only. The accepted values are written
/// here, not taken from Infrastructure, because only Program.cs may reference that project.
/// </summary>
public static class ProductionTopologyValidator
{
    public const string ErrorTitle = "Production configuration is not ready for multiple API instances:";

    internal const string Redis = "Redis";
    internal const string LocalStorage = "Local";
    internal const string SslOnConnect = "SslOnConnect";
    internal const string StartTls = "StartTls";

    /// <summary>Whether Program.cs must run <see cref="Validate"/>: Production only, never for CLI verbs or the OpenAPI export.</summary>
    public static bool ShouldValidate(bool isOpenApiExport, bool isCliCommand, IHostEnvironment env)
    {
        return !isOpenApiExport && !isCliCommand && env.IsProduction();
    }

    public static IReadOnlyList<string> Validate(IConfiguration configuration)
    {
        List<string> errors = [];

        if (!Is(configuration["CacheSettings:Provider"], Redis))
        {
            errors.Add("CacheSettings:Provider must be Redis so every instance shares one cache.");
        }

        if (!Is(configuration["RealtimeSettings:Backplane"], Redis))
        {
            errors.Add("RealtimeSettings:Backplane must be Redis so SignalR messages reach clients on every instance.");
        }

        if (string.IsNullOrWhiteSpace(configuration["ConnectionStrings:Redis"]))
        {
            errors.Add("ConnectionStrings:Redis must be set.");
        }

        // A missing or blank key falls back to the class default, Local.
        string? storageProvider = configuration["StorageSettings:Provider"];
        if (string.IsNullOrWhiteSpace(storageProvider) || Is(storageProvider, LocalStorage))
        {
            errors.Add("StorageSettings:Provider must be set and must not be Local: instances do not share a local disk.");
        }

        if (!Is(configuration["RateLimiterSettings:Store"], Redis))
        {
            errors.Add("RateLimiterSettings:Store must be Redis so rate limits are counted across instances.");
        }

        // The class default is true, so a missing key counts as enabled; an unparseable value is not trusted either.
        if (!(bool.TryParse(configuration["DatabaseSettings:MigrateOnStartup"], out bool migrate) && !migrate))
        {
            errors.Add("DatabaseSettings:MigrateOnStartup must be false; run migrations with the migrate command.");
        }

        if (!HasEntries(configuration, "ForwardedHeadersSettings:KnownProxies")
            && !HasEntries(configuration, "ForwardedHeadersSettings:KnownNetworks"))
        {
            errors.Add("ForwardedHeadersSettings:KnownProxies or ForwardedHeadersSettings:KnownNetworks must list the trusted proxies.");
        }

        if (!(bool.TryParse(configuration["AuthCookieSettings:Secure"], out bool secure) && secure))
        {
            errors.Add("AuthCookieSettings:Secure must be true.");
        }

        string? secureSocket = configuration["EmailSettings:SecureSocket"];
        if (!Is(secureSocket, SslOnConnect) && !Is(secureSocket, StartTls))
        {
            errors.Add("EmailSettings:SecureSocket must be SslOnConnect or StartTls.");
        }

        return errors;
    }

    private static bool Is(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEntries(IConfiguration configuration, string section)
    {
        return configuration.GetSection(section).GetChildren().Any(child => !string.IsNullOrWhiteSpace(child.Value));
    }
}

using System.Text.RegularExpressions;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Tests.Services.Roles;

public class SystemRoleDisplayNamesTests
{
    [Theory]
    [InlineData("OWNER")]
    [InlineData(" admin ")]
    [InlineData("Member")]
    [InlineData("chủ sở hữu")]
    [InlineData("CHỦ SỞ HỮU")]
    public void IsReserved_SystemNamesAnyCaseOrForm_True(string name)
    {
        // PRD00-AC-291, BR-79
        Assert.True(SystemRoleDisplayNames.IsReserved(name.Normalize(System.Text.NormalizationForm.FormD)));
        Assert.True(SystemRoleDisplayNames.IsReserved(name));
    }

    [Theory]
    [InlineData("Tester")]
    [InlineData("Owner team")]
    public void IsReserved_OtherName_False(string name)
    {
        Assert.False(SystemRoleDisplayNames.IsReserved(name));
    }

    [Theory]
    [InlineData(SystemRoleKind.Owner, "vi", "Chủ sở hữu")]
    [InlineData(SystemRoleKind.Owner, "en", "Owner")]
    [InlineData(SystemRoleKind.Admin, "vi", "Admin")]
    [InlineData(SystemRoleKind.Admin, "en", "Admin")]
    [InlineData(SystemRoleKind.Member, "vi", "Member")]
    [InlineData(SystemRoleKind.Member, "en", "Member")]
    public void Get_KindAndLanguage_ReturnsDisplayName(SystemRoleKind kind, string language, string expected)
    {
        Assert.Equal(expected, SystemRoleDisplayNames.Get(kind, language));
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Get_AllKinds_MatchFrontendLocale(string language)
    {
        string text = File.ReadAllText(Path.Combine(FindRepoRoot(), "frontend", "src", "locales", language + ".ts"));
        int start = text.IndexOf($"const {language}: LocaleSchema", StringComparison.Ordinal);
        Assert.True(start >= 0);
        Match block = Regex.Match(
            text[start..],
            @"systemKind:\s*\{\s*Owner:\s*'([^']*)',\s*Admin:\s*'([^']*)',\s*Member:\s*'([^']*)',?\s*\}");
        Assert.True(block.Success);
        Assert.Equal(SystemRoleDisplayNames.Get(SystemRoleKind.Owner, language), block.Groups[1].Value);
        Assert.Equal(SystemRoleDisplayNames.Get(SystemRoleKind.Admin, language), block.Groups[2].Value);
        Assert.Equal(SystemRoleDisplayNames.Get(SystemRoleKind.Member, language), block.Groups[3].Value);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

using System.Reflection;
using System.Xml.Linq;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Tests.Resources;

public class MessageKeysTests
{
    [Fact]
    public void Resx_BothCulturesHaveTheSameKeys()
    {
        HashSet<string> vi = ReadKeys("Messages.resx");
        HashSet<string> en = ReadKeys("Messages.en.resx");

        Assert.NotEmpty(vi);
        Assert.Empty(vi.Except(en));
        Assert.Empty(en.Except(vi));
    }

    [Fact]
    public void DomainMessages_EveryKeyHasText()
    {
        AssertEveryKeyInBothFiles(ConstKeys(typeof(DomainMessages)));
    }

    [Fact]
    public void ApplicationMessages_EveryKeyHasText()
    {
        AssertEveryKeyInBothFiles(ConstKeys(typeof(ApplicationMessages)));
    }

    private static void AssertEveryKeyInBothFiles(List<string> keys)
    {
        Assert.NotEmpty(keys);
        HashSet<string> vi = ReadKeys("Messages.resx");
        HashSet<string> en = ReadKeys("Messages.en.resx");

        List<string> missingInVi = keys.Where(k => !vi.Contains(k)).ToList();
        List<string> missingInEn = keys.Where(k => !en.Contains(k)).ToList();

        Assert.Empty(missingInVi);
        Assert.Empty(missingInEn);
    }

    private static List<string> ConstKeys(Type messages) =>
        messages
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    private static HashSet<string> ReadKeys(string fileName)
    {
        string path = Path.Combine(FindBackendRoot(), "src", "FeedbackHub.Application", "Resources", fileName);
        Assert.True(File.Exists(path), $"Resource file not found: {path}");

        return XDocument.Load(path)
            .Descendants("data")
            .Select(d => (string)d.Attribute("name")!)
            .ToHashSet();
    }

    private static string FindBackendRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FeedbackHub.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "FeedbackHub.sln not found above the test output directory");
        return dir!.FullName;
    }
}

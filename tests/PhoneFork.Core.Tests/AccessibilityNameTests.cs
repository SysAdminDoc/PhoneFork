using System.Xml;
using System.Xml.Linq;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F122 — a screen-reader user must be able to identify the controls that disable packages and
/// write settings. WPF derives an automation name from a control's Content, so a Button reading
/// "Disable Selected" is already named; a DataGrid, TextBox, ComboBox or tab is not.
///
/// This is a static gate over the shipped XAML so a new unlabelled control fails the build
/// instead of waiting for someone to run an accessibility inspector by hand.
/// </summary>
public class AccessibilityNameTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>Elements that carry no text of their own, so WPF cannot infer a name.</summary>
    private static readonly string[] RequiresExplicitName =
    {
        "DataGrid", "TabControl", "TabItem", "TextBox", "PasswordBox", "ComboBox", "ListBox", "ListView",
    };

    public static TheoryData<string> ViewFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(ViewsDirectory(), "*.xaml"))
            data.Add(Path.GetFileName(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(ViewFiles))]
    public void EveryControlWithoutInferableTextHasAnAutomationName(string fileName)
    {
        var document = XDocument.Load(Path.Combine(ViewsDirectory(), fileName), LoadOptions.SetLineInfo);

        var unnamed = document.Descendants()
            .Where(e => e.Name.Namespace == Xaml && RequiresExplicitName.Contains(e.Name.LocalName))
            .Where(e => string.IsNullOrWhiteSpace(NameOf(e)))
            .Select(e => $"{e.Name.LocalName} (line {((IXmlLineInfo)e).LineNumber})")
            .ToList();

        Assert.Empty(unnamed);
    }

    [Fact]
    public void EveryTabIsNamed()
    {
        var mainWindow = XDocument.Load(Path.Combine(ViewsDirectory(), "MainWindow.xaml"));

        var tabs = mainWindow.Descendants(Xaml + "TabItem").ToList();

        Assert.NotEmpty(tabs);
        Assert.All(tabs, tab => Assert.False(string.IsNullOrWhiteSpace(NameOf(tab))));
    }

    [Fact]
    public void EveryViewCarriesAtLeastOneAutomationName()
    {
        var bare = Directory.EnumerateFiles(ViewsDirectory(), "*.xaml")
            .Where(f => !File.ReadAllText(f).Contains("AutomationProperties.Name", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(bare);
    }

    /// <summary>
    /// Reads AutomationProperties.Name whether it is written as an attribute or as an element.
    /// </summary>
    private static string? NameOf(XElement element)
    {
        var attribute = element.Attributes()
            .FirstOrDefault(a => a.Name.LocalName is "Name" && a.Name.NamespaceName.Length == 0
                                 && a.Parent is not null)
            ?.Value;

        // The common form is the attached attribute AutomationProperties.Name="…", which XLinq
        // surfaces with that literal local name and no namespace.
        var attached = element.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.Name")
            ?.Value;

        return attached ?? attribute;
    }

    private static string ViewsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhoneFork.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "PhoneFork.App", "Views");
    }
}

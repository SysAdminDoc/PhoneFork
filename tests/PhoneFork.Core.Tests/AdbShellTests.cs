using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class AdbShellTests
{
    [Fact]
    public void Arg_SingleQuotesAndEscapesEmbeddedQuotes()
    {
        Assert.Equal("'abc'\\''def'", AdbShell.Arg("abc'def"));
    }

    [Fact]
    public void PackageArg_AcceptsAndroidPackageIdentifier()
    {
        Assert.Equal("'com.example_app.service'", AdbShell.PackageArg("com.example_app.service"));
    }

    [Theory]
    [InlineData("com.example;rm -rf /")]
    [InlineData("com.example app")]
    [InlineData("")]
    public void PackageArg_RejectsShellSyntaxAndEmptyValues(string value)
    {
        Assert.Throws<ArgumentException>(() => AdbShell.PackageArg(value));
    }
}

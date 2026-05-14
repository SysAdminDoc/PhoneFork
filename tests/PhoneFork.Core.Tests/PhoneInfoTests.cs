using PhoneFork.Core.Models;

namespace PhoneFork.Core.Tests;

public sealed class PhoneInfoTests
{
    [Fact]
    public void FormattedOneUiVersion_FormatsSamsungEncodedVersion()
    {
        var phone = new PhoneInfo("R5", "samsung", "SM-S938B", "16", "80000", "pa3q", true);

        Assert.Equal("8.0.0", phone.FormattedOneUiVersion);
    }

    [Fact]
    public void ShortLabel_ToleratesShortSerials()
    {
        var phone = new PhoneInfo("R5", "samsung", "SM-S938B", "16", "80000", "pa3q", true);

        Assert.Equal("samsung SM-S938B (R5)", phone.ShortLabel);
    }
}

using System.Windows.Controls;

namespace PhoneFork.App.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();

    public string Title
    {
        get => TitleBlock.Text;
        set => TitleBlock.Text = value;
    }

    public string Version
    {
        get => VersionBlock.Text;
        set => VersionBlock.Text = value;
    }

    public string Body
    {
        get => BodyBlock.Text;
        set => BodyBlock.Text = value;
    }
}

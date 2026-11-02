using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NetSeal.WinUI.ViewModels;

namespace NetSeal.WinUI.Controls;

public sealed partial class CredentialsEnterView : UserControl
{
    public CredentialsEnterViewModel ViewModel { get; set; }

    public CredentialsEnterView()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<CredentialsEnterViewModel>();
    }

    private void UsernameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {

    }

    private void PasswordTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {

    }
}

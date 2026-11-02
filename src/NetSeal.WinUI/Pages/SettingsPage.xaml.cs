using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NetSeal.WinUI.ViewModels;

namespace NetSeal.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPageModel ViewModel { get; set; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsPageModel>();
    }
}

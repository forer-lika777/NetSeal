using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetSeal.WinUI.ViewModels;
using System;
using System.IO;
using System.Reflection;
using Windows.System;

namespace NetSeal.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPageModel ViewModel { get; set; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsPageModel>();
    }

    private async void PermissionHyperLinkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string text = LoadEmbeddedLicenseText();
        ScrollViewer scrollViewer = new ScrollViewer();
        TextBlock textBlock = new TextBlock { Text = text };
        scrollViewer.Content = textBlock;

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            RequestedTheme = this.ActualTheme,
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
            Title = "许可条款",
            Content = scrollViewer,
        };

        await dialog.ShowAsync();
    }

    private string LoadEmbeddedLicenseText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "NetSeal.WinUI.Assets.LICENSE";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return "找不到许可协议文件。";
        }

        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async void OpenRepo_Click(object sender, RoutedEventArgs e)
    {
        Uri repoUri = new Uri("https://github.com/forer-lika777/NetSeal");
        await Launcher.LaunchUriAsync(repoUri);
    }
}

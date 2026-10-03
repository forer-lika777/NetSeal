using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NetSeal.Models;
using NetSeal.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace NetSeal.Pages;

public sealed partial class StatusPage : Page, IRecipient<ChangeNetworkConfirmMessage>
{
    public StatusPageModel ViewModel { get; set; } = null!;

    public StatusPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<StatusPageModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StrongReferenceMessenger.Default.Register<ChangeNetworkConfirmMessage>(this);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StrongReferenceMessenger.Default.Unregister<ChangeNetworkConfirmMessage>(this);
    }

    public async void Receive(ChangeNetworkConfirmMessage message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Title = "确定要更改选定网络吗？",
            Content = "此网络与您先前拨号连接的网络并不相同，更改选定网络可能导致意外的错误连接。",
        };

        dialog.PrimaryButtonClick += (s, e) => message.TaskCompletionSource.SetResult();
        dialog.SecondaryButtonClick += (s, e) => message.TaskCompletionSource.SetCanceled();

        await dialog.ShowAsync();
    }

    private void PasswordEnterBox_GotFocus(object sender, RoutedEventArgs e)
    {
        PasswordEnterBox.SelectAll();
    }

    private void UsernameEnterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        PasswordEnterBox.Focus(FocusState.Programmatic);
    }

    private void PasswordEnterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel.ConnectCommand.CanExecute(e))
            ViewModel.ConnectCommand.Execute(e);
    }
}

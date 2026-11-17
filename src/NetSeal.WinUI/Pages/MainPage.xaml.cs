using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NetSeal.Core.Models;
using System.Linq;

namespace NetSeal.WinUI.Pages;

public sealed partial class MainPage : Page, IRecipient<ModifyEnableBackground>
{
    public string CurrentTag { get; set; } = string.Empty;

    public MainPage()
    {
        InitializeComponent();
        NavigationView.MenuItems.Add(new NavigationViewItem { Content = "开始", Icon = new SymbolIcon(Symbol.Home), Tag = "home" });
        NavigationView.MenuItems.Add(new NavigationViewItem { Content = "状态", Icon = new FontIcon { Glyph = "\uE968" }, Tag = "status" });
        NavigationView.Tag = CurrentTag;
        NavigationView.SelectedItem = NavigationView.MenuItems[0];
        Navigate("home");
    }

    private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (MainFrame.CanGoBack)
        {
            var page = MainFrame.BackStack.Last().SourcePageType;

            if (page.Equals(typeof(HomePage)))
            {
                CurrentTag = "home";
                NavigationView.SelectedItem = NavigationView.MenuItems[0];
            }
            else if (page.Equals(typeof(StatusPage)))
            {
                CurrentTag = "status";
                NavigationView.SelectedItem = NavigationView.MenuItems[1];
            }
            else if (page.Equals(typeof(SettingsPage)))
            {
                CurrentTag = "settings";
                NavigationView.SelectedItem = NavigationView.SettingsItem;
            }

            MainFrame.GoBack();
        }
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            Navigate("settings");
        }
        else
        {
            Navigate(args.InvokedItemContainer.Tag.ToString() ?? "home");
        }
    }

    private void Navigate(string tag)
    {
        if (CurrentTag == tag)
            return;

        CurrentTag = tag;

        if (tag == "home")
        {
            MainFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
        }
        else if (tag == "status")
        {
            MainFrame.Navigate(typeof(StatusPage), null, new DrillInNavigationTransitionInfo());
        }
        else if (tag == "settings")
        {
            MainFrame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());
        }
    }

    public void Receive(ModifyEnableBackground message)
    {
        Background.Visibility = message.Enabled ? Visibility.Visible : Visibility.Collapsed;
    }
}

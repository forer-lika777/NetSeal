using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NetSeal.WinUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

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

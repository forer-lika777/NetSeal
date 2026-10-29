using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace NetSeal.WinUI.ViewModels;

public partial class CredentialsEnterViewModel : ObservableObject
{
    private readonly IAppSettings settings;

    private bool initialized;

    public CredentialsEnterViewModel(IAppSettings settings)
    {
        this.settings = settings;
    }

    private bool usernameInitialized = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsernameValidation))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial string Username { get; set; } = string.Empty;

    partial void OnUsernameChanged(string oldValue, string newValue)
    {
        if (!usernameInitialized)
        {
            usernameInitialized = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(newValue))
        {
            UsernameValidation.IsValid = false;
            UsernameValidation.Message = "用户名不能为空";

            if (!UsernameValidation.ShouldDisplayInvalid)
                UsernameValidation.ShouldDisplayInvalid = true;
        }
        else
        {
            UsernameValidation.IsValid = true;
            UsernameValidation.Message = "";
            UsernameValidation.ShouldDisplayInvalid = false;
        }
    }

    [ObservableProperty]
    public partial ValidationDisplayStatus UsernameValidation { get; set; } = new();

    private bool passwordInitialized = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordValidation))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial string Password { get; set; } = string.Empty;

    partial void OnPasswordChanged(string oldValue, string newValue)
    {
        if (!passwordInitialized)
        {
            passwordInitialized = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(newValue))
        {
            PasswordValidation.IsValid = false;
            PasswordValidation.Message = "密码不能为空";

            if (!PasswordValidation.ShouldDisplayInvalid)
                PasswordValidation.ShouldDisplayInvalid = true;
        }
        else
        {
            PasswordValidation.IsValid = true;
            PasswordValidation.Message = "";
            PasswordValidation.ShouldDisplayInvalid = false;
        }
    }

    [ObservableProperty]
    public partial ValidationDisplayStatus PasswordValidation { get; set; } = new();

    [ObservableProperty]
    public partial string CommitErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCommitting { get; set; } = false;

    private bool CanCommit()
    {
        return UsernameValidation.IsValid && PasswordValidation.IsValid;
    }

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        var credentials = new RasCredentials(Username, Password);

        try
        {
            await settings.RasCredentials.SetValueAsync(credentials);
        }
        catch (Exception ex)
        {
            CommitErrorMessage = ex.Message;
            throw;
        }
    }
}

public partial class ValidationDisplayStatus : ObservableObject
{
    [ObservableProperty]
    public partial bool IsValid { get; set; } = false;

    [ObservableProperty]
    public partial bool ShouldDisplayInvalid { get; set; } = false;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;
}

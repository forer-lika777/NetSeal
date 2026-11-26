namespace NetSeal.Core.Models;

public record ChangeNetworkConfirmMessage(TaskCompletionSource TaskCompletionSource);
public record KeepRefreshingStateMessage(bool DoKeep);
public record ModifyCredentialsMessage(bool HasCredentials);
public record ModifyEnableBackground(bool Enabled);
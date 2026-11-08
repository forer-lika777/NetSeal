namespace NetSeal.Core.Models;

public record ChangeNetworkConfirmMessage(TaskCompletionSource TaskCompletionSource);
public record KeepRefreshingStatusMessage(bool doKeep);
public record ModifyCredentialsMessage(bool HasCredentials);

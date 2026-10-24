using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetSeal.Core.Models;

public record NetworkInformationChangedMessage();

public record ChangeNetworkConfirmMessage(TaskCompletionSource TaskCompletionSource);

using Api_Vapp.Constants;
using Api_Vapp.Interfaces;

namespace Api_Vapp.Tests.Shared;

internal sealed class NoOpUserPushNotifier : IUserPushNotifier
{
    public Task NotifyAsync(
        int userId,
        NotificationCategory category,
        string title,
        string body,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> NotifyBroadcastAsync(
        NotificationCategory category,
        string title,
        string body,
        CancellationToken cancellationToken = default) => Task.FromResult(0);
}

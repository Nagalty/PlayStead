using System.Reflection;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Notifications;

public sealed class NotificationModelTests
{
    [Fact]
    public void Enums_expose_stable_values()
    {
        Assert.Equal(1, (int)NotificationState.Unread);
        Assert.Equal(2, (int)NotificationState.Read);
        Assert.Equal(3, (int)NotificationState.Resolved);
        Assert.Equal(1, (int)NotificationPriority.Info);
        Assert.Equal(2, (int)NotificationPriority.Warning);
        Assert.Equal(3, (int)NotificationPriority.ActionRequired);
        Assert.Equal(1, (int)NotificationProducer.IdentityResolution);
        Assert.Equal(1, (int)NotificationListFilter.Active);
        Assert.Equal(2, (int)NotificationListFilter.Resolved);
    }

    [Fact]
    public void Notification_id_round_trips_strict_guid_format()
    {
        var id = NotificationId.New();
        var text = id.ToString();

        Assert.Equal(id.Value.ToString("D"), text);
        Assert.Equal(id, NotificationId.Parse(text));
        Assert.True(NotificationId.TryParse(text, out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(NotificationId.TryParse("{" + text + "}", out _));
        Assert.Throws<FormatException>(() => NotificationId.Parse("invalid"));
    }

    [Fact]
    public void Deduplication_key_requires_a_non_empty_persisted_value()
    {
        var key = new NotificationDeduplicationKey("identity:game:ambiguous");

        Assert.Equal("identity:game:ambiguous", key.Value);
        Assert.Throws<ArgumentException>(
            () => new NotificationDeduplicationKey(" "));
    }

    [Fact]
    public void Notification_record_preserves_nullable_lifecycle_timestamps()
    {
        var now = DateTimeOffset.Parse("2026-09-16T18:00:00Z");
        var record = new NotificationRecord(
            NotificationId.New(),
            NotificationProducer.IdentityResolution,
            "game-1",
            "ambiguous",
            "identity:game-1:ambiguous",
            NotificationPriority.ActionRequired,
            NotificationState.Unread,
            "Possible match",
            "Review this game.",
            null,
            now,
            now,
            null,
            null);

        Assert.Null(record.ReadUtc);
        Assert.Null(record.ResolvedUtc);
    }

    [Fact]
    public void Publish_request_contains_the_complete_publish_contract()
    {
        var request = new NotificationPublishRequest(
            NotificationProducer.IdentityResolution,
            "game-1",
            "ambiguous",
            new NotificationDeduplicationKey("identity:game-1:ambiguous"),
            NotificationPriority.ActionRequired,
            "Possible match",
            "Review this game.",
            "{}");

        Assert.Equal("identity:game-1:ambiguous", request.DeduplicationKey.Value);
        Assert.Equal("{}", request.PayloadJson);
    }

    [Fact]
    public void Store_and_service_expose_only_the_planned_signatures()
    {
        var storeMethods = typeof(INotificationStore).GetMethods();
        Assert.Equal(8, storeMethods.Length);
        Assert.Contains(storeMethods, method => method.Name == nameof(INotificationStore.InsertAsync));
        Assert.Contains(storeMethods, method => method.Name == nameof(INotificationStore.DeleteResolvedOlderThanAsync));

        var serviceMethods = typeof(INotificationCenterService).GetMethods();
        Assert.Equal(6, serviceMethods.Length);
        Assert.Contains(serviceMethods, method => method.Name == nameof(INotificationCenterService.PublishOrRefreshAsync));
        Assert.Contains(serviceMethods, method => method.Name == nameof(INotificationCenterService.PurgeExpiredResolvedAsync));
        Assert.DoesNotContain(
            serviceMethods,
            method => method.Name.Contains("Confirm", StringComparison.OrdinalIgnoreCase));
    }
}

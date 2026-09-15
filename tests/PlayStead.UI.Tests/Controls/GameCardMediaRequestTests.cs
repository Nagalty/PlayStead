using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.UI.Controls;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Controls;

public sealed class GameCardMediaRequestTests
{
    [Fact]
    public void Loaded_card_without_cover_requests_its_item()
    {
        RunSta(() =>
        {
            var card = new GameCard();
            var requests = ObserveRequests(card);
            var item = CreateItem();
            card.DataContext = item;

            Assert.False(card.IsLoaded);
            Assert.Empty(requests);

            WithLoadedCard(card, () =>
            {
                var request = Assert.Single(requests);
                Assert.Same(card, request.Sender);
                Assert.Same(item, request.Item);
            });
        });
    }

    [Fact]
    public void Loaded_card_with_cover_does_not_request_media()
    {
        RunSta(() =>
        {
            var card = new GameCard();
            var requests = ObserveRequests(card);
            var item = CreateItem();
            item.SetCoverPath(@"C:\Media\existing.jpg");
            card.DataContext = item;

            WithLoadedCard(card, () => Assert.Empty(requests));
        });
    }

    [Fact]
    public void Loaded_card_without_compatible_DataContext_does_not_request_media()
    {
        RunSta(() =>
        {
            foreach (var context in new object?[] { null, new object() })
            {
                var card = new GameCard();
                var requests = ObserveRequests(card);
                card.DataContext = context;
                WithLoadedCard(card, () => Assert.Empty(requests));
            }
        });
    }

    [Fact]
    public void Repeated_Loaded_notification_in_same_load_does_not_duplicate_request()
    {
        RunSta(() =>
        {
            var card = new GameCard();
            var requests = ObserveRequests(card);
            var item = CreateItem();
            card.DataContext = item;

            WithLoadedCard(card, () =>
            {
                Assert.Single(requests);
                // Still attached and loaded: no intervening Unloaded or new item.
                card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, card));
                card.UpdateLayout();
                card.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

                Assert.Same(item, Assert.Single(requests).Item);
            });
        });
    }

    private static List<(object? Sender, LibraryItemViewModel Item)> ObserveRequests(GameCard card)
    {
        // Reflection keeps this RED compilable while the public event is absent.
        var mediaEvent = typeof(GameCard).GetEvent("MediaRequested");
        Assert.True(mediaEvent is not null,
            "GameCard must expose MediaRequested for loaded items without a Cover.");
        Assert.Equal(typeof(EventHandler<LibraryItemViewModel>), mediaEvent!.EventHandlerType);
        var requests = new List<(object? Sender, LibraryItemViewModel Item)>();
        EventHandler<LibraryItemViewModel> handler = (sender, item) => requests.Add((sender, item));
        mediaEvent.AddEventHandler(card, handler);
        return requests;
    }

    private static LibraryItemViewModel CreateItem() =>
        new(GameId.New(), "Arma Reforger", ProviderKind.Steam, "Steam",
            @"C:\Games\Arma Reforger", null);

    private static void WithLoadedCard(GameCard card, Action assertion)
    {
        var window = new Window
        {
            Content = card,
            Width = 320,
            Height = 420,
            ShowInTaskbar = false,
            ShowActivated = false
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Assert.True(card.IsLoaded);
            assertion();
        }
        finally
        {
            window.Close();
        }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

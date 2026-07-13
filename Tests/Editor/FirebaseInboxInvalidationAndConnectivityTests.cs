using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActionFit.Connectivity;
using NUnit.Framework;

namespace ActionFit.Inbox.Firebase.Tests
{
    public class FirebaseInboxInvalidationAndConnectivityTests
    {
        [Test]
        public async Task OfflineThenOnlineRefresh_QueriesBackendOnlyAfterRecovery()
        {
            var connectivity = new FakeConnectivityService(ConnectivityState.Offline);
            var adapter = new ActionFitConnectivityAdapter(connectivity);
            var client = new CountingDatabaseClient();
            client.Children.Add(new FirebaseInboxDataNode(
                "1",
                new Dictionary<string, object>
                {
                    ["itemType"] = "Cash",
                    ["itemName"] = "cash",
                    ["qty"] = 1L
                }));
            var inbox = CreateInbox(adapter, new FirebaseInboxBackend(client));

            InboxLoadResult offline = await inbox.LoadAsync();
            connectivity.SetState(ConnectivityState.Online);
            InboxLoadResult recovered = await inbox.RefreshAsync();

            Assert.That(offline.Status, Is.EqualTo(InboxLoadStatus.Offline));
            Assert.That(recovered.Status, Is.EqualTo(InboxLoadStatus.Succeeded));
            Assert.That(client.LoadCount, Is.EqualTo(1));
            Assert.That(recovered.Messages, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ConnectivityAdapter_WaitsForUnderlyingOnlineState()
        {
            var connectivity = new FakeConnectivityService(ConnectivityState.Offline);
            var adapter = new ActionFitConnectivityAdapter(connectivity);

            Task wait = adapter.WaitForOnlineAsync();
            Assert.That(adapter.IsOnline, Is.False);
            connectivity.SetState(ConnectivityState.Online);
            await wait;

            Assert.That(adapter.IsOnline, Is.True);
        }

        [Test]
        public async Task InvalidationSignal_InvalidatesLoadedInboxCache()
        {
            var connectivity = new FakeConnectivityService(ConnectivityState.Online);
            var inbox = CreateInbox(
                new ActionFitConnectivityAdapter(connectivity),
                new FirebaseInboxBackend(new CountingDatabaseClient()));
            await inbox.LoadAsync();
            Assert.That(inbox.IsCacheValid, Is.True);

            var invalidation = new FirebaseInboxInvalidationAdapter();
            using IDisposable binding = invalidation.Bind(inbox);
            bool handled = invalidation.TryHandle(new Dictionary<string, string>
            {
                ["invalidate"] = "postbox"
            });

            Assert.That(handled, Is.True);
            Assert.That(inbox.IsCacheValid, Is.False);
        }

        [Test]
        public void InvalidationSignal_IgnoresOtherPayloadsAndStopsAfterDispose()
        {
            var invalidation = new FirebaseInboxInvalidationAdapter();
            var inbox = new RecordingInboxService();
            IDisposable binding = invalidation.Bind(inbox);

            Assert.That(invalidation.TryHandle(new Dictionary<string, string>
            {
                ["invalidate"] = "another-cache"
            }), Is.False);
            Assert.That(inbox.InvalidationCount, Is.Zero);

            binding.Dispose();
            Assert.That(invalidation.TryHandle(new Dictionary<string, string>
            {
                ["invalidate"] = "postbox"
            }), Is.True);
            Assert.That(inbox.InvalidationCount, Is.Zero);
        }

        [Test]
        public void InvalidationOptions_AreProjectConfigurable()
        {
            var invalidation = new FirebaseInboxInvalidationAdapter(
                new FirebaseInboxInvalidationOptions("mailbox_changed", "target", "inbox"));

            bool handled = invalidation.TryHandle(new Dictionary<string, string>
            {
                ["target"] = "inbox"
            });

            Assert.That(handled, Is.True);
            Assert.That(invalidation.Options.Topic, Is.EqualTo("mailbox_changed"));
        }

        private static InboxService CreateInbox(
            ActionFitConnectivityAdapter connectivity,
            IInboxBackend backend)
        {
            return new InboxService(
                new UserIdProvider(),
                connectivity,
                backend,
                new ReceiptStore(),
                new RewardHandler(),
                options: new InboxServiceOptions(0, TimeSpan.Zero));
        }

        private sealed class CountingDatabaseClient : IFirebaseInboxDatabaseClient
        {
            public List<FirebaseInboxDataNode> Children { get; } = new List<FirebaseInboxDataNode>();
            public int LoadCount { get; private set; }

            public Task<IReadOnlyList<FirebaseInboxDataNode>> LoadChildrenAsync(
                string path,
                CancellationToken cancellationToken)
            {
                LoadCount++;
                return Task.FromResult<IReadOnlyList<FirebaseInboxDataNode>>(Children);
            }

            public Task<FirebaseInboxDataNode> LoadNodeAsync(
                string path,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<FirebaseInboxDataNode>(null);
            }

            public Task RemoveNodeAsync(string path, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeConnectivityService : ActionFit.Connectivity.IConnectivityService
        {
            private TaskCompletionSource<bool> _onlineCompletion = NewCompletion();

            public FakeConnectivityService(ConnectivityState state)
            {
                State = state;
                if (state == ConnectivityState.Online) _onlineCompletion.TrySetResult(true);
            }

            public ConnectivityState State { get; private set; }
            public bool IsPaused => false;
            public bool IsMonitoring => false;
            public event Action<ConnectivityState> StateChanged;

            public void SetState(ConnectivityState state)
            {
                if (state == State) return;
                State = state;
                if (state == ConnectivityState.Online) _onlineCompletion.TrySetResult(true);
                else if (_onlineCompletion.Task.IsCompleted) _onlineCompletion = NewCompletion();
                StateChanged?.Invoke(state);
            }

            public Task<bool> CheckNowAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(State == ConnectivityState.Online);
            }

            public Task<bool> CheckWithRetryAsync(CancellationToken cancellationToken = default)
            {
                return CheckNowAsync(cancellationToken);
            }

            public async Task WaitForOnlineAsync(CancellationToken cancellationToken = default)
            {
                using CancellationTokenRegistration registration = cancellationToken.Register(
                    () => _onlineCompletion.TrySetCanceled());
                await _onlineCompletion.Task;
            }

            public void StartMonitoring() { }
            public void StopMonitoring() { }
            public void Pause() { }

            public Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
            {
                return CheckNowAsync(cancellationToken);
            }

            private static TaskCompletionSource<bool> NewCompletion()
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private sealed class UserIdProvider : IInboxUserIdProvider
        {
            public string GetUserId() => "user-1";
        }

        private sealed class ReceiptStore : IInboxReceiptStore
        {
            public InboxReceipt Load(string userId, string messageId) => default;
            public void Save(string userId, string messageId, InboxReceipt receipt) { }
            public IReadOnlyList<InboxReceiptRecord> LoadPending(string userId) => Array.Empty<InboxReceiptRecord>();
        }

        private sealed class RewardHandler : IInboxRewardHandler
        {
            public Task GrantAsync(
                string userId,
                string messageId,
                IReadOnlyList<InboxAttachment> attachments,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingInboxService : IInboxService
        {
            public int InvalidationCount { get; private set; }
            public IReadOnlyList<InboxMessage> CurrentMessages => Array.Empty<InboxMessage>();
            public bool IsCacheValid => false;
            public event Action<IReadOnlyList<InboxMessage>> MessagesChanged;
            public event Action CacheInvalidated;

            public Task<InboxLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<InboxLoadResult> RefreshAsync(CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<bool> MarkReadAsync(string messageId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<InboxClaimResult> ClaimAsync(string messageId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<InboxBatchClaimResult> ClaimAllAsync(CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<InboxBatchClaimResult> RetryPendingClaimsAsync(CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public void InvalidateCache()
            {
                InvalidationCount++;
                CacheInvalidated?.Invoke();
            }
        }
    }
}

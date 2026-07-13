using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Messaging;

namespace ActionFit.Inbox.Firebase.Messaging
{
    public sealed class FirebaseMessagingInboxInvalidationSource : IDisposable
    {
        private readonly FirebaseInboxInvalidationAdapter _adapter;
        private bool _started;

        public FirebaseMessagingInboxInvalidationSource(FirebaseInboxInvalidationAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public bool IsStarted => _started;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_started) return;

            FirebaseMessaging.MessageReceived += HandleMessageReceived;
            _started = true;
            try
            {
                await FirebaseMessaging.SubscribeAsync(_adapter.Options.Topic);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                StopListening();
                throw;
            }
            catch (Exception exception)
            {
                StopListening();
                throw new FirebaseInboxDataException(
                    FirebaseInboxFailureKind.Unavailable,
                    "Firebase Messaging inbox topic subscription failed.",
                    exception);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_started) return;

            StopListening();
            try
            {
                await FirebaseMessaging.UnsubscribeAsync(_adapter.Options.Topic);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FirebaseInboxDataException(
                    FirebaseInboxFailureKind.Unavailable,
                    "Firebase Messaging inbox topic unsubscription failed.",
                    exception);
            }
        }

        public void Dispose()
        {
            StopListening();
        }

        private void HandleMessageReceived(object sender, MessageReceivedEventArgs eventArgs)
        {
            if (eventArgs?.Message?.Data == null) return;

            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in eventArgs.Message.Data)
                data[pair.Key] = pair.Value;
            _adapter.TryHandle(data);
        }

        private void StopListening()
        {
            if (!_started) return;
            FirebaseMessaging.MessageReceived -= HandleMessageReceived;
            _started = false;
        }
    }
}

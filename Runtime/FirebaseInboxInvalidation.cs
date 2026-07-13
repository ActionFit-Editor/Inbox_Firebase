using System;
using System.Collections.Generic;

namespace ActionFit.Inbox.Firebase
{
    public sealed class FirebaseInboxInvalidationOptions
    {
        public FirebaseInboxInvalidationOptions(
            string topic = "postbox_invalidate",
            string dataKey = "invalidate",
            string dataValue = "postbox")
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("FCM topic must not be empty.", nameof(topic));
            if (string.IsNullOrWhiteSpace(dataKey))
                throw new ArgumentException("FCM data key must not be empty.", nameof(dataKey));
            if (string.IsNullOrWhiteSpace(dataValue))
                throw new ArgumentException("FCM data value must not be empty.", nameof(dataValue));

            Topic = topic;
            DataKey = dataKey;
            DataValue = dataValue;
        }

        public string Topic { get; }
        public string DataKey { get; }
        public string DataValue { get; }

        public static FirebaseInboxInvalidationOptions Default { get; } =
            new FirebaseInboxInvalidationOptions();
    }

    public sealed class FirebaseInboxInvalidationAdapter
    {
        private readonly FirebaseInboxInvalidationOptions _options;

        public FirebaseInboxInvalidationAdapter(FirebaseInboxInvalidationOptions options = null)
        {
            _options = options ?? FirebaseInboxInvalidationOptions.Default;
        }

        public FirebaseInboxInvalidationOptions Options => _options;

        public event Action CacheInvalidationRequested;

        public bool TryHandle(IReadOnlyDictionary<string, string> data)
        {
            if (data == null
                || !data.TryGetValue(_options.DataKey, out string value)
                || !string.Equals(value, _options.DataValue, StringComparison.Ordinal))
            {
                return false;
            }

            CacheInvalidationRequested?.Invoke();
            return true;
        }

        public IDisposable Bind(IInboxService inboxService)
        {
            if (inboxService == null) throw new ArgumentNullException(nameof(inboxService));
            return new FirebaseInboxInvalidationBinding(this, inboxService);
        }

        private sealed class FirebaseInboxInvalidationBinding : IDisposable
        {
            private FirebaseInboxInvalidationAdapter _adapter;
            private IInboxService _inboxService;

            public FirebaseInboxInvalidationBinding(
                FirebaseInboxInvalidationAdapter adapter,
                IInboxService inboxService)
            {
                _adapter = adapter;
                _inboxService = inboxService;
                _adapter.CacheInvalidationRequested += HandleInvalidation;
            }

            public void Dispose()
            {
                if (_adapter == null) return;
                _adapter.CacheInvalidationRequested -= HandleInvalidation;
                _adapter = null;
                _inboxService = null;
            }

            private void HandleInvalidation()
            {
                _inboxService?.InvalidateCache();
            }
        }
    }
}

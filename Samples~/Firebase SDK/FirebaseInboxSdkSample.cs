using System;
using ActionFit.Inbox.Firebase.Database;
using ActionFit.Inbox.Firebase.Messaging;
using Firebase;
using Firebase.Database;

namespace ActionFit.Inbox.Firebase.Samples
{
    public static class FirebaseInboxSdkSample
    {
        public static FirebaseInboxBackend CreateBackend(
            FirebaseApp app,
            FirebaseInboxBackendOptions options = null)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            var client = new FirebaseRealtimeDatabaseClient(
                FirebaseDatabase.GetInstance(app).RootReference);
            return new FirebaseInboxBackend(client, options);
        }

        public static FirebaseMessagingInboxInvalidationSource CreateInvalidationSource(
            IInboxService inboxService,
            out IDisposable binding,
            FirebaseInboxInvalidationOptions options = null)
        {
            if (inboxService == null) throw new ArgumentNullException(nameof(inboxService));
            var adapter = new FirebaseInboxInvalidationAdapter(options);
            binding = adapter.Bind(inboxService);
            return new FirebaseMessagingInboxInvalidationSource(adapter);
        }
    }
}

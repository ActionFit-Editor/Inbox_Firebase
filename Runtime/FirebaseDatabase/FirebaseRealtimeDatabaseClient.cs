using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Database;

namespace ActionFit.Inbox.Firebase.Database
{
    public sealed class FirebaseRealtimeDatabaseClient : IFirebaseInboxDatabaseClient
    {
        private readonly DatabaseReference _rootReference;

        public FirebaseRealtimeDatabaseClient(DatabaseReference rootReference)
        {
            _rootReference = rootReference ?? throw new ArgumentNullException(nameof(rootReference));
        }

        public async Task<IReadOnlyList<FirebaseInboxDataNode>> LoadChildrenAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                DataSnapshot snapshot = await _rootReference.Child(path).GetValueAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshot == null || !snapshot.Exists) return Array.Empty<FirebaseInboxDataNode>();

                var nodes = new List<FirebaseInboxDataNode>();
                foreach (DataSnapshot child in snapshot.Children)
                {
                    if (child == null || !child.Exists || string.IsNullOrWhiteSpace(child.Key)) continue;
                    nodes.Add(new FirebaseInboxDataNode(child.Key, child.Value));
                }
                return nodes;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Wrap(exception, "Firebase RTDB inbox read failed.");
            }
        }

        public async Task<FirebaseInboxDataNode> LoadNodeAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                DataSnapshot snapshot = await _rootReference.Child(path).GetValueAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshot == null || !snapshot.Exists || string.IsNullOrWhiteSpace(snapshot.Key)) return null;
                return new FirebaseInboxDataNode(snapshot.Key, snapshot.Value);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Wrap(exception, "Firebase RTDB inbox read failed.");
            }
        }

        public async Task RemoveNodeAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Firebase treats removing an already missing node as success, which keeps
                // ConfirmClaimAsync idempotent across retries.
                await _rootReference.Child(path).RemoveValueAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Wrap(exception, "Firebase RTDB inbox confirmation failed.");
            }
        }

        internal static FirebaseInboxDataException Wrap(Exception exception, string safeMessage)
        {
            return new FirebaseInboxDataException(Classify(exception), safeMessage, exception);
        }

        private static FirebaseInboxFailureKind Classify(Exception exception)
        {
            foreach (Exception candidate in Enumerate(exception))
            {
                if (candidate is TimeoutException) return FirebaseInboxFailureKind.Unavailable;

                string message = candidate.Message ?? string.Empty;
                if (message.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0)
                    return FirebaseInboxFailureKind.PermissionDenied;
                if (message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return FirebaseInboxFailureKind.Unavailable;
                }
                if (message.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0)
                    return FirebaseInboxFailureKind.Network;
            }

            return FirebaseInboxFailureKind.Unknown;
        }

        private static IEnumerable<Exception> Enumerate(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                foreach (Exception inner in aggregateException.Flatten().InnerExceptions)
                {
                    foreach (Exception nested in Enumerate(inner)) yield return nested;
                }
                yield break;
            }

            for (Exception current = exception; current != null; current = current.InnerException)
                yield return current;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActionFit.Inbox.Firebase
{
    public sealed class FirebaseInboxBackend : IInboxBackend
    {
        private readonly IFirebaseInboxDatabaseClient _client;
        private readonly FirebaseInboxBackendOptions _options;
        private readonly IFirebaseInboxExceptionMapper _exceptionMapper;

        public FirebaseInboxBackend(
            IFirebaseInboxDatabaseClient client,
            FirebaseInboxBackendOptions options = null,
            IFirebaseInboxExceptionMapper exceptionMapper = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options ?? FirebaseInboxBackendOptions.Default;
            _exceptionMapper = exceptionMapper ?? new DefaultFirebaseInboxExceptionMapper();
        }

        public async Task<IReadOnlyList<InboxMessage>> LoadMessagesAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            string path = FirebaseInboxPath.Build(_options.RootPath, userId);
            IReadOnlyList<FirebaseInboxDataNode> nodes = await ExecuteAsync(
                token => _client.LoadChildrenAsync(path, token),
                "load",
                cancellationToken);

            if (nodes == null || nodes.Count == 0) return Array.Empty<InboxMessage>();

            var messages = new List<InboxMessage>(nodes.Count);
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FirebaseInboxDataNode node in nodes)
            {
                if (node == null || !messageIds.Add(node.Key))
                {
                    if (_options.SkipMalformedMessages) continue;
                    throw new InboxBackendException(
                        "Firebase inbox response contains an invalid or duplicate message.",
                        false);
                }

                if (FirebaseInboxMessageParser.TryParse(node, out InboxMessage message))
                {
                    messages.Add(message);
                    continue;
                }

                if (!_options.SkipMalformedMessages)
                {
                    throw new InboxBackendException(
                        "Firebase inbox response contains a malformed message.",
                        false);
                }
            }

            return messages;
        }

        public async Task<InboxMessage> LoadMessageAsync(
            string userId,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            string path = FirebaseInboxPath.Build(_options.RootPath, userId, messageId);
            FirebaseInboxDataNode node = await ExecuteAsync(
                token => _client.LoadNodeAsync(path, token),
                "load-one",
                cancellationToken);

            if (node == null) return null;
            if (FirebaseInboxMessageParser.TryParse(node, out InboxMessage message)) return message;
            if (_options.SkipMalformedMessages) return null;

            throw new InboxBackendException(
                "Firebase inbox response contains a malformed message.",
                false);
        }

        public Task ConfirmClaimAsync(
            string userId,
            string messageId,
            CancellationToken cancellationToken)
        {
            string path = FirebaseInboxPath.Build(_options.RootPath, userId, messageId);
            return ExecuteAsync(
                token => _client.RemoveNodeAsync(path, token),
                "confirm",
                cancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<T> operationTask;
            try
            {
                operationTask = operation(cancellationToken)
                    ?? throw new InvalidOperationException("Firebase inbox client returned a null task.");
                Task timeoutTask = Task.Delay(_options.OperationTimeout, cancellationToken);
                Task completed = await Task.WhenAny(operationTask, timeoutTask);
                if (completed != operationTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObserveFault(operationTask);
                    throw new TimeoutException("Firebase inbox operation timed out.");
                }

                return await operationTask;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (exception is InboxBackendException backendException) throw backendException;
                throw _exceptionMapper.Map(exception, operationName);
            }
        }

        private async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            await ExecuteAsync(
                async token =>
                {
                    await operation(token);
                    return true;
                },
                operationName,
                cancellationToken);
        }

        private static void ObserveFault(Task task)
        {
            _ = task.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    public sealed class DefaultFirebaseInboxExceptionMapper : IFirebaseInboxExceptionMapper
    {
        public InboxBackendException Map(Exception exception, string operation)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            if (exception is InboxBackendException backendException) return backendException;

            Exception effective = Unwrap(exception);
            bool transient = effective is TimeoutException;
            if (effective is FirebaseInboxDataException dataException)
            {
                transient = dataException.FailureKind == FirebaseInboxFailureKind.Network
                    || dataException.FailureKind == FirebaseInboxFailureKind.Unavailable;
            }

            string safeOperation = string.IsNullOrWhiteSpace(operation) ? "operation" : operation;
            return new InboxBackendException(
                "Firebase inbox " + safeOperation + " failed.",
                transient,
                exception);
        }

        private static Exception Unwrap(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                AggregateException flattened = aggregateException.Flatten();
                if (flattened.InnerExceptions.Count == 1)
                    return Unwrap(flattened.InnerExceptions[0]);
            }

            return exception;
        }
    }
}

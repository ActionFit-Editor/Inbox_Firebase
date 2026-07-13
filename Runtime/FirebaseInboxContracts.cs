using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActionFit.Inbox.Firebase
{
    public enum FirebaseInboxFailureKind
    {
        Unknown,
        Network,
        Unavailable,
        PermissionDenied,
        InvalidData
    }

    public sealed class FirebaseInboxDataException : Exception
    {
        public FirebaseInboxDataException(
            FirebaseInboxFailureKind failureKind,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            FailureKind = failureKind;
        }

        public FirebaseInboxFailureKind FailureKind { get; }
    }

    public sealed class FirebaseInboxDataNode
    {
        public FirebaseInboxDataNode(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Firebase inbox node key must not be empty.", nameof(key));

            Key = key;
            Value = value;
        }

        public string Key { get; }
        public object Value { get; }
    }

    /// <summary>
    /// Small RTDB transport boundary. The optional Firebase SDK assembly implements this
    /// interface while deterministic tests can provide an in-memory client.
    /// </summary>
    public interface IFirebaseInboxDatabaseClient
    {
        Task<IReadOnlyList<FirebaseInboxDataNode>> LoadChildrenAsync(
            string path,
            CancellationToken cancellationToken);

        Task<FirebaseInboxDataNode> LoadNodeAsync(
            string path,
            CancellationToken cancellationToken);

        Task RemoveNodeAsync(
            string path,
            CancellationToken cancellationToken);
    }

    public interface IFirebaseInboxExceptionMapper
    {
        InboxBackendException Map(Exception exception, string operation);
    }

    public sealed class FirebaseInboxBackendOptions
    {
        public FirebaseInboxBackendOptions(
            string rootPath = "rewards",
            TimeSpan? operationTimeout = null,
            bool skipMalformedMessages = true)
        {
            RootPath = FirebaseInboxPath.NormalizeRoot(rootPath);
            OperationTimeout = operationTimeout ?? TimeSpan.FromSeconds(5);
            if (OperationTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(operationTimeout));

            SkipMalformedMessages = skipMalformedMessages;
        }

        public string RootPath { get; }
        public TimeSpan OperationTimeout { get; }
        public bool SkipMalformedMessages { get; }

        public static FirebaseInboxBackendOptions Default { get; } =
            new FirebaseInboxBackendOptions();
    }

    internal static class FirebaseInboxPath
    {
        private static readonly char[] ForbiddenCharacters = { '.', '#', '$', '[', ']' };

        public static string NormalizeRoot(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("Firebase inbox root path must not be empty.", nameof(rootPath));

            string[] segments = rootPath.Trim('/').Split('/');
            if (segments.Length == 0)
                throw new ArgumentException("Firebase inbox root path must not be empty.", nameof(rootPath));

            foreach (string segment in segments)
                ValidateSegment(segment, nameof(rootPath));

            return string.Join("/", segments);
        }

        public static string Build(string rootPath, string userId)
        {
            ValidateSegment(userId, nameof(userId));
            return rootPath + "/" + userId;
        }

        public static string Build(string rootPath, string userId, string messageId)
        {
            ValidateSegment(messageId, nameof(messageId));
            return Build(rootPath, userId) + "/" + messageId;
        }

        private static void ValidateSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Firebase path segment must not be empty.", parameterName);
            if (value.IndexOf('/') >= 0 || value.IndexOfAny(ForbiddenCharacters) >= 0)
                throw new ArgumentException("Firebase path segment contains a reserved character.", parameterName);

            foreach (char character in value)
            {
                if (char.IsControl(character))
                    throw new ArgumentException("Firebase path segment contains a control character.", parameterName);
            }
        }
    }
}

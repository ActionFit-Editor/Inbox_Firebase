using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ActionFit.Inbox.Firebase.Tests
{
    public class FirebaseInboxBackendAndParserTests
    {
        [Test]
        public async Task LoadMessages_MapsLegacyRewardsSchema()
        {
            var client = new FakeDatabaseClient();
            client.Children.Add(LegacyNode("17", "Cash", "cash", 25, "Gift", "Welcome"));
            var backend = new FirebaseInboxBackend(client);

            IReadOnlyList<InboxMessage> messages = await backend.LoadMessagesAsync("user-1", default);

            Assert.That(client.LastPath, Is.EqualTo("rewards/user-1"));
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].MessageId, Is.EqualTo("17"));
            Assert.That(messages[0].Title, Is.EqualTo("Gift"));
            Assert.That(messages[0].Body, Is.EqualTo("Welcome"));
            Assert.That(messages[0].Attachments, Has.Count.EqualTo(1));
            Assert.That(messages[0].Attachments[0].AttachmentId, Is.EqualTo("17:legacy"));
            Assert.That(messages[0].Attachments[0].RewardType, Is.EqualTo("Cash"));
            Assert.That(messages[0].Attachments[0].ItemKey, Is.EqualTo("cash"));
            Assert.That(messages[0].Attachments[0].Quantity, Is.EqualTo(25));
        }

        [Test]
        public void Parser_MapsMultipleAttachmentsAndMessageMetadata()
        {
            var node = new FirebaseInboxDataNode(
                "message-a",
                new Dictionary<string, object>
                {
                    ["title"] = "Bundle",
                    ["body"] = "Two rewards",
                    ["createdAtUtc"] = "2026-07-13T10:00:00Z",
                    ["expiresAt"] = 1783940400000L,
                    ["isRead"] = true,
                    ["attachments"] = new Dictionary<string, object>
                    {
                        ["cash"] = new Dictionary<string, object>
                        {
                            ["rewardType"] = "Cash",
                            ["itemKey"] = "cash",
                            ["quantity"] = 10L,
                            ["attributes"] = new Dictionary<string, object>
                            {
                                ["source"] = "operator"
                            }
                        },
                        ["energy"] = new Dictionary<string, object>
                        {
                            ["attachmentId"] = "energy-attachment",
                            ["itemType"] = "Energy",
                            ["itemName"] = "energy",
                            ["qty"] = "5"
                        }
                    }
                });

            bool parsed = FirebaseInboxMessageParser.TryParse(node, out InboxMessage message);

            Assert.That(parsed, Is.True);
            Assert.That(message.IsRead, Is.True);
            Assert.That(message.CreatedAtUtc, Is.EqualTo(DateTimeOffset.Parse("2026-07-13T10:00:00Z")));
            Assert.That(message.ExpiresAtUtc, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1783940400000L)));
            Assert.That(message.Attachments, Has.Count.EqualTo(2));
            Assert.That(message.Attachments[0].AttachmentId, Is.EqualTo("cash"));
            Assert.That(message.Attachments[0].Attributes["source"], Is.EqualTo("operator"));
            Assert.That(message.Attachments[1].AttachmentId, Is.EqualTo("energy-attachment"));
        }

        [Test]
        public async Task LoadMessages_EmptySnapshot_ReturnsEmptyList()
        {
            var backend = new FirebaseInboxBackend(new FakeDatabaseClient());

            IReadOnlyList<InboxMessage> messages = await backend.LoadMessagesAsync("user-1", default);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public async Task LoadMessages_DefaultPolicy_SkipsMalformedMessages()
        {
            var client = new FakeDatabaseClient();
            client.Children.Add(new FirebaseInboxDataNode(
                "bad",
                new Dictionary<string, object> { ["qty"] = 0 }));
            client.Children.Add(LegacyNode("good", "Cash", "cash", 1, null, null));
            var backend = new FirebaseInboxBackend(client);

            IReadOnlyList<InboxMessage> messages = await backend.LoadMessagesAsync("user-1", default);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].MessageId, Is.EqualTo("good"));
        }

        [Test]
        public async Task LoadMessages_StrictPolicy_RejectsMalformedMessages()
        {
            var client = new FakeDatabaseClient();
            client.Children.Add(new FirebaseInboxDataNode(
                "bad",
                new Dictionary<string, object> { ["qty"] = 0 }));
            var options = new FirebaseInboxBackendOptions(skipMalformedMessages: false);
            var backend = new FirebaseInboxBackend(client, options);

            InboxBackendException exception = await CaptureBackendException(
                () => backend.LoadMessagesAsync("user-1", default));

            Assert.That(exception.IsTransient, Is.False);
        }

        [Test]
        public async Task LoadMessages_StrictPolicy_RejectsDuplicateMessageIds()
        {
            var client = new FakeDatabaseClient();
            client.Children.Add(LegacyNode("same", "Cash", "cash", 1, null, null));
            client.Children.Add(LegacyNode("same", "Cash", "cash", 2, null, null));
            var backend = new FirebaseInboxBackend(
                client,
                new FirebaseInboxBackendOptions(skipMalformedMessages: false));

            await CaptureBackendException(() => backend.LoadMessagesAsync("user-1", default));
        }

        [Test]
        public async Task LoadMessage_UsesExactMessagePath()
        {
            var client = new FakeDatabaseClient
            {
                Node = LegacyNode("42", "Energy", "energy", 3, null, null)
            };
            var backend = new FirebaseInboxBackend(
                client,
                new FirebaseInboxBackendOptions("operator/rewards"));

            InboxMessage message = await backend.LoadMessageAsync("user-1", "42");

            Assert.That(client.LastPath, Is.EqualTo("operator/rewards/user-1/42"));
            Assert.That(message.MessageId, Is.EqualTo("42"));
        }

        [Test]
        public async Task ConfirmClaim_RemovesExactMessageAndCanRepeat()
        {
            var client = new FakeDatabaseClient();
            var backend = new FirebaseInboxBackend(client);

            await backend.ConfirmClaimAsync("user-1", "42", default);
            await backend.ConfirmClaimAsync("user-1", "42", default);

            Assert.That(client.RemovedPaths, Is.EqualTo(new[]
            {
                "rewards/user-1/42",
                "rewards/user-1/42"
            }));
        }

        [Test]
        public async Task LoadMessages_Timeout_IsTransientAndDoesNotExposePath()
        {
            var client = new FakeDatabaseClient
            {
                LoadChildrenHandler = _ => new TaskCompletionSource<IReadOnlyList<FirebaseInboxDataNode>>(
                    TaskCreationOptions.RunContinuationsAsynchronously).Task
            };
            var backend = new FirebaseInboxBackend(
                client,
                new FirebaseInboxBackendOptions(operationTimeout: TimeSpan.FromMilliseconds(10)));

            InboxBackendException exception = await CaptureBackendException(
                () => backend.LoadMessagesAsync("private-user-id", default));

            Assert.That(exception.IsTransient, Is.True);
            Assert.That(exception.Message, Does.Not.Contain("private-user-id"));
        }

        [Test]
        public async Task LoadMessages_PermissionFailure_IsNotTransient()
        {
            var client = new FakeDatabaseClient
            {
                LoadChildrenHandler = _ => Task.FromException<IReadOnlyList<FirebaseInboxDataNode>>(
                    new FirebaseInboxDataException(
                        FirebaseInboxFailureKind.PermissionDenied,
                        "safe transport failure"))
            };
            var backend = new FirebaseInboxBackend(client);

            InboxBackendException exception = await CaptureBackendException(
                () => backend.LoadMessagesAsync("user-1", default));

            Assert.That(exception.IsTransient, Is.False);
            Assert.That(exception.Message, Is.EqualTo("Firebase inbox load failed."));
        }

        [Test]
        public async Task LoadMessages_NetworkFailure_IsTransient()
        {
            var client = new FakeDatabaseClient
            {
                LoadChildrenHandler = _ => Task.FromException<IReadOnlyList<FirebaseInboxDataNode>>(
                    new FirebaseInboxDataException(
                        FirebaseInboxFailureKind.Network,
                        "safe transport failure"))
            };
            var backend = new FirebaseInboxBackend(client);

            InboxBackendException exception = await CaptureBackendException(
                () => backend.LoadMessagesAsync("user-1", default));

            Assert.That(exception.IsTransient, Is.True);
        }

        [Test]
        public async Task LoadMessages_Cancellation_Propagates()
        {
            var client = new FakeDatabaseClient();
            var backend = new FirebaseInboxBackend(client);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            try
            {
                await backend.LoadMessagesAsync("user-1", cancellation.Token);
                Assert.Fail("Expected cancellation to propagate.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        [TestCase("user/child")]
        [TestCase("user.with-dot")]
        [TestCase("user#hash")]
        [TestCase("user[0]")]
        public async Task LoadMessages_RejectsUnsafeFirebasePathSegments(string userId)
        {
            var backend = new FirebaseInboxBackend(new FakeDatabaseClient());

            try
            {
                await backend.LoadMessagesAsync(userId, default);
                Assert.Fail("Expected an unsafe Firebase path to be rejected.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static async Task<InboxBackendException> CaptureBackendException(Func<Task> operation)
        {
            try
            {
                await operation();
                Assert.Fail("Expected an InboxBackendException.");
                return null;
            }
            catch (InboxBackendException exception)
            {
                return exception;
            }
        }

        private static FirebaseInboxDataNode LegacyNode(
            string key,
            string itemType,
            string itemName,
            long quantity,
            string title,
            string description)
        {
            return new FirebaseInboxDataNode(
                key,
                new Dictionary<string, object>
                {
                    ["itemType"] = itemType,
                    ["itemName"] = itemName,
                    ["qty"] = quantity,
                    ["title"] = title,
                    ["desc"] = description
                });
        }

        private sealed class FakeDatabaseClient : IFirebaseInboxDatabaseClient
        {
            public List<FirebaseInboxDataNode> Children { get; } = new List<FirebaseInboxDataNode>();
            public List<string> RemovedPaths { get; } = new List<string>();
            public FirebaseInboxDataNode Node { get; set; }
            public string LastPath { get; private set; }
            public Func<CancellationToken, Task<IReadOnlyList<FirebaseInboxDataNode>>> LoadChildrenHandler { get; set; }

            public Task<IReadOnlyList<FirebaseInboxDataNode>> LoadChildrenAsync(
                string path,
                CancellationToken cancellationToken)
            {
                LastPath = path;
                return LoadChildrenHandler != null
                    ? LoadChildrenHandler(cancellationToken)
                    : Task.FromResult<IReadOnlyList<FirebaseInboxDataNode>>(Children);
            }

            public Task<FirebaseInboxDataNode> LoadNodeAsync(
                string path,
                CancellationToken cancellationToken)
            {
                LastPath = path;
                return Task.FromResult(Node);
            }

            public Task RemoveNodeAsync(string path, CancellationToken cancellationToken)
            {
                RemovedPaths.Add(path);
                return Task.CompletedTask;
            }
        }
    }
}

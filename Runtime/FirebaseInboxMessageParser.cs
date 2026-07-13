using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace ActionFit.Inbox.Firebase
{
    public static class FirebaseInboxMessageParser
    {
        public static bool TryParse(FirebaseInboxDataNode node, out InboxMessage message)
        {
            message = null;
            if (node == null || string.IsNullOrWhiteSpace(node.Key)) return false;
            if (!(node.Value is IDictionary fields)) return false;

            try
            {
                IReadOnlyList<InboxAttachment> attachments = ParseAttachments(node.Key, fields);
                if (attachments.Count == 0) return false;

                string title = ReadString(fields, "title") ?? string.Empty;
                string body = ReadString(fields, "body")
                    ?? ReadString(fields, "desc")
                    ?? string.Empty;
                DateTimeOffset? createdAt = ReadTimestamp(fields, "createdAtUtc", "createdAt");
                DateTimeOffset? expiresAt = ReadTimestamp(fields, "expiresAtUtc", "expiresAt");
                bool isRead = ReadBoolean(fields, "isRead");

                message = new InboxMessage(
                    node.Key,
                    title,
                    body,
                    attachments,
                    createdAt,
                    expiresAt,
                    isRead);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static IReadOnlyList<InboxAttachment> ParseAttachments(
            string messageId,
            IDictionary fields)
        {
            if (TryGet(fields, "attachments", out object rawAttachments) && rawAttachments != null)
            {
                var parsed = new List<InboxAttachment>();
                if (rawAttachments is IDictionary attachmentDictionary)
                {
                    foreach (DictionaryEntry entry in attachmentDictionary)
                    {
                        string fallbackId = ToInvariantString(entry.Key);
                        if (!TryParseAttachment(entry.Value, fallbackId, out InboxAttachment attachment))
                            throw new FormatException("Firebase inbox attachment is malformed.");
                        parsed.Add(attachment);
                    }
                    return parsed;
                }

                if (rawAttachments is IEnumerable attachmentList && !(rawAttachments is string))
                {
                    int index = 0;
                    foreach (object value in attachmentList)
                    {
                        if (!TryParseAttachment(value, index.ToString(CultureInfo.InvariantCulture), out InboxAttachment attachment))
                            throw new FormatException("Firebase inbox attachment is malformed.");
                        parsed.Add(attachment);
                        index++;
                    }
                    return parsed;
                }

                throw new FormatException("Firebase inbox attachments must be a map or list.");
            }

            if (!TryParseAttachment(fields, messageId + ":legacy", out InboxAttachment legacyAttachment))
                return Array.Empty<InboxAttachment>();
            return new[] { legacyAttachment };
        }

        private static bool TryParseAttachment(
            object rawValue,
            string fallbackId,
            out InboxAttachment attachment)
        {
            attachment = null;
            if (!(rawValue is IDictionary fields)) return false;

            string attachmentId = ReadString(fields, "attachmentId") ?? fallbackId;
            string rewardType = ReadString(fields, "rewardType") ?? ReadString(fields, "itemType");
            string itemKey = ReadString(fields, "itemKey") ?? ReadString(fields, "itemName") ?? string.Empty;
            if (!TryReadPositiveLong(fields, out long quantity, "quantity", "qty")) return false;
            if (string.IsNullOrWhiteSpace(attachmentId) || string.IsNullOrWhiteSpace(rewardType)) return false;

            IReadOnlyDictionary<string, string> attributes = ReadAttributes(fields);
            attachment = new InboxAttachment(
                attachmentId,
                rewardType,
                itemKey,
                quantity,
                attributes);
            return true;
        }

        private static IReadOnlyDictionary<string, string> ReadAttributes(IDictionary fields)
        {
            if (!TryGet(fields, "attributes", out object rawAttributes)
                || !(rawAttributes is IDictionary dictionary))
            {
                return null;
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = ToInvariantString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) throw new FormatException("Attribute key is empty.");
                attributes[key] = ToInvariantString(entry.Value) ?? string.Empty;
            }
            return attributes;
        }

        private static DateTimeOffset? ReadTimestamp(
            IDictionary fields,
            string primaryName,
            string legacyName)
        {
            if (!TryGet(fields, primaryName, out object value)
                && !TryGet(fields, legacyName, out value))
            {
                return null;
            }
            if (value == null) return null;
            if (value is DateTimeOffset offset) return offset.ToUniversalTime();
            if (value is DateTime dateTime)
                return new DateTimeOffset(dateTime.ToUniversalTime());

            string text = ToInvariantString(value);
            if (DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return parsed;
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixMilliseconds))
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);

            throw new FormatException("Firebase inbox timestamp is invalid.");
        }

        private static bool ReadBoolean(IDictionary fields, string name)
        {
            if (!TryGet(fields, name, out object value) || value == null) return false;
            if (value is bool boolean) return boolean;
            return bool.TryParse(ToInvariantString(value), out bool parsed) && parsed;
        }

        private static bool TryReadPositiveLong(
            IDictionary fields,
            out long value,
            params string[] names)
        {
            value = 0;
            object rawValue = null;
            bool found = false;
            foreach (string name in names)
            {
                if (!TryGet(fields, name, out rawValue)) continue;
                found = true;
                break;
            }
            if (!found || rawValue == null) return false;

            if (rawValue is long longValue) value = longValue;
            else if (rawValue is int intValue) value = intValue;
            else if (rawValue is double doubleValue && Math.Abs(doubleValue % 1d) < double.Epsilon)
                value = checked((long)doubleValue);
            else if (!long.TryParse(
                         ToInvariantString(rawValue),
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out value))
                return false;

            return value > 0;
        }

        private static string ReadString(IDictionary fields, string name)
        {
            return TryGet(fields, name, out object value) ? ToInvariantString(value) : null;
        }

        private static bool TryGet(IDictionary dictionary, string name, out object value)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!string.Equals(ToInvariantString(entry.Key), name, StringComparison.Ordinal)) continue;
                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }

        private static string ToInvariantString(object value)
        {
            if (value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}

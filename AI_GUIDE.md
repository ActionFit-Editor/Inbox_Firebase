# AI Guide - ActionFit Inbox Firebase

This file ships with the UPM package so an AI assistant can preserve the Firebase inbox adapter boundary and its data-safety contract in consuming projects.

## Package Identity

- Package ID: `com.actionfit.inbox.firebase`
- Display name: ActionFit Inbox Firebase
- Repository: `https://github.com/ActionFitGames/Inbox_Firebase.git`
- Current package version at generation time: `1.0.3`
- Unity version: `6000.2`
- Runtime dependencies: `com.actionfit.inbox` 1.0.0 and `com.actionfit.connectivity` 1.0.0
- Optional SDK packages: validated `com.google.firebase.database` and `com.google.firebase.messaging` 12.10.1 packages

## Purpose

ActionFit Inbox Firebase implements the `IInboxBackend` boundary for Firebase RTDB, converts legacy and multi-attachment reward nodes into backend-neutral inbox models, maps failures without exposing sensitive data, adapts ActionFit Connectivity, and converts FCM topic/data messages into cache invalidation requests.

It does not own project reward mutation, receipt persistence, UI, identity generation, Firebase Auth, RTDB rules, production data migration, notification permission UI, or server transaction policy.

## Agent Skills

- `Skills~/manifest.json` registers schema v2 `inbox-firebase-help` and `inbox-firebase-audit` for Codex and Claude with read-only access.
- Help reads the generated `PACKAGE_SKILLS.md` inventory before explaining SDK isolation, RTDB paths and schema, failure mapping, connectivity, invalidation, tests, and project-owned boundaries.
- Audit remains offline and source-based. It must not inspect credentials or Firebase config values, access RTDB or FCM, install SDKs, change manifests or rules, invoke Unity, edit files, or expose user IDs, tokens, paths, messages, and attachment payloads.

## Project Router Registration

This package should be listed in `Packages/com.actionfit.custompackagemanager/PACKAGE_AI_GUIDE_ROUTER.md`.

Requested router entry:

- `Packages/com.actionfit.inbox.firebase/AI_GUIDE.md` - ActionFit Inbox Firebase owns RTDB inbox transport, legacy reward schema conversion, Firebase failure mapping, connectivity adaptation, and FCM cache invalidation signals.

Read this file when:

- changing files under `Packages/com.actionfit.inbox.firebase/`
- changing Firebase RTDB inbox paths or schema conversion
- changing Firebase Database or Messaging SDK bridges
- integrating `com.actionfit.inbox` with Firebase
- preparing a release for `com.actionfit.inbox.firebase`

## Assembly Boundary

- `com.actionfit.inbox.firebase` always compiles and contains SDK-neutral backend, parser, options, failure mapping, connectivity adapter, and invalidation event/binding.
- `com.actionfit.inbox.firebase.database` activates only when the validated `com.google.firebase.database` 12.10.1 package is installed. Only this assembly references `Firebase.App.dll` and `Firebase.Database.dll`.
- `com.actionfit.inbox.firebase.messaging` activates only when the validated `com.google.firebase.messaging` 12.10.1 package is installed. Only this assembly references `Firebase.App.dll` and `Firebase.Messaging.dll`.
- The core `com.actionfit.inbox` package must never reference Firebase assemblies.
- Do not add Firebase packages to a consuming project's manifest automatically. The project owns Firebase SDK distribution and configuration.

## RTDB Contract

The default root is `rewards`, producing `rewards/{userId}/{messageId}`. Root paths are configurable through `FirebaseInboxBackendOptions`. User and message IDs must be non-empty Firebase-safe path segments and may not contain `/`, `.`, `#`, `$`, `[` or `]`.

`FirebaseRealtimeDatabaseClient` reads child snapshots and removes one message node. Removing an already absent node is treated as success, preserving idempotent `ConfirmClaimAsync` behavior.

Never call remote confirmation before the inbox core has durably saved `RewardGranted`. The required reward/receipt ordering remains owned by `com.actionfit.inbox`.

## Schema Compatibility

Legacy Cat Merge Cafe nodes use `itemType`, `itemName`, `qty`, optional `title`, and optional `desc`. They map to one attachment whose ID is `<messageId>:legacy`.

New nodes may use an `attachments` map or list. Each attachment accepts `rewardType`/`itemType`, `itemKey`/`itemName`, `quantity`/`qty`, optional `attachmentId`, and optional string `attributes`. Message metadata accepts `title`, `body`/`desc`, `createdAtUtc`/`createdAt`, `expiresAtUtc`/`expiresAt`, and `isRead`.

Malformed messages are skipped by default for operational compatibility. Strict consumers may set `skipMalformedMessages: false`, which fails the load without partially returning an invalid response. Never silently keep only some attachments from one malformed multi-attachment message.

## Timeout, Retry, And Error Rules

- `FirebaseInboxBackendOptions.OperationTimeout` bounds each transport call and defaults to five seconds. Timeout maps to transient `InboxBackendException`.
- Network and unavailable transport failures are transient. Permission, invalid data, and unknown failures are non-transient by default.
- `InboxServiceOptions` owns retry count and delay. The Firebase adapter must not add a second hidden retry loop.
- Cancellation propagates as `OperationCanceledException`.
- Exception messages exposed by the adapter are fixed operation-level messages. Never include raw user IDs, message IDs, paths, message bodies, attachment payloads, credentials, tokens, or backend response bodies.

## Connectivity And FCM

`ActionFitConnectivityAdapter` maps only `ConnectivityState.Online` to the inbox core's minimal `IsOnline` contract and exposes the underlying `WaitForOnlineAsync` for explicit composition. It does not start monitoring or hidden probes.

`FirebaseInboxInvalidationAdapter` matches a configured data key/value — the default signal is topic `postbox_invalidate` with data `invalidate=postbox` — and emits `CacheInvalidationRequested`. `Bind(IInboxService)` translates that event to `InvalidateCache()` only. It must not automatically refresh, mutate UI, or claim rewards.

`FirebaseMessagingInboxInvalidationSource` owns only topic subscription and message-data forwarding. It must not log registration tokens or payloads. Notification permission and foreground/background UX remain consuming-project responsibilities.

## Testing

Run EditMode assembly `com.actionfit.inbox.firebase.Editor.Tests`. Keep all unit tests deterministic and SDK-neutral. Cover legacy and multi-attachment parsing, malformed policy, empty and duplicate results, exact paths, repeated confirmation, timeout, permission, transient network failure, cancellation, path validation, offline recovery, connectivity waiting, invalidation matching, and binding disposal.

Also compile the full consuming project when Firebase SDK packages are present so the optional Database and Messaging assemblies are validated. Do not use production RTDB, real user IDs, or FCM tokens in package tests.

## Package Tools Menu

- Unity menu root: `Tools/Package/ActionFit Inbox Firebase/`.
- `README` opens the installed package README.
- This package owns no settings ScriptableObject.

## Release Notes

- Publishing is manual through Custom Package Manager.
- Before reusing a version, check remote Git tags. Published tags are immutable.
- Update `package.json`, this guide, README, tests, and `Editor/PackageInfo/ActionFitPackageInfo_SO.asset` together for behavior changes.
- Do not create repositories, push package tags, publish, or append catalog rows unless the user explicitly requests publishing.

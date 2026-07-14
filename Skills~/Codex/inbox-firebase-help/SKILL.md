---
name: inbox-firebase-help
description: Explain ActionFit Inbox Firebase, its installed skills, SDK isolation, RTDB schema and path contracts, safe failure mapping, connectivity adaptation, cache invalidation, tests, and safety boundaries. Use when a user asks how the Firebase inbox adapter works or which package skill applies.
---

# ActionFit Inbox Firebase Help

Answer in the user's language. Explain the package without accessing Firebase, reading credentials or payloads, running an audit, subscribing to FCM, or executing tests unless the user separately requests that operation.

1. Read `PACKAGE_SKILLS.md` first. Treat its generated package identity, complete related-skill table, `$skill-name` invocations, descriptions, and access boundaries as authoritative.
2. Read `Packages/com.actionfit.inbox.firebase/README.md` and `Packages/com.actionfit.inbox.firebase/AI_GUIDE.md` when present. If downloaded, resolve `Library/PackageCache/com.actionfit.inbox.firebase@*` without editing it.
3. Explain the SDK-neutral core, optional Database and Messaging assemblies, RTDB path validation, legacy and multi-attachment conversion, timeout and error mapping, retry ownership, connectivity adaptation, and invalidation-only FCM boundary.
4. Keep Firebase installation/configuration, RTDB rules, identity, reward mutation, receipts, UI, notification permission, production data, and server transaction policy in the consuming project.
5. List `README` under `Tools > Package > ActionFit Inbox Firebase` and identify `com.actionfit.inbox.firebase.Editor.Tests` as the deterministic EditMode suite.
6. State that package help and audit must not access production RTDB, subscribe to topics, print tokens or payloads, install Firebase, change manifests or rules, claim rewards, publish, tag, or update the package catalog.

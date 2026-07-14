---
name: inbox-firebase-audit
description: Audit ActionFit Inbox Firebase source for SDK assembly isolation, safe RTDB paths and schema conversion, failure mapping, retry ownership, connectivity adaptation, and cache-only invalidation without accessing Firebase or changing state. Use when reviewing Firebase inbox adapter changes or integrations.
---

# Audit ActionFit Inbox Firebase

Keep the audit read-only, offline, and source-based. Do not access Firebase, inspect credentials or config values, subscribe to FCM, read production messages, contact RTDB, invoke Unity, edit manifests or rules, install SDKs, or publish package state.

1. Read the repository instructions only, so project routing and safety rules apply before inspection.
2. From the repository root, capture `git status --short --untracked-files=all` as the audit baseline. Preserve all pre-existing changes.
3. Resolve the physical package root from `Packages/com.actionfit.inbox.firebase`; otherwise use `Library/PackageCache/com.actionfit.inbox.firebase@*` without editing it. Then read the package `README.md` and `AI_GUIDE.md`, plus the consuming project's Firebase inbox architecture document and adapters when present.
4. Use `rg` and read-only file inspection to trace asmdefs and version defines, backend options, path validation, transport and parser boundaries, failure mapping, connectivity adaptation, invalidation binding, optional SDK bridges, and tests. Do not query runtime Firebase objects.
5. Verify and report evidence for these contracts:
   - The always-compiled core references only Inbox and Connectivity; Firebase Database and Messaging references stay in optional isolated assemblies.
   - The package does not add Firebase dependencies or rewrite a consuming project's manifest, config, or RTDB rules.
   - Caller-provided path segments and inbound transport node keys reject unsafe characters; repeated confirmation of an absent node converges successfully.
   - Legacy and multi-attachment conversion rejects malformed partial rewards according to the configured policy.
   - Timeout, network, unavailable, permission, invalid-data, unknown, and cancellation outcomes retain the documented transient and propagation rules without sensitive text in the top-level error, `InnerException`, or formatted exception chain.
   - Retry count remains owned by Inbox core; the adapter adds no hidden retry or connectivity monitoring.
   - The package invalidation binding only marks the cache stale and never refreshes, opens UI, claims rewards, or logs FCM tokens and payloads. Identify any consuming-project explicit refresh as separate project-owned behavior.
6. Inspect deterministic tests for schema, exact caller and inbound-node paths, idempotent confirmation, full exception-chain redaction, failure mapping, cancellation, connectivity waiting, invalidation matching, and binding disposal. Confirm SDK integration requires a separate consuming-project compile.
7. Capture the same Git status command again and compare it with the baseline. If state changed during the audit, report the paths and do not claim a no-change result.
8. Return findings grouped as passed contracts, risks, missing evidence, and recommended validation. Mention EditMode and full-project compile as follow-up commands; do not run them from this skill.

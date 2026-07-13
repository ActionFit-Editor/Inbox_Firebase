# Firebase SDK Integration Sample

This sample exposes helpers for creating the RTDB backend and FCM cache-invalidation source when the validated Firebase Database and Messaging 12.10.1 packages are installed.

The sample assembly remains disabled when either SDK package is missing. The consuming project still owns Firebase initialization, notification permission, `InboxService` construction, durable receipts, and idempotent reward handling.

# ActionFit Inbox Firebase (`com.actionfit.inbox.firebase`)

`com.actionfit.inbox`의 백엔드 계약을 Firebase Realtime Database와 Firebase Cloud Messaging에 연결하는 어댑터 패키지입니다. 우편함 코어는 Firebase SDK를 참조하지 않으며 이 패키지의 선택적 SDK assembly만 Firebase DLL을 참조합니다.

## 주요 기능

- `FirebaseInboxBackend`의 목록 조회, 단건 조회, 수령 확인 삭제
- 기본 `rewards/{userId}/{messageId}` RTDB 경로와 프로젝트별 root path 설정
- 기존 `itemType`, `itemName`, `qty`, `title`, `desc` 단일 보상 스키마 호환
- 신규 `attachments` map/list 기반 복수 보상, UTC 생성·만료 시각 및 읽음 상태 변환
- timeout, 네트워크, 일시적 사용 불가, 권한 오류를 `InboxBackendException`으로 안전하게 변환
- `ActionFitConnectivityAdapter`를 통한 `com.actionfit.connectivity` 연결 상태 사용
- `postbox_invalidate` topic과 `invalidate=postbox` data 신호를 `IInboxService.InvalidateCache()`로 연결
- 사용자 ID, 메시지 원문, 보상 payload, credential을 포함하지 않는 고정 오류 메시지

## Firebase SDK 설치 경계

패키지의 backend, schema, 오류 변환 및 invalidation 계약은 Firebase SDK 없이 독립 컴파일됩니다. 실제 SDK bridge는 다음 패키지가 설치됐을 때 각 assembly가 활성화됩니다.

- 검증 버전 `com.google.firebase.database` 12.10.1 → `com.actionfit.inbox.firebase.database`
- 검증 버전 `com.google.firebase.messaging` 12.10.1 → `com.actionfit.inbox.firebase.messaging`

Google Firebase Unity SDK의 배포 방식은 프로젝트가 소유합니다. 이 패키지는 Firebase 패키지를 자동 설치하거나 `Packages/manifest.json`, RTDB rules, 운영 데이터를 변경하지 않습니다.

## RTDB 구성

기본 경로는 다음과 같습니다.

```text
rewards/{userId}/{messageId}
```

기존 Cat Merge Cafe 메시지는 다음 형식을 그대로 읽습니다.

```json
{
  "itemType": "Cash",
  "itemName": "cash",
  "qty": 100,
  "title": "Gift",
  "desc": "Welcome"
}
```

복수 보상은 `attachments` 아래 map 또는 list로 전달할 수 있습니다.

```json
{
  "title": "Bundle",
  "body": "Two rewards",
  "createdAtUtc": "2026-07-13T10:00:00Z",
  "expiresAtUtc": "2026-07-20T10:00:00Z",
  "attachments": {
    "cash": {
      "rewardType": "Cash",
      "itemKey": "cash",
      "quantity": 100
    },
    "energy": {
      "rewardType": "Energy",
      "itemKey": "energy",
      "quantity": 5
    }
  }
}
```

기본 정책은 운영 호환을 위해 malformed 메시지만 건너뜁니다. 전체 응답을 엄격하게 거부해야 하는 프로젝트는 `FirebaseInboxBackendOptions(skipMalformedMessages: false)`를 사용하세요.

## 기본 구성

```csharp
using ActionFit.Inbox.Firebase;
using ActionFit.Inbox.Firebase.Database;
using Firebase.Database;

var databaseClient = new FirebaseRealtimeDatabaseClient(
    FirebaseDatabase.DefaultInstance.RootReference);
var backend = new FirebaseInboxBackend(
    databaseClient,
    new FirebaseInboxBackendOptions(
        rootPath: "rewards",
        operationTimeout: TimeSpan.FromSeconds(5)));
```

`FirebaseInboxBackend.ConfirmClaimAsync`는 메시지 노드를 삭제합니다. Firebase의 존재하지 않는 노드 삭제 성공 특성을 사용하므로 같은 메시지를 반복 확인해도 멱등입니다. 이 메서드는 `InboxService`가 보상 지급 성공과 `RewardGranted` 영속 저장을 마친 뒤에만 호출합니다.

## FCM 무효화

```csharp
var invalidation = new FirebaseInboxInvalidationAdapter();
IDisposable binding = invalidation.Bind(inboxService);
var messaging = new FirebaseMessagingInboxInvalidationSource(invalidation);
await messaging.StartAsync(cancellationToken);
```

`StartAsync`는 기본 `postbox_invalidate` topic을 구독합니다. `invalidate=postbox` data 메시지만 cache invalidation으로 변환하며 우편함 조회나 UI 갱신을 직접 실행하지 않습니다. topic과 data key/value는 `FirebaseInboxInvalidationOptions`로 주입할 수 있습니다.

## 연결 및 재시도

`ActionFitConnectivityAdapter`는 `com.actionfit.connectivity`의 `Online` 상태만 코어에 전달합니다. offline 상태에서는 코어가 backend를 호출하지 않습니다. 연결 복구 후 소비자가 `RefreshAsync`를 호출하면 RTDB를 다시 조회합니다. 일시적 backend 오류 재시도 횟수와 간격은 `InboxServiceOptions`가 소유합니다.

## 설치

현재 Cat Merge Cafe에서는 embedded package로 사용합니다. 수동 게시 후 다른 프로젝트에서는 다음 Git UPM 주소를 사용합니다.

```json
"com.actionfit.inbox.firebase": "https://github.com/ActionFitGames/Inbox_Firebase.git#1.0.3"
```

## Agent Skills

Custom Package Manager의 `Install or Refresh Agent Skills`를 실행하면 Codex와 Claude에 다음 read-only 진입점이 설치됩니다.

- `inbox-firebase-help`: SDK assembly 격리, RTDB schema/path, 오류 변환과 cache invalidation 경계를 설명합니다.
- `inbox-firebase-audit`: Firebase에 접속하지 않고 optional SDK assembly, 안전한 경로, retry 소유권과 invalidation-only 계약을 소스 기준으로 점검합니다.

스킬은 Firebase credential·payload를 읽거나 RTDB/FCM에 접근하고 manifest, RTDB rules, 운영 데이터를 변경하지 않습니다.

패키지 게시와 catalog 등록은 Custom Package Manager에서 수동으로 수행합니다.

## Unity Menu

- README: `Tools > Package > ActionFit Inbox Firebase > README`

## 테스트

EditMode assembly `com.actionfit.inbox.firebase.Editor.Tests`는 레거시/복수 보상 변환, 빈 결과, malformed 정책, 단건 조회, 멱등 확인, timeout·권한·네트워크 오류, cancellation, 안전한 경로, offline 복구 및 FCM invalidation binding을 검증합니다.

실제 RTDB rules, 운영 보상, FCM 전달, Android/iOS background 수신은 consuming project에서 별도 검증해야 합니다.

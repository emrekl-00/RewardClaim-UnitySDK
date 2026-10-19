# Reward Claims SDK for Unity

A small Unity SDK that:

1. **Manages player resources:** configurable resources such as coins and lives, with balances,
   spending and an optional max balance.
2. **Claims rewards through an API:** it POSTs the claim to httpbin's `/anything` endpoint and
   applies the reward once the server accepts it. A reward id is never granted twice.
3. **Preserves progress and survives network failures:** balances and unfinished claims survive
   restarts. No connection, timeouts and API errors are handled, and a pending claim never blocks
   spending.

It ships as a `.unitypackage` with a minimal usage example. This repo also has **Coin Catcher**, a
small playable mobile game built on the SDK.

| Download | What it is |
|---|---|
| [`Packages-out/RewardClaimsSDK-1.0.0.unitypackage`](Packages-out/RewardClaimsSDK-1.0.0.unitypackage) | The SDK plus the BasicUsage example (about 23 KB) |
| [`Packages-out/CoinCatcherSample-1.0.0.unitypackage`](Packages-out/CoinCatcherSample-1.0.0.unitypackage) | The sample game (needs the SDK package) |

---

## Requirements

- **Unity 6 (6000.x).** Built and tested with **6000.6.5f1**. The SDK awaits `UnityWebRequest`
  directly, which needs Unity 2023.1 or newer.
- **SDK dependencies:** none beyond Unity's built-in modules (UnityWebRequest, JSONSerialize). It
  works with any render pipeline; the fresh-project test used the Built-in 3D template.
- **Sample game dependencies:** Input System, uGUI and TextMeshPro. All three come with Unity 6's
  2D templates, and the package bundles TMP Essential Resources.

---

## Integration

### 1. Import the package
**Assets → Import Package → Custom Package…** → `RewardClaimsSDK-1.0.0.unitypackage`.
Everything lands in `Assets/RewardClaimsSDK/`:
- `Runtime/` is the SDK. It has its own assembly, `RewardClaims.Runtime`, and namespace `RewardClaims`.
- `Samples/BasicUsage/` is a runnable example scene.

### 2. Create a config asset
**Assets → Create → Reward Claims → Config**, then fill it in the Inspector:

| Field | Example | Notes |
|---|---|---|
| Player Id | `test-player` | Sent with every claim; also keys the saved progress |
| Api Url | `https://httpbin.org/anything` | Default |
| Request Timeout Seconds | `10` | A slower request counts as a timeout |
| Resources | `coins` (start 0, no max), `lives` (start 5, max 5) | Any ids your game needs |

`config.Validate()` lists every mistake (empty or duplicate ids, a max below the starting
balance, …), and `Initialize` refuses an invalid config with all those messages.

### 3. Initialize once at startup
```csharp
using RewardClaims;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private RewardClaimsConfig config;

    private void Awake()
    {
        if (!RewardClaimsSdk.IsInitialized)   // a reloaded scene must not initialize twice
            RewardClaimsSdk.Initialize(config);
    }
}
```
`Initialize` loads the saved progress and immediately retries any claim the last session couldn't
finish. It also retries when the app returns from the background.

### 4. Read and spend resources
```csharp
var wallet = RewardClaimsSdk.Wallet;

int coins = wallet.GetBalance("coins");

if (wallet.TrySpend("lives", 1))      // false (never an exception) when the balance is too low
    StartRun();

wallet.BalanceChanged += (resourceId, newBalance) => hud.Redraw(resourceId, newBalance);
```

### 5. Claim a reward and show its status
```csharp
ClaimRecord claim = await RewardClaimsSdk.Claims.ClaimAsync("level-10-first-completion", "coins", 100);

switch (claim.Status)
{
    case ClaimStatus.Succeeded: ShowToast($"+{claim.Amount} coins!"); break;
    case ClaimStatus.Failed:    ShowToast("Couldn't reach the server. We'll retry."); break;
    case ClaimStatus.Rejected:  ShowToast("Reward not available"); break;
}

// Or react to every status change, including background retries:
RewardClaimsSdk.Claims.ClaimStatusChanged += claim => UpdateRewardUi(claim);
```
`ClaimAsync` is safe to call again with the same reward id. A granted reward is never granted twice
(see [Claim rules](#claim-rules)).

### 6. Optional: your own storage or backend
```csharp
RewardClaimsSdk.Initialize(config, myStorage, myRewardApi);   // IStorage, IRewardApi
```
`IStorage` (Load/Save a string by key) lets a game use its own save system or cloud save.
`IRewardApi` lets it point at a real backend or wrap the default client. The sample game wraps it
to add a "Simulate offline" switch without touching SDK code.

Remember to unsubscribe from `BalanceChanged` / `ClaimStatusChanged` in `OnDisable` or
`OnDestroy`. The SDK lives for the whole app run, so it outlives your UI objects.

---

## API overview

| Type | Members |
|---|---|
| `RewardClaimsSdk` (static) | `Initialize(config)`, `Initialize(config, storage, api)`, `IsInitialized`, `Wallet`, `Claims` |
| `ResourceWallet` | `GetBalance`, `HasResource`, `CanSpend`, `TrySpend`, `Add` (returns the amount actually added), event `BalanceChanged(resourceId, newBalance)` |
| `ClaimManager` | `ClaimAsync(rewardId, resourceId, amount)`, `GetClaim(rewardId)`, `Claims`, `RetryUnfinishedClaimsAsync()`, event `ClaimStatusChanged(ClaimRecord)` |
| `ClaimRecord` (read-only) | `RewardId`, `ResourceId`, `Amount`, `Status`, `LastError`, `AttemptCount` |
| `ClaimStatus` | `Pending`, `Succeeded`, `Failed` (will be retried), `Rejected` (final) |
| `ClaimError` | `None`, `NoConnection`, `Timeout`, `ServerError`, `Rejected`, `InvalidResponse`, `Unknown` |
| `RewardClaimsConfig` | ScriptableObject: `PlayerId`, `ApiUrl`, `RequestTimeoutSeconds`, `Resources`, `Validate()` |
| Extension points | `IStorage` (`PlayerPrefsStorage`, `InMemoryStorage`), `IRewardApi` (`HttpBinRewardApi`), `ApiResult`, `ClaimPayload` |

**Error policy:** programmer mistakes throw, normal game situations don't.
- **Throws:** an unknown resource id, an amount ≤ 0, using the SDK before `Initialize`.
- **Returns `false` or a status:** not enough balance, no network, a server error.

Network problems never throw into game code.

---

## Claim rules

```
            ClaimAsync (new reward id)
                     │  saved BEFORE the request is sent
                     ▼
                 ┌────────┐   2xx + echo matches   ┌───────────┐
                 │Pending │ ─────────────────────▶ │ Succeeded │  reward added in the same save
                 └────────┘                        └───────────┘
                  │      │ 4xx                     ┌───────────┐
                  │      └───────────────────────▶ │ Rejected  │  final, never retried
                  │ no connection / timeout /      └───────────┘
                  │ 5xx / 408 / 429 / bad response ┌───────────┐
                  └──────────────────────────────▶ │  Failed   │ ── retry ──▶ Pending
                                                   └───────────┘
```

What `ClaimAsync` does when the reward id already exists:

| Existing claim | Result |
|---|---|
| `Succeeded` or `Rejected` | Returns the saved record; nothing is sent or granted |
| Request still in flight (e.g. a double tap) | Returns the **same task**; one request only |
| `Failed`, or `Pending` left over from a killed session | Sends it again, using the **original** resource and amount |
| Called with a different resource or amount | Keeps the original and logs a warning |

Unfinished claims (`Failed` or leftover `Pending`) are retried automatically in three cases:
- on `Initialize`;
- when the app returns from the background;
- when the game calls `RetryUnfinishedClaimsAsync()`.

---

## Design decisions

- **Plain C# core, Unity at the edges.** The wallet, claim manager and progress store are ordinary
  classes, so most behaviour is unit-tested in milliseconds without Play mode. Unity-specific
  parts sit behind two interfaces: `IStorage` (PlayerPrefs) and `IRewardApi` (UnityWebRequest).
  The static `RewardClaimsSdk` facade is the one door games use, and it wires everything together.
- **One saved document per player.** Balances and claims are one JSON document (`JsonUtility`)
  under `RewardClaims.Progress.<playerId>`. When a claim succeeds, "reward added" and "claim
  succeeded" are written **in a single save**, so a crash can't record one without the other.
  `PlayerPrefs.Save()` runs after every change, because a crash or an OS kill never reaches
  Unity's save-on-quit.
- **Save before send.** A claim is saved as `Pending` before its request goes out. If the app dies
  mid-request, the next launch still has the claim and finishes it.
- **Idempotency on the client, keyed by reward id.** httpbin remembers nothing, so the SDK
  enforces "at most once" itself: it checks the saved record, and it shares the in-flight task so
  double taps don't send two requests.
- **Retryable vs final errors.** No connection, timeouts, 5xx, 408 and 429 are temporary, so the
  claim stays `Failed` and is retried. Any other 4xx means the server refused this exact request,
  so it's `Rejected` for good. An exception from the API layer is caught and logged, and the claim
  becomes `Failed/Unknown`.
- **Tightly scoped `try/catch`.** Only the network call is wrapped, never the success handling. If
  a game's `BalanceChanged` listener throws after a reward was saved, the claim stays `Succeeded`.
  Otherwise it would be marked `Failed` and granted twice on retry. A unit test covers this case.
- **What counts as acceptance.** For httpbin, success means a 2xx whose echoed `reward_id`
  matches the claim. This also catches HTML error pages from captive portals.
- **Max balance clamps.** Adding beyond the max stops at the max, and `Add` returns what was
  actually added. A claim for a full resource still succeeds, and the SDK saves that status itself
  even when the wallet had nothing to add.
- **Pending claims reserve nothing.** The existing balance is the player's to spend. A pending
  reward isn't spendable until the server accepts it.
- **Config changes between game versions.** A new resource gets its starting balance. A lowered
  max clamps saved balances. Balances and claims of removed resources stay in the save untouched,
  and stale claims are skipped by retries instead of breaking them.
- **Main thread only.** Unity runs our code and every `await` continuation on the main thread, so
  the SDK uses no locks.
- **Static state is reset on entering Play mode,** so the SDK also behaves with "Enter Play Mode
  Options → no domain reload".

## Known limitations

- **Idempotency is client-side only.** If the server accepted a claim but the app died before
  saving, the retry sends the request again. A real backend must also dedupe by `reward_id`;
  httpbin can't.
- **The client is the authority.** PlayerPrefs is neither encrypted nor tamper-proof. A real game
  would keep balances on its server and treat the local save as a cache.
- **No timed retry or backoff.** Unfinished claims are retried at startup, on app resume, on a
  repeated `ClaimAsync`, or when the game calls `RetryUnfinishedClaimsAsync()`.
- **One player per app run.** The player id comes from the config asset. A game with a login
  system would need an overload that takes the id at runtime.
- **`ClaimRecord.Amount` is the requested amount,** not the amount the wallet could actually add
  after clamping.

---

## How it was tested

**202 EditMode unit tests** run headless, with fakes for storage and the network:

| Area | Test files |
|---|---|
| Config validation | `RewardClaimsConfigTests` |
| Wallet: spend, add, max clamp, overflow, events | `ResourceWalletTests`, `ResourceWalletEventTests` |
| Persistence, restarts, corrupt saves, config changes | `ProgressStoreTests`, `WalletPersistenceTests`, `InMemoryStorageTests`, `PlayerPrefsStorageTests` |
| Claim model and request JSON shape | `ClaimModelTests` |
| Claims: happy path, save-before-send, idempotency, double taps | `ClaimManagerTests` |
| Failures: every error type, Rejected is final, API exceptions, the throwing-listener case, retries | `ClaimFailureTests` |
| Killed mid-request, restart, retry, granted exactly once | `ClaimRestartTests` |
| Spending while a claim is pending | `SpendWhileClaimPendingTests` |
| HTTP result mapping (status codes, timeouts, echo check), no network | `HttpBinRewardApiTests` |
| Facade: init rules, startup retry | `RewardClaimsSdkTests` |

"In flight" is tested with a fake API whose answer the test releases through a
`TaskCompletionSource`.

**6 network tests against the real httpbin.** These are PlayMode tests marked `[Explicit]`, so
normal runs skip them:
- `/anything` → success;
- `/status/500` → ServerError;
- `/status/400` → Rejected;
- `/delay/10` with a 2 s timeout → Timeout;
- an unresolvable host → NoConnection;
- one end-to-end claim that is granted once.

**Manual checks:**
- **Unity Editor:** the BasicUsage scene played twice (the second run shows "already claimed").
  Coin Catcher was played through the offline → fail → restart → recovered scenario.
- **Android emulator** (Pixel 7, API 36, IL2CPP ARM64 APK):
  - a real claim succeeds;
  - in airplane mode the claim fails; after backgrounding the app, reconnecting and resuming, the
    SDK retries automatically and grants the reward once;
  - after a force-stop and relaunch, balances and claims are kept.
- **Fresh-project import:** the SDK package was imported into a new Built-in 3D project, and both
  packages into a new Universal 2D project. Both compiled with 0 errors, and both completed a real
  claim.

---

## Sample game: Coin Catcher

Catch falling coins with the basket. Reach the level's target before you miss too many. There are
5 levels, each faster than the last.
- **Controls:** drag (touch or mouse), or A/D and the arrow keys.
- **How it uses the SDK:**
  - every run costs **1 life** (`TrySpend`), with lives capped at 5;
  - the first completion of each level claims **100 coins** (`level-N-first-completion`);
  - the shop sells a life for 50 coins;
  - a **daily bonus** claims `daily-life-<date>`, so it's once per day by idempotency.
- **Debug tools:** a **Simulate offline** switch and a **claim history** panel make the SDK's
  behaviour visible.

All SDK calls live in [`Assets/SampleGame/Scripts/Sdk/`](RewardClaimsProject/Assets/SampleGame/Scripts/Sdk).
The gameplay scripts (`GameManager`, `Basket`, `Coin`, …) don't know the SDK exists.

**Try the case requirements yourself:**
1. Complete level 1 → "Claiming reward…" → "+100 coins!". Replay it → "Reward already claimed",
   and nothing is sent.
2. Turn on **Simulate offline** and complete level 2 → "Couldn't reach the server…". Buy a life in
   the shop meanwhile: spending works while the claim is unfinished.
3. Stop and restart the game, which starts online. The startup retry grants level 2's reward
   **once**, and the history shows *Succeeded · 2 attempts*.

**To run it:**
1. Open `Assets/SampleGame/Scenes/CoinCatcher.unity` (it's the first scene in the build list).
2. Pick a portrait Game view such as 1080×1920, and press Play.

**When importing the sample into another project:**
- Import the SDK package first, then the sample package.
- Keep the Input System enabled.
- Add the scene to the build list if you want to build a player.
- If the Game view and the Device Simulator are both open, `Screen.safeArea` and the screen size
  come from different views and the HUD looks squeezed. Use just one of them.

To start over, delete the PlayerPrefs key `RewardClaims.Progress.coin-catcher-player`, or use
**Edit → Clear All PlayerPrefs**.

---

## Working on this repo

```
rollic-case/
├── README.md
├── Packages-out/                     exported .unitypackage files
└── RewardClaimsProject/              Unity 6000.6.5f1 project
    └── Assets/
        ├── RewardClaimsSDK/
        │   ├── Runtime/              the SDK (RewardClaims.Runtime.asmdef)
        │   ├── Samples/BasicUsage/   minimal example scene + config
        │   └── Tests/                Editor (unit) and PlayMode (network) tests, not exported
        ├── SampleGame/               Coin Catcher (SampleGame.asmdef)
        ├── BuildTools/Editor/        package export and Android build scripts, not exported
        └── TextMesh Pro/             TMP Essential Resources (used by the sample)
```

The commands below use the Unity CLI (`unity`) from the repo root.
Close the Unity Editor first, because an open Editor locks the project.

- **Unit tests:** run this, or use **Window → General → Test Runner → EditMode**.
  ```bash
  unity test RewardClaimsProject --mode EditMode
  ```
- **Network tests (need internet):**
  ```bash
  unity test RewardClaimsProject --mode PlayMode --filter HttpBinNetworkTests
  ```
- **Export both packages to `Packages-out/`:** run this, or use **Build → Export Packages** in the
  Editor.
  ```bash
  unity run RewardClaimsProject -- -executeMethod BuildTools.PackageExporter.Export
  ```
- **Android APK** (needs Android Build Support): run this, or use **Build → Android APK**.
  ```bash
  unity build RewardClaimsProject --target Android --execute-method BuildTools.AndroidBuild.BuildApk --output-path RewardClaimsProject/Builds/Android/CoinCatcher.apk
  ```

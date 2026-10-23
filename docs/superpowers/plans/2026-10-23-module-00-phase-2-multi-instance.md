# Module 00 — Phần 2: Sẵn sàng nhiều máy API — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chạy được nhiều máy API song song sau một bộ cân bằng tải.
- Giới hạn tần suất đếm chung qua Redis.
- Production dừng khởi động khi cấu hình không đủ cho nhiều máy hoặc thiếu điều kiện bảo mật.
- Mọi máy giải được dữ liệu Data Protection của nhau.
- SignalR không cần sticky session.

**Architecture:**
- **Giới hạn tần suất:** `RedisFixedWindowRateLimiter` (Infrastructure) là một `RateLimiter` cho từng khoá phân vùng, cắm vào hai policy `AddRateLimiter` có sẵn qua một hàm chọn phân vùng. `RateLimiterSettings:Store` chọn `Memory` (mặc định, dev, test) hoặc `Redis`.
- **Kiểm cấu hình:** `ProductionTopologyValidator` là hàm thuần trả danh sách lỗi. `Program.cs` gọi nó chỉ khi khởi động web host ở môi trường Production.
- **Cấu hình triển khai:** `docker-compose.prod.yml` chạy Redis, SeaweedFS và một dịch vụ `migrate` chạy một lần trước `api`.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.RateLimiting`, StackExchange.Redis 3.1, Testcontainers.Redis 4.15, MailKit, Vue 3 + `@microsoft/signalr`, Docker Compose.

**Spec:** `docs/tech/00-nen-tang.md` v1.0:
- §2.1 (hàng `RedisFixedWindowRateLimiter`, `ProductionTopologyValidator`);
- §4.8;
- §10.2 bước 1;
- §10.3;
- §7.2 T16, T17;
- §11 (hàng *Giới hạn tần suất chung*);
- §12 QĐ-01, QĐ-02, QĐ-03, QĐ-04, QĐ-32.

Ràng buộc gốc: `docs/process/system-context.md` § Triển khai và vận hành.

## Global Constraints

- **Nhánh:** `feature/module-00`, làm sau phần 1 (Task 10 của phần 1 đã sửa `SmtpEmailSender`).
- **Commit:** Conventional Commits, stage theo đường dẫn. Message kết thúc bằng:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HaxzoPfz2fU7WZC8dYVjBu
  ```
- **C#:** theo `code-conventions.md`: không `var`, có `CancellationToken`. Test theo `testing.md`: `Method_Scenario_Expected`, xUnit `Assert`.
- **Giới hạn tần suất qua Redis:**
  - Cửa sổ cố định. Một script Lua: `INCR`, rồi nếu khoá chưa có hạn (`PTTL` = -1) thì `PEXPIRE` theo độ dài cửa sổ; trả `{count, pttl}`. Đếm bắt đầu từ request đầu tiên của cửa sổ.
  - Khoá có dạng `feedbackhub:ratelimit:{policy}:{partition}`.
  - Redis lỗi thì dùng limiter trong bộ nhớ của máy đó với cùng tham số, và ghi log cảnh báo tối đa một lần mỗi 60 giây cho mỗi policy. Không bao giờ fail-open.
- **Khoá phân vùng:** `HttpContext.Connection.RemoteIpAddress` sau `UseForwardedHeaders`, như hiện nay.
- **`RateLimiterSettings:Store`:** nhận `Memory` hoặc `Redis`, không phân biệt hoa thường, mặc định `Memory`. Giá trị khác làm khởi động thất bại.
- **`EmailSettings:SecureSocket`:**
  - Nhận `SslOnConnect`, `StartTls` hoặc `StartTlsWhenAvailable`.
  - Để trống thì suy từ `UseSsl`: `true` → `SslOnConnect`, `false` → `StartTlsWhenAvailable`.
  - Production phải khai rõ `SslOnConnect` hoặc `StartTls`.
- **Data Protection:** `SetApplicationName("FeedbackHub")`.
- **SignalR client:** `transport: signalR.HttpTransportType.WebSockets`, `skipNegotiation: true`.
- **Môi trường dev và test:** vẫn dùng được `Memory`, `None`, `Local`. `ApiFactoryFixture` không đổi hành vi.
- **Bí mật:** không đọc, không sửa `appsettings.json` hay `appsettings.*.json`, trừ `appsettings.Example.json`.

## Review Focus

1. **Redis chết giữa lúc đang bị dò mật khẩu:** giới hạn vẫn áp theo từng máy, không mở toang, và log không tràn. Test ở Task 1.
2. **Hai máy cùng đếm một IP:** tổng số request được nhận trên mọi máy không vượt `PermitLimit` trong một cửa sổ. Test ở Task 1 (hai limiter, cùng Redis).
3. **Lệnh `migrate` hoặc export OpenAPI chạy với cấu hình production chưa đủ:** không bị validator chặn, vì validator chỉ chạy khi khởi động web host. Test ở Task 4.
4. **Cấu hình ghi sai hoa thường** (`redis`, `seaweedfs`, `starttls`): được chấp nhận đúng như giá trị chuẩn. Riêng `StorageSettings:Provider` = `local` (chữ thường) vẫn bị coi là `Local` và bị chặn. Test ở Task 4.
5. **Request không có `RemoteIpAddress`** (test host, socket Unix): rơi vào phân vùng `"unknown"` như hiện nay, không ném lỗi. Test ở Task 2.

---

### Task 1: `RedisFixedWindowRateLimiter`

**Files:**
- Create: `backend/src/FeedbackHub.Infrastructure/Services/RateLimiting/RedisFixedWindowRateLimiter.cs`
- Create: `backend/tests/FeedbackHub.Infrastructure.Tests/TestSupport/RedisContainerFixture.cs`. Tách từ lớp `RedisFixture` lồng trong `StackExchangeRedisStringStoreTests.cs`, rồi cho test đó dùng lớp mới.
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Services/RateLimiting/RedisFixedWindowRateLimiterTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed class RedisFixedWindowRateLimiter : RateLimiter
  {
      public RedisFixedWindowRateLimiter(IConnectionMultiplexer redis, string key, int permitLimit, TimeSpan window,
          RateLimiter fallback, ILogger logger, TimeProvider timeProvider);
  }
  ```
  - `AttemptAcquireCore` luôn trả lease không được cấp, không gọi Redis. Middleware ASP.NET sẽ gọi tiếp `AcquireAsync`.
  - `AcquireAsyncCore` chạy script Lua. Kết quả `<= permitLimit` thì cấp, ngược lại từ chối.
  - `RedisException`, `RedisTimeoutException` hoặc `RedisConnectionException` → trả lease của `fallback.AcquireAsync`.
  - `GetStatistics` trả `null`.
  - `IdleDuration` trả `null` khi đang có request trong limiter hoặc `fallback.IdleDuration` là `null`; ngược lại trả thời gian từ lần acquire cuối. ASP.NET chỉ dọn phân vùng có `IdleDuration` khác `null`, nên trả `null` mãi sẽ giữ mọi phân vùng trong bộ nhớ.
  - `fallback` phải tự nạp lại (`AutoReplenishment = true`): limiter này không phải `ReplenishingRateLimiter` nên ASP.NET không nạp hộ; constructor từ chối fallback không tự nạp.

- [ ] **Step 1: Viết test thất bại (Redis Testcontainers)**

```csharp
AcquireAsync_WithinLimit_Grants ; AcquireAsync_OverLimit_Rejects
AcquireAsync_TwoLimitersSameKey_ShareOneCount          // Review Focus 2: limit 5, mỗi limiter 3 lần → đúng 5 được cấp
AcquireAsync_AfterWindow_CountsFromZero                // window 1 s
AcquireAsync_KeyAlwaysHasTtl                           // PTTL > 0 sau lần đầu
AcquireAsync_RedisDown_UsesFallbackAndStillLimits      // Review Focus 1: multiplexer trỏ cổng chết → fallback limit 2: lần 3 bị từ chối
AcquireAsync_RedisDown_LogsAtMostOncePerMinute         // 10 lần lỗi trong 1 phút giả (TimeProvider) → 1 cảnh báo
AttemptAcquire_NeverCallsRedisAndIsNotAcquired
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~RedisFixedWindowRateLimiterTests|FullyQualifiedName~StackExchangeRedisStringStoreTests"`
Expected: FAIL (lớp chưa có). Test cũ vẫn qua sau khi tách fixture.

- [ ] **Step 3: Cài đặt** theo Interfaces và Global Constraints.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(infra): add a Redis fixed-window rate limiter with a local fallback`

### Task 2: Cắm limiter vào các policy có sẵn

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Common/Settings/RateLimiterSettings.cs`
  - `backend/src/FeedbackHub.API/Program.cs` (khoảng L116–153)
  - `backend/src/FeedbackHub.API/appsettings.Example.json`
  - `backend/tests/FeedbackHub.API.Tests/FeedbackHub.API.Tests.csproj` (thêm `Testcontainers.Redis` 4.15.0)
- Create:
  - `backend/src/FeedbackHub.Infrastructure/Services/RateLimiting/RateLimitPartitions.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/RateLimiting/RateLimiterStore.cs`
- Test: `backend/tests/FeedbackHub.API.Tests/RateLimiting/RateLimitPartitionsTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `RateLimiterSettings.Store` (string, mặc định `"Memory"`).
  - `public static class RateLimiterStore`:
    - `public const string Memory = "Memory"`, `public const string Redis = "Redis"`;
    - `public static string Resolve(string? value)`: không phân biệt hoa thường; giá trị khác ném `InvalidOperationException`.
  - `public static class RateLimitPartitions`:
    - `public static RateLimitPartition<string> FixedWindow(HttpContext context, string policyName, int permitLimit, TimeSpan window, string store)`.
    - `Memory` → `RateLimitPartition.GetFixedWindowLimiter`, giống hiện nay (`QueueLimit = 0`).
    - `Redis` → `RateLimitPartition.Get(partitionKey, key => new RedisFixedWindowRateLimiter(...))`. Lấy `IConnectionMultiplexer`, `ILoggerFactory`, `TimeProvider` từ `context.RequestServices`. `fallback` là một `FixedWindowRateLimiter` mới cho mỗi limiter (limiter dispose fallback của nó), cùng tham số, `AutoReplenishment = true`. Không dùng `RateLimitPartition.GetFixedWindowLimiter` cho fallback: nó ép `AutoReplenishment = false`. Tên policy không có dấu `:`.
  - `Program.cs` gọi `RateLimiterStore.Resolve` một lần lúc cấu hình. Khi `Store = Redis`, bảo đảm `IConnectionMultiplexer` đã đăng ký: dùng lại `CachingExtensions.EnsureRedisMultiplexer`, chuyển nó sang `public` hoặc `internal` kèm `InternalsVisibleTo` theo cách repo đang làm. Hai policy `auth` và `auth-refresh` dùng `RateLimitPartitions.FixedWindow`.
  - `TimeProvider.System` đăng ký singleton nếu chưa có.

- [ ] **Step 1: Viết test thất bại**

Dựng một `WebApplication` tối giản trên `TestServer` với `AddRateLimiter` thật và một endpoint `[EnableRateLimiting("auth")]`, Redis Testcontainers:

```csharp
Resolve_AcceptsMemoryAndRedisIgnoringCase ; Resolve_UnknownValue_Throws
FixedWindow_RedisStore_ThroughRealMiddleware_Returns429AfterLimit
FixedWindow_MemoryStore_Returns429AfterLimit
FixedWindow_NoRemoteIp_UsesUnknownPartition              // Review Focus 5
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~RateLimitPartitionsTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**

Thêm `"Store": "Memory"` vào khối `RateLimiterSettings` của `appsettings.Example.json`.

- [ ] **Step 4: Chạy test, phải qua**

Run:
- lệnh ở Step 2;
- `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~TestHostConfigurationTests"`

Expected: PASS. Test host vẫn ở `Memory`.

- [ ] **Step 5: Commit** `feat(api): count rate limits in Redis when RateLimiterSettings:Store is Redis`

### Task 3: `EmailSettings:SecureSocket`

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Common/Settings/EmailSettings.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Email/SmtpEmailSender.cs`
  - `appsettings.Example.json`
- Create: `backend/src/FeedbackHub.Infrastructure/Services/Email/EmailSecureSocket.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Email/SmtpEmailSenderTests.cs`, `EmailSecureSocketTests.cs`

**Interfaces:**
- Produces:
  - `EmailSettings.SecureSocket` (string, mặc định `""`).
  - `internal static class EmailSecureSocket`:
    - `public const string SslOnConnect = "SslOnConnect"`, `StartTls = "StartTls"`, `StartTlsWhenAvailable = "StartTlsWhenAvailable"`;
    - `public static SecureSocketOptions Resolve(EmailSettings settings)`: không phân biệt hoa thường; rỗng thì suy từ `UseSsl`; giá trị lạ ném `InvalidOperationException`.

- [ ] **Step 1: Viết test thất bại**

```csharp
Resolve_Empty_UseSslTrue_IsSslOnConnect ; Resolve_Empty_UseSslFalse_IsStartTlsWhenAvailable
Resolve_ExplicitValuesIgnoringCase_MapToMailKitOptions ; Resolve_Unknown_Throws
SendAsync_SecureSocketStartTls_ConnectsWithStartTls
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~EmailSecureSocketTests|FullyQualifiedName~SmtpEmailSenderTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**

`SmtpEmailSender` dùng `EmailSecureSocket.Resolve` thay biểu thức `UseSsl ? … : …`. Thêm `"SecureSocket": ""` vào `appsettings.Example.json`.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS. Test `SendAsync_UseSslTrue_ConnectsWithSslOnConnect` cũ vẫn qua.

- [ ] **Step 5: Commit** `feat(email): choose the SMTP encryption mode with EmailSettings:SecureSocket`

### Task 4: `ProductionTopologyValidator`

**Files:**
- Create: `backend/src/FeedbackHub.API/Common/ProductionTopologyValidator.cs`
- Modify: `backend/src/FeedbackHub.API/Program.cs`
- Test: `backend/tests/FeedbackHub.API.Tests/Common/ProductionTopologyValidatorTests.cs`

**Interfaces:**
- Produces:
  - `public static class ProductionTopologyValidator`;
  - `public static IReadOnlyList<string> Validate(IConfiguration configuration)`: trả mỗi điều kiện sai một dòng tiếng Anh, nêu khoá cấu hình. Danh sách rỗng nghĩa là đạt.
- Các điều kiện, đọc từ `IConfiguration` bằng cùng tên section và lớp settings đang dùng:
  1. `CacheSettings:Provider` khác `Redis`, không phân biệt hoa thường.
  2. `RealtimeSettings:Backplane` khác `Redis`.
  3. `ConnectionStrings:Redis` rỗng.
  4. `StorageSettings:Provider` bằng `Local`, không phân biệt hoa thường.
  5. `RateLimiterSettings:Store` khác `Redis`.
  6. `DatabaseSettings:MigrateOnStartup` khác `false`. Mặc định của lớp là `true`, nên thiếu khoá cũng là lỗi.
  7. `ForwardedHeadersSettings:KnownProxies` và `KnownNetworks` đều rỗng.
  8. `AuthCookieSettings:Secure` khác `true`.
  9. `EmailSettings:SecureSocket` không phải `SslOnConnect` hay `StartTls`. Rỗng cũng là lỗi: production phải khai rõ.
- Chỗ gọi trong `Program.cs`:
  - đặt sau `CliCommands.TryRunAsync` (khi nó trả `null`) và chỉ khi `!isOpenApiExport && app.Environment.IsProduction()`;
  - danh sách khác rỗng → ném `InvalidOperationException` có tiêu đề "Production configuration is not ready for multiple API instances:" và mọi dòng lỗi.

- [ ] **Step 1: Viết test thất bại**

```csharp
Validate_CompleteProductionConfig_ReturnsNoErrors
Validate_EachConditionBroken_ReportsExactlyThatKey      // Theory: 9 dòng, mỗi dòng phá một điều kiện
Validate_MigrateOnStartupMissing_IsError
Validate_ValuesIgnoringCase_AreAccepted                 // Review Focus 4: "redis", "seaweedfs", "starttls"
Validate_StorageLocalLowercase_IsError
Program_CliCommandInProductionWithIncompleteConfig_IsNotBlocked   // Review Focus 3: chạy CliCommands với verb giả trong Production, cấu hình thiếu → không ném
```

Test cuối dựng host qua `WebApplicationFactory` với `UseEnvironment("Production")` và một `ICliCommand` test, hoặc tách phần quyết định "có gọi validator không" thành `bool ShouldValidate(bool isOpenApiExport, bool isCliCommand, IHostEnvironment env)` rồi test hàm đó. Chọn cách thứ hai nếu cách đầu cần sửa `ApiFactoryFixture`.

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~ProductionTopologyValidatorTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**

- [ ] **Step 4: Chạy lại, phải qua**

Run:
- lệnh ở Step 2;
- toàn bộ `backend/scripts/test.sh`.

Expected: PASS. Test host chạy ở `Development` nên không bị chặn.

- [ ] **Step 5: Commit** `feat(api): refuse to start in production without a multi-instance-ready configuration`

### Task 5: Data Protection và SignalR client không cần sticky session

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Infrastructure/Persistence/PersistenceExtensions.cs` (L29–30)
  - `frontend/src/stores/notifications.ts` (L81–84)
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/DataProtectionTests.cs`
  - `frontend/src/stores/__tests__/notifications.spec.ts`, hoặc chỗ test hiện có của store

**Interfaces:**
- Produces:
  - `AddDataProtection().SetApplicationName("FeedbackHub").PersistKeysToDbContext<AppDbContext>()`.
  - `withUrl(url, { withCredentials: true, transport: signalR.HttpTransportType.WebSockets, skipNegotiation: true })`.

- [ ] **Step 1: Viết test thất bại**

```csharp
DataProtection_TwoProvidersSameDatabase_UnprotectEachOthersPayload
// hai ServiceProvider độc lập, cùng DB Testcontainers, ContentRoot khác nhau → Protect ở A, Unprotect ở B thành công
```

```ts
it('connects the notification hub with WebSockets only and skips negotiation')
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run:
- `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~DataProtectionTests"`
- `bun run --cwd frontend test:run -- notifications`

Expected: FAIL. Test backend thất bại vì application name mặc định lấy theo content root.

- [ ] **Step 3: Cài đặt**

- [ ] **Step 4: Chạy lại, phải qua**

Run: các lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat: share data protection across API instances and connect SignalR over WebSockets only`

### Task 6: Cấu hình triển khai production và ghi quyết định

**Files:**
- Modify:
  - `docker-compose.prod.yml`
  - `.env.prod.example`
  - `.claude/decisions.md`
  - `backend/src/FeedbackHub.API/appsettings.Example.json` (chỉ ghi chú nếu cần)

**Interfaces:**
- Consumes: Task 2, Task 3, Task 4 (tên khoá cấu hình).
- Produces `docker-compose.prod.yml`:
  - `redis`: `redis:8-alpine`, lệnh `redis-server --appendonly yes --maxmemory-policy noeviction` (`.claude/decisions.md` 2026-10-16), có healthcheck `redis-cli ping`, có volume.
  - `seaweedfs`: cùng image và tag với `docker-compose.yml` (`chrislusf/seaweedfs:4.39`), có volume.
  - `migrate`: cùng image `api`, lệnh `migrate`, `restart: "no"`, `depends_on: postgres` healthy.
  - `api`:
    - `depends_on`: `migrate` (`service_completed_successfully`), `redis` healthy, `seaweedfs` started;
    - biến môi trường:
      - `CacheSettings__Provider: Redis`;
      - `RealtimeSettings__Backplane: Redis`;
      - `RateLimiterSettings__Store: Redis`;
      - `ConnectionStrings__Redis: redis:6379,abortConnect=false`;
      - `StorageSettings__Provider: SeaweedFS` và `SeaweedFsSettings__ServiceUrl: http://seaweedfs:8333`. Tên khoá lấy đúng theo `SeaweedFsSettings.cs`;
      - `DatabaseSettings__MigrateOnStartup: "false"`;
    - bỏ volume `feedbackhub-api-uploads`;
    - `start_period` ngắn lại vì migration không còn chạy lúc khởi động.
  - Ghi chú đầu tệp viết lại:
    - production chạy nhiều máy, Redis và kho file dùng chung là bắt buộc;
    - `appsettings.json` phải khai `EmailSettings:SecureSocket` (`SslOnConnect` hoặc `StartTls`) và khoá truy cập SeaweedFS;
    - nhân bản bằng `docker compose up -d --scale api=N`, khi đó nginx của `web` phải cân bằng tới các máy `api`.
- Produces `.claude/decisions.md`, các mục mới ở đầu, tiếng Anh, mỗi mục dưới khoảng 80 chữ:
  - QĐ-01: Redis bắt buộc ở production. Có dòng `Supersedes: 2026-08-25 — Cache + SignalR pick Memory/Redis via config; Redis is not hard-required`.
  - QĐ-02: giới hạn tần suất qua Redis có limiter cục bộ dự phòng.
  - QĐ-03: lệnh `migrate` chạy một lần, `MigrateOnStartup = false` ở production.
  - QĐ-04: tên ứng dụng Data Protection cố định, SignalR chỉ dùng WebSockets.
  - QĐ-32: các điều kiện bảo mật trong validator.

- [ ] **Step 1: Kiểm compose hợp lệ**

Run: `docker compose -f docker-compose.prod.yml --env-file .env.prod.example config --quiet`
Expected: không lỗi.

- [ ] **Step 2: Kiểm validator nhận cấu hình compose**

Viết test `Validate_DockerComposeProdEnvironment_OnlyMissesOperatorSecrets`. Test nạp các biến `environment:` của dịch vụ `api` từ `docker-compose.prod.yml` (đọc YAML) cộng `appsettings.Example.json`, rồi khẳng định lỗi duy nhất còn lại là `EmailSettings:SecureSocket`, vì người vận hành phải tự khai.

- [ ] **Step 3: Chạy test**

Run: `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~ProductionTopologyValidatorTests"`
Expected: PASS.

- [ ] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [ ] **Step 5: Commit** `chore(deploy): run production with Redis, shared storage and a one-shot migrate service`

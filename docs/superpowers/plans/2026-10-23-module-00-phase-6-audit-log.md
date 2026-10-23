# Module 00 — Phần 6: Đọc nhật ký kiểm toán — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23); quyền push/mở PR vẫn do người dùng.

**Goal:** Người có quyền xem nhật ký kiểm toán đọc được các mục ngữ nghĩa của tổ chức đang làm việc qua API-33:
- mới nhất trước, *Xem thêm* bằng cursor;
- chỉ tham số đã khai báo;
- không IP, không user agent.

Route snapshot thô `api/admin/audit-logs` bị bỏ.

**Architecture:**
- Lõi ghi nhật ký đã có từ phần 3: `AuditEvent`, `IAuditTrail`, danh mục `AuditEventCatalog` đủ 13 loại kèm tra ngược `TryResolve` (Task 1); M9, `AuditLog.Kind`, `AuditLog.ActorKind` (Task 2). Các mục do phần 3–5 ghi. Phần này không khai báo hay thêm loại nào.
- Phần này chỉ đọc:
  - `AuditCursor` (Application) mã hoá và giải mã khoá keyset;
  - `AuditLogRepository.ListEventsAsync` (Infrastructure) chạy truy vấn keyset trên chỉ mục một phần `kind = 'Event'`;
  - `AuditLogService.ListAsync` dựng `AuditEntryDto`, lọc tham số theo danh mục, bỏ dòng không ánh xạ được;
  - `CurrentOrganizationController` thêm `GET api/organization/audit-log`.
- `AuditLogsController` và mọi code chỉ phục vụ nó bị xoá.
- Màn hình `AuditLogView.vue` thuộc phần 8.

**Tech Stack:** .NET 10 (xUnit, NSubstitute, Testcontainers PostgreSQL), EF Core + Npgsql, System.Text.Json, orval.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0:
  - §2.1 (hàng `CurrentOrganizationController` … `AuditLogService`, `AuditLogRepository`);
  - §3.7 (đoạn cuối: không endpoint nào trả mục `Change` hay mục không có tổ chức);
  - §4.7 (`InvalidCursor`), §4.8 (ngoại lệ keyset), §4.10;
  - §5.1 (`DenyMode.Forbidden` cho nhật ký), §5.2 (hàng API-33);
  - §6.8, §7.2 T10, T19, §9 (API-33);
  - §11 (các hàng *Audit theo thuộc tính và mục không có tổ chức*, *Nhật ký kiểm toán (FR-68)*);
  - §12 QĐ-34, QĐ-36.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: US-27, FR-68, BR-83, BR-84, D12, D19; AC-161 (phần nhật ký), AC-335, AC-359–AC-375.

## Lộ trình module 00

Module 00 phát hành một lần, không có cờ tính năng (TECH00 §10.2). Mọi phần làm trên nhánh `feature/module-00`, và chỉ mở một PR vào `dev` sau phần cuối. Mỗi phần là một kế hoạch riêng, người dùng duyệt trước khi làm.

Bảng của phần 1, cập nhật theo quyết định của người điều phối 2026-10-23: lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục) chuyển từ phần 6 lên đầu phần 3, để mọi service dựng ở phần 3–5 tự ghi mục của mình. Hàng 5 ghi thêm API-23 (`accountCreated`), phần 5 làm cùng lời mời. Bảng này giống hệt nhau ở mọi kế hoạch phần 3–8.

| Phần | Nội dung | TECH00 |
|---|---|---|
| 1 | Sửa bảo mật B1, B2; M7, M8; `TokenIssuer` kiểm `Status`; audit loại cột nhạy cảm; `SmtpEmailSender` | §3.8, §4.9, §6.6, §6.7, §7.3 |
| 2 | Sẵn sàng nhiều máy: giới hạn tần suất qua Redis, `ProductionTopologyValidator`, `EmailSettings:SecureSocket`, Data Protection | §4.8, §10.3 |
| 3 | Lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục 13 loại); cổng tổ chức, admin hệ thống, phiên: `GetAccessAsync`, `OrganizationContextAuthorizationHandler`, refresh `org_id = null`, `isSystemAdministrator`, CLI `grant`/`revoke-system-admin`, `SystemOrganizationService`, API-01–API-05, M1, M2 (`system_administrators`) | §3.1, §3.5, §3.7 (lõi), §4.1, §4.5, §5.1, §5.2, §6.1, §6.5 |
| 4 | Vai trò và thành viên: `RoleGrantPolicy`, `IOrganizationLock`, `MemberService`, `RoleService`, M3–M5 | §3.2, §3.3, §4.3, §5.3, §5.4, §6.4 |
| 5 | Lời mời: M2 (`organization_invitations`), API-10–API-13, API-20–API-23, email mời | §3.4, §4.2, §4.4, §6.2, §6.3 |
| **6 (tệp này)** | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Nhánh và commit**
  - Làm trên nhánh `feature/module-00`, sau phần 5.
  - Commit theo Conventional Commits, tiêu đề ≤ 100 ký tự, stage từng tệp theo đường dẫn.
  - Mỗi message kết thúc bằng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    ```
- **C#:** theo `code-conventions.md`: không `var`, có `CancellationToken`. Lỗi 400 từ Application ném `DomainException(key)`, như `FieldRequired` ở phần 1.
- **Mã lỗi:** `InvalidCursor` có chữ ở `Messages.resx` (vi) và `Messages.en.resx` (en). Chữ phía frontend thuộc phần 8.
- **Test:** theo `testing.md`:
  - tên `Method_Scenario_Expected`;
  - Infrastructure trên Postgres thật (Testcontainers);
  - API qua HTTP với `[Collection(nameof(ApiCollection))]`, có 401 và 403.
- **API-33:**
  - `limit` mặc định 50, `[Range(1, 100, ErrorMessage = "FieldRange")]`. Ngoài khoảng → 400, không kẹp.
  - Cursor là base64url (không đệm) của chuỗi UTF-8 `"{ticks}:{id}"`:
    - `ticks` = `Timestamp.Ticks` (UTC);
    - `id` = `audit_logs.id`.
  - Thứ tự `"timestamp" DESC, id DESC`, đúng thứ tự `ListPagedAsync` cũ.
  - Lọc `organization_id = org_id` của JWT và `kind = 'Event'` trong SQL, không lọc sau khi tải.
- **Không trả** `ipAddress`, `userAgent`, `entityId`, `oldValues`, `newValues`, và không trả khoá nào ngoài khoá đã khai báo cho loại đó (BR-83 điểm 4, B1).
- **Không bao giờ** mặc định một `actor_kind` lạ hay `null` thành `Account` (C2).
- **Log:** dòng bị bỏ qua ghi cảnh báo chỉ kèm `id` của dòng. Không email, không tham số.
- **Contract:** export OpenAPI rồi `bun run --cwd frontend codegen`, một lần ở Task 6.
- **PR module 00:** mục `## Changelog` có dòng
  `- Removed: GET api/admin/audit-logs and GET api/admin/audit-logs/{id}; the organization audit log is now read at GET api/organization/audit-log.`
  (TECH00 §4.10, QĐ-36). Người mở PR ở cuối phần 8 chép dòng này.

## Review Focus

1. **Nhiều mục cùng `timestamp`** (một lần lưu ghi nhiều mục) nằm vắt qua ranh giới trang: *Xem thêm* không lặp, không sót mục nào. Test ở Task 2.
2. **Có mục mới chen vào đầu nhật ký giữa hai lần *Xem thêm*:** trang sau đi tiếp đúng chỗ, không lặp (AC-360). Test ở Task 2.
3. **Cursor bị sửa tay hoặc lấy từ tổ chức khác:** dạng hỏng → 400 `InvalidCursor`, không 500. Cursor hợp lệ mang `id` của tổ chức khác chỉ trả mục của tổ chức đang làm việc. Test ở Task 1 và Task 4.
4. **Dòng `Event` không chuẩn** (`action` lạ, `actor_kind` null hay lạ, khoá thừa, `new_values` không phải JSON object): dòng lạ bị bỏ, khoá thừa bị lọc, có cảnh báo chỉ kèm `id`, phản hồi vẫn 200. Một trang toàn dòng bị bỏ vẫn có `nextCursor`. Test ở Task 3.
5. **Tài khoản người làm đã bị xoá hoặc đổi tên:** `actorName` là `null` hoặc tên hiện tại. `actorKind = SystemAdministrator` luôn có `actorName = null`, kể cả khi tài khoản còn. Test ở Task 2 và Task 3.

---

### Task 1: `AuditCursor`

**Files:**
- Create: `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditCursor.cs`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/AuditLogs/AuditCursorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public readonly record struct AuditCursor(DateTime TimestampUtc, long Id)
  {
      public string Encode();
      public static bool TryDecode(string value, out AuditCursor cursor);
  }
  ```

- [ ] **Step 1: Viết test thất bại**

```csharp
[Fact] public void Encode_ThenTryDecode_RoundTripsUtcTimestampAndId()   // Kind == Utc sau khi giải mã
[Fact] public void Encode_IsBase64UrlWithoutPadding()                    // không '+', '/', '='
[Theory]
[InlineData("@@@")]                         // base64 sai
[InlineData("MTIz")]                        // "123": thiếu ':'
[InlineData(/* b64url("abc:5") */)]         // ticks không phải số
[InlineData(/* b64url("-1:5") */)]          // ticks âm (ngoài DateTime.MinValue.Ticks..MaxValue.Ticks)
[InlineData(/* b64url("3155378976000000000:5") */)]   // > DateTime.MaxValue.Ticks
[InlineData(/* b64url("638000000000000000:0") */)]    // id ≤ 0
[InlineData(/* b64url("638000000000000000:x") */)]
[InlineData(/* b64url("638000000000000000:5:6") */)]  // thừa ':'
[InlineData(/* b64url(" 638000000000000000:5") */)]   // khoảng trắng, dấu '+'
public void TryDecode_Malformed_ReturnsFalse(string value)
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~AuditCursorTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**
  - Đọc số bằng `long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, …)`.
  - Mọi ngoại lệ giải mã đều thành `false`, không bao giờ ném (B5).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(audit): encode and strictly decode the audit log keyset cursor`

### Task 2: `AuditLogRepository.ListEventsAsync`, truy vấn keyset

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/IAuditLogRepository.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Repositories/AuditLogRepository.cs`
- Create: `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditEventRow.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/Repositories/AuditLogRepositoryTests.cs` (viết lại)

**Interfaces:**
- Consumes:
  - Task 1.
  - Phần 3 Task 2: cột M9 trên `AuditLog`: `Kind` (`AuditLogKind`, `Change = 1`, `Event = 2`, lưu chuỗi) và `string? ActorKind` (`actor_kind` varchar(20) null).
- Produces:
  - `record AuditEventRow(long Id, DateTime Timestamp, string EntityName, string Action, string? ActorKind, string? ActorName, string? NewValues)`. `ActorKind` là chuỗi thô của cột, để Application tự quyết dòng lạ.
  - `IAuditLogRepository.ListEventsAsync(Guid organizationId, AuditCursor? before, int take, CancellationToken ct) → Task<IReadOnlyList<AuditEventRow>>`.
  - `ListPagedAsync` và `GetByIdAsync` cũ bị xoá ở Task 4, cùng lúc với nơi gọi chúng.

- [ ] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
ListEventsAsync_ReturnsOnlyEventRowsOfThatOrganization
// cùng tổ chức có dòng Change (snapshot của ApiKey, B2) → không trả; tổ chức khác → không trả; organization_id null → không trả
ListEventsAsync_OrdersByTimestampDescThenIdDesc
ListEventsAsync_ManyRowsSameTimestamp_PagesWithoutRepeatOrGap        // Review Focus 1: 5 dòng cùng timestamp, take 2 → đọc hết 5 qua 3 trang
ListEventsAsync_RowInsertedBeforeFirstPageAfterRead_NextPageUnaffected   // Review Focus 2, AC-360
ListEventsAsync_ActorNameIsCurrentAccountName                        // đổi Account.Name sau khi ghi → tên mới
ListEventsAsync_ActorAccountDeleted_ActorNameNull                    // Review Focus 5
ListEventsAsync_ReturnsRawActorKindIncludingNullAndUnknown           // "Account", null, "Bogus" đều đọc được, không ném
ListEventsAsync_UsesPartialEventIndex                                // EXPLAIN của câu SQL sinh ra có tên chỉ mục một phần của M9; chạy `SET LOCAL enable_seqscan = off` trong transaction của test trước `EXPLAIN`, vì với vài dòng Postgres chọn quét tuần tự
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~AuditLogRepositoryTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt theo TECH00 §6.8 bước 1**
  - Điều kiện keyset viết `log.Timestamp < t || (log.Timestamp == t && log.Id < id)`.
  - `LEFT JOIN accounts` theo `user_id = account.Id::text` như truy vấn cũ.
  - `ORDER BY "timestamp" DESC, id DESC`, `Take(take)`, `AsNoTracking`.
  - `AuditLog.ActorKind` là `string?` (phần 3 Task 2), đọc thẳng; không đổi sang enum ở tầng này, để giá trị lạ trong cột không làm EF ném. Test `ReturnsRawActorKindIncludingNullAndUnknown` ghim điều này.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(audit): read an organization's semantic audit entries with a keyset query`

### Task 3: `AuditLogService.ListAsync` và `AuditEntryDto`

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/IAuditLogService.cs`
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditLogService.cs`
  - `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Create:
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditLogQuery.cs`
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditEntryDto.cs` (cùng `AuditEntryPage`)
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/AuditLogs/AuditLogServiceTests.cs` (viết lại)

**Interfaces:**
- Consumes:
  - Task 1, Task 2.
  - Phần 3 Task 1: `ActorKind { Account = 1, SystemAdministrator = 2, System = 3 }`, `AuditEventType`, `AuditEventCatalog.TryResolve(string entityName, string action, out AuditEventType type)`, `AuditEventCatalog.Get(type).Parameters` (khoá → `AuditParameterKind { Text, TextList, Role, RoleList }`).
  - Phần 3 Task 10: `RoleRefDto(Guid Id, string Name, SystemRoleKind? SystemKind)`.
- Produces:
  - `record AuditLogQuery(string? Cursor = null, [Range(1, 100, ErrorMessage = "FieldRange")] int Limit = 50)`.
  - `record AuditEntryDto(long Id, DateTime OccurredAt, ActorKind ActorKind, string? ActorName, AuditEventType Type, IReadOnlyDictionary<string, object> Parameters)`. Giá trị theo `AuditParameterKind` của khoá: `Text` → `string`, `TextList` → `IReadOnlyList<string>`, `Role` → `RoleRefDto`, `RoleList` → `IReadOnlyList<RoleRefDto>` (đúng kiểu `parameters` của TECH00 §4.10). Không dùng `JsonElement`: controller ghi JSON bằng Newtonsoft (`ConfigureNewtonsoftJsonOptions`), không ghi đúng kiểu đó.
  - `record AuditEntryPage(IReadOnlyList<AuditEntryDto> Items, string? NextCursor)`.
  - `IAuditLogService.ListAsync(AuditLogQuery query, CancellationToken ct) → Task<AuditEntryPage>`.
  - `ApplicationMessages.InvalidCursor` (400). Chữ: vi "Không tải tiếp được danh sách. Hãy tải lại trang." / en "The list could not continue. Reload the page."

- [ ] **Step 1: Viết test Application thất bại**

```csharp
ListAsync_MalformedCursor_Throws400InvalidCursorWithoutQuerying       // repository DidNotReceive
ListAsync_AsksForLimitPlusOne_NextCursorFromLimitthRow
ListAsync_FewerThanLimitPlusOne_NextCursorNull
ListAsync_ParametersOnlyDeclaredKeys                                  // new_values có "email", "role", "tokenHash", "ip" → chỉ "email", "role" (B1)
ListAsync_ParametersConvertedByDeclaredKind                           // "role" → RoleRefDto, "rolesAfter" → list RoleRefDto, "permissionCodes" → list string
ListAsync_DeclaredKeyWithWrongShape_SkipsRowAndWarns                  // "email": 5, "role": "Owner" → bỏ dòng
ListAsync_UnknownAction_SkipsRowAndWarnsWithIdOnly                    // Review Focus 4
ListAsync_NullOrUnknownOrNumericActorKind_SkipsRowNeverAccount       // null, "Bogus", "1" → bỏ (C2)
ListAsync_NewValuesNotJsonObject_SkipsRowAndWarns
ListAsync_AllRowsOfPageSkipped_ItemsEmptyButNextCursorKept
ListAsync_SystemAdministratorActor_ActorNameNullEvenIfAccountExists  // AC-368, Review Focus 5
ListAsync_AccountActor_ActorNameFromRow
ListAsync_PersonalContext_ThrowsForbiddenOrganizationAccessDenied    // lưới sau cổng
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~AuditLogServiceTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt theo TECH00 §4.10 và §6.8**
  - Tổ chức lấy từ `ICurrentTenantProvider.OrganizationId`, như `AuditLogService` hiện tại.
  - `Cursor` khác `null` mà `TryDecode` sai → `throw new DomainException(ApplicationMessages.InvalidCursor)`.
  - Gọi `ListEventsAsync(org, cursor, limit + 1)`. `NextCursor` = khoá của dòng thô thứ `limit` khi có dòng thứ `limit + 1`. Tính trên dòng thô, trước khi bỏ dòng lạ.
  - `ActorKind` của dòng:
    - parse bằng `Enum.TryParse` phân biệt hoa thường, rồi `Enum.IsDefined`, để chuỗi số như `"1"` không lọt;
    - lỗi thì bỏ dòng, log `"Skipped audit entry {AuditLogId}"`.
  - `ActorName` chỉ khi `ActorKind == Account`.
  - `Type`: `AuditEventCatalog.TryResolve(row.EntityName, row.Action, out type)` sai → bỏ dòng, log như trên.
  - `Parameters`: parse `NewValues` bằng `System.Text.Json` thành JSON object; với mỗi khoá trong `AuditEventCatalog.Get(type).Parameters`, đổi giá trị theo `AuditParameterKind` sang kiểu ở Interfaces; bỏ mọi khoá khác. Không phải object, hoặc một khoá đã khai có hình dạng sai → bỏ dòng.
  - `ActorKind` và `AuditEventType` ra JSON dạng tên nhờ `StringEnumConverter` toàn cục (`ConfigureNewtonsoftJsonOptions`) và `EnumSchemaTransformer` có sẵn; không thêm thuộc tính. Test API ở Task 4 ghim chuỗi.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(audit): build audit entries with declared parameters only and skip unmappable rows`

### Task 4: API-33, bỏ `api/admin/audit-logs`

**Files:**
- Modify:
  - `backend/src/FeedbackHub.API/Controllers/CurrentOrganizationController.cs`
  - `IAuditLogService.cs`, `AuditLogService.cs`, `IAuditLogRepository.cs`, `AuditLogRepository.cs` (xoá `GetAllAsync`, `GetByIdAsync`, `ListPagedAsync` và repo `GetByIdAsync`)
- Delete:
  - `backend/src/FeedbackHub.API/Controllers/AuditLogsController.cs`
  - `backend/src/FeedbackHub.Application/Services/AuditLogs/AuditLogDto.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/AuditLogsControllerTests.cs`
- Test: `backend/tests/FeedbackHub.API.Tests/Controllers/OrganizationAuditLogTests.cs`

**Interfaces:**
- Consumes: Task 3.
- Produces: `GET api/organization/audit-log`, `[Authorize(Policy = Permissions.AuditLogsView)]`, `[FromQuery] AuditLogQuery query`, trả 200 `AuditEntryPage`.

- [ ] **Step 1: Viết test API thất bại**

```csharp
AuditLog_Anonymous_Returns401
AuditLog_PersonalContext_Returns403OrganizationAccessDenied           // AC-366
AuditLog_MemberWithoutAuditLogsView_Returns403                        // AC-362
AuditLog_ApiKey_Returns403                                            // B7
AuditLog_SuspendedOrganization_Returns403OrganizationSuspended
AuditLog_LimitZeroOr101_Returns400 ; AuditLog_MalformedCursor_Returns400InvalidCursor
AuditLog_OnlyEventEntriesOfCurrentOrganization                        // AC-363; dòng Change và dòng organization_id null không ra (T19)
AuditLog_CursorFromOtherOrganization_ReturnsOnlyCurrentOrganizationRows   // Review Focus 3
AuditLog_ResponseHasNoIpUserAgentEntityIdOrRawValues                  // AC-371: JSON không có "ipAddress", "userAgent", "entityId", "oldValues", "newValues"
AuditLog_EnumsSerializeAsNames                                        // "actorKind": "Account", "type": "InvitationSent"
AuditLog_ViewerWithoutMembersManage_SeesEmailInParameters             // AC-335, D12
AuditLog_PagesNewestFirstWithNextCursor                               // AC-360
OldAdminAuditLogsRoutes_SignedIn_Return404                            // GET api/admin/audit-logs và /{id}
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~OrganizationAuditLogTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**
  - Thêm action. Xoá controller cũ cùng các phương thức, DTO và test chỉ phục vụ nó.
  - Kiểm không còn tham chiếu: `codegraph explore "AuditLogDto ListPagedAsync AuditLogsController"` không ra kết quả trong `backend/src`.

- [ ] **Step 4: Chạy lại, phải qua**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~AuditLog"`
Expected: PASS.

- [ ] **Step 5: Commit** `feat(api): serve the audit log at api/organization/audit-log and drop api/admin/audit-logs`

### Task 5: Mỗi thao tác của danh mục ghi đúng một mục, đúng tổ chức, kiểm qua API-33

**Files:**
- Test: `backend/tests/FeedbackHub.API.Tests/Controllers/AuditLogEndToEndTests.cs`
- Modify (chỉ khi test tìm ra thiếu): service của phần 3–5 còn thiếu lời ghi sự kiện.

**Interfaces:**
- Consumes: Task 4; mọi endpoint của phần 3–5.
- Danh mục (13 loại, tham số, `TryResolve`) do phần 3 Task 1 khai và kiểm (`AuditEventCatalogTests`: `Get_Definitions_MatchTech00Catalogue`, `TryResolve_StoredFormOfEveryType_ReturnsSameType`). Phần này không khai lại, không thêm loại nào.

- [ ] **Step 1: Viết test thất bại**

```csharp
// API qua HTTP, người đọc chỉ có auditlogs.view
EachModule00Operation_ProducesExactlyOneEntryVisibleAtApi33
// lần lượt API-04, 05, 07, 08, 09, 11, 12, 13 (chờ), 13 (hết hạn), 16, 17, 18, 21, 26 → mỗi thao tác đúng một mục mới, đúng type và khoá (AC-367, AC-372–AC-374)
RejectedOperations_AddNoEntry                                        // API-08 tự gỡ (409), API-11 thiếu quyền (403) → mục mới nhất không đổi (AC-370)
ResendExpiredInvitation_SingleInvitationSentEntry                    // AC-325, AC-373
CreateOrganization_NoEntry                                           // AC-364
AcceptFromOrganizationB_EntryInAOnly ; AcceptFromPersonalContext_EntryInA   // A1
RegisterWithInvitation_EntryInAWithNewAccountAsActor                 // actorName = tên tài khoản vừa tạo
SystemAdministratorNotMember_DeactivatesA_EntryInAAsSystemAdministrator      // AC-368: actorName null, không có ở tổ chức C đang làm việc
SystemAdministratorMember_DeactivatesThenReactivatesA_BothEntriesAccountWithName   // AC-375
EntryKeepsActorKindAfterActorLeavesOrganization
```

- [ ] **Step 2: Chạy test, phải thất bại hoặc qua**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~AuditLogEndToEndTests"`
Expected: FAIL nếu một đường ghi còn thiếu. Nếu tất cả đã qua ngay, ghi lại trong báo cáo task rằng phần 3–5 đã đủ.

- [ ] **Step 3: Bổ sung phần còn thiếu**
  - Test cho thấy danh mục của phần 3 lệch bảng §3.7 → dừng, báo controller; không sửa danh mục ở phần này.
  - Một endpoint chưa ghi mục → thêm lời gọi `IAuditTrail.RecordAsync(AuditEvent.For(…))` ở service của nó, trước `SaveChangesAsync`, với tổ chức lấy từ entity đã tra (C1).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `test(audit): pin the module 00 event catalogue end to end through the audit log endpoint`. Nếu Step 3 có sửa code thì dùng `fix(audit): …` và nêu service đã bổ sung.

### Task 6: Contract, bản đồ tính năng, gate

**Files:**
- Modify: `shared/openapi/openapi.json` (export), `docs/feature-map.md`

**Interfaces:**
- Consumes: Task 4, Task 5.

- [ ] **Step 1: Export contract và sinh lại client**

Run:
- `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
- `bun run --cwd frontend codegen`

Expected:
- `openapi.json` có `/api/organization/audit-log` với `cursor`, `limit`, và schema `AuditEntryPage`;
- không còn `/api/admin/audit-logs`;
- `frontend/src/api/generated/audit-logs/` bị orval xoá (`clean: true`).

- [ ] **Step 2: Frontend vẫn biên dịch**

Run: `bun run --cwd frontend build-only`, rồi `bun run --cwd frontend type-check`, rồi `bun run --cwd frontend test:run`.
Expected: PASS. Hiện không có code tay nào gọi client cũ; nếu type-check báo một chỗ, xoá lời gọi đó.

- [ ] **Step 3: Thêm mục `## Nhật ký kiểm toán` vào `docs/feature-map.md`**
  - PRD: FR-68, BR-83, BR-84.
  - Backend: `GET /api/organization/audit-log` → `CurrentOrganizationController` → `AuditLogService` → `AuditLogRepository.ListEventsAsync`; quyền `auditlogs.view`.
  - Ghi chú: route cũ `api/admin/audit-logs` đã bỏ.
  - Test của Task 1–5.
  - Dòng *Vào bằng* và *Frontend* ghi "phần 8 (`AuditLogView.vue`, route `/organization/audit-log`)".

- [ ] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [ ] **Step 5: Commit** `chore(contract): export the audit log endpoint and map the feature`

## Giả định về phần khác

- **Phần 3 Task 2 (M9, `AuditTrail`):**
  - `AuditLog` có `Kind` (`AuditLogKind`, lưu chuỗi `'Change'`/`'Event'`) và `string? ActorKind` (cột `actor_kind`).
  - Chỉ mục một phần `IX_audit_logs_event_keyset` trên `(organization_id, "timestamp" DESC, id DESC) WHERE kind = 'Event'` đã có.
  - Mục `Event` có `ip_address`, `user_agent` là `null`; `action` là `AuditEventType.ToString()`, `entity_name` lấy từ danh mục; `role` ghi thành `{ "id", "name", "systemKind" }`.
- **Phần 3 Task 1 (danh mục):** `AuditEventCatalog.TryResolve(string entityName, string action, out AuditEventType type)` là tra ngược duy nhất; khoá và kiểu tham số lấy từ `AuditEventCatalog.Get(type).Parameters`. Phần 6 không dựng ánh xạ thứ hai.
- **Phần 3 (cổng):**
  - Policy `auditlogs.view` dựng `OrganizationContextRequirement` với `DenyMode.Forbidden`.
  - Khoá API bị cổng từ chối 403.
  - `CurrentOrganizationController` có sẵn.
- **Phần 3–5:** mọi thao tác ở bảng §3.7 đã ghi mục của mình. Task 5 chỉ bổ sung khi test tìm ra chỗ thiếu.
- **Phần 8:** `AuditLogView.vue`, chữ locale cho từng loại và cho `InvalidCursor`.

## Câu hỏi mở

Không có. TECH00 §4.10, §6.8 và QĐ-36 đã quyết mọi điều phần này cần.

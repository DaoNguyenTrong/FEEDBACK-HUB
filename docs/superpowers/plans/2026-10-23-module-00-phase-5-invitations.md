# Module 00 — Phần 5: Lời mời — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23). **Push/PR:** người dùng cho phép (2026-10-23): sau khi phần 8 xong và `scripts/test-gate.sh` qua, push `feature/module-00` và mở một PR vào `dev` cho cả module 00; không bao giờ vào `main`.

**Goal:** Lời mời là đường duy nhất để vào một tổ chức đã có:
- người có quyền quản lý thành viên gửi, mời lại, thu hồi, xoá lời mời và xem danh sách;
- người được mời mở link, chấp nhận bằng tài khoản đang đăng nhập, hoặc đăng ký mới và vào thẳng tổ chức;
- email mời gửi đồng bộ, lỗi thì không đổi gì.

**Architecture:**
- **Domain:** entity `OrganizationInvitation` (cài `IHasOrganization`). Hạn đúng 7×24 giờ tính trong `Create`. Trạng thái không lưu: dòng còn là đang chờ hoặc hết hạn, dùng hay thu hồi thì xoá dòng.
- **Application:** `InvitationService` làm API-10–API-13 và API-20–API-22.
  - Mọi thao tác ghi chạy trong `ExecuteInTransactionAsync`, mở đầu bằng `IOrganizationLock.AcquireAsync`.
  - BR-58 qua `RoleGrantPolicy` (hàm thuần) của phần 4; đầu vào (`RoleGrantCaller`, `RoleGrantRole`) do `RoleGrantReader` của phần 4 đọc thẳng từ cơ sở dữ liệu sau khoá.
  - Mỗi sự kiện của danh mục §3.7 ghi qua `IAuditTrail` của phần 3, cùng lần lưu với thay đổi nó mô tả.
  - API-22 phát phiên qua `TokenIssuer.IssueTokensAsync`.
- **Infrastructure:** migration M2 (phần `organization_invitations`). `OrganizationInvitation` loại khỏi audit tự động.
- **API:**
  - Các action quản lý nằm trên `CurrentOrganizationController` (`api/organization`, phần 3 dựng).
  - `InvitationsController` mới cho `api/invitations`.
  - `AuthController` thêm `register-with-invitation`; `external/{provider}` thêm `accountCreated`.
  - Policy giới hạn tần suất mới `invitation`.
- Phần này không có giao diện. Màn hình *Members › Lời mời* và *InviteAccept* thuộc phần 8.

**Tech Stack:** .NET 10 (xUnit, NSubstitute, Testcontainers PostgreSQL), EF Core + Npgsql, MailKit (qua `IEmailSender`), orval.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0:
  - §2.1 (các hàng `OrganizationInvitation`, `InvitationService`, `InvitationsController`, `AuthController`);
  - §3.4, §3.7 (các loại `InvitationSent`, `InvitationRevoked`, `InvitationDeleted`, `MemberJoined`);
  - §4.2 (API-10–API-13), §4.4 (API-20–API-23), §4.6 (`InvitationDto`, `InvitationLookupDto`), §4.7, §4.8;
  - §5.2 (các hàng API-10–API-13, API-20–API-22), §5.3 (các hàng lời mời), §5.4;
  - §6.2, §6.3, §7.2 T1–T3, T13, §7.3;
  - §9 (chỉ mục), §10.1 M2, §10.2 (N-new-3), §11 (các hàng *Lời mời…*, *Mở và chấp nhận lời mời…*, *Một vị từ email đã xác minh*, *Nhật ký kiểm toán* phần lời mời);
  - §12 QĐ-09, QĐ-10, QĐ-11, QĐ-13, QĐ-23; §14 P3, T3.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: FR-25, FR-42, FR-61; BR-24, BR-51, BR-63, BR-75, BR-81, BR-84 điểm 1–4; D4, D5, D10; AC-57–AC-60, AC-115–AC-119, AC-144, AC-151, AC-152, AC-177–AC-182, AC-196, AC-266–AC-275, AC-303–AC-312, AC-316, AC-324, AC-325, AC-332–AC-334, AC-351, AC-369, AC-372–AC-374.

## Lộ trình module 00

Module 00 phát hành một lần, không có cờ tính năng (TECH00 §10.2). Mọi phần làm trên nhánh `feature/module-00`, và chỉ mở một PR vào `dev` sau phần cuối. Mỗi phần là một kế hoạch riêng, người dùng duyệt trước khi làm.

Bảng của phần 1, cập nhật theo quyết định của người điều phối 2026-10-23: lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục) chuyển từ phần 6 lên đầu phần 3, để mọi service dựng ở phần 3–5 tự ghi mục của mình. Hàng 5 ghi thêm API-23 (`accountCreated`), phần 5 làm cùng lời mời. Bảng này giống hệt nhau ở mọi kế hoạch phần 3–8.

| Phần | Nội dung | TECH00 |
|---|---|---|
| 1 | Sửa bảo mật B1, B2; M7, M8; `TokenIssuer` kiểm `Status`; audit loại cột nhạy cảm; `SmtpEmailSender` | §3.8, §4.9, §6.6, §6.7, §7.3 |
| 2 | Sẵn sàng nhiều máy: giới hạn tần suất qua Redis, `ProductionTopologyValidator`, `EmailSettings:SecureSocket`, Data Protection | §4.8, §10.3 |
| 3 | Lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục 13 loại); cổng tổ chức, admin hệ thống, phiên: `GetAccessAsync`, `OrganizationContextAuthorizationHandler`, refresh `org_id = null`, `isSystemAdministrator`, CLI `grant`/`revoke-system-admin`, `SystemOrganizationService`, API-01–API-05, M1, M2 (`system_administrators`) | §3.1, §3.5, §3.7 (lõi), §4.1, §4.5, §5.1, §5.2, §6.1, §6.5 |
| 4 | Vai trò và thành viên: `RoleGrantPolicy`, `IOrganizationLock`, `MemberService`, `RoleService`, M3–M5 | §3.2, §3.3, §4.3, §5.3, §5.4, §6.4 |
| **5 (tệp này)** | Lời mời: M2 (`organization_invitations`), API-10–API-13, API-20–API-23, email mời | §3.4, §4.2, §4.4, §6.2, §6.3 |
| 6 | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Nhánh và commit**
  - Làm trên nhánh `feature/module-00`, sau phần 3 và phần 4.
  - Commit theo Conventional Commits, tiêu đề ≤ 100 ký tự, stage từng tệp theo đường dẫn.
  - Mỗi message kết thúc bằng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    ```
- **C#:** theo `code-conventions.md`:
  - không dùng `var`; truyền `CancellationToken`;
  - entity chỉ tạo qua `static Create(XxxParams p)`;
  - lỗi Domain là `DomainException(DomainMessages.X)`;
  - lỗi Application là `ConflictException`, `ForbiddenException`, `NotFoundException`, `ServiceUnavailableException`… với key trong `ApplicationMessages`.
- **Mã lỗi:** mỗi key mới có chữ ở cả `backend/src/FeedbackHub.Application/Resources/Messages.resx` (vi) và `Messages.en.resx` (en). Key Domain cũng đặt chữ ở hai tệp này, như các key Domain hiện có. Chữ của mã lỗi phía frontend (`locales/{vi,en}.ts`) thuộc phần 8.
- **Test:** theo `testing.md`:
  - tên `Method_Scenario_Expected`, dùng xUnit `Assert`, NSubstitute `Received`/`DidNotReceive` cho tác dụng phụ;
  - Infrastructure dùng Postgres thật (Testcontainers), không EF InMemory;
  - test API dùng `[Collection(nameof(ApiCollection))]`, có 401 cho mọi endpoint `[Authorize]` và 403 cho endpoint có policy quyền.
- **Mã lời mời** (QĐ-09):
  - 32 byte `RandomNumberGenerator`, base64url, không đệm `=`;
  - chỉ lưu `TokenHash.Compute(mã)` (SHA-256 hex chữ thường, 64 ký tự);
  - link `{FrontendBaseUrl}/#/invite?token=<mã>`;
  - mã thô chỉ ở bộ nhớ của request và trong thư: không log, không job, không nhật ký kiểm toán, không thông điệp ngoại lệ.
- **Hạn lời mời:** đúng `TimeSpan.FromHours(168)` từ lúc tạo, không làm tròn (BR-24, D4). Hết hạn khi `ExpiresAt ≤ now`.
- **Email:** chuẩn hoá bằng `Account.NormalizeEmail` ở mọi chỗ so sánh hoặc lưu.
- **Log** (TECH00 §7.3): không email, không mã (thô hay hash), không thân request. Log dùng `invitationId`, `accountId`.
- **Nhật ký kiểm toán:**
  - Chỉ dùng tên trong danh mục §3.7: `InvitationSent`, `InvitationRevoked`, `InvitationDeleted`, `MemberJoined`. Không dùng `Revoked`, `Deleted` của §6.2.
  - Tổ chức của mục luôn lấy từ entity đã tra qua `AuditEvent.For<TSubject>` của phần 3, không từ JWT (A1): `InvitationSent`, `InvitationRevoked`, `InvitationDeleted` dùng `For(invitation, …)` (`OrganizationInvitation : BaseEntity<Guid>, IHasOrganization` thoả ràng buộc của `For`); `MemberJoined` dùng `For(member, …)` với dòng `OrganizationMember` vừa tạo hay kích hoạt lại, vì danh mục khai `EntityName = OrganizationMember` cho loại này. Dòng thành viên đó thuộc tổ chức của lời mời.
  - Tham số `role` là `AuditRoleRef` của phần 3 (kiểu `AuditParameterKind.Role`; `AuditRoleRef.From(Role)` hoặc dựng từ `Id`, `Name`, `SystemKind` của `RoleGrantRole`), không phải `RoleRefDto`.
  - `ActorKind` luôn là `Account` cho các sự kiện của phần này (BR-53, AC-369).
  - Không mục nào khi thao tác bị từ chối, khi rollback, hay cho lời mời bị thay (AC-325).
- **Thứ tự kiểm** trong service: (5) tra đối tượng theo `id` và `org_id` → 404; (6) BR-58 → 403; (7) điều kiện khác → 409 (TECH00 §5.1, D2).
- **Contract:** sau khi đổi controller hoặc DTO, export OpenAPI rồi `bun run --cwd frontend codegen`, một lần ở task backend cuối (Task 8). Commit `shared/openapi/openapi.json`.
- **Bí mật:** không đọc, không sửa `appsettings.json` hay `appsettings.*.json`, trừ `appsettings.Example.json`.
- **Migration:** thứ tự và quy tắc id ở Global Constraints của phần 3 (§ Migration). M2 `AddOrganizationInvitations` là migration cuối của module 00, id lớn hơn id của M5 (phần 4).

## Review Focus

1. **Email gõ khác hoa thường hoặc có khoảng trắng** (`" A@X.com "` khi đã có lời mời cho `a@x.com`): thay đúng lời mời cũ, không 500 do chỉ mục unique; tài khoản `a@x.com` chấp nhận được. Test ở Task 3 và Task 5.
2. **SMTP lỗi hoặc quá 15 giây khi đang thay một lời mời:** 503 `InvitationEmailDeliveryFailed`, lời mời cũ còn nguyên và mã cũ vẫn mở được, không mục `InvitationSent`, log chỉ có `invitationId`. Test ở Task 3 (Application) và Task 7 (HTTP).
3. **Hai tab chấp nhận đồng thời:** lần sau vẫn 200 với cùng tổ chức, chỉ một dòng thành viên đang hoạt động và đúng một mục `MemberJoined`. Test ở Task 5.
4. **Đăng ký qua lời mời với tên đăng nhập hoặc email đã có:** 409, không tạo tài khoản, không phiên, không cookie, lời mời còn nguyên để thử lại. Test ở Task 6 và Task 7.
5. **Vai trò của lời mời bị xoá trước khi người nhận mở link:** dòng lời mời biến mất theo FK cascade, mở link báo `NoLongerValid`, không 500. Test ở Task 2 và Task 5.

---

### Task 1: Domain, `OrganizationInvitation`

**Files:**
- Create: `backend/src/FeedbackHub.Domain/Entities/OrganizationInvitation.cs`
- Modify: `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test: `backend/tests/FeedbackHub.Domain.Tests/Entities/OrganizationInvitationTests.cs`

**Interfaces:**
- Produces:
  - `record OrganizationInvitationParams(Guid OrganizationId, string Email, Guid RoleId, string TokenHash, DateTime IssuedAt)`.
  - `sealed class OrganizationInvitation : BaseEntity<Guid>, IHasOrganization`:
    - `public static readonly TimeSpan Lifetime = TimeSpan.FromHours(168)`;
    - `static OrganizationInvitation Create(OrganizationInvitationParams p)`: `Id = IdGenerator.NewUuidV7()`, `ExpiresAt = p.IssuedAt + Lifetime`;
    - `Guid OrganizationId`, `string Email`, `Guid RoleId`, `string TokenHash`, `DateTime ExpiresAt`, đều `{ get; private set; }`;
    - `bool IsExpiredAt(DateTime utcNow) => ExpiresAt <= utcNow`.
  - `DomainMessages.InvitationOrganizationRequired`, `InvitationRoleRequired`, `InvitationTokenHashInvalid`. Email trống dùng lại `AccountEmailRequired`.
    - Chữ vi/en:
      - `InvitationOrganizationRequired`: "Lời mời phải thuộc một tổ chức." / "An invitation must belong to an organization."
      - `InvitationRoleRequired`: "Lời mời phải có vai trò." / "An invitation must have a role."
      - `InvitationTokenHashInvalid`: "Mã lời mời không hợp lệ." / "The invitation code is invalid."

- [x] **Step 1: Viết test Domain thất bại**

```csharp
[Fact] public void Create_ValidParams_NormalizesEmailAndExpiresExactly168HoursLater()
// Email " A@X.com " → "a@x.com"; IssuedAt = 2026-10-23T10:17:33.123Z → ExpiresAt = 2026-10-30T10:17:33.123Z (không làm tròn, D4)
[Fact] public void Create_EmptyOrganization_Throws()     // InvitationOrganizationRequired
[Fact] public void Create_EmptyRole_Throws()             // InvitationRoleRequired
[Fact] public void Create_BlankEmail_Throws()            // AccountEmailRequired
[Theory] [InlineData("")] [InlineData("abc")] [InlineData(/* 64 ký tự có chữ hoa */)]
public void Create_TokenHashNot64LowerHex_Throws(string hash)   // InvitationTokenHashInvalid
[Fact] public void IsExpiredAt_ExactlyAtExpiry_IsTrue()  // ExpiresAt == now → hết hạn (BR-75: ExpiresAt > now mới là đang chờ)
[Fact] public void IsExpiredAt_OneTickBefore_IsFalse()
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Domain.Tests -m:1 --filter "FullyQualifiedName~OrganizationInvitationTests"`
Expected: FAIL (kiểu chưa tồn tại).

- [x] **Step 3: Cài đặt**
  - `Create` kiểm theo thứ tự trong test.
  - `TokenHash` phải khớp `^[0-9a-f]{64}$`.
  - `IssuedAt` lưu như được truyền; Domain không đọc đồng hồ, nên test dùng đồng hồ giả tạo được lời mời đã hết hạn.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(domain): add OrganizationInvitation with a fixed 7-day lifetime`

### Task 2: Lưu trữ, M2 (`organization_invitations`)

**Files:**
- Create: `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/OrganizationInvitationConfiguration.cs`
- Modify: `backend/src/FeedbackHub.Infrastructure/Persistence/AppDbContext.cs` (`DbSet`, `AuditExcludedEntityTypes`)
- Create: `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_AddOrganizationInvitations.cs` (sinh bằng `dotnet ef migrations add AddOrganizationInvitations`; id lớn hơn id của M5 `GrantFeedbackHubAdminToAdminRoles` của phần 4, theo quy tắc § Migration). Kiểm `Up` không có `DropIndex` nào cho `roles`: có thì snapshot của M3 chưa khớp, sửa ở phần 4 trước
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/OrganizationInvitationPersistenceTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - Bảng `organization_invitations` theo TECH00 §3.4:
    - `Email` varchar(200) not null;
    - `TokenHash` char(64) not null (`HasMaxLength(64).IsFixedLength()`);
    - `ExpiresAt` timestamptz not null;
    - các cột audit chung của `BaseEntity`.
  - FK `OrganizationId` → `organizations` ON DELETE CASCADE; FK `RoleId` → `roles` ON DELETE CASCADE.
  - Chỉ mục: unique `(OrganizationId, Email)`, unique `(TokenHash)`, thường `(RoleId)`.
  - `AppDbContext.OrganizationInvitations`; `typeof(OrganizationInvitation)` trong `AuditExcludedEntityTypes` (QĐ-13).

- [x] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
[Fact] public async Task SaveChanges_SecondInvitationSameOrganizationAndEmail_ViolatesUniqueIndex()
[Fact] public async Task SaveChanges_SameEmailInTwoOrganizations_Succeeds()
[Fact] public async Task SaveChanges_DeleteOldAndAddNewSameEmailInOneSave_Succeeds()   // cách API-11 thay lời mời
[Fact] public async Task DeleteRole_CascadesToItsInvitations()                         // Review Focus 5, BR-63 điểm 5, AC-181
[Fact] public async Task DeleteOrganization_CascadesToItsInvitations()
[Fact] public async Task SaveChanges_Invitation_WritesNoAuditRow()
// thêm, xoá lời mời → không dòng audit_logs nào có entity_name 'OrganizationInvitation'; không dòng nào chứa TokenHash
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~OrganizationInvitationPersistenceTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt**
  - Viết cấu hình theo mẫu các `*Configuration.cs` hiện có (tên bảng snake, tên cột như entity).
  - Sinh migration. Kiểm `Up` chỉ tạo bảng và chỉ mục, không đụng bảng khác.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(persistence): add organization_invitations table (M2) kept out of the automatic audit`

### Task 3: API-11 gửi lời mời và email mời

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/Invitations/IInvitationService.cs`
  - `backend/src/FeedbackHub.Application/Services/Invitations/InvitationService.cs`
  - `backend/src/FeedbackHub.Application/Services/Invitations/InvitationEmail.cs`
  - `backend/src/FeedbackHub.Application/Services/Invitations/InvitationDto.cs` (cùng `InvitationStatus`)
  - `backend/src/FeedbackHub.Application/Services/Invitations/SendInvitationRequest.cs`
  - `backend/src/FeedbackHub.Application/Common/RandomToken.cs`
- Modify:
  - `backend/src/FeedbackHub.Application/DependencyInjection.cs`
  - `backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
  - `backend/src/FeedbackHub.Application/Services/Auth/RegistrationService.cs`, `backend/src/FeedbackHub.Application/Services/Accounts/EmailChangeLink.cs` (thay `GenerateRawToken` riêng bằng `RandomToken.Create()`)
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Invitations/InvitationServiceSendTests.cs`, `backend/tests/FeedbackHub.Application.Tests/Services/Invitations/InvitationEmailTests.cs`

**Interfaces:**
- Consumes:
  - Task 1, Task 2.
  - Phần 3 Task 1: `IAuditTrail.RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)`; `AuditEvent.For<TSubject>(TSubject subject, AuditEventType type, ActorKind actorKind, Guid? actorAccountId, IReadOnlyDictionary<string, object>? parameters = null) where TSubject : BaseEntity<Guid>, IHasOrganization`; `AuditEventType`, `ActorKind`, `AuditRoleRef.From(Role)`. Danh mục đã khai đủ 13 loại, gồm bốn loại của phần này; phần 5 không thêm loại nào.
  - Phần 3 Task 10: `RoleRefDto(Guid Id, string Name, SystemRoleKind? SystemKind)` (`systemKind` ra JSON dạng tên).
  - Phần 4 Task 3: `IOrganizationLock.AcquireAsync(Guid organizationId, CancellationToken cancellationToken)`.
  - Phần 4 Task 4:
    - `RoleGrantReader.LoadCallerAsync(Guid organizationId, Guid accountId, CancellationToken)` → `RoleGrantCaller`;
    - `RoleGrantReader.LoadRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken)` → `IReadOnlyList<RoleGrantRole>` (chỉ vai trò của tổ chức đó);
    - static `RoleGrantPolicy.CanGrant(caller, role)`, `CanTakeAway(caller, role)`, `CheckGrantAll(caller, roles)`, `CheckTakeAwayAll(caller, roles)` → `RoleGrantDenial?`, `ErrorCode(RoleGrantDenial)` → `OwnerOnlyOperation` hoặc `InsufficientRolePermissions`;
    - `RoleLockReason { OwnerOnly = 1, InsufficientRolePermissions = 2 }`.

    Từ chối BR-58 luôn là `throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial))`; phần 4 không có hàm ném riêng.
  - Phần 4 Task 1: `SystemRoleDisplayNames.Get(SystemRoleKind kind, string language)`.
- Produces:
  - `internal static class RandomToken { public static string Create(); }`: 32 byte, base64url, không đệm.
  - `record SendInvitationRequest([Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email, [Required(ErrorMessage = "FieldRequired")] Guid RoleId)`.
  - `enum InvitationStatus { Pending, Expired }`, ra JSON dạng chuỗi.
  - `record InvitationDto(Guid Id, string Email, RoleRefDto Role, DateTime ExpiresAt, InvitationStatus Status, bool CanRevokeOrDelete, bool CanResend, RoleLockReason? LockReason)`.
  - `IInvitationService.SendAsync(SendInvitationRequest request, CancellationToken ct) → Task<InvitationDto>`.
  - `internal static class InvitationEmail { public static (string Subject, string HtmlBody) Render(string organizationName, string roleName, DateTime expiresAtUtc, string url); }`.
  - Key `ApplicationMessages` mới, chữ vi / en:
    - `InvitationEmailAlreadyMember` (409): "Người này đã là thành viên của tổ chức." / "This person is already a member of the organization."
    - `InvitationEmailDeliveryFailed` (503): "Không gửi được email mời. Chưa có gì thay đổi, hãy thử lại sau." / "The invitation email could not be sent. Nothing was changed; try again later."

- [x] **Step 1: Viết test Application thất bại**

```csharp
// InvitationServiceSendTests
SendAsync_NewEmail_CreatesInvitationRecordsSentAndMailsLinkAfterSave
// ExpiresAt == clock.UtcNow + 168h; IAuditTrail.Received(1) InvitationSent với email "b@x.com", role là AuditRoleRef của vai trò;
// IEmailSender.Received(1).SendAsync("b@x.com", …); thân chứa "#/invite?token=" + mã mà TokenHash.Compute(mã) == invitation.TokenHash;
// thứ tự: AcquireAsync → SaveChangesAsync → SendAsync (Received.InOrder)
SendAsync_EmailWithCaseAndSpaces_ReplacesExistingInvitation          // Review Focus 1: " B@X.com " thay lời mời "b@x.com"
SendAsync_ExistingPendingOrExpired_DeletesOldNoAuditForOld            // đúng một mục, là InvitationSent của lời mời mới (AC-325)
SendAsync_RoleNotInOrganization_Throws404AndSendsNothing              // vai trò của tổ chức khác → NotFoundException
SendAsync_CannotGrantNewRole_Throws403WithPolicyCode                  // OwnerOnlyOperation hoặc InsufficientRolePermissions; SendAsync DidNotReceive
SendAsync_CanGrantNewButCannotTakeAwayOldInvitationRole_Throws403OldKept   // BR-75, D4, AC-271
SendAsync_EmailIsActiveMember_Throws409AlreadyMember                  // AC-180
SendAsync_EmailIsActiveMemberAndCannotGrant_Throws403Not409           // D2
SendAsync_EmailIsInactiveMember_Succeeds                              // người đã rời mời lại được
SendAsync_SmtpFails_Throws503RollsBackLogsOnlyInvitationId            // Review Focus 2
// IEmailSender ném ServiceUnavailableException(EmailDeliveryFailed) → ServiceUnavailableException(InvitationEmailDeliveryFailed);
// ném từ trong ExecuteInTransactionAsync nên không commit; không entry log nào chứa email hay mã; có entry chứa invitationId
SendAsync_CallerCancels_PropagatesOperationCanceled                   // không đổi thành 503
SendAsync_ReturnsDtoWithFlagsFromPolicy                               // Status Pending, CanResend == CanRevokeOrDelete == true, LockReason null
// InvitationEmailTests
Render_EncodesEveryValue            // tên tổ chức, tên vai trò chứa "<b>&" → xuất hiện dạng mã hoá HTML, không có "<b>" thô
Render_ContainsOrganizationRoleExpiryAndLinkButNoInviter
Render_ExpiryUsesFixedUtcPlus7AndVietnamLabel     // 2026-10-23 17:30 UTC → "24/10/2026 00:30 (giờ Việt Nam)"; không phụ thuộc múi giờ máy chạy
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~InvitationServiceSendTests|FullyQualifiedName~InvitationEmailTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt `SendAsync` theo TECH00 §6.2**

Tổ chức lấy từ `ICurrentTenantProvider.OrganizationId`, người gọi từ `ICurrentUserService.UserId`, như các service tổ chức hiện có. Mọi bước chạy trong `ExecuteInTransactionAsync`, theo đúng thứ tự:
1. `IOrganizationLock.AcquireAsync(orgId)`.
2. `RoleGrantReader.LoadRolesAsync(orgId, [request.RoleId])` rỗng → 404 (vai trò không có hoặc của tổ chức khác).
3. Tìm lời mời cũ cùng `(orgId, email đã chuẩn hoá)`.
4. `caller = RoleGrantReader.LoadCallerAsync(orgId, callerId)`. `denial = RoleGrantPolicy.CheckGrantAll(caller, [vai trò mới])`, rồi nếu chưa bị từ chối và có lời mời cũ: `CheckTakeAwayAll(caller, LoadRolesAsync(orgId, [vai trò lời mời cũ]))`. `denial` khác `null` → `ForbiddenException(RoleGrantPolicy.ErrorCode(denial))` (403).
5. Email là của một tài khoản đang là thành viên hoạt động của `orgId` → 409 `InvitationEmailAlreadyMember`.
6. Xoá lời mời cũ. Tạo lời mời mới với `IssuedAt = IDateTimeProvider.UtcNow`.
7. `IAuditTrail.RecordAsync(AuditEvent.For(invitation, AuditEventType.InvitationSent, ActorKind.Account, callerId, { ["email"] = email, ["role"] = new AuditRoleRef(role.Id, role.Name, role.SystemKind) }))`, với `role` là `RoleGrantRole` ở bước 2.
8. `SaveChangesAsync`.
9. Gửi thư. `ServiceUnavailableException` từ `IEmailSender`:
   - log `"Failed to send invitation {InvitationId}"`;
   - ném `ServiceUnavailableException(InvitationEmailDeliveryFailed)`; giao dịch rollback.

   Lỗi do người gọi huỷ (`OperationCanceledException`) để nguyên.

Thư mời (TECH00 §6.2, §14 P3): mẫu tạm, chỉ tiếng Việt, viết cứng như `EmailChangeLink`.
- Tiêu đề: `$"Lời mời tham gia {organizationName} trên FeedbackHub"`.
- Thân:
  ```html
  <p>Xin chào,</p>
  <p>Bạn được mời tham gia tổ chức <strong>{tổ chức}</strong> trên FeedbackHub với vai trò <strong>{vai trò}</strong>.</p>
  <p>Nhấn vào liên kết bên dưới để mở lời mời:</p>
  <p><a href="{link}">{link}</a></p>
  <p>Lời mời có hiệu lực đến {hạn} (giờ Việt Nam).</p>
  <p>Nếu bạn không chờ lời mời này, hãy bỏ qua thư này.</p>
  ```
- Mọi giá trị qua `WebUtility.HtmlEncode`. Không có tên người mời.
- Vai trò hệ thống lấy tên `vi` từ `SystemRoleDisplayNames`; vai trò tự tạo dùng `Role.Name`.
- `{hạn}` theo dạng `dd/MM/yyyy HH:mm`, đổi từ UTC bằng độ lệch cố định UTC+7, không tra cơ sở dữ liệu múi giờ (Việt Nam không có giờ mùa hè). Cùng với chữ `(giờ Việt Nam)` của mẫu, thư ghi `dd/MM/yyyy HH:mm (giờ Việt Nam)`. Mẫu chính thức do PRD05 quyết.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~RegistrationServiceTests|FullyQualifiedName~AccountServiceTests"` (sau khi đổi sang `RandomToken`).
Expected: PASS.

- [x] **Step 5: Commit** `feat(invitations): send an invitation by email and replace an earlier one for the same address`

### Task 4: API-10 danh sách, API-12 mời lại, API-13 thu hồi hoặc xoá

**Files:**
- Modify:
  - `IInvitationService.cs`, `InvitationService.cs`
  - `backend/src/FeedbackHub.Application/Services/Roles/RoleDto.cs`, `IRoleQueries.cs`, `RoleService.cs` (phần 4 Task 8 dựng; thêm `pendingInvitationCount`)
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Queries/RoleQueries.cs`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Invitations/InvitationServiceManageTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Invitations/InvitationListQueryTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/RoleServiceReadTests.cs`, `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/RoleQueriesTests.cs`, `backend/tests/FeedbackHub.API.Tests/Controllers/CurrentOrganizationRolesTests.cs` (phần 4 tạo)

**Interfaces:**
- Consumes: Task 3; phần 3 Task 8 `PageRequest` (`Application/Common/Models/PageRequest.cs`, mặc định 50, tối đa 100, ngoài khoảng → 400); phần 4 Task 8 `RoleRow`, `IRoleQueries.ListAsync`, `RoleDto`, `RoleService.ListAsync`.
- Produces:
  - `Task<PagedResult<InvitationDto>> ListAsync(PageRequest request, CancellationToken ct)`.
  - `Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken ct)`.
  - `Task RevokeOrDeleteAsync(Guid invitationId, CancellationToken ct)`.
  - `RoleDto.pendingInvitationCount` (TECH00 §4.6, phần 4 để lại cho phần này vì bảng lời mời chỉ có từ Task 2):
    - `RoleRow` thêm `int PendingInvitationCount`: số lời mời của vai trò có `ExpiresAt > utcNow`, đếm bằng một `GROUP BY` trên `organization_invitations` trong cùng truy vấn (TECH00 §9);
    - `IRoleQueries.ListAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken)`; `RoleService.ListAsync` truyền `IDateTimeProvider.UtcNow`;
    - `RoleDto` thêm `int? PendingInvitationCount` ngay sau `HolderCount`: bằng `RoleRow.PendingInvitationCount` khi `caller.Permissions` (từ `LoadCallerAsync`) có `organizations.members.manage`, ngược lại `null` (BR-75, T9).

- [x] **Step 1: Viết test thất bại**

```csharp
// Application
ResendAsync_Expired_ReplacesWithSameRoleNewIdFreshExpiry          // AC-267, D4: ExpiresAt == now + 168h; InvitationSent đúng một mục (AC-373)
ResendAsync_Pending_ReplacesAndOldCodeNoLongerFound               // BR-63 điểm 3
ResendAsync_OtherOrganizationsInvitation_Throws404                 // AC-310
ResendAsync_CannotGrantRole_Throws403AndOldKept                    // AC-254
ResendAsync_EmailBecameActiveMember_Throws409InvitationEmailAlreadyMember   // BR-63 điểm 4; lời mời cũ giữ nguyên, SendAsync DidNotReceive, IAuditTrail DidNotReceive
ResendAsync_EmailBecameActiveMemberAndCannotGrant_Throws403Not409            // BR-58 chạy trước, như API-11
ResendAsync_SmtpFails_Throws503OldKept
RevokeOrDeleteAsync_Pending_RecordsInvitationRevokedAndDeletes     // AC-272: tham số email, role
RevokeOrDeleteAsync_Expired_RecordsInvitationDeletedAndDeletes     // AC-268, D5
RevokeOrDeleteAsync_CannotTakeAwayRole_Throws403NoAuditNoDelete    // AC-253, AC-255, AC-269, AC-270, AC-370
RevokeOrDeleteAsync_OwnerRoleInvitationByNonOwner_Throws403OwnerOnly
RevokeOrDeleteAsync_OtherOrganizationsInvitation_Throws404
// Infrastructure (Testcontainers, truy vấn thật)
ListAsync_OrdersByExpiresAtDescThenId_PendingAndExpiredWithStatus  // TECH00 §4.8; Status theo đồng hồ giả
ListAsync_OnlyCurrentOrganization
ListAsync_FlagsAndLockReasonFromPolicy                             // người gọi thiếu quyền của vai trò → CanResend false, LockReason InsufficientRolePermissions; vai trò Owner, người gọi không phải Owner → OwnerOnly
// RoleQueriesTests (phần 4), thêm
ListAsync_PendingInvitationCount_CountsOnlyUnexpiredInvitationsOfTheRole   // hai đang chờ, một hết hạn (đồng hồ giả) → 2; vai trò khác → 0
// RoleServiceReadTests (phần 4), thêm
ListAsync_CallerWithoutMembersManage_PendingInvitationCountNull            // BR-75, T9
ListAsync_CallerWithMembersManage_PendingInvitationCountFromQuery
// CurrentOrganizationRolesTests (phần 4), thêm
Roles_RolesManagerWithoutMembersManage_ResponseHasNullPendingInvitationCount
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~InvitationServiceManageTests|FullyQualifiedName~InvitationListQueryTests|FullyQualifiedName~RoleServiceReadTests|FullyQualifiedName~RoleQueriesTests|FullyQualifiedName~CurrentOrganizationRolesTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt**
  - `ResendAsync` dùng lại đường của `SendAsync` với `email` và vai trò của lời mời cũ, sau bước tra lời mời theo `id` và `orgId` (404). `RoleGrantPolicy.CheckGrantAll(caller, [vai trò])` là điều kiện BR-58 duy nhất (TECH00 §5.3); không gọi `CheckTakeAwayAll` cho lời mời cũ vì vai trò không đổi.
  - Kiểm thành viên đang hoạt động cũng áp cho mời lại, theo BR-63 điểm 4, sau các kiểm BR-58: email đã là thành viên đang hoạt động → 409 `InvitationEmailAlreadyMember`, lời mời cũ giữ nguyên, không gửi email, không ghi mục. Thêm 409 này vào contract API-12 (thay đổi cộng thêm). TECH00 §4.2 không liệt kê 409 cho API-12, nên PR khai đây là lệch nhỏ.
  - `RevokeOrDeleteAsync`, theo thứ tự:
    1. khoá;
    2. tra theo `id` và `orgId` → 404;
    3. `denial = RoleGrantPolicy.CheckTakeAwayAll(caller, [vai trò lời mời])` khác `null` → `ForbiddenException(RoleGrantPolicy.ErrorCode(denial))` (403);
    4. ghi `AuditEvent.For(invitation, …)` loại `InvitationRevoked` nếu `!IsExpiredAt(now)`, ngược lại `InvitationDeleted`, tham số `{ email, role }` (`role` là `AuditRoleRef`);
    5. xoá dòng → save.
  - `ListAsync`:
    - một truy vấn lời mời nối vai trò, lọc `orgId`, sắp `ExpiresAt DESC, Id DESC`;
    - cờ tính bằng cùng `RoleGrantPolicy`, đầu vào đọc từ cơ sở dữ liệu, không khoá (TECH00 §5.3): một lần `LoadCallerAsync`, một lần `LoadRolesAsync` cho các vai trò của trang; `CanResend = CanGrant(caller, role)`, `CanRevokeOrDelete = CanTakeAway(caller, role)`, `LockReason = CheckTakeAwayAll(caller, [role])?.Reason`.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(invitations): list, resend, revoke and delete invitations with audit entries`

### Task 5: API-20 tra lời mời, API-21 chấp nhận

**Files:**
- Modify: `IInvitationService.cs`, `InvitationService.cs`, `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Create:
  - `backend/src/FeedbackHub.Application/Services/Invitations/InvitationTokenRequest.cs`
  - `backend/src/FeedbackHub.Application/Services/Invitations/InvitationLookupDto.cs` (cùng `InvitationLookupStatus`)
  - `backend/src/FeedbackHub.Application/Services/Invitations/AcceptInvitationResult.cs`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Invitations/InvitationServiceAcceptTests.cs`

**Interfaces:**
- Consumes: Task 3. Phần 3: `ITenantAccessService.InvalidateMemberAsync`. `IPermissionResolver.InvalidateMemberAsync` có sẵn.
- Produces:
  - `record InvitationTokenRequest([Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Token)`.
  - `enum InvitationLookupStatus { Valid, Expired, NoLongerValid, WrongAccount, EmailNotConfirmed, OrganizationSuspended }`, ra JSON dạng chuỗi.
  - `record InvitationLookupDto(InvitationLookupStatus Status, string? OrganizationName, RoleRefDto? Role, DateTime? ExpiresAt, string? Email)`.
  - `record AcceptInvitationResult(Guid OrganizationId, string OrganizationName)`.
  - `Task<InvitationLookupDto> LookupAsync(InvitationTokenRequest request, CancellationToken ct)`: người gọi lấy từ `ICurrentUserService`; khoá API hay ẩn danh coi như không có phiên.
  - `Task<AcceptInvitationResult> AcceptAsync(InvitationTokenRequest request, CancellationToken ct)`.
  - Không gửi thông báo trong ứng dụng khi vào tổ chức (`OrganizationMemberAdded` cũ phục vụ việc thêm trực tiếp mà BR-73 đã bỏ); PRD05/TECH05 quyết thông báo về việc vào tổ chức.
  - `internal Task JoinAsync(OrganizationInvitation invitation, Account account, CancellationToken ct)`: kích hoạt lại hoặc tạo dòng thành viên, để thành viên giữ **đúng** vai trò của lời mời (xoá dòng `organization_member_roles` sót lại nếu có), ghi `AuditEvent.For(member, AuditEventType.MemberJoined, ActorKind.Account, account.Id, { accountName, email, role })` với `member` là dòng thành viên đó, xoá lời mời. Không save.
  - Key `ApplicationMessages` mới (409), chữ vi / en:
    - `InvitationExpired`: "Lời mời đã hết hạn." / "This invitation has expired."
    - `InvitationNoLongerValid`: "Lời mời không còn hiệu lực." / "This invitation is no longer valid."
    - `InvitationWrongAccount`: "Lời mời này không dành cho tài khoản bạn đang dùng." / "This invitation is not for the account you are using."
    - `InvitationOrganizationSuspended`: "Tổ chức đang tạm ngừng hoạt động nên chưa vào được." / "The organization is suspended, so you can't join it yet."
  - Dùng lại `EmailNotConfirmed` (403).

- [x] **Step 1: Viết test thất bại**

Bảng §6.3 thành một `[Theory]` cho mỗi endpoint. Mỗi dòng dựng: lời mời (có, không, hết hạn); phiên (không, đúng email, sai email, đúng email nhưng chưa xác minh); tổ chức (hoạt động, bị vô hiệu).

```csharp
[Theory] [MemberData(nameof(LookupCases))]
LookupAsync_Br81Order_ReturnsStatusWithOnlyAllowedFields(...)
// NoLongerValid, WrongAccount, EmailNotConfirmed: mọi trường khác null (D10, AC-306, AC-308, AC-351)
// Expired: chỉ ExpiresAt; OrganizationSuspended (có phiên đúng): OrganizationName, ExpiresAt
// Valid: đúng bốn trường OrganizationName, Role, ExpiresAt, Email (AC-309); ẩn danh với tổ chức bị vô hiệu → Valid (AC-332)
LookupAsync_WrongAccountCheckedBeforeEmailNotConfirmed           // tài khoản chưa xác minh, email khác → WrongAccount
LookupAsync_AccountWhosePendingEmailMatches_IsWrongAccount        // AC-348, BR-51
LookupAsync_DoesNotConsume                                        // gọi hai lần → cùng Valid, không SaveChanges
[Theory] [MemberData(nameof(AcceptCases))]
AcceptAsync_Br81Order_ThrowsMatchingCode(...)                    // 409 NoLongerValid, Expired, WrongAccount, OrganizationSuspended; 403 EmailNotConfirmed
AcceptAsync_Valid_JoinsWithInvitationRoleDeletesInvitationRecordsMemberJoined
// MemberJoined: tổ chức của lời mời, người làm là người nhận, tham số accountName, email, role (AC-374);
// InvalidateMemberAsync của ITenantAccessService và IPermissionResolver được gọi sau commit
AcceptAsync_FormerMember_ReactivatesSameRowWithOnlyInvitationRole // QĐ-16
AcceptAsync_OrganizationSuspendedOrEmailNotConfirmed_InvitationKept  // AC-151
AcceptAsync_EmailWithDifferentCase_Joins                          // Review Focus 1
AcceptAsync_Valid_SendsNoInAppNotification                         // không có thông báo nào cho việc vào tổ chức qua lời mời; PRD05/TECH05 quyết
AcceptAsync_AlreadyActiveMember_DeletesInvitationReturns200NoRoleChangeNoAudit   // TECH00 §6.3
AcceptAsync_InvitationGoneAfterLockAndCallerIsActiveMember_Returns200            // Review Focus 3: tab thứ hai
AcceptAsync_InvitationGoneAfterLockAndCallerNotMember_Throws409NoLongerValid
AcceptAsync_RoleDeleted_Throws409NoLongerValid                    // Review Focus 5 (dòng đã bị cascade)
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~InvitationServiceAcceptTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt theo TECH00 §6.3**
  - `LookupAsync`:
    - tra theo `TokenHash.Compute(token)`, không khoá, không ghi;
    - kiểm đúng thứ tự bảng §6.3, dừng ở điều kiện đầu tiên không đạt;
    - so `Account.Email` với email lời mời; `PendingEmail` không được tính.
  - `AcceptAsync` trong `ExecuteInTransactionAsync`:
    1. Tra lời mời theo hash; không có → `NoLongerValid`. Giữ `organizationId` của lần tra này.
    2. `AcquireAsync(organizationId)`, rồi tra lại theo hash.
    3. Nếu dòng đã mất: người gọi là thành viên đang hoạt động của `organizationId` → trả 200 với tổ chức đó; ngược lại → `NoLongerValid`.
    4. Kiểm theo thứ tự §6.3: hết hạn → `WrongAccount` → `EmailNotConfirmed` → tổ chức bị vô hiệu (`Organization.Status == false`).
    5. Người gọi đã là thành viên đang hoạt động → xoá lời mời, save, trả 200.
    6. Ngược lại `JoinAsync`, rồi save.
  - Sau commit, xoá cache truy cập và quyền của người vào.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(invitations): look up an invitation without using it and accept it while signed in`

### Task 6: API-22 đăng ký qua lời mời (phiên qua `TokenIssuer`) và API-23 `accountCreated`

**Files:**
- Modify:
  - `IInvitationService.cs`, `InvitationService.cs`
  - `backend/src/FeedbackHub.Application/Services/Auth/LoginResponse.cs` (bỏ `sealed` để hai phản hồi dưới kế thừa)
  - `backend/src/FeedbackHub.Application/Services/Auth/IAuthService.cs`, `AuthService.cs` (`ExternalLoginAsync`)
- Create:
  - `backend/src/FeedbackHub.Application/Services/Invitations/RegisterWithInvitationRequest.cs`
  - `backend/src/FeedbackHub.Application/Services/Invitations/RegisterWithInvitationResult.cs` (cùng `InvitationOutcome`)
  - `backend/src/FeedbackHub.Application/Services/Auth/RegisterWithInvitationResponse.cs`
  - `backend/src/FeedbackHub.Application/Services/Auth/ExternalLoginResult.cs`, `ExternalLoginResponse.cs`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Invitations/InvitationServiceRegisterTests.cs`, `backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: Task 5 (`JoinAsync`). `ITokenIssuer.IssueTokensAsync(Account, Guid?, string?, string?, bool, DateTime, Guid?, CancellationToken)` (phần 1 kiểm `Status`, phần 3 đặt `isSystemAdministrator`). `IPasswordHasher`.
- Produces:
  - `record RegisterWithInvitationRequest`. Các trường `Token` (`[Required]`, `MaxLength(200)`), `Name`, `Username`, `Password`, `Phone?`, `Position?`, `Address?` mang đúng các thuộc tính kiểm hợp lệ của `RegisterRequest`. Không có `Email`.
  - `enum InvitationOutcome { Joined, OrganizationSuspended }`, ra JSON dạng chuỗi.
  - `record RegisterWithInvitationResult(AuthResult Auth, InvitationOutcome Outcome)`.
  - `Task<RegisterWithInvitationResult> RegisterAsync(RegisterWithInvitationRequest request, string? deviceInfo, string? ipAddress, CancellationToken ct)`.
  - `record RegisterWithInvitationResponse : LoginResponse`, thêm `InvitationOutcome InvitationOutcome`.
  - `record ExternalLoginResult(AuthResult Auth, bool AccountCreated)`; `IAuthService.ExternalLoginAsync` trả kiểu này.
  - `record ExternalLoginResponse : LoginResponse`, thêm `bool AccountCreated`.

- [x] **Step 1: Viết test thất bại**

```csharp
RegisterAsync_Valid_CreatesConfirmedAccountWithInvitationEmailJoinsAndIssuesSessionThroughTokenIssuer
// account.Email == email lời mời, EmailConfirmed == true, PasswordHash đặt (AC-57, AC-182, BR-63 điểm 6);
// tokenIssuer.Received(1).IssueTokensAsync(Arg.Is<Account>(a => a.Id == created.Id), invitation.OrganizationId,
//     deviceInfo, ipAddress, false, clock.UtcNow, null, Arg.Any<CancellationToken>());
// Outcome == Joined; MemberJoined với người làm là tài khoản vừa tạo, tổ chức của lời mời
RegisterAsync_OrganizationSuspended_CreatesAccountNoJoinInvitationKeptSessionWithoutOrganization
// IssueTokensAsync(…, organizationId: null, …, isPersistent: false, …); Outcome == OrganizationSuspended; không MemberJoined (AC-333)
RegisterAsync_UnknownOrUsedToken_Throws409NoLongerValidCreatesNothing      // không AddAsync Account; IssueTokensAsync DidNotReceive
RegisterAsync_Expired_Throws409ExpiredCreatesNothing
RegisterAsync_UsernameTaken_Throws409InvitationKeptNoSession                // Review Focus 4
RegisterAsync_EmailAlreadyHasAccount_Throws409AccountEmailAlreadyExists      // TECH00 §6.3: tài khoản chưa xác minh không đăng ký lại
RegisterAsync_IgnoresAnyEmailInBody                                          // email luôn lấy từ lời mời (T3)
// AuthServiceTests
ExternalLoginAsync_NewAccount_AccountCreatedTrue
ExternalLoginAsync_LinkedOrEmailMatched_AccountCreatedFalse
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~InvitationServiceRegisterTests|FullyQualifiedName~AuthServiceTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt**
  - `RegisterAsync`, trong `ExecuteInTransactionAsync`:
    1. Tra lời mời theo hash, khoá tổ chức của nó, tra lại.
    2. Không có → `NoLongerValid`. Hết hạn → `InvitationExpired`.
    3. Kiểm trùng tên đăng nhập, rồi trùng email, như `RegistrationService.EnsureUniqueAccountAsync` (409).
    4. `Account.Create` với email của lời mời, `SetPasswordHash`, `ConfirmEmail()`, thêm.
    5. Tổ chức hoạt động → `JoinAsync`, `Outcome = Joined`. Ngược lại giữ lời mời, `Outcome = OrganizationSuspended`.
    6. Save.
  - Sau commit:
    - xoá cache của người vào;
    - rồi `tokenIssuer.IssueTokensAsync(account, joined ? invitation.OrganizationId : null, deviceInfo, ipAddress, isPersistent: false, loginAt: clock.UtcNow, familyId: null, ct)`.

    Phát phiên sau commit để quyền đọc ra là quyền đã lưu, và cache không bị ghi từ một giao dịch có thể rollback.
  - `ExternalLoginAsync` báo `AccountCreated = true` chỉ ở nhánh tạo tài khoản mới. Hành vi khác không đổi (TECH00 §4.4 API-23).

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(invitations): register through an invitation and sign in via TokenIssuer`

### Task 7: Endpoint, giới hạn tần suất `invitation`, danh sách ẩn danh

**Files:**
- Modify:
  - `backend/src/FeedbackHub.API/Controllers/CurrentOrganizationController.cs` (phần 3 dựng)
  - `backend/src/FeedbackHub.API/Controllers/AuthController.cs`
  - `backend/src/FeedbackHub.API/Program.cs` (policy `invitation`)
  - `backend/src/FeedbackHub.Application/Common/Settings/RateLimiterSettings.cs`
  - `backend/src/FeedbackHub.API/appsettings.Example.json`
  - `.claude/rules/authentication.md` (danh sách `[AllowAnonymous]`, TECH00 §10.2 N-new-3)
  - `backend/tests/FeedbackHub.API.Tests/TestSupport/SentEmailCollector.cs` (chế độ gửi lỗi)
- Create: `backend/src/FeedbackHub.API/Controllers/InvitationsController.cs`
- Test:
  - `backend/tests/FeedbackHub.API.Tests/Controllers/InvitationsControllerTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/OrganizationInvitationsTests.cs`
  - `AuthControllerTests.cs`

**Interfaces:**
- Consumes: Task 3–6.
- Produces:
  - Trên `CurrentOrganizationController`, mỗi action `[Authorize(Policy = Permissions.OrganizationMembersManage)]`:
    - `GET invitations` → 200 `PagedResult<InvitationDto>`;
    - `POST invitations` → 201 `InvitationDto`;
    - `POST invitations/{id:guid}/resend` → 201 `InvitationDto`;
    - `DELETE invitations/{id:guid}` → 204.
  - `InvitationsController`, `[Route("api/invitations")]`, `[Authorize(Policy = AuthorizationPolicies.SignedInUser)]` ở mức lớp:
    - `POST lookup`: `[AllowAnonymous]`, `[EnableRateLimiting("invitation")]`, 200 `InvitationLookupDto`. Đặt header `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`.
    - `POST accept`: `[EnableRateLimiting("invitation")]`, 200 `AcceptInvitationResult`.
  - `AuthController`:
    - `POST register-with-invitation`: `[AllowAnonymous]`, `[EnableRateLimiting("auth")]`, 200 `RegisterWithInvitationResponse`, đặt cookie bằng `SetAuthCookies`;
    - `POST external/{provider}` trả `ExternalLoginResponse`.
  - `RateLimiterSettings.InvitationPermitLimit = 30`, `InvitationWindowMinutes = 1`. Policy `"invitation"` dựng bằng `RateLimitPartitions.FixedWindow(httpContext, "invitation", …, rateLimiterStore)` như hai policy có sẵn.
  - `SentEmailCollector.FailNextSend()`: lần gửi kế tiếp ném `ServiceUnavailableException(ApplicationMessages.EmailDeliveryFailed)`, như `SmtpEmailSender`.

- [x] **Step 1: Viết test API thất bại**

```csharp
// OrganizationInvitationsTests
List_Anonymous_Returns401 ; Send_Anonymous_Returns401 ; Resend_Anonymous_Returns401 ; Delete_Anonymous_Returns401
List_MemberWithoutMembersManage_Returns403 ; Send_PersonalContext_Returns403 ; Send_ApiKey_Returns403   // AC-316, §5.2
Send_InvalidEmail_Returns400FieldEmailAddress
Send_Valid_Returns201AndMailsLink                       // SentEmailCollector có một thư tới email, link "#/invite?token="
Send_SmtpFails_Returns503OldInvitationStillOpens         // Review Focus 2 qua HTTP: FailNextSend; mã của lời mời cũ vẫn Valid ở lookup
Delete_OtherOrganizationsInvitation_Returns404
// InvitationsControllerTests
Lookup_Anonymous_Returns200WithNoStoreNoReferrerNoindexHeaders
Lookup_SignedInWrongAccount_ReturnsWrongAccountOnly
Accept_Anonymous_Returns401
Accept_UnconfirmedAccount_Returns403EmailNotConfirmedInvitationKept   // AC-351
Accept_FromOtherOrganizationContext_MemberJoinedRecordedInInvitedOrganization  // A1: K làm việc ở B, nhận lời mời vào A → mục ở A, không ở B
Accept_FromPersonalContext_MemberJoinedRecordedInInvitedOrganization
InvitationsController_AnonymousActions_MatchTheDeliberateList          // chỉ "Lookup"
RateLimitedActions_UseInvitationPolicy                                 // Lookup, Accept mang EnableRateLimiting("invitation")
// AuthControllerTests
RegisterWithInvitation_Joined_SetsSessionCookiesNotPersistentAndReturnsOrganization
// 200; organizationId == tổ chức; invitationOutcome "Joined"; isSystemAdministrator false;
// cookie refresh_token không có Expires (không "ghi nhớ"); mục MemberJoined ở tổ chức với user_id = tài khoản mới
RegisterWithInvitation_Suspended_ReturnsOutcomeAndNullOrganization
RegisterWithInvitation_UsernameTaken_Returns409WithoutSetCookie        // Review Focus 4
RegisterWithInvitation_UsesAuthRateLimitPolicy
ExternalLogin_ResponseHasAccountCreated
// cập nhật AuthController_AnonymousActions_MatchTheDeliberateList: thêm "RegisterWithInvitation"
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~InvitationsControllerTests|FullyQualifiedName~OrganizationInvitationsTests|FullyQualifiedName~AuthControllerTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt**
  - Action mỏng, chỉ gọi service.
  - `register-with-invitation` dựng `RegisterWithInvitationResponse` từ `AuthResult` theo cùng cách `ToResponse` đang dựng `LoginResponse`.
  - `.claude/rules/authentication.md`: thêm `register-with-invitation` vào danh sách của `AuthController`. Thêm một gạch đầu dòng cho `InvitationsController`: `lookup` ẩn danh, `accept` cần `SignedInUser`, ghim bằng test controller.
  - Thêm `"InvitationPermitLimit": 30, "InvitationWindowMinutes": 1` vào `RateLimiterSettings` của `appsettings.Example.json`.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi `dotnet test backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~ArchitectureTests"`.
Expected: PASS.

- [x] **Step 5: Commit** `feat(api): expose invitation management, lookup, accept and register-with-invitation`

### Task 8: Contract, bản đồ tính năng, gate

**Files:**
- Modify: `shared/openapi/openapi.json` (export), `docs/feature-map.md`

**Interfaces:**
- Consumes: Task 7.

- [x] **Step 1: Export contract và sinh lại client**

Run:
- `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
- `bun run --cwd frontend codegen`

Expected:
- API-12 (`/api/organization/invitations/{id}/resend`) khai thêm phản hồi 409 `InvitationEmailAlreadyMember`;
- `openapi.json` có `/api/organization/invitations`, `/api/organization/invitations/{id}/resend`, `/api/invitations/lookup`, `/api/invitations/accept`, `/api/auth/register-with-invitation`;
- `InvitationStatus`, `InvitationLookupStatus`, `InvitationOutcome` là enum chuỗi;
- `ExternalLoginResponse` có `accountCreated`, `RegisterWithInvitationResponse` có `invitationOutcome`;
- `RoleDto` có `pendingInvitationCount` (nullable).

- [x] **Step 2: Frontend vẫn biên dịch và qua test**

Run: `bun run --cwd frontend build-only`, rồi `bun run --cwd frontend type-check`, rồi `bun run --cwd frontend test:run`.
Expected: PASS. Màn hình dùng các route này thuộc phần 8.

- [x] **Step 3: Thêm mục `## Lời mời` vào `docs/feature-map.md`** theo cấu trúc mục hiện có.
  - PRD: FR-25, FR-61, BR-81.
  - Backend: route, service, test của Task 3–7.
  - Link trong thư: `{FrontendBaseUrl}/#/invite?token=...`, hạn 7×24 giờ.
  - Dòng *Vào bằng* và *Frontend* ghi "phần 8".

- [x] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [x] **Step 5: Commit** `chore(contract): export the invitation endpoints and map the feature`

Mô tả PR khai báo lệch nhỏ: API-12 thêm phản hồi 409 `InvitationEmailAlreadyMember` (BR-63 điểm 4) mà TECH00 §4.2 chưa liệt kê; thay đổi cộng thêm vào contract.

## Giả định về phần khác

- **Phần 3 Task 1–2 (lõi nhật ký):**
  - `IAuditTrail.RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)` thêm một dòng `kind = 'Event'` vào lần `SaveChangesAsync` kế tiếp. Nó không tự save.
  - `AuditEvent.For<TSubject>(subject, type, actorKind, actorAccountId, parameters)` với `TSubject : BaseEntity<Guid>, IHasOrganization`; `OrganizationInvitation` (Task 1) và `OrganizationMember` (phần 4 Task 1) thoả ràng buộc.
  - Danh mục `AuditEventCatalog` của phần 3 Task 1 đã khai đủ 13 loại, gồm `InvitationSent`, `InvitationRevoked`, `InvitationDeleted` (`email` Text, `role` Role); `MemberJoined` (`accountName` Text, `email` Text, `role` Role). Phần 5 không thêm, không sửa danh mục, và không chờ phần 6.
  - `role` là `AuditRoleRef`, ghi thành `{ "id", "name", "systemKind" }`.
- **Phần 3 (cổng, controller):**
  - `CurrentOrganizationController` (`api/organization`) đã có.
  - Policy theo mã quyền dựng `OrganizationContextRequirement` với `DenyMode.Forbidden`.
  - `TenantAccessMiddleware` miễn `/api/invitations`.
  - `LoginResponse` đã có `IsSystemAdministrator`, do `TokenIssuer` đặt.
  - `RoleRefDto(Guid Id, string Name, SystemRoleKind? SystemKind)` (Task 10), `SystemKind` ra JSON dạng tên qua `StringEnumConverter` toàn cục; mọi enum mới của phần này cũng vậy, không thêm thuộc tính.
  - `PageRequest` (Task 8), dùng cho API-10.
- **Phần 4:**
  - `IOrganizationLock.AcquireAsync(Guid organizationId, CancellationToken cancellationToken)` (Task 3).
  - `RoleGrantReader.LoadCallerAsync`, `LoadRolesAsync`; static `RoleGrantPolicy.CanGrant`, `CanTakeAway`, `CheckGrantAll`, `CheckTakeAwayAll`, `ErrorCode`; `RoleGrantCaller`, `RoleGrantRole`, `RoleGrantDenial`, `RoleLockReason` (Task 4). Chữ ký đầy đủ ở khối Consumes của Task 3.
  - `SystemRoleDisplayNames.Get(kind, "vi")` cho tên vai trò hệ thống (Task 1).
  - `RoleRow`, `IRoleQueries.ListAsync`, `RoleDto` (Task 8); Task 4 của phần này thêm `pendingInvitationCount`.
  - `OrganizationMember` cài `IHasOrganization`.
  - Thành viên bị gỡ hay rời không còn dòng `organization_member_roles` (M4, BR-30).
- **Phần 8:** chữ `locales/{vi,en}.ts` cho các mã lỗi lời mời, màn hình *Lời mời* và *InviteAccept*.

## Câu hỏi mở

- **Đã chốt (SA, 2026-10-23):** Hạn trong email mời ghi `dd/MM/yyyy HH:mm (giờ Việt Nam)`, đổi bằng độ lệch cố định UTC+7, không tra cơ sở dữ liệu múi giờ. PRD05 chốt mẫu chính thức (Task 3).
- **Đã chốt (SA, 2026-10-23):** API-12 khi email đã là thành viên đang hoạt động trả 409 `InvitationEmailAlreadyMember` (BR-63 điểm 4), sau các kiểm BR-58; lời mời cũ giữ nguyên, không email, không mục nhật ký. Thêm 409 vào contract API-12; PR khai là lệch nhỏ so với TECH00 §4.2 (Task 4, Task 8).
- **Đã chốt (SA, 2026-10-23):** Không gửi thông báo trong ứng dụng khi chấp nhận lời mời; `OrganizationMemberAdded` cũ thuộc việc thêm trực tiếp mà BR-73 đã bỏ. PRD05/TECH05 quyết thông báo về việc vào tổ chức (Task 5).

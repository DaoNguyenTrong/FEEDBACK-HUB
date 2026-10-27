# Module 00 — Phần 3: Lõi nhật ký kiểm toán, cổng tổ chức, admin hệ thống, phiên — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23). **Push/PR:** người dùng cho phép (2026-10-23): sau khi phần 8 xong và `scripts/test-gate.sh` qua, push `feature/module-00` và mở một PR vào `dev` cho cả module 00; không bao giờ vào `main`.

**Goal:** Dựng phần nền mà phần 4–8 dùng chung:
- lõi nhật ký kiểm toán ngữ nghĩa (`AuditEvent`, `IAuditTrail`, danh mục đóng, M9);
- cổng tổ chức theo thứ tự BR-77 bước 1–4, với mã lỗi riêng cho từng lý do;
- admin hệ thống: bảng, policy, lệnh CLI cấp và bỏ, API-24–API-26, cờ `isSystemAdministrator`;
- phiên: refresh mất quyền vào tổ chức thì về `org_id = null`, chuyển tổ chức báo đúng "tạm ngừng";
- tổ chức đang làm việc: đọc, đổi tên, vô hiệu (API-03–API-05), tạo tổ chức kiểm định danh (API-02).

**Architecture:**
- **Domain:** `AuditEvent` chỉ dựng được từ một entity đã tra (`AuditEvent.For`), không nhận `Guid` tổ chức. `Organization` thêm trường vô hiệu, `Create` kiểm định danh, `Update` chỉ đổi tên. Entity mới `SystemAdministrator`.
- **Application:** cổng `IAuditTrail` và danh mục `AuditEventCatalog` (namespace riêng `Services/AuditTrail`, không phụ thuộc service nào khác). `OrganizationContextService` (API-03–API-05), `SystemOrganizationService` cùng `SystemAdministratorActor` (API-24–API-26), `SystemAdministratorService` (CLI). `ITenantAccessService.GetAccessAsync` trả ba trạng thái.
- **Infrastructure:** `AuditTrail` ghi dòng `kind = 'Event'` vào `audit_logs` trong cùng `SaveChanges` với thay đổi. `OrganizationContextAuthorizationHandler` thay hai handler `ActiveOrganization*`. `SystemAdministratorResolver` có cache. Migration M9, M1, M2 (phần `system_administrators`).
- **API:** `CurrentOrganizationController` (`api/organization`), `SystemOrganizationsController` (`api/system/organizations`), `AuthorizationResultHandler` ghi mã theo lý do từ chối, lệnh `grant-system-admin`, `revoke-system-admin`.
- **Frontend:** chỉ sửa tối thiểu để contract mới biên dịch được. Màn hình thuộc phần 7–8.

**Tech Stack:** .NET 10 (xUnit, NSubstitute, Testcontainers PostgreSQL và Redis), EF Core + Npgsql, ASP.NET Core Authorization, Vue 3 + orval.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0: §2.1, §3.1, §3.5, §3.7 (lõi: `AuditEvent`, danh mục, `ActorKind`, M9), §4.1 (API-01–API-05), §4.5, §4.7, §4.8, §5.1, §5.2, §6.1, §6.5, §7.2 T6–T8, §10.1 (M1, M2, M9), §11, §12 QĐ-06, QĐ-07, QĐ-13, QĐ-15, QĐ-19, QĐ-20, QĐ-26, QĐ-31, QĐ-36.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: FR-03, FR-21, FR-24, FR-30, FR-38, FR-42, FR-44, FR-46, FR-53, FR-54, FR-63, FR-68; BR-20, BR-21, BR-45, BR-53, BR-54, BR-64, BR-65, BR-77, BR-78, BR-84.

## Lộ trình module 00

Module 00 phát hành một lần, không có cờ tính năng (TECH00 §10.2). Mọi phần làm trên nhánh `feature/module-00`, và chỉ mở một PR vào `dev` sau phần cuối. Mỗi phần là một kế hoạch riêng, người dùng duyệt trước khi làm.

Bảng của phần 1, cập nhật theo quyết định của người điều phối 2026-10-23: lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục) chuyển từ phần 6 lên đầu phần 3, để mọi service dựng ở phần 3–5 tự ghi mục của mình. Hàng 5 ghi thêm API-23 (`accountCreated`), phần 5 làm cùng lời mời. Bảng này giống hệt nhau ở mọi kế hoạch phần 3–8.

| Phần | Nội dung | TECH00 |
|---|---|---|
| 1 | Sửa bảo mật B1, B2; M7, M8; `TokenIssuer` kiểm `Status`; audit loại cột nhạy cảm; `SmtpEmailSender` | §3.8, §4.9, §6.6, §6.7, §7.3 |
| 2 | Sẵn sàng nhiều máy: giới hạn tần suất qua Redis, `ProductionTopologyValidator`, `EmailSettings:SecureSocket`, Data Protection | §4.8, §10.3 |
| **3 (tệp này)** | Lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục 13 loại); cổng tổ chức, admin hệ thống, phiên: `GetAccessAsync`, `OrganizationContextAuthorizationHandler`, refresh `org_id = null`, `isSystemAdministrator`, CLI `grant`/`revoke-system-admin`, `SystemOrganizationService`, API-01–API-05, M1, M2 (`system_administrators`) | §3.1, §3.5, §3.7 (lõi), §4.1, §4.5, §5.1, §5.2, §6.1, §6.5 |
| 4 | Vai trò và thành viên: `RoleGrantPolicy`, `IOrganizationLock`, `MemberService`, `RoleService`, M3–M5 | §3.2, §3.3, §4.3, §5.3, §5.4, §6.4 |
| 5 | Lời mời: M2 (`organization_invitations`), API-10–API-13, API-20–API-23, email mời | §3.4, §4.2, §4.4, §6.2, §6.3 |
| 6 | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Nhánh và commit**
  - Làm trên nhánh `feature/module-00`, sau phần 2.
  - Commit theo Conventional Commits, subject ≤ 100 ký tự, stage từng tệp theo đường dẫn.
  - Mỗi message kết thúc bằng đúng một dòng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    ```
- **C#:** theo `code-conventions.md`:
  - không `var`;
  - truyền `CancellationToken`;
  - entity chỉ tạo qua `static Create(XxxParams p)`;
  - lỗi Domain là `throw new DomainException(DomainMessages.X)`;
  - lỗi Application là `ConflictException`, `ForbiddenException`… với key trong `ApplicationMessages`.
- **Mã lỗi:** mã lỗi là key resx. Mỗi key mới có chữ ở cả `Messages.resx` (vi) và `Messages.en.resx` (en). Key Domain đặt ở `DomainMessages`, chữ cũng ở hai tệp resx đó.
- **Test:** theo `testing.md`:
  - tên `Method_Scenario_Expected`, xUnit `Assert`;
  - Application dùng NSubstitute, khẳng định tác dụng phụ bằng `Received`/`DidNotReceive`;
  - Infrastructure dùng PostgreSQL thật qua Testcontainers, không EF InMemory;
  - API qua HTTP, `[Collection(nameof(ApiCollection))]`: mọi endpoint `[Authorize]` có test 401, endpoint có policy tổ chức hay quyền có test 403.
  - Test nêu mã AC của PRD-00 trong comment khi kiểm một AC.
- **Log:** không ghi email, tên tổ chức, lý do vô hiệu, mã hay token vào log (TECH00 §7.3). Log chỉ dùng `accountId`, `organizationId`.
- **Nhật ký kiểm toán:**
  - Mục `Event` chỉ ghi qua `IAuditTrail.RecordAsync`, trong cùng `SaveChangesAsync` với thay đổi nó mô tả.
  - `OrganizationId` của mục lấy từ entity đã tra (`AuditEvent.For`), không bao giờ từ JWT, thân hay query.
  - Thao tác bị từ chối, rollback, hoặc không đổi gì thì không ghi mục.
  - Không ghi mục khi tạo tổ chức (AC-364).
- **Cache:** sau khi commit một thay đổi trạng thái tổ chức, gọi `ITenantAccessService.InvalidateOrganizationAsync` và `IPermissionResolver.InvalidateOrganizationAsync` của tổ chức đó.
- **Contract:** một lần export OpenAPI cho cả phần, ở task backend cuối (Task 11): `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`, rồi `bun run --cwd frontend codegen`. Commit `shared/openapi/openapi.json`.
- **Build:** mọi lệnh `dotnet` dùng `-m:1` (chung `obj/`).
- **Migration** (M-id theo TECH00 §10.1). Thứ tự áp dụng của cả module:
  - phần 1: M7 `20261023073639_AddEmailChange`, M8 `20261023120000_ReverifyMicrosoftOnlyAccounts` (đặt tay);
  - phần 3: M9 `AddAuditEventKind` (Task 2), M1 `AddOrganizationDeactivation` (Task 3), M2 `AddSystemAdministrators` (Task 7);
  - phần 4: M3 `RenameDuplicateRoleNames`, M4 `RemoveRolesOfInactiveMembers`, M5 `GrantFeedbackHubAdminToAdminRoles`;
  - phần 5: M2 `AddOrganizationInvitations`.

  Id mỗi migration lớn hơn hẳn id mới nhất đang có trong `Migrations/`; không hai migration chung timestamp. `dotnet ef migrations add` lấy giờ UTC lúc chạy, có thể nhỏ hơn `20261023120000` của M8. Khi đó đổi id thành id mới nhất cộng một giây, ở tên hai tệp (`.cs`, `.Designer.cs`) và thuộc tính `[Migration("…")]`.
- **Bí mật:** không đọc, không sửa `appsettings.json` hay `appsettings.*.json`, trừ `appsettings.Example.json`.

## Review Focus

1. **Người không phải thành viên gọi vào một tổ chức đang tạm ngừng** (JWT cũ còn `org_id`): nhận `OrganizationAccessDenied` (hoặc 404), thân không có `organizationName`, không bao giờ `OrganizationSuspended`. Test ở Task 4 (middleware), Task 5 (cổng), Task 6 (API-28).
2. **Request ngay sau khi tổ chức bị vô hiệu**, trên cache còn ghi "Active": request kế tiếp nhận 403 `OrganizationSuspended`, vì cache bị xoá sau commit. Test ở Task 8 và Task 10.
3. **Refresh của người vừa bị gỡ hoặc của tổ chức vừa tạm ngừng**: trả 200 với `organizationId = null`, cùng phiên (`familyId`). Tài khoản bị khoá vẫn 401. Test ở Task 6.
4. **Định danh gõ chữ hoa, có khoảng trắng hoặc có dấu** (`" Sao-Mai "`, `"sao-mài"`): chữ hoa và khoảng trắng được chuẩn hoá rồi nhận; có dấu thì 400 `OrganizationSlugInvalid`. Tổ chức có slug cũ sai định dạng vẫn đổi tên được. Test ở Task 3.
5. **Hai người tạo tổ chức cùng định danh gần như đồng thời**: một bên 201, bên kia 409 `OrganizationSlugAlreadyExists`, không 500. Test ở Task 10.

---

### Task 1: Lõi nhật ký kiểm toán (1/2): `AuditEvent`, danh mục, `IAuditTrail`, `SystemAdministratorActor`

**Files:**
- Create:
  - `backend/src/FeedbackHub.Domain/Auditing/AuditEvent.cs`
  - `backend/src/FeedbackHub.Domain/Auditing/AuditEventType.cs`
  - `backend/src/FeedbackHub.Domain/Auditing/ActorKind.cs`
  - `backend/src/FeedbackHub.Domain/Auditing/AuditRoleRef.cs`
  - `backend/src/FeedbackHub.Application/Services/AuditTrail/IAuditTrail.cs`
  - `backend/src/FeedbackHub.Application/Services/AuditTrail/AuditEventCatalog.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemOrganizations/SystemAdministratorActor.cs`
- Modify: `backend/src/FeedbackHub.Application/DependencyInjection.cs` (đăng ký scoped `SystemAdministratorActor`)
- Test:
  - `backend/tests/FeedbackHub.Domain.Tests/Auditing/AuditEventTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/AuditTrail/AuditEventCatalogTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/SystemOrganizations/SystemAdministratorActorTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Architecture/ArchitectureTests.cs`

**Interfaces:**
- Produces (Domain, namespace `FeedbackHub.Domain.Auditing`):
  - `public enum ActorKind { Account = 1, SystemAdministrator = 2, System = 3 }`.
  - `public enum AuditEventType`, đủ 13 loại của TECH00 §3.7, đúng tên: `InvitationSent`, `InvitationRevoked`, `InvitationDeleted`, `MemberJoined`, `MemberRolesChanged`, `MemberRemoved`, `MemberLeft`, `RoleCreated`, `RoleUpdated`, `RoleDeleted`, `OrganizationRenamed`, `OrganizationDeactivated`, `OrganizationReactivated`. Giá trị bắt đầu từ 1.
  - `public sealed record AuditRoleRef(Guid Id, string Name, SystemRoleKind? SystemKind)` với `public static AuditRoleRef From(Role role)`.
  - `AuditEvent`:
    ```csharp
    public sealed class AuditEvent
    {
        public Guid OrganizationId { get; private init; }
        public Guid SubjectId { get; private init; }
        public AuditEventType Type { get; private init; }
        public ActorKind ActorKind { get; private init; }
        public Guid? ActorAccountId { get; private init; }
        public IReadOnlyDictionary<string, object> Parameters { get; private init; }

        public static AuditEvent For<TSubject>(TSubject subject, AuditEventType type, ActorKind actorKind,
            Guid? actorAccountId, IReadOnlyDictionary<string, object>? parameters = null)
            where TSubject : BaseEntity<Guid>, IHasOrganization;

        public static AuditEvent For(Organization organization, AuditEventType type, ActorKind actorKind,
            Guid? actorAccountId, IReadOnlyDictionary<string, object>? parameters = null);
    }
    ```
    - Không constructor công khai. `For<TSubject>` chép `subject.OrganizationId` và `subject.Id`; `For(Organization)` chép `organization.Id` vào cả hai.
    - `OrganizationId == Guid.Empty` → `ArgumentException`.
    - `Parameters` là bản sao chỉ đọc; `null` thành từ điển rỗng.
- Produces (Application, namespace `FeedbackHub.Application.Services.AuditTrail`):
  - `public interface IAuditTrail { Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken); }`. Doc comment: chỉ "xếp" dòng vào unit of work; lần `SaveChangesAsync` kế tiếp của người gọi mới ghi.
  - `public enum AuditParameterKind { Text, TextList, Role, RoleList }`. Kiểu giá trị tương ứng: `string`, `IReadOnlyList<string>`, `AuditRoleRef`, `IReadOnlyList<AuditRoleRef>`.
  - `public sealed record AuditEventDefinition(string EntityName, IReadOnlyDictionary<string, AuditParameterKind> Parameters)`.
  - `public static class AuditEventCatalog`:
    - `AuditEventDefinition Get(AuditEventType type)`;
    - `void Validate(AuditEvent auditEvent)`: lỗi lập trình nên ném `InvalidOperationException`;
    - `bool TryResolve(string entityName, string action, out AuditEventType type)`: tra ngược dạng đã lưu của một dòng (`entity_name`, `action` do Task 2 ghi) về loại. Đúng khi `action` là đúng tên một giá trị enum (phân biệt hoa thường, không nhận chuỗi số như `"1"`) và `entityName == Get(type).EntityName`; ngược lại `false`, không ném. Phần 6 (API-33) dùng `TryResolve` và `Get(type).Parameters` (khoá và `AuditParameterKind`) để bỏ dòng lạ và lọc khoá; không dựng ánh xạ thứ hai.
  - Danh mục, đúng bảng TECH00 §3.7:

    | Loại | `EntityName` | Tham số |
    |---|---|---|
    | `InvitationSent`, `InvitationRevoked`, `InvitationDeleted` | `OrganizationInvitation` | `email` Text, `role` Role |
    | `MemberJoined` | `OrganizationMember` | `accountName` Text, `email` Text, `role` Role |
    | `MemberRolesChanged` | `OrganizationMember` | `accountName` Text, `email` Text, `rolesBefore` RoleList, `rolesAfter` RoleList |
    | `MemberRemoved` | `OrganizationMember` | `accountName` Text, `email` Text, `roles` RoleList |
    | `MemberLeft` | `OrganizationMember` | `accountName` Text, `email` Text |
    | `RoleCreated` | `Role` | `role` Role, `permissionCodes` TextList |
    | `RoleUpdated` | `Role` | `nameBefore` Text, `nameAfter` Text, `permissionsAdded` TextList, `permissionsRemoved` TextList |
    | `RoleDeleted` | `Role` | `role` Role |
    | `OrganizationRenamed` | `Organization` | `nameBefore` Text, `nameAfter` Text |
    | `OrganizationDeactivated`, `OrganizationReactivated` | `Organization` | không có |

- Produces (Application, namespace `FeedbackHub.Application.Services.SystemOrganizations`):
  - `public sealed class SystemAdministratorActor(IUnitOfWork unitOfWork)` với `Task<ActorKind> ResolveAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken)`.
  - Trả `ActorKind.Account` khi có dòng `organization_members` `IsActive` của đúng tổ chức và tài khoản đó, ngược lại `ActorKind.SystemAdministrator`. Đọc thẳng qua `IUnitOfWork`, không qua `ITenantAccessService` hay cache.

- [x] **Step 1: Viết test thất bại**

```csharp
// Domain: AuditEventTests
For_Subject_CopiesSubjectOrganizationAndId            // ApiKey của org A → OrganizationId == A, SubjectId == apiKey.Id
For_Organization_CopiesOrganizationIdIntoBoth
For_SubjectWithEmptyOrganizationId_Throws
For_NullParameters_IsEmptyAndCallerCannotMutate        // sửa từ điển gốc sau For → Parameters không đổi
// Application: AuditEventCatalogTests
Get_EveryEnumValue_HasDefinition                       // Enum.GetValues<AuditEventType>()
Get_Definitions_MatchTech00Catalogue                   // [Theory] 13 dòng của bảng trên: EntityName, tên và kiểu tham số
Validate_UndeclaredParameter_Throws                    // OrganizationRenamed + "reason" → InvalidOperationException
Validate_MissingDeclaredParameter_Throws
Validate_WrongValueKind_Throws                         // "role" là string thay vì AuditRoleRef
Validate_AccountOrSystemAdministratorWithoutActor_Throws   // C2
Validate_SystemWithActor_Throws                            // C2
Validate_EmptyOrganizationId_Throws
Validate_ValidEvent_DoesNotThrow
[Theory] TryResolve_StoredFormOfEveryType_ReturnsSameType     // mọi AuditEventType: (Get(t).EntityName, t.ToString()) → t
[Theory] TryResolve_UnknownActionWrongEntityOrNumeric_ReturnsFalse
// ("OrganizationInvitation", "Revoked"), ("OrganizationMember", "Left"), ("Organization", "1"),
// ("OrganizationInvitation", "invitationsent"), ("Role", "InvitationSent")
// SystemAdministratorActorTests
ResolveAsync_ActiveMemberOfThatOrganization_ReturnsAccount
ResolveAsync_InactiveMemberOrMemberOfOtherOrganization_ReturnsSystemAdministrator
// ArchitectureTests
AuditEvent_HasNoPublicConstructorOrFactoryTakingOrganizationGuid
// không constructor public; mọi method public static trả AuditEvent không có tham số kiểu Guid
// (chỉ Guid? actorAccountId); setter của OrganizationId không public (C1)
AuditTrailNamespace_DependsOnNoOtherServiceNamespace
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~AuditEventTests|FullyQualifiedName~AuditEventCatalogTests|FullyQualifiedName~SystemAdministratorActorTests|FullyQualifiedName~ArchitectureTests"`
Expected: FAIL (các kiểu chưa tồn tại).

- [x] **Step 3: Cài đặt** theo Interfaces. `Validate` so tập khoá đúng bằng tập khai báo (không thiếu, không thừa), rồi kiểu từng giá trị, rồi quy tắc người làm.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(audit): add the closed audit event catalogue and an event bound to its subject`

### Task 2: Lõi nhật ký kiểm toán (2/2): `AuditTrail`, cột `kind`, `actor_kind` (M9)

**Files:**
- Create:
  - `backend/src/FeedbackHub.Infrastructure/Persistence/AuditLogKind.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/AuditTrail.cs`
  - `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_AddAuditEventKind.cs` (sinh bằng `dotnet ef migrations add AddAuditEventKind`)
- Modify:
  - `backend/src/FeedbackHub.Infrastructure/Persistence/AuditLog.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/AuditEntry.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs`
  - `backend/src/FeedbackHub.Infrastructure/DependencyInjection.cs` (đăng ký scoped `IAuditTrail` → `AuditTrail`)
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/AuditTrailTests.cs`

**Interfaces:**
- Consumes: Task 1 (`AuditEvent`, `AuditEventCatalog`, `IAuditTrail`).
- Produces:
  - `public enum AuditLogKind { Change = 1, Event = 2 }`. Không có giá trị 0, nên dòng quên gán không thành `Event`.
  - `AuditLog` thêm `AuditLogKind Kind` và `string? ActorKind`.
  - `AuditEntry.ToAuditLog` gán `Kind = AuditLogKind.Change` tường minh.
  - `internal sealed class AuditTrail(AppDbContext dbContext, IDateTimeProvider dateTimeProvider) : IAuditTrail`. Không nhận `ICurrentTenantProvider`, `ICurrentUserService` hay `IHttpContextAccessor`.
  - Dòng `Event` mà `RecordAsync` thêm vào `dbContext.AuditLogs` (chưa save):
    - `Kind = Event`, `Action = Type.ToString()`, `EntityName` lấy từ danh mục, `EntityId = SubjectId.ToString()`;
    - `OrganizationId = auditEvent.OrganizationId`, `UserId = ActorAccountId?.ToString()`, `ActorKind = ActorKind.ToString()`;
    - `NewValues` = JSON của `Parameters` (camelCase; `AuditRoleRef` thành `{ "id", "name", "systemKind" }`, `systemKind` là tên enum hoặc `null`); từ điển rỗng thì `null`;
    - `OldValues`, `IpAddress`, `UserAgent` = `null` (B4);
    - `Timestamp = dateTimeProvider.UtcNow`.
  - M9 (TECH00 §10.1):
    - `audit_logs.kind` varchar(10) not null default `'Change'`, lưu tên enum;
    - `audit_logs.actor_kind` varchar(20) null;
    - nới `audit_logs.action` từ varchar(20) lên varchar(50), vì `OrganizationDeactivated` dài 23 ký tự. Nới cột không ảnh hưởng code cũ;
    - chỉ mục một phần `IX_audit_logs_event_keyset` trên `(organization_id, "timestamp" DESC, id DESC) WHERE kind = 'Event'`.

- [x] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
RecordAsync_ThenSaveChanges_WritesOneEventRowFromTheEvent
// OrganizationDeactivated, For(organization A), Account, actor K; HttpContext giả có IP và User-Agent, JWT org_id = B
// → đúng một dòng kind 'Event': organization_id A (không phải B), user_id K, actor_kind 'Account',
//   action 'OrganizationDeactivated', entity_name 'Organization', entity_id A, ip_address null, user_agent null
RecordAsync_RoleParameters_SerializesSystemKind
// RoleDeleted với role Owner hệ thống → new_values {"role":{"id":…,"name":"Owner","systemKind":"Owner"}}
RecordAsync_WithoutSaveChanges_WritesNothing
RecordAsync_TransactionRolledBack_WritesNothing      // ExecuteInTransactionAsync ném sau SaveChanges → 0 dòng (AC-337)
RecordAsync_InvalidEvent_ThrowsAndStagesNothing      // tham số lạ → InvalidOperationException, ChangeTracker không có AuditLog
SaveChanges_SnapshotOfApiKey_IsKindChange            // snapshot tự động của entity IHasOrganization có kind 'Change' (B2)
Migration_RowInsertedWithoutKind_DefaultsToChange    // INSERT thô không có cột kind → 'Change'
AuditTrail_Constructor_TakesNoTenantOrUserContext
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~AuditTrailTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt**
  - `RecordAsync` gọi `AuditEventCatalog.Validate` trước, rồi thêm dòng. Không gọi `SaveChangesAsync`.
  - `AuditLogConfiguration`: `Kind` lưu chuỗi (`HasConversion<string>()`, `HasMaxLength(10)`), `ActorKind` `HasMaxLength(20)`, `Action` `HasMaxLength(50)`, thêm chỉ mục một phần bằng `HasIndex(...).IsDescending(false, true, true).HasFilter("kind = 'Event'").HasDatabaseName("IX_audit_logs_event_keyset")`.
  - Sinh migration. Kiểm `Up` có `defaultValue: "Change"` cho `kind` và chỉ thêm hay nới cột, không đụng dữ liệu.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(audit): record semantic audit events in the same save as the change (M9)`

### Task 3: `Organization` kiểm định danh, chỉ đổi tên, vô hiệu và kích hoạt lại (M1)

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Domain/Entities/Organization.cs`
  - `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs`
  - `backend/src/FeedbackHub.Application/Resources/Messages.resx`, `Messages.en.resx`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs`
  - Mọi chỗ gọi `new OrganizationParams(…, Status)` hoặc `organization.Deactivate()` (tìm bằng CodeGraph): `OrganizationService`, `DevDataSeeder`, test hiện có.
- Create: `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_AddOrganizationDeactivation.cs` (`dotnet ef migrations add AddOrganizationDeactivation`)
- Test:
  - `backend/tests/FeedbackHub.Domain.Tests/Entities/OrganizationTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/OrganizationPersistenceTests.cs`

**Interfaces:**
- Produces:
  - `public record OrganizationParams(string Name, string Slug)`. Bỏ `Status`.
  - `public record OrganizationNameParams(string Name)`.
  - `public record DeactivationParams(Guid ActorAccountId, bool BySystemAdministrator, string? Reason, DateTime DeactivatedAt)`.
  - `Organization`:
    - `static Organization Create(OrganizationParams p)`: slug trim và chữ thường; trống → `OrganizationSlugRequired` (có sẵn); không khớp `^[a-z0-9]+(-[a-z0-9]+)*$` hoặc dài ngoài 3–50 → `DomainException(DomainMessages.OrganizationSlugInvalid)`; rồi gọi `Update(new OrganizationNameParams(p.Name))`.
    - `void Update(OrganizationNameParams p)`: tên trim, trống → `OrganizationNameRequired`. Không đụng `Slug` hay trạng thái.
    - `DateTime? DeactivatedAt`, `Guid? DeactivatedByAccountId`, `bool DeactivatedBySystemAdministrator`, `string? DeactivationReason`, đều `{ get; private set; }`.
    - `void Deactivate(DeactivationParams p)`: `Status = false`, gán bốn trường; lý do trim, chuỗi trống thành `null`. Gọi khi đã vô hiệu thì không đổi gì.
    - `void Reactivate()`: `Status = true`, xoá bốn trường.
  - `DomainMessages.OrganizationSlugInvalid`. Chữ:
    - vi "Định danh chỉ gồm chữ thường không dấu, chữ số và dấu gạch ngang nằm giữa, dài 3–50 ký tự."
    - en "The identifier may use only lowercase letters a–z, digits and single hyphens between them, and must be 3–50 characters long."
  - M1: cột `organizations."DeactivatedAt"` timestamptz null, `"DeactivatedByAccountId"` uuid null (không FK), `"DeactivatedBySystemAdministrator"` boolean not null default `false`, `"DeactivationReason"` varchar(500) null.

- [x] **Step 1: Viết test thất bại**

```csharp
// Domain
[Theory] Create_SlugBreaksFormat_ThrowsSlugInvalid      // "sao_mai", "-saomai", "saomai-", "sao--mai" (AC-283), "sao-mài"
[Theory] Create_SlugLengthOutside3To50_ThrowsSlugInvalid // "ab", 51 ký tự (AC-284)
[Theory] Create_SlugAtLengthLimits_Succeeds              // "abc", 50 ký tự (AC-285)
Create_SlugWithUpperCaseAndSpaces_IsNormalized           // " Sao-Mai " → "sao-mai" (AC-329)
Update_RenamesAndKeepsSlug                               // AC-225
Update_BlankName_ThrowsNameRequired                      // AC-187
Deactivate_SetsStatusAndAllFourFields
Deactivate_BlankReason_StoresNull
Deactivate_AlreadyDeactivated_KeepsFirstActorAndReason
Reactivate_ClearsAllFourFields
// Infrastructure (Testcontainers)
SaveAndLoad_DeactivationFields_RoundTrip
Rename_LegacySlugOutsideFormat_SavesWithoutRecheck       // INSERT thô slug "Old_Slug", tải, Update tên, SaveChanges → slug giữ nguyên
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~OrganizationTests|FullyQualifiedName~OrganizationPersistenceTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Sửa các chỗ gọi cũ cho biên dịch được: `OrganizationService.DeactivateAsync` tạm truyền `DeactivationParams(callerId, false, null, now)`; Task 10 xoá hàm này. Sinh migration, kiểm `Up` chỉ thêm cột.

- [x] **Step 4: Chạy lại, phải qua**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~Organization"`
Expected: PASS.

- [x] **Step 5: Commit** `feat(organizations): validate the identifier at creation and record who deactivated (M1)`

### Task 4: `GetAccessAsync` ba trạng thái, lưới cuối `TenantAccessMiddleware` ghi mã

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Common/Interfaces/ITenantAccessService.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/TenantAccessService.cs`
  - `backend/src/FeedbackHub.API/Middleware/TenantAccessMiddleware.cs`
  - `backend/src/FeedbackHub.API/Middleware/ExceptionHandlingMiddleware.cs`
  - `backend/src/FeedbackHub.Domain/Exceptions/ForbiddenException.cs`
  - `backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
  - Người gọi `HasActiveAccessAsync`: `AuthService.RefreshTokenAsync`, `ActiveOrganizationMembershipAuthorizationHandler` (giữ hành vi, so `== TenantAccessStatus.Active`; Task 5 xoá handler, Task 6 đổi refresh).
- Create: `backend/src/FeedbackHub.Application/Common/Interfaces/TenantAccessStatus.cs`
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/TenantAccessServiceTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/MiddlewareTests.cs`

**Interfaces:**
- Produces:
  - `public enum TenantAccessStatus { NotMember = 1, Suspended = 2, Active = 3 }`.
  - `ITenantAccessService.GetAccessAsync(Guid accountId, Guid organizationId, CancellationToken cancellationToken = default) → Task<TenantAccessStatus>`. Thay `HasActiveAccessAsync`. Hai hàm `Invalidate*` giữ nguyên.
    - Tra dòng thành viên `IsActive` trước. Không có thì trả `NotMember`, không đọc tổ chức.
    - Có thì đọc tổ chức: `Status == false` → `Suspended`, ngược lại `Active`.
    - Cache cùng scope `tenant-access:{org}`, cùng TTL.
  - `ForbiddenException(string message, IReadOnlyDictionary<string, string>? extensions = null)` với `IReadOnlyDictionary<string, string> Extensions` (rỗng khi không truyền). `ExceptionHandlingMiddleware` chép từng phần tử vào `problemDetails.Extensions`.
  - `ProblemDetails.Extensions` là `[JsonExtensionData]` và `ApiProblemDetailsFactory.WriteAsync` ghi bằng `WriteAsJsonAsync`, nên `organizationName` nằm ở cấp cao nhất của thân, cạnh `code` (`{ "status": 403, "code": "OrganizationSuspended", "organizationName": "…" }`), không trong một object `extensions`. Phần 8 đọc đúng `problem.organizationName`. Mọi đường ghi 403 `OrganizationSuspended` (middleware, `AuthorizationResultHandler`, `ExceptionHandlingMiddleware`) cho cùng hình dạng này.
  - `ApplicationMessages.OrganizationSuspended`. Chữ: vi "Tổ chức này đang tạm ngừng hoạt động.", en "This organization is suspended." Tên tổ chức đi trong extension `organizationName`, không trong `detail`.
  - `TenantAccessMiddleware`:
    - ghi `CodedProblemDetails` qua `ApiProblemDetailsFactory`: `NotMember` → 403 `OrganizationAccessDenied`; `Suspended` → 403 `OrganizationSuspended` kèm `organizationName`;
    - danh sách miễn trừ thêm `/api/invitations` và `/api/system` (TECH00 §5.1), giữ `/api/auth`, `/api/organizations`, `/api/permissions`. `/api/organization` (số ít) không được miễn: `StartsWithSegments` so theo đoạn nên `/api/organizations` không che nó.

- [x] **Step 1: Viết test thất bại**

```csharp
// Infrastructure (Testcontainers)
GetAccessAsync_ActiveMemberActiveOrganization_ReturnsActive
GetAccessAsync_ActiveMemberSuspendedOrganization_ReturnsSuspended
[Theory] GetAccessAsync_NotMemberOfSuspendedOrganization_ReturnsNotMember   // chưa từng vào; đã rời (IsActive false) — AC-320
GetAccessAsync_InvalidatedOnOneInstance_OtherInstanceSeesChangeAtOnce
// hai RedisCacheService cùng một Redis (RedisContainerFixture); máy 1 đọc Active, đổi Status, máy 2 InvalidateOrganizationAsync
// → máy 1 đọc Suspended ngay (AC-190, phần cache)
// API (MiddlewareTests), gọi GET api/notifications với JWT mang org_id
TenantAccess_SuspendedOrganizationMember_Returns403SuspendedWithName      // body.organizationName ở cấp cao nhất, cạnh body.code
TenantAccess_RemovedMember_Returns403AccessDeniedWithoutName              // thân không có khoá organizationName
TenantAccess_SystemAndInvitationsPaths_AreExempt                          // không bị middleware chặn
ForbiddenException_WithExtensions_AreWrittenToProblemDetails
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~TenantAccessServiceTests|FullyQualifiedName~MiddlewareTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Tên tổ chức cho `Suspended` đọc bằng `IUnitOfWork.Repository<Organization, Guid>().GetByIdAsync` chỉ ở nhánh từ chối.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(auth): tell a suspended organization apart from lost membership`

### Task 5: Cổng tổ chức `OrganizationContextAuthorizationHandler`

**Files:**
- Create (Infrastructure, `Services/Auth/`):
  - `DenyMode.cs`
  - `OrganizationContextRequirement.cs`
  - `OrganizationContextAuthorizationHandler.cs`
  - `CodedAuthorizationFailureReason.cs`
- Modify:
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/OrganizationPermissionPolicyProvider.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/AuthExtensions.cs`
  - `backend/src/FeedbackHub.Application/Common/Authorization/AuthorizationPolicies.cs`
  - `backend/src/FeedbackHub.Application/Common/Authorization/Permissions.cs`
  - `backend/src/FeedbackHub.API/Authorization/AuthorizationResultHandler.cs`
  - `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
  - `.claude/rules/authentication.md` (§ Organizations and permissions)
- Delete: `ActiveOrganizationMembershipRequirement.cs`, `ActiveOrganizationMembershipAuthorizationHandler.cs`, `ActiveOrganizationPermissionRequirement.cs`, `ActiveOrganizationPermissionAuthorizationHandler.cs` và hai tệp test của chúng.
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/OrganizationContextAuthorizationHandlerTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/OrganizationPermissionPolicyProviderTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Authorization/AuthorizationResultHandlerTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/FilesControllerTests.cs`, `ApiKeysControllerTests.cs` (thêm ca cổng)

**Interfaces:**
- Consumes: Task 4 (`GetAccessAsync`, `OrganizationSuspended`).
- Produces:
  - `public enum DenyMode { Forbidden = 1, NotFound = 2 }`.
  - `public sealed class OrganizationContextRequirement(string? permissionCode, DenyMode denyMode) : IAuthorizationRequirement`.
  - `public sealed class CodedAuthorizationFailureReason(IAuthorizationHandler handler, int statusCode, string code, string? organizationName = null) : AuthorizationFailureReason`. Phần 3 cũng dùng nó cho policy `SystemAdministrator` (Task 7).
  - Handler, theo đúng thứ tự TECH00 §5.1, dừng ở bước đầu tiên không đạt:
    1. chưa đăng nhập: không làm gì (challenge mặc định 401);
    2. không có `org_id`, `sub` không phải Guid (khoá API), hoặc `GetAccessAsync` = `NotMember` → `Forbidden`: 403 `OrganizationAccessDenied`; `NotFound`: 404 `ResourceNotFound`;
    3. `Suspended` → 403 `OrganizationSuspended` kèm tên tổ chức, ở cả hai chế độ;
    4. có `PermissionCode` mà `IPermissionResolver` không trả mã đó → 403 `OrganizationAccessDenied`.
  - `AuthorizationPolicies.ActiveOrganizationMember` dựng `OrganizationContextRequirement(null, DenyMode.Forbidden)`.
  - `AuthorizationPolicies.ActiveOrganizationMemberOrNotFound = "ActiveOrganizationMemberOrNotFound"`, dựng `OrganizationContextRequirement(null, DenyMode.NotFound)`. Dùng cho API-06 (phần 4), feedback và Tổng quan (TECH03, TECH06).
  - `OrganizationPermissionPolicyProvider`: mã trong `Permissions.ActiveOrganizationScoped` dựng `OrganizationContextRequirement(code, DenyMode.Forbidden)`. `Permissions.ActiveOrganizationScoped` thêm `OrganizationManage`: route duy nhất còn dùng nó theo `{id}` (`DELETE api/organizations/{id}`) bị bỏ ở Task 10. `organizations.members.manage` và `organizations.roles.manage` vẫn theo route `{id}` đến khi phần 4 bỏ các route đó.
  - `AuthorizationResultHandler`: khi Forbidden, lấy `CodedAuthorizationFailureReason` đầu tiên trong `authorizeResult.AuthorizationFailure?.FailureReasons`, ghi `CodedProblemDetails` với đúng status, mã và extension `organizationName` (nếu có). Không có lý do mang mã thì giữ 403 `OrganizationAccessDenied` như nay.
  - `ApplicationMessages.ResourceNotFound`. Chữ: vi "Không tìm thấy.", en "Not found." Không nêu loại hay id đối tượng.

- [x] **Step 1: Viết test thất bại**

```csharp
// Infrastructure: OrganizationContextAuthorizationHandlerTests
[Theory] Handle_PersonalContextOrApiKey_FailsByDenyMode          // Forbidden → 403 OrganizationAccessDenied; NotFound → 404 ResourceNotFound
[Theory] Handle_NotMember_FailsByDenyMode                        // kể cả tổ chức đang tạm ngừng: không có organizationName (AC-320)
Handle_SuspendedMember_FailsSuspendedWithNameInBothModes        // AC-301
Handle_SuspendedMemberMissingPermission_FailsSuspendedNotAccessDenied   // bước 3 trước bước 4
Handle_ActiveMemberMissingPermission_FailsAccessDeniedWithoutDataQuery  // AC-316
Handle_ActiveMemberWithPermission_Succeeds
Handle_NoPermissionCode_ActiveMember_Succeeds
// OrganizationPermissionPolicyProviderTests: cập nhật theo kiểu requirement mới; organizations.manage → OrganizationContextRequirement
// API: AuthorizationResultHandlerTests (gọi HandleAsync với PolicyAuthorizationResult.Forbid(failure) dựng tay)
HandleAsync_NotFoundReason_Writes404ResourceNotFound
HandleAsync_SuspendedReason_Writes403WithOrganizationName
HandleAsync_NoCodedReason_Writes403AccessDenied
// API qua HTTP
Files_PersonalContext_Returns403AccessDenied                        // AC-298
ApiKeys_SuspendedOrganizationMemberWithoutPermission_Returns403Suspended   // tạm ngừng trước thiếu quyền (AC-321)
ApiKeys_NonMemberOfSuspendedOrganization_Returns403WithoutName
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~OrganizationContextAuthorizationHandlerTests|FullyQualifiedName~OrganizationPermissionPolicyProviderTests|FullyQualifiedName~AuthorizationResultHandlerTests|FullyQualifiedName~FilesControllerTests|FullyQualifiedName~ApiKeysControllerTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Không đạt thì gọi `context.Fail(new CodedAuthorizationFailureReason(...))`, không chỉ `return`. Sửa đoạn policy trong `authentication.md`:
  - liệt kê `ActiveOrganizationMember`, `ActiveOrganizationMemberOrNotFound`, `SignedInUser`, `SystemAdministrator` (Task 7);
  - một câu nêu thứ tự cổng: chưa đăng nhập, không phải thành viên, tạm ngừng, thiếu quyền.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(auth): gate active-organization requests in BR-77 order with a code per denial`

### Task 6: Phiên: refresh về `org_id = null`, chuyển tổ chức, tổ chức mặc định

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Auth/AuthService.cs` (`RefreshTokenAsync`, `SwitchOrganizationAsync`)
  - `backend/src/FeedbackHub.Application/Services/Auth/TokenIssuer.cs` (`ResolveDefaultOrganizationIdAsync`)
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/Auth/TokenIssuerTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/AuthControllerTests.cs`

**Interfaces:**
- Consumes: Task 4 (`GetAccessAsync`, `ForbiddenException` có extensions).
- Produces:
  - `RefreshTokenAsync`: token có `OrganizationId` mà `GetAccessAsync` ≠ `Active` → vẫn xoay token, gọi `IssueTokensAsync` với `organizationId: null`, cùng `FamilyId`, cùng `IsPersistent`, `LoginAt`. Tài khoản bị khoá và token không hợp lệ giữ 401 như nay (QĐ-31).
  - `SwitchOrganizationAsync` (API-28): `NotMember` → `ForbiddenException(OrganizationAccessDenied)`; `Suspended` → `ForbiddenException(OrganizationSuspended, { ["organizationName"] = organization.Name })`.
  - `ResolveDefaultOrganizationIdAsync`: chỉ đếm tổ chức `Status == true`. Đúng một → id đó; còn lại → `null` (QĐ-26).

- [x] **Step 1: Viết test thất bại**

```csharp
// Application
RefreshTokenAsync_RemovedMember_IssuesTokensWithoutOrganizationSameFamily
RefreshTokenAsync_SuspendedOrganization_IssuesTokensWithoutOrganization    // AC-183, AC-199
RefreshTokenAsync_LockedAccount_StillThrows401
SwitchOrganizationAsync_SuspendedOrganizationMember_ThrowsSuspendedWithName
SwitchOrganizationAsync_NotMemberOfSuspendedOrganization_ThrowsAccessDeniedWithoutName
ResolveDefaultOrganizationIdAsync_OneActiveOneSuspended_ReturnsTheActive
ResolveDefaultOrganizationIdAsync_OnlySuspended_ReturnsNull                // AC-184
// API
Refresh_RemovedMember_Returns200WithNullOrganizationId
Refresh_SuspendedOrganization_Returns200WithNullOrganizationId
SwitchOrganization_SuspendedOrganization_Returns403SuspendedWithName
SwitchOrganization_NonMember_Returns403AccessDeniedWithoutName
Login_MemberOfOnlySuspendedOrganization_HasNoOrganization                   // AC-184
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~AuthServiceTests|FullyQualifiedName~TokenIssuerTests|FullyQualifiedName~AuthControllerTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(auth): keep the session without an organization when access to it is lost`

### Task 7: Admin hệ thống: bảng (M2), resolver có cache, policy, `isSystemAdministrator` (API-27)

**Files:**
- Create:
  - `backend/src/FeedbackHub.Domain/Entities/SystemAdministrator.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/SystemAdministratorConfiguration.cs`
  - `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_AddSystemAdministrators.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemAdministrators/ISystemAdministratorResolver.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/SystemAdministratorResolver.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/SystemAdministratorRequirement.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/SystemAdministratorAuthorizationHandler.cs`
- Modify:
  - `AppDbContext.cs` (`DbSet<SystemAdministrator>`)
  - `AuthExtensions.cs`, `AuthorizationPolicies.cs`
  - `TokenIssuer.cs`, `AuthResult.cs`, `LoginResponse.cs`, `backend/src/FeedbackHub.API/Controllers/AuthController.cs` (`ToResponse`)
  - `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/SystemAdministratorResolverTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/SystemAdministratorAuthorizationHandlerTests.cs`
  - `TokenIssuerTests.cs`, `AuthControllerTests.cs`

**Interfaces:**
- Consumes: Task 5 (`CodedAuthorizationFailureReason`).
- Produces:
  - `public record SystemAdministratorParams(Guid AccountId)`; `SystemAdministrator : BaseEntity<Guid>` có `Guid AccountId`, `Create` sinh id `IdGenerator.NewUuidV7()`; `AccountId == Guid.Empty` → `DomainException(DomainMessages.AccountIdRequired)` (key có sẵn).
  - M2 phần này (TECH00 §3.5): bảng `system_administrators`, chỉ mục unique `AccountId`, FK `accounts` ON DELETE CASCADE, trường audit chung. Không cài `IHasOrganization`; snapshot tự động có `organization_id = null`.
  - `ISystemAdministratorResolver`:
    - `Task<bool> IsSystemAdministratorAsync(Guid accountId, CancellationToken cancellationToken)`;
    - `Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken)`.
  - `SystemAdministratorResolver`: `ICacheService.GetOrSetAsync` scope `system-admin`, khoá `accountId`, TTL 60 giây.
  - `AuthorizationPolicies.SystemAdministrator = "SystemAdministrator"`: `RequireAuthenticatedUser()`, chỉ scheme JWT (như `SignedInUser`, nên khoá API nhận 401), cộng `SystemAdministratorRequirement`. Handler: `sub` là Guid và resolver trả `true` → đạt; ngược lại `Fail(new CodedAuthorizationFailureReason(this, 403, SystemAdministratorRequired))`. Không đọc `org_id`.
  - `ApplicationMessages.SystemAdministratorRequired`. Chữ: vi "Chỉ admin hệ thống làm được việc này.", en "Only a system administrator can do this."
  - `AuthResult` và `LoginResponse` thêm `bool IsSystemAdministrator` (tham số cuối). `TokenIssuer.IssueTokensAsync` là chỗ duy nhất đặt giá trị, qua resolver.

- [x] **Step 1: Viết test thất bại**

```csharp
// Infrastructure
IsSystemAdministratorAsync_RowExists_True ; IsSystemAdministratorAsync_NoRow_False
InvalidateAsync_OnOneInstance_OtherInstanceSeesRevocationAtOnce   // hai RedisCacheService, một Redis
SaveChanges_SystemAdministratorSnapshot_HasNoOrganization          // audit_logs: organization_id null, kind 'Change'
DeleteAccount_CascadesSystemAdministrator
Handle_SystemAdministrator_Succeeds
Handle_NotSystemAdministrator_FailsWithSystemAdministratorRequired
Handle_SubNotGuid_Fails
// Application
IssueTokensAsync_SystemAdministrator_SetsFlag ; IssueTokensAsync_Other_FlagFalse
// API
Login_SystemAdministrator_ResponseHasIsSystemAdministratorTrue
SwitchOrganization_Response_HasIsSystemAdministrator
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~SystemAdministrator|FullyQualifiedName~TokenIssuerTests|FullyQualifiedName~AuthControllerTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Sinh migration, kiểm `Up` chỉ tạo bảng.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(auth): add system administrators with a cached policy and the login flag (M2)`

### Task 8: API-24–API-26, danh sách tổ chức, vô hiệu và kích hoạt lại cho admin hệ thống

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Common/Models/PageRequest.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/DeactivateOrganizationRequest.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemOrganizations/ISystemOrganizationService.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemOrganizations/SystemOrganizationService.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemOrganizations/SystemOrganizationDto.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemOrganizations/ISystemOrganizationQueries.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Queries/SystemOrganizationQueries.cs`
  - `backend/src/FeedbackHub.API/Controllers/SystemOrganizationsController.cs`
- Modify: DI của Application và Infrastructure; `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/SystemOrganizations/SystemOrganizationServiceTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/SystemOrganizationQueriesTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/SystemOrganizationsControllerTests.cs`

**Interfaces:**
- Consumes: Task 1 (`IAuditTrail`, `AuditEvent.For(Organization…)`, `SystemAdministratorActor`), Task 3 (`Deactivate`, `Reactivate`), Task 7 (policy `SystemAdministrator`).
- Produces:
  - `public sealed record PageRequest([Range(1, int.MaxValue, ErrorMessage = "FieldRange")] int PageNumber = 1, [Range(1, 100, ErrorMessage = "FieldRange")] int PageSize = 50)`. Dùng chung cho mọi danh sách `PagedResult` của module 00 (API-06, API-10, API-24). Ngoài khoảng → 400, không kẹp.
  - `public sealed record DeactivateOrganizationRequest([MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Reason)`. Dùng chung cho API-05 (Task 10) và API-25.
  - `public sealed record SystemOrganizationDto(Guid Id, string Name, int MemberCount, bool IsDeactivated)`.
  - `ISystemOrganizationQueries.ListAsync(PageRequest request, CancellationToken cancellationToken) → Task<PagedResult<SystemOrganizationDto>>`: sắp `EF.Functions.Collate(o.Name, "und-x-icu")` rồi `Id`; `MemberCount` đếm thành viên `IsActive` trong cùng truy vấn.
  - `ISystemOrganizationService`:
    - `Task<PagedResult<SystemOrganizationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken)`;
    - `Task DeactivateAsync(Guid organizationId, DeactivateOrganizationRequest request, CancellationToken cancellationToken)`;
    - `Task ReactivateAsync(Guid organizationId, CancellationToken cancellationToken)`.
  - `DeactivateAsync`, thứ tự:
    1. `Reason` trống hoặc chỉ khoảng trắng → `DomainException(ApplicationMessages.DeactivationReasonRequired)` (400);
    2. trong `ExecuteInTransactionAsync`: tải tổ chức (không có → `NotFoundException(nameof(Organization), id)`);
    3. đã vô hiệu → return, không ghi gì;
    4. `ActorKind actorKind = await systemAdministratorActor.ResolveAsync(org.Id, callerId, ct)`;
    5. `Deactivate(new DeactivationParams(callerId, actorKind == ActorKind.SystemAdministrator, reason, clock.UtcNow))`;
    6. `RecordAsync(AuditEvent.For(organization, OrganizationDeactivated, actorKind, callerId))`, không tham số, không lý do (BR-84);
    7. `SaveChangesAsync`; sau commit xoá cache tổ chức.
  - `ReactivateAsync`: cùng khuôn, không bước 1; đang hoạt động → return; `Reactivate()`; mục `OrganizationReactivated`.
  - `ApplicationMessages.DeactivationReasonRequired`. Chữ: vi "Cần ghi lý do vô hiệu tổ chức.", en "A reason is required to deactivate the organization."
  - `SystemOrganizationsController`, `[Route("api/system/organizations")]`, `[Authorize(Policy = AuthorizationPolicies.SystemAdministrator)]`:
    - `GET` → 200 `PagedResult<SystemOrganizationDto>`;
    - `POST {id:guid}/deactivate` → 204;
    - `POST {id:guid}/reactivate` → 204.

- [x] **Step 1: Viết test thất bại**

```csharp
// Application
DeactivateAsync_BlankReason_Throws400AndLoadsNothing                     // AC-138, AC-139
DeactivateAsync_NotMember_RecordsSystemAdministratorAndFlagsOrganization  // DeactivatedBySystemAdministrator true (AC-368)
DeactivateAsync_ActiveMember_RecordsAccountAndFlagFalse                    // AC-375
DeactivateAsync_AlreadyDeactivated_NoSaveNoRecordNoInvalidate
DeactivateAsync_Saves_ThenInvalidatesTenantAccessAndPermissions           // Received.InOrder
ReactivateAsync_Active_NoRecord ; ReactivateAsync_Deactivated_ClearsFieldsAndRecords
DeactivateAsync_UnknownOrganization_Throws404
// Infrastructure (Testcontainers)
ListAsync_OrdersByNameIgnoringCaseAndAccents                               // "Cát Tường", "an Bình", "Bảo Minh" → an Bình, Bảo Minh, Cát Tường (AC-302)
ListAsync_MemberCount_CountsOnlyActiveMembers                              // AC-125
ListAsync_IncludesDeactivatedWithFlag ; ListAsync_Empty_ReturnsNoItems     // AC-124, AC-127
// API
[Theory] AllEndpoints_Anonymous_Return401
[Theory] AllEndpoints_OwnerNotSystemAdministrator_Return403SystemAdministratorRequired   // AC-126, AC-131
[Theory] AllEndpoints_ApiKey_Return401
List_PageSizeOver100_Returns400
Deactivate_UnknownId_Returns404
Deactivate_NoReason_Returns400ReasonRequiredAndOrganizationStaysActive
Deactivate_MemberNextRequest_Returns403Suspended                          // AC-105, AC-183 (cache)
Deactivate_AdminWorkingInOtherOrganization_EventInTargetOnly
// admin làm việc ở C, không là thành viên A → audit_logs có một Event ở A: actor_kind 'SystemAdministrator',
// new_values null (không lý do), không có Event nào ở C (AC-368)
DeactivateThenReactivate_AdminWhoIsMember_BothEventsAccount                // AC-375
Deactivate_AlreadyDeactivated_Returns204AndNoNewEvent
Reactivate_KeepsMembersAndRoles                                            // AC-106, AC-128
SystemRoutes_AreExactlyTheThreeOrganizationActions                         // không route HTTP nào ghi system_administrators (T8, AC-51, AC-52)
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~SystemOrganization"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Test `SystemRoutes_AreExactlyTheThreeOrganizationActions` đọc `EndpointDataSource` của host test: mọi route bắt đầu bằng `api/system` đúng ba cặp (phương thức, mẫu route) trên.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(system): let system administrators list, deactivate and reactivate organizations`

### Task 9: Lệnh CLI `grant-system-admin`, `revoke-system-admin`

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/SystemAdministrators/ISystemAdministratorService.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemAdministrators/SystemAdministratorService.cs`
  - `backend/src/FeedbackHub.Application/Services/SystemAdministrators/SystemAdministratorChange.cs`
  - `backend/src/FeedbackHub.API/Commands/GrantSystemAdminCommand.cs`
  - `backend/src/FeedbackHub.API/Commands/RevokeSystemAdminCommand.cs`
- Modify: `backend/src/FeedbackHub.API/Commands/CliCommands.cs` (`All`), DI của Application
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/SystemAdministrators/SystemAdministratorServiceTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Commands/SystemAdminCommandsTests.cs`

**Interfaces:**
- Consumes: Task 7 (`SystemAdministrator`, `ISystemAdministratorResolver`), Task 8 (API-24 để kiểm thu hồi qua HTTP).
- Produces:
  - `public enum SystemAdministratorChangeOutcome { Granted, AlreadyGranted, Revoked, NotGranted, AccountNotFound, EmailNotConfirmed }`.
  - `public sealed record SystemAdministratorChange(SystemAdministratorChangeOutcome Outcome, Guid? AccountId, string? Username)`.
  - `ISystemAdministratorService`:
    - `Task<SystemAdministratorChange> GrantAsync(string email, CancellationToken cancellationToken)`;
    - `Task<SystemAdministratorChange> RevokeAsync(string email, CancellationToken cancellationToken)`.
  - Tra tài khoản theo `Account.NormalizeEmail(email)` so với `Email`, không so `PendingEmail`.
  - `GrantAsync` chỉ nhận tài khoản `EmailConfirmed` (QĐ-29, vị từ duy nhất). Cả hai hàm gọi `resolver.InvalidateAsync(accountId)` sau khi lưu.
  - Lệnh: `Verb` là `grant-system-admin` / `revoke-system-admin`, `Usage` là `--email <email>`. Mã thoát:
    - 0: `Granted`, `AlreadyGranted`, `Revoked`, `NotGranted`;
    - 1: sai cú pháp;
    - 2: `AccountNotFound`;
    - 3: `EmailNotConfirmed`.
  - Dòng in ra (stdout khi thành công, stderr khi lỗi), không in email:
    - "Granted system administrator to {username} ({accountId})."
    - "{username} ({accountId}) is already a system administrator."
    - "Revoked system administrator from {username} ({accountId})."
    - "{username} ({accountId}) is not a system administrator."
    - "No account has this email."
    - "This account's email is not verified. Ask the person to verify it, then run the command again."

- [x] **Step 1: Viết test thất bại**

```csharp
// Application
GrantAsync_ConfirmedAccount_AddsRowAndInvalidatesCache
GrantAsync_UnconfirmedAccount_ReturnsEmailNotConfirmedAndAddsNothing      // AC-351
GrantAsync_EmailMatchesOnlySomeonesPendingEmail_ReturnsAccountNotFound
GrantAsync_AlreadyGranted_AddsNothing
RevokeAsync_Granted_RemovesRowAndInvalidatesCache ; RevokeAsync_NotGranted_ChangesNothing
// API (ApiFactoryFixture, PostgreSQL thật)
[Theory] Grant_BadArguments_ExitsOne                                        // không đối số, thiếu giá trị, đối số lạ
Grant_ConfirmedEmailWithOtherCaseAndSpaces_ExitsZeroAndPrintsUsernameAndId // AC-51
Grant_Unverified_ExitsThree ; Grant_Unknown_ExitsTwo
Grant_Output_DoesNotContainEmail
Revoke_ThenNextRequest_Returns403                                           // GET api/system/organizations: 200 trước, 403 sau (AC-52)
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~SystemAdministratorServiceTests|FullyQualifiedName~SystemAdminCommandsTests|FullyQualifiedName~CliCommandsTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo mẫu `MigrateCommand`, `SeedDevCommand`: lệnh chỉ đọc đối số, gọi service, in, trả mã.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(cli): grant and revoke system administrators from the server only`

### Task 10: Tổ chức của tôi và tổ chức đang làm việc (API-01–API-05)

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/Roles/RoleRefDto.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/IOrganizationContextService.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/OrganizationContextService.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/CurrentOrganizationDto.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/RenameOrganizationRequest.cs`
  - `backend/src/FeedbackHub.API/Controllers/CurrentOrganizationController.cs`
- Modify:
  - `OrganizationService.cs`, `IOrganizationService.cs` (bỏ `DeactivateAsync`), `OrganizationDto.cs`
  - `OrganizationsController.cs` (bỏ action `Deactivate`, `DELETE api/organizations/{id}`)
  - `backend/src/FeedbackHub.Infrastructure/Persistence/UnitOfWork.cs`
  - DI của Application
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Organizations/OrganizationServiceTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/Organizations/OrganizationContextServiceTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/UnitOfWorkUniqueViolationTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/OrganizationsControllerTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/CurrentOrganizationControllerTests.cs`

**Interfaces:**
- Consumes: Task 1 (`IAuditTrail`), Task 3, Task 5 (policies), Task 7 (`ISystemAdministratorResolver`), Task 8 (`DeactivateOrganizationRequest`).
- Produces:
  - `public sealed record RoleRefDto(Guid Id, string Name, SystemRoleKind? SystemKind)` với `public static RoleRefDto From(Role role)`. Phần 4, 5, 6 dùng lại. `systemKind` ra JSON dạng tên (`"Owner"`, `"Admin"`, `"Member"`, `null`) nhờ `StringEnumConverter` toàn cục của `ConfigureNewtonsoftJsonOptions` và `EnumSchemaTransformer` của OpenAPI có sẵn; không thêm thuộc tính JSON nào. Mọi enum trong DTO của phần 3–6 ra chuỗi theo cùng cách.
  - `OrganizationDto(Guid Id, string Name, string Slug, bool Status, IReadOnlyList<string> MyRoleNames, IReadOnlyList<RoleRefDto> MyRoles, IReadOnlyList<string> MyPermissionCodes)`.
  - `OrganizationService.CreateAsync` (API-02), thứ tự:
    1. tài khoản `EmailConfirmed == false` → `ForbiddenException(ApplicationMessages.EmailNotConfirmed)` (403, BR-23);
    2. `Organization.Create` (400 `OrganizationSlugInvalid`);
    3. trùng slug → 409 `OrganizationSlugAlreadyExists`, không nêu tổ chức đang dùng.

    Không ghi mục audit (AC-364).
  - `UnitOfWork.SaveChangesAsync`: vi phạm unique `IX_organizations_Slug` → `ConflictException(OrganizationSlugAlreadyExists)`, cạnh nhánh `IX_accounts_Email` có sẵn.
  - `OrganizationService.ListMineAsync` (API-01): giữ tổ chức đang vô hiệu (`status = false`); tải tổ chức bằng một truy vấn theo danh sách id, không gọi `GetByIdAsync` trong vòng lặp; điền `MyRoles`.
  - `CurrentOrganizationDto(Guid Id, string Name, string Slug, bool Status)`.
  - `RenameOrganizationRequest([MaxLength(200, ErrorMessage = "FieldMaxLength")] string? Name)`. Không `[Required]`: tên trống phải ra mã `OrganizationNameRequired` của Domain.
  - `IOrganizationContextService`, tổ chức lấy từ `ICurrentTenantProvider.OrganizationId` (cổng đã bảo đảm có):
    - `Task<CurrentOrganizationDto> GetAsync(CancellationToken cancellationToken)`;
    - `Task<CurrentOrganizationDto> RenameAsync(RenameOrganizationRequest request, CancellationToken cancellationToken)`. Thứ tự: quyền `organizations.manage` (403) và tên trống (400 `OrganizationNameRequired`) chạy trước phép so sánh. Tên sau trim giống hệt tên hiện tại (so sánh phân biệt hoa thường) → trả 200 với tổ chức không đổi, không lưu, không ghi mục `OrganizationRenamed`. Đổi mỗi chữ hoa thường vẫn là đổi tên và được ghi. Ngược lại `Update` rồi ghi `OrganizationRenamed { nameBefore, nameAfter }`, `ActorKind.Account`, người gọi;
    - `Task DeactivateAsync(DeactivateOrganizationRequest request, CancellationToken cancellationToken)`. Người gọi là admin hệ thống (resolver) mà lý do trống → 400 `DeactivationReasonRequired` (BR-54, AC-147). Ngược lại `Deactivate(new DeactivationParams(callerId, false, reason, now))`, ghi `OrganizationDeactivated` với `ActorKind.Account`, lưu, xoá cache. Không gọi `SystemAdministratorActor`: người gọi đã qua cổng thành viên.
  - `CurrentOrganizationController`, `[Route("api/organization")]`:
    - `GET` → `ActiveOrganizationMember`, 200 `CurrentOrganizationDto`;
    - `PATCH` → `Permissions.OrganizationManage`, 200 `CurrentOrganizationDto`;
    - `POST deactivate` → `Permissions.OrganizationManage`, 204.
  - API-04 bỏ qua mọi trường ngoài `name`, kể cả `slug`, như mọi trường lạ: `RenameOrganizationRequest` không có trường `slug`. Thân chỉ có `slug` thì `Name` rỗng → 400 `OrganizationNameRequired`. Thân có cả `name` lẫn `slug` thì đổi tên, `slug` không đổi.

- [x] **Step 1: Viết test thất bại**

```csharp
// Application
CreateAsync_UnconfirmedEmail_Throws403AndAddsNothing                       // AC-351
CreateAsync_InvalidSlug_Throws400 ; CreateAsync_ExistingSlug_Throws409
CreateAsync_RecordsNoAuditEvent                                            // IAuditTrail DidNotReceive (AC-364)
ListMineAsync_IncludesDeactivatedWithStatusFalseAndMyRoles                 // AC-73
RenameAsync_NewName_UpdatesAndRecordsRenamedWithBothNames
RenameAsync_SameNameAfterTrim_NoSaveNoRecord
RenameAsync_CaseOnlyChange_UpdatesAndRecordsRenamed                        // đổi hoa thường là đổi tên, có ghi
RenameAsync_BlankName_Throws400BeforeComparison                            // kiểm tên trống chạy trước phép so sánh
DeactivateAsync_SystemAdministratorMemberWithoutReason_Throws400NothingSaved   // AC-147
DeactivateAsync_SystemAdministratorMemberWithReason_RecordsAccountActor        // AC-148, AC-369
DeactivateAsync_ManagerWithoutReason_DeactivatesAndRecords                     // AC-141, AC-146
// Infrastructure (Testcontainers)
SaveChanges_DuplicateSlugFromTwoContexts_SecondThrowsConflict              // Review Focus 5
// API
[Theory] CurrentOrganization_Anonymous_Returns401                            // GET, PATCH, POST deactivate
Get_PersonalContext_Returns403AccessDenied                                   // AC-299
Get_SuspendedMember_Returns403SuspendedWithName                              // AC-301
Rename_WithoutOrganizationsManage_Returns403AndNameUnchanged                 // AC-186
Rename_NewName_Returns200AndListMineShowsIt_SlugUnchanged                    // AC-185, AC-225
Rename_BlankName_Returns400OrganizationNameRequired                          // AC-187
Rename_SameNameAfterTrim_Returns200WithoutSaveOrAudit                        // AuditLog không có thêm dòng OrganizationRenamed
Rename_CaseOnlyChange_Returns200AndLogsRenamed                               // nameBefore, nameAfter khác nhau chỉ ở hoa thường
Rename_SameNameWithoutOrganizationsManage_Returns403                         // 403 chạy trước phép so sánh
Rename_BodyWithSlugOnly_Returns400OrganizationNameRequired                   // AC-226: thân chỉ có slug; slug không đổi
Rename_BodyWithNameAndSlug_RenamesAndSlugUnchanged                           // AC-226: slug bị bỏ qua; slug không đổi ở cả hai ca
Deactivate_Manager_Returns204AndNextRequest403Suspended                      // AC-146, AC-183
Deactivate_SystemAdministratorMemberWithoutReason_Returns400StillActive      // AC-147
Deactivate_EventHasAccountActorAndNoReason                                   // AC-369 (API-05), AC-367
Create_Unconfirmed_Returns403EmailNotConfirmed                               // AC-351
[Theory] Create_InvalidSlug_Returns400SlugInvalid                            // AC-283, AC-284
Create_SlugUsedBySuspendedOrganization_Returns409WithoutItsName              // AC-286, AC-330
Create_UppercaseSlug_CreatedLowercase                                        // AC-282, AC-329
Create_ThenAuditLog_HasNoEventRow                                            // AC-364
ListMine_IncludesDeactivatedOrganization                                     // AC-73
ListMine_MyRolesCarrySystemKindAsName                                        // "myRoles":[{"id":…,"name":"Owner","systemKind":"Owner"}]
DeleteOrganization_OldRoute_Returns404                                       // route đã bỏ (QĐ-06)
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~OrganizationServiceTests|FullyQualifiedName~OrganizationContextServiceTests|FullyQualifiedName~UnitOfWorkUniqueViolationTests|FullyQualifiedName~OrganizationsControllerTests|FullyQualifiedName~CurrentOrganizationControllerTests"`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Xoá `OrganizationService.DeactivateAsync` cùng test của nó. Giữ `GetMembersAsync` và các route `{id}` thành viên, vai trò: phần 4 thay chúng.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [x] **Step 5: Commit** `feat(organizations): read, rename and deactivate the working organization`

### Task 11: Contract, frontend tối thiểu, feature map, quyết định, gate

**Files:**
- Modify:
  - `shared/openapi/openapi.json` (export)
  - `frontend/src/stores/organizations.ts` (bỏ `deactivate`, vì `organizationsDeactivate` không còn)
  - `frontend/src/components/OrganizationMembersDialog.vue` (bỏ nút và hàm vô hiệu; phần 8 thay cả component)
  - `docs/feature-map.md`
  - `.claude/decisions.md`

**Interfaces:**
- Consumes: Task 1–10.
- Produces:
  - `docs/feature-map.md`, mục mới `## Tổ chức đang làm việc và admin hệ thống`:
    - PRD: FR-42, FR-44, FR-46, FR-53, FR-63, FR-38;
    - vào bằng: chưa có màn hình (phần 8); CLI `grant-system-admin --email`, `revoke-system-admin --email`;
    - backend: các route API-01–API-05, API-24–API-26, policy, `IAuditTrail`;
    - test: tên các tệp test của phần này.
  - `.claude/decisions.md`, mục mới ở đầu, tiếng Anh, mỗi mục dưới khoảng 80 chữ:
    - QĐ-07: một requirement cổng tổ chức theo thứ tự BR-77, mã riêng cho từng lý do, `GetAccessAsync` ba trạng thái;
    - QĐ-13 và phần lõi của QĐ-36: mục nhật ký ngữ nghĩa qua `IAuditTrail`, tổ chức và người làm tường minh, danh mục đóng; loại bỏ phương án lấy `organization_id` từ JWT;
    - QĐ-15: admin hệ thống trong bảng riêng, chỉ CLI ghi, policy tra có cache;
    - QĐ-31: refresh mất quyền vào tổ chức thì phát token `org_id = null`.

- [x] **Step 1: Export contract và sinh lại client**

Run:
- `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
- `bun run --cwd frontend codegen`

Expected: `openapi.json` có `api/organization`, `api/organization/deactivate`, ba route `api/system/organizations`, `isSystemAdministrator`, `myRoles`; không còn `DELETE api/organizations/{id}`.

- [x] **Step 2: Sửa frontend cho biên dịch được**, rồi chạy:
- `bun run --cwd frontend build-only`
- `bun run --cwd frontend type-check`
- `bun run --cwd frontend test:run`

Expected: PASS.

- [x] **Step 3: Ghi feature map và quyết định** theo Interfaces.

- [x] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [x] **Step 5: Commit** `chore(contract): export the organization gate and system admin contract`

### Lệch nhỏ khai trong PR

- `audit_logs.action` nới từ 20 lên 50 ký tự trong M9 (Task 2).
- M2 tách thành hai migration: phần 3 Task 7 (bảng admin hệ thống) và phần 5 Task 2 (`organization_invitations`).
- Mã mới `ResourceNotFound` cho 404 của cổng tổ chức (Task 5).

---

## Câu hỏi mở

- **Đã chốt (SA, 2026-10-23):** Đổi tên tổ chức thành đúng tên hiện tại (API-04) trả 200 với tổ chức không đổi, không lưu và không ghi mục `OrganizationRenamed`. Kiểm quyền (403) và tên trống (400) chạy trước; "cùng tên" là giống hệt sau trim, phân biệt hoa thường, nên đổi mỗi chữ hoa thường vẫn là đổi tên và được ghi (Task 10).
- **Đã chốt (SA, 2026-10-23):** Thân API-04 có cả `name` lẫn `slug` thì đổi tên và bỏ qua `slug` như mọi trường lạ; thân chỉ có `slug` thì 400 `OrganizationNameRequired`. Test AC-226 kiểm `slug` không đổi ở cả hai ca (Task 10).

# Module 00 — Phần 4: Vai trò và thành viên — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23). **Push/PR:** người dùng cho phép (2026-10-23): sau khi phần 8 xong và `scripts/test-gate.sh` qua, push `feature/module-00` và mở một PR vào `dev` cho cả module 00; không bao giờ vào `main`.

**Goal:** Quản lý thành viên và vai trò của tổ chức đang làm việc, dưới `api/organization/...`, trong giới hạn chống leo thang quyền (BR-58):
- danh sách thành viên (API-06), đổi vai trò (API-07), gỡ (API-08), tự rời (API-09);
- danh sách chọn vai trò (API-14), phần Vai trò (API-15), tạo, sửa, xoá vai trò tự tạo (API-16–API-18);
- quyền Quản trị FeedbackHub (`feedbackhub.admin`), tên vai trò không trùng không phân biệt hoa thường (BR-79);
- bỏ mọi route `api/organizations/{id}/...` và việc thêm thẳng thành viên (FR-59);
- mỗi thao tác ghi đúng một mục nhật ký qua `IAuditTrail` của phần 3.

**Architecture:**
- **Domain:** `Role` và `OrganizationMember` cài `IHasOrganization`; `Role` chuẩn hoá tên (trim, NFC).
- **Application:**
  - `RoleGrantPolicy` (hàm thuần) là chỗ duy nhất tính BR-58 điểm 1–3. `RoleGrantReader` đọc đầu vào của nó thẳng từ cơ sở dữ liệu, không qua `IPermissionResolver`.
  - Mọi thao tác ghi chạy trong `ExecuteInTransactionAsync` và bắt đầu bằng `IOrganizationLock.AcquireAsync` (khoá dòng tổ chức).
  - `MemberService` thay `OrganizationMembershipService`. `RoleService` viết lại, lấy tổ chức từ `org_id`.
  - Hai cổng đọc danh sách (`IOrganizationMemberQueries`, `IRoleQueries`) để sắp theo collation ICU trong SQL.
- **Infrastructure:** `OrganizationLock` (`SELECT … FOR UPDATE`), hai lớp truy vấn, migration M3–M5, ánh xạ vi phạm chỉ mục tên vai trò về 409.
- **API:** các route mới trên `CurrentOrganizationController`. Mọi mã quyền qua cổng tổ chức của phần 3; hai handler theo route `{id}` bị xoá.
- **Frontend:** chỉ sửa tối thiểu để contract mới biên dịch được (xoá hai dialog cũ dùng route `{id}`). Màn hình mới thuộc phần 8.

**Tech Stack:** .NET 10 (xUnit, NSubstitute, Testcontainers PostgreSQL), EF Core + Npgsql, Vue 3 + orval.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0: §2.1, §3.2, §3.3, §4.1 (API-06–API-09), §4.2 (API-14), §4.3, §4.6 (`OrganizationMemberDto`, `RoleOptionDto`, `RoleDto`), §4.7, §4.8, §5.1, §5.2, §5.3, §5.4, §6.4, §7.2 T4, T9, §9, §10.1 (M3, M4, M5), §11, §12 QĐ-05, QĐ-06, QĐ-08, QĐ-14, QĐ-16, QĐ-17, QĐ-18, QĐ-20, QĐ-25, QĐ-27.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: FR-23, FR-29, FR-52, FR-59, FR-60, FR-62, FR-64, FR-66; BR-30, BR-58, BR-60, BR-74, BR-79, BR-80, BR-84.
- Phần 3 (`2026-10-23-module-00-phase-3-organization-gate.md`): `IAuditTrail`, `AuditEvent.For`, `AuditRoleRef`, `RoleRefDto`, `PageRequest`, policy `ActiveOrganizationMemberOrNotFound`, `CodedAuthorizationFailureReason`.

## Lộ trình module 00

Module 00 phát hành một lần, không có cờ tính năng (TECH00 §10.2). Mọi phần làm trên nhánh `feature/module-00`, và chỉ mở một PR vào `dev` sau phần cuối. Mỗi phần là một kế hoạch riêng, người dùng duyệt trước khi làm.

Bảng của phần 1, cập nhật theo quyết định của người điều phối 2026-10-23: lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục) chuyển từ phần 6 lên đầu phần 3, để mọi service dựng ở phần 3–5 tự ghi mục của mình. Hàng 5 ghi thêm API-23 (`accountCreated`), phần 5 làm cùng lời mời. Bảng này giống hệt nhau ở mọi kế hoạch phần 3–8.

| Phần | Nội dung | TECH00 |
|---|---|---|
| 1 | Sửa bảo mật B1, B2; M7, M8; `TokenIssuer` kiểm `Status`; audit loại cột nhạy cảm; `SmtpEmailSender` | §3.8, §4.9, §6.6, §6.7, §7.3 |
| 2 | Sẵn sàng nhiều máy: giới hạn tần suất qua Redis, `ProductionTopologyValidator`, `EmailSettings:SecureSocket`, Data Protection | §4.8, §10.3 |
| 3 | Lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục 13 loại); cổng tổ chức, admin hệ thống, phiên: `GetAccessAsync`, `OrganizationContextAuthorizationHandler`, refresh `org_id = null`, `isSystemAdministrator`, CLI `grant`/`revoke-system-admin`, `SystemOrganizationService`, API-01–API-05, M1, M2 (`system_administrators`) | §3.1, §3.5, §3.7 (lõi), §4.1, §4.5, §5.1, §5.2, §6.1, §6.5 |
| **4 (tệp này)** | Vai trò và thành viên: `RoleGrantPolicy`, `IOrganizationLock`, `MemberService`, `RoleService`, M3–M5 | §3.2, §3.3, §4.3, §5.3, §5.4, §6.4 |
| 5 | Lời mời: M2 (`organization_invitations`), API-10–API-13, API-20–API-23, email mời | §3.4, §4.2, §4.4, §6.2, §6.3 |
| 6 | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Nhánh và commit**
  - Làm trên nhánh `feature/module-00`, sau phần 3.
  - Conventional Commits, subject ≤ 100 ký tự, stage từng tệp theo đường dẫn.
  - Mỗi message kết thúc bằng đúng một dòng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    ```
- **C#:** theo `code-conventions.md`: không `var`, có `CancellationToken`, entity chỉ tạo qua `Create(XxxParams)`, lỗi Domain là `DomainException(DomainMessages.X)`, lỗi Application dùng key trong `ApplicationMessages`.
- **Mã lỗi:** mỗi key mới có chữ ở cả `Messages.resx` (vi) và `Messages.en.resx` (en).
- **Thứ tự kiểm trong service (TECH00 §5.1 bước 5–7, D2):**
  1. đầu vào sai → 400;
  2. đối tượng không thuộc `org_id` → 404 giống hệt không tồn tại;
  3. vai trò hệ thống bị sửa, xoá → 403 `OrganizationSystemRoleImmutable` (QĐ-27);
  4. BR-58 → 403;
  5. điều kiện còn lại → 409.
- **BR-58:**
  - Lỗi là `OwnerOnlyOperation` khi nhánh Chủ sở hữu không đạt (kể cả khi điều kiện quyền cũng không đạt); ngược lại `InsufficientRolePermissions`.
  - Khi ghi, `P(caller)`, `isOwner(caller)`, `P(r)` và người giữ vai trò đọc từ cơ sở dữ liệu **sau** `IOrganizationLock.AcquireAsync`, trong cùng giao dịch.
  - Cờ trên DTO gọi cùng `RoleGrantPolicy`, đầu vào đọc trong truy vấn danh sách, không khoá.
- **Đếm người giữ vai trò** chỉ tính thành viên `IsActive = true` (BR-60).
- **Nhật ký kiểm toán:**
  - mỗi thao tác thành công ghi đúng một mục qua `IAuditTrail.RecordAsync`, trước `SaveChangesAsync` của cùng giao dịch;
  - `ActorKind.Account`, người làm là người gọi;
  - `OrganizationId` lấy qua `AuditEvent.For(member | role, …)`;
  - thao tác bị từ chối (kể cả 409 `CannotRemoveSelf`, AC-337) thì không ghi.
- **Cache:** sau commit, đổi vai trò, gỡ, rời gọi `ITenantAccessService.InvalidateMemberAsync` và `IPermissionResolver.InvalidateMemberAsync` của người bị đổi. Sửa, xoá vai trò gọi `IPermissionResolver.InvalidateOrganizationAsync`.
- **Thứ tự vai trò hiển thị** (cột vai trò của một dòng, `lockRole`, danh sách vai trò trong mục nhật ký): `Owner`, `Admin`, `Member`, rồi vai trò tự tạo theo tên với cùng `COLLATE "und-x-icu"` mà danh sách vai trò (API-15) dùng, rồi `Id`. Sắp ngay trong truy vấn tải vai trò (`Collate(Name, "und-x-icu")`); `lockRole` và danh sách vai trò trong mục nhật ký lấy đúng thứ tự đó. Không dùng bộ so sánh văn hoá trong bộ nhớ (không `StringComparer`).
- **Danh sách:** `PageRequest` của phần 3 (mặc định 50, tối đa 100). Sắp theo `COLLATE "und-x-icu"` rồi `Id` (QĐ-20).
- **Test:** theo `testing.md`: `Method_Scenario_Expected`, xUnit `Assert`; Application dùng NSubstitute với `Received`/`DidNotReceive`; Infrastructure dùng PostgreSQL thật qua Testcontainers; API qua HTTP, mọi endpoint mới có test 401 và 403. Test nêu mã AC trong comment.
- **Contract:** một lần export OpenAPI ở Task 10. Lệnh `dotnet` dùng `-m:1`.
- **Migration:** thứ tự và quy tắc id ở Global Constraints của phần 3 (§ Migration). Ba migration của phần này nằm sau M2 `AddSystemAdministrators` của phần 3 và trước M2 `AddOrganizationInvitations` của phần 5: M3, rồi M4, rồi M5, mỗi id lớn hơn id trước một giây trở lên, không trùng timestamp nào.
- **Bí mật:** không đọc, không sửa `appsettings.json` hay `appsettings.*.json`.

## Review Focus

1. **Tên vai trò trùng tên hệ thống viết khác** (`"OWNER"`, `" admin "`, `"Chủ sở hữu"` dạng tổ hợp NFD): 409 `RoleNameReservedForSystemRole`, không lưu. Test ở Task 1 và Task 9.
2. **Hai Chủ sở hữu duy nhất gỡ nhau cùng lúc**: một bên thành công, bên kia 409 `OrganizationCannotRemoveLastOwner`; tổ chức luôn còn một Chủ sở hữu. Test ở Task 3 (khoá) và Task 7.
3. **`roleIds` có id trùng, id của tổ chức khác, hoặc rỗng**: id trùng coi như một; id ngoài tổ chức → 404, không ghi gì; rỗng → 400. Test ở Task 7.
4. **Quyền của người gọi vừa bị bỏ mà cache còn cũ**: BR-58 vẫn từ chối, vì đọc từ cơ sở dữ liệu. Test ở Task 7 và Task 9.
5. **Lưu đổi vai trò hoặc sửa vai trò mà không đổi gì**: 204/200, không ghi mục nhật ký, không xoá cache. Test ở Task 7 và Task 9.

---

### Task 1: Domain và danh mục quyền: `IHasOrganization`, tên vai trò NFC, `feedbackhub.admin`, tên vai trò hệ thống

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Domain/Entities/Role.cs`
  - `backend/src/FeedbackHub.Domain/Entities/OrganizationMember.cs`
  - `backend/src/FeedbackHub.Application/Common/Authorization/Permissions.cs`
  - `backend/src/FeedbackHub.Application/Common/Authorization/SystemRoleDefaults.cs`
  - `frontend/src/lib/permissions.ts`, `frontend/src/lib/permission-label.ts`
  - `frontend/src/locales/vi.ts`, `frontend/src/locales/en.ts` (cả `LocaleSchema`)
- Create: `backend/src/FeedbackHub.Application/Services/Roles/SystemRoleDisplayNames.cs`
- Test:
  - `backend/tests/FeedbackHub.Domain.Tests/Entities/RoleTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/SystemRoleDisplayNamesTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Common/Authorization/SystemRoleDefaultsTests.cs`

**Interfaces:**
- Produces:
  - `Role : BaseEntity<Guid>, IHasOrganization`. `Role.Update` trim rồi `Normalize(NormalizationForm.FormC)` tên; trống → `RoleNameRequired` (có sẵn).
  - `OrganizationMember : BaseEntity<Guid>, IHasOrganization`.
  - `Permissions.FeedbackHubAdmin = "feedbackhub.admin"`, thêm vào `Permissions.All`.
  - `SystemRoleDefaults.PermissionsByKind[Admin] = [OrganizationMembersManage, FeedbackHubAdmin]`. Owner vẫn là `Permissions.All` tính động.
  - `public static class SystemRoleDisplayNames`:
    - `string Get(SystemRoleKind kind, string language)`, `language` là `"vi"` hoặc `"en"`: Owner → "Chủ sở hữu" / "Owner"; Admin → "Admin" / "Admin"; Member → "Member" / "Member";
    - `bool IsReserved(string name)`: trim, NFC, `ToLowerInvariant()`, rồi so với tập `{ "owner", "admin", "member", "chủ sở hữu" }` (BR-79, AC-291).
  - Locale:
    - `roles.systemKind.Owner|Admin|Member`, vi và en đúng như `SystemRoleDisplayNames`. Phần 7 (`roleDisplayName`) và phần 8 dùng đúng các khoá này để hiện vai trò hệ thống theo `systemKind`, không thêm khoá thứ hai;
    - `organizations.permissionFeedbackhubAdmin`: vi "Quản trị FeedbackHub", en "FeedbackHub administration";
    - `lib/permissions.ts` thêm `FeedbackHubAdmin: 'feedbackhub.admin'`, `permission-label.ts` ánh xạ nó.

- [ ] **Step 1: Viết test thất bại**

```csharp
// Domain
Update_NameWithSpacesAndDecomposedUnicode_StoresTrimmedNfc     // "  Chủ sở hữu " → "Chủ sở hữu" (FormC)
Update_BlankName_Throws                                         // AC-287
Role_And_OrganizationMember_ImplementIHasOrganization
// Application
[Theory] IsReserved_SystemNamesAnyCaseOrForm_True               // "OWNER", " admin ", "Member", "chủ sở hữu", "CHỦ SỞ HỮU", dạng NFD
IsReserved_OtherName_False                                      // "Tester", "Owner team"
Names_MatchFrontendLocales
// đọc frontend/src/locales/vi.ts và en.ts (tìm thư mục gốc repo bằng cách đi lên từ AppContext.BaseDirectory
// đến khi thấy AGENTS.md); khối systemKind có đúng ba dòng Owner, Admin, Member với chữ của Get(kind, lang)
AdminDefaults_ContainMembersManageAndFeedbackHubAdmin            // FR-23, BR-60
PermissionsAll_ContainsFeedbackHubAdmin
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~RoleTests|FullyQualifiedName~SystemRoleDisplayNamesTests|FullyQualifiedName~SystemRoleDefaultsTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi `bun run --cwd frontend build-only`, `bun run --cwd frontend type-check`, `bun run --cwd frontend test:run`.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(roles): add the FeedbackHub admin permission and normalize role names`

### Task 2: Migration M3–M5 và 409 cho tên vai trò trùng đồng thời

**Files:**
- Create (`backend/src/FeedbackHub.Infrastructure/Migrations/`):
  - `<timestamp>_RenameDuplicateRoleNames.cs` (M3)
  - `<timestamp>_RemoveRolesOfInactiveMembers.cs` (M4)
  - `<timestamp>_GrantFeedbackHubAdminToAdminRoles.cs` (M5)
- Modify:
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/UnitOfWork.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/Migrations/RoleMigrationsTests.cs`

**Interfaces:**
- Produces:
  - Mỗi migration có `public const string UpSql`, theo mẫu `20261023120000_ReverifyMicrosoftOnlyAccounts.cs`. `Up` gọi `migrationBuilder.Sql(UpSql)`.
  - M3 đổi model (bỏ `HasIndex(OrganizationId, Name)`), nên sinh bằng `dotnet ef migrations add RenameDuplicateRoleNames` sau khi sửa `RoleConfiguration`, để `AppDbContextModelSnapshot` khớp model. Thiếu bước này, `migrations add` kế tiếp (M2 `AddOrganizationInvitations` của phần 5) sinh lại `DropIndex` cho chỉ mục M3 đã bỏ và lỗi khi chạy. Trong tệp sinh ra, thay `DropIndex` của EF bằng `migrationBuilder.Sql(UpSql)` (UpSql tự bỏ chỉ mục cũ ở bước 5), giữ lại `CreateIndex` nào EF sinh thêm (ví dụ `IX_roles_OrganizationId` cho khoá ngoại). M4, M5 chỉ đổi dữ liệu, viết tay như M8 với `[Migration("<id>")]` đặt tay.
  - **M3** (TECH00 §10.1). Khối `DO $$ … $$`:
    1. Khoá so sánh của một tên là `lower(normalize(btrim("Name"), NFC))`.
    2. Duyệt vai trò tự tạo (`"SystemRoleKind" IS NULL`) cần đổi tên, theo `"OrganizationId"`, `"CreatedAt"`, `"Id"`. Một vai trò cần đổi tên khi:
       - khoá của nó thuộc `{owner, admin, member, chủ sở hữu}`; hoặc
       - trong cùng tổ chức đã có một vai trò khác đứng trước nó có cùng khoá. Vai trò hệ thống luôn đứng trước và luôn giữ tên.
    3. Với mỗi vai trò cần đổi, thử `n = 2, 3, …`:
       - ứng viên là `left(normalize(btrim("Name"), NFC), 100 - length(' (n)')) || ' (n)'`;
       - lấy ứng viên đầu tiên mà khoá không trùng khoá nào trong tổ chức và không thuộc tập tên hệ thống;
       - cập nhật, rồi `RAISE NOTICE 'M3 renamed role % in organization %', "Id", "OrganizationId"`. Không in tên.
    4. Cuối cùng `UPDATE roles SET "Name" = normalize(btrim("Name"), NFC) WHERE "Name" <> normalize(btrim("Name"), NFC)`.
    5. `DROP INDEX "IX_roles_OrganizationId_Name"`, rồi `CREATE UNIQUE INDEX "IX_roles_OrganizationId_lower_Name" ON roles ("OrganizationId", lower("Name"))`.

    `Down` tạo lại chỉ mục cũ và bỏ chỉ mục mới. Không hoàn tên.
  - **M4:** `DELETE FROM organization_member_roles r USING organization_members m WHERE r."OrganizationMemberId" = m."Id" AND m."IsActive" = false`. `Down` trống.
  - **M5:** thêm `role_permissions ("Id", "RoleId", "PermissionCode", "CreatedAt")` = `(gen_random_uuid(), r."Id", 'feedbackhub.admin', now())` cho mọi `roles` có `"SystemRoleKind" = 'Admin'` chưa có mã đó (idempotent). `Down` trống.
  - Trước khi chép SQL, kiểm tên cột thật trong `AppDbContextModelSnapshot.cs`.
  - `RoleConfiguration` bỏ `HasIndex(OrganizationId, Name)`. Chỉ mục biểu thức chỉ sống trong SQL của M3, vì EF không mô tả được `lower("Name")`. Ghi một dòng comment chỉ tới M3.
  - `UnitOfWork.SaveChangesAsync`: vi phạm unique `IX_roles_OrganizationId_lower_Name` → `ConflictException(OrganizationRoleNameAlreadyExists)`, cạnh hai nhánh có sẵn.

- [ ] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
M3_DuplicatesIgnoringCaseAndNfc_RenamedWithSuffix
// org X: "Tester", "tester", "TESTER" → "Tester", "tester (2)", "TESTER (3)"; org Y "Tester" không đổi
M3_CustomRoleNamedLikeSystemRole_Renamed           // vai trò tự tạo "owner", "Chủ sở hữu" dạng NFD → hậu tố; vai trò hệ thống giữ tên
M3_SuffixSkipsTakenName                            // đã có "Tester (2)" → bản trùng thành "Tester (3)"
M3_NameAt100Chars_SuffixFitsIn100
M3_Notice_ContainsIdsNotNames
M3_AfterRun_LowerNameIndexRejectsCaseDuplicate
M4_DeletesRolesOnlyOfInactiveMembers
M5_AddsFeedbackHubAdminToEveryAdminRole_RunTwiceAddsNothing
SaveChanges_ConcurrentSameNameDifferentCase_SecondThrowsConflict    // hai context (AC-290, AC-326)
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~RoleMigrationsTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** ba migration và nhánh ánh xạ. Test chạy `UpSql` trên dữ liệu gieo sẵn như test M8 hiện có. Test M3 phải đưa schema về đúng trước M3 rồi mới gieo dữ liệu trùng tên và chạy `UpSql` của M3: hoặc migrate tới migration đứng ngay trước M3, hoặc tạo lại chỉ mục cũ `IX_roles_OrganizationId_Name` và bỏ chỉ mục `lower("Name")` mới. Nếu không, trên schema đã migrate đủ `DROP INDEX` lỗi và chỉ mục mới chặn dữ liệu gieo trùng tên.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(persistence): make role names unique ignoring case and clean up member roles (M3-M5)`

### Task 3: `IOrganizationLock`

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/OrganizationLocking/IOrganizationLock.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/OrganizationLock.cs`
- Modify: `backend/src/FeedbackHub.Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/OrganizationLockTests.cs`

**Interfaces:**
- Produces:
  - `public interface IOrganizationLock { Task AcquireAsync(Guid organizationId, CancellationToken cancellationToken); }`. Namespace riêng `Services/OrganizationLocking` vì thành viên, vai trò, lời mời (phần 5) cùng dùng, và `Organizations` đã phụ thuộc `Roles`: để trong `Organizations` sẽ thành vòng phụ thuộc namespace.
  - `internal sealed class OrganizationLock(AppDbContext dbContext) : IOrganizationLock`:
    - chạy `SELECT 1 FROM organizations WHERE "Id" = {organizationId} FOR UPDATE` qua `ExecuteSqlInterpolatedAsync`;
    - gọi ngoài giao dịch (`dbContext.Database.CurrentTransaction is null`) → `InvalidOperationException`, vì khoá nhả ngay.

- [ ] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
AcquireAsync_SecondTransactionWaitsUntilFirstCommits
// T1 khoá org; T2 AcquireAsync chưa xong sau 500 ms; T1 commit → T2 xong
AcquireAsync_DifferentOrganizations_DoNotBlock
AcquireAsync_OutsideTransaction_Throws
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~OrganizationLockTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(organizations): lock the organization row for membership and role changes`

### Task 4: `RoleGrantPolicy` và `RoleGrantReader`

**Files:**
- Create (`backend/src/FeedbackHub.Application/Services/Roles/`):
  - `RoleGrantPolicy.cs`
  - `RoleGrantReader.cs`
- Modify: `backend/src/FeedbackHub.Application/DependencyInjection.cs`, `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/RoleGrantPolicyTests.cs`
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/RoleGrantReaderTests.cs`

**Interfaces:**
- Consumes: Task 1 (`Permissions.All` có `feedbackhub.admin`).
- Produces:
  ```csharp
  public sealed record RoleGrantCaller(Guid AccountId, bool IsOwner, IReadOnlySet<string> Permissions);
  public sealed record RoleGrantRole(Guid Id, string Name, SystemRoleKind? SystemKind, IReadOnlySet<string> Permissions);
  public enum RoleLockReason { OwnerOnly = 1, InsufficientRolePermissions = 2 }
  public sealed record RoleGrantDenial(RoleLockReason Reason, RoleGrantRole? Role);

  public static class RoleGrantPolicy
  {
      public static bool CanGrant(RoleGrantCaller caller, RoleGrantRole role);
      public static bool CanTakeAway(RoleGrantCaller caller, RoleGrantRole role);
      public static RoleGrantDenial? CheckTakeAwayAll(RoleGrantCaller caller, IReadOnlyList<RoleGrantRole> roles);
      public static RoleGrantDenial? CheckGrantAll(RoleGrantCaller caller, IReadOnlyList<RoleGrantRole> roles);
      public static bool HasAllPermissions(RoleGrantCaller caller, IEnumerable<string> permissionCodes);
      public static string ErrorCode(RoleGrantDenial denial);
  }
  ```
  - `CanGrant(r)` = `r.SystemKind == Owner ? caller.IsOwner : r.Permissions ⊆ caller.Permissions`. `CanTakeAway` cùng công thức (TECH00 §5.3).
  - `CheckTakeAwayAll`, `CheckGrantAll` theo thứ tự hiển thị của `roles`:
    - có vai trò Owner mà người gọi không phải Owner → `OwnerOnly` với `Role = null`, kể cả khi vai trò khác cũng thiếu quyền (AC-341);
    - ngược lại, vai trò đầu tiên không đạt → `InsufficientRolePermissions` với `Role` = vai trò đó (AC-338);
    - đạt hết → `null`.
  - `ErrorCode`: `OwnerOnly` → `ApplicationMessages.OwnerOnlyOperation`, còn lại → `ApplicationMessages.InsufficientRolePermissions`.
  - `internal sealed class RoleGrantReader(IUnitOfWork unitOfWork)`, chỉ đọc qua `IUnitOfWork`, không nhận `IPermissionResolver`:
    - `Task<RoleGrantCaller> LoadCallerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken)`. Không phải thành viên đang hoạt động → `IsOwner = false`, tập rỗng; Owner → `Permissions.All`;
    - `Task<IReadOnlyList<RoleGrantRole>> LoadRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)`. Chỉ vai trò của tổ chức đó; Owner mang `Permissions.All`;
    - `Task<IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>>> LoadRolesOfMembersAsync(IReadOnlyCollection<Guid> organizationMemberIds, CancellationToken cancellationToken)`. Khoá là id dòng thành viên.

    Mọi danh sách trả theo thứ tự hiển thị ở Global Constraints. Cả ba hàm `Load…` sắp trong truy vấn (`Owner`, `Admin`, `Member`, rồi `Collate(Name, "und-x-icu")`, rồi `Id`), không sắp lại trong bộ nhớ bằng bộ so sánh văn hoá.
  - Key mới. Chữ:
    - `OwnerOnlyOperation`: vi "Chỉ Chủ sở hữu làm được thao tác này.", en "Only an Owner can do this."
    - `InsufficientRolePermissions`: vi "Bạn không có đủ quyền của vai trò này.", en "You don't have every permission of this role."

- [ ] **Step 1: Viết test thất bại**

```csharp
// RoleGrantPolicyTests
CanGrant_OwnerRole_OnlyOwnerCaller                                // AC-155, AC-157
CanGrant_SubsetOfCallerPermissions_True ; CanGrant_ExtraPermission_False   // AC-153, AC-156
CanTakeAway_OwnerRoleByCallerWithAllPermissionsButNotOwner_False   // D15, AC-340
CheckTakeAwayAll_OwnerAndLackingRole_ReturnsOwnerOnlyNotInsufficient // AC-341
CheckTakeAwayAll_TwoLackingRoles_ReturnsFirstInDisplayOrder        // AC-338
CheckGrantAll_AllGrantable_ReturnsNull
ErrorCode_MapsReasonToMessageKey
HasAllPermissions_SubsetTrue_OtherFalse
// RoleGrantReaderTests
LoadCallerAsync_Owner_HasAllPermissions ; LoadCallerAsync_InactiveMember_Empty
LoadCallerAsync_UnionOfRolePermissions
LoadRolesAsync_IgnoresRolesOfOtherOrganization
LoadRolesOfMembersAsync_ReturnsDisplayOrder                        // Owner, Admin, Member, rồi tự tạo theo tên
RoleGrantReader_Constructor_DoesNotTakePermissionResolver
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~RoleGrant"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(roles): compute privilege-escalation rules in one RoleGrantPolicy`

### Task 5: Bỏ route `api/organizations/{id}/...`, mọi mã quyền qua cổng tổ chức

**Files:**
- Modify:
  - `backend/src/FeedbackHub.API/Controllers/OrganizationsController.cs` (chỉ còn `GET`, `POST`)
  - `backend/src/FeedbackHub.Application/Services/Organizations/OrganizationService.cs`, `IOrganizationService.cs` (bỏ `GetMembersAsync`)
  - `backend/src/FeedbackHub.Application/Services/Roles/RoleService.cs`, `IRoleService.cs` (chỉ giữ `SeedSystemRolesAsync`; Task 8–9 thêm hàm mới)
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/OrganizationPermissionPolicyProvider.cs`
  - `backend/src/FeedbackHub.Infrastructure/Services/Auth/AuthExtensions.cs`
  - `backend/src/FeedbackHub.Application/Common/Authorization/AuthorizationPolicies.cs` (bỏ `OrganizationMember`)
  - `backend/src/FeedbackHub.Application/Common/Authorization/Permissions.cs` (bỏ `ActiveOrganizationScoped`)
  - `backend/src/FeedbackHub.API/Middleware/TenantAccessMiddleware.cs` (sửa comment lý do miễn trừ `/api/organizations`)
  - `backend/src/FeedbackHub.Application/DependencyInjection.cs`
  - `.claude/rules/authentication.md`
- Delete:
  - `OrganizationMembershipService.cs`, `IOrganizationMembershipService.cs`, `AddMemberRequest.cs`
  - `OrganizationMembershipRequirement.cs`, `OrganizationMembershipAuthorizationHandler.cs`, `OrganizationPermissionRequirement.cs`, `OrganizationPermissionAuthorizationHandler.cs`
  - test của các tệp trên.
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/OrganizationPermissionPolicyProviderTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/OrganizationsControllerTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Architecture/ArchitectureTests.cs`

**Interfaces:**
- Consumes: phần 3 (`OrganizationContextRequirement`, `DenyMode`).
- Produces:
  - `OrganizationPermissionPolicyProvider`: mọi mã trong `Permissions.All` dựng `OrganizationContextRequirement(code, DenyMode.Forbidden)`; tên khác rơi về provider mặc định.
  - Hằng `NotificationTypes.OrganizationMemberAdded` và mẫu hiển thị của nó giữ nguyên, để thông báo cũ trong cơ sở dữ liệu vẫn hiện (QĐ-17); không còn chỗ nào phát nó.

- [ ] **Step 1: Viết test thất bại**

```csharp
[Theory] GetPolicyAsync_EveryPermissionCode_BuildsGateRequirementForbidden
[Theory] OldOrganizationIdRoutes_SignedIn_Return404
// GET/POST members, PATCH/DELETE members/{accountId}, GET/POST roles, PATCH/DELETE roles/{roleId}
// dưới api/organizations/{id} (FR-59, AC-135, AC-260)
AddMemberDirectly_OldRoute_DoesNotCreateMembership                 // AC-260
NoController_HasOrganizationIdRouteParameter                       // không route nào mẫu "api/organizations/{id"
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~OrganizationPermissionPolicyProviderTests|FullyQualifiedName~OrganizationsControllerTests|FullyQualifiedName~ArchitectureTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt.** Xoá theo danh sách. Sửa đoạn policy trong `authentication.md`:
  - bỏ `OrganizationMember`;
  - ghi mọi mã `Permissions` qua cổng tổ chức;
  - ghi không còn route nào nhận id tổ chức (QĐ-06).

- [ ] **Step 4: Chạy lại toàn bộ backend, phải qua**

Run: `backend/scripts/test.sh`
Expected: PASS (test của route cũ đã xoá cùng route).

- [ ] **Step 5: Commit** `refactor(api)!: remove organization-id routes and direct member adds`

### Task 6: API-06 danh sách thành viên

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/Organizations/MemberService.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/IMemberService.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/IOrganizationMemberQueries.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Queries/OrganizationMemberQueries.cs`
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Organizations/OrganizationMemberDto.cs`
  - `backend/src/FeedbackHub.API/Controllers/CurrentOrganizationController.cs`
  - DI của Application và Infrastructure
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Organizations/MemberServiceListTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/OrganizationMemberQueriesTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/CurrentOrganizationMembersTests.cs`

**Interfaces:**
- Consumes: Task 4 (`RoleGrantReader`, `RoleGrantPolicy`), phần 3 (`RoleRefDto`, `PageRequest`, `ActiveOrganizationMemberOrNotFound`).
- Produces:
  - `public sealed record MemberRow(Guid MemberId, Guid AccountId, string AccountName, string Email)`.
  - `IOrganizationMemberQueries.ListActiveAsync(Guid organizationId, PageRequest request, CancellationToken cancellationToken) → Task<PagedResult<MemberRow>>`: một truy vấn nối `organization_members` (`IsActive`) với `accounts`, sắp `Collate(Name, "und-x-icu")` rồi `AccountId`.
  - `OrganizationMemberDto`:
    ```csharp
    public sealed record OrganizationMemberDto(
        Guid AccountId, string AccountName, string? Email,
        IReadOnlyList<Guid> RoleIds, IReadOnlyList<string> RoleNames, IReadOnlyList<RoleRefDto> Roles,
        bool IsSelf, bool CanChangeRoles, bool CanRemove,
        RoleLockReason? LockReason, RoleRefDto? LockRole);
    ```
  - `IMemberService.ListAsync(PageRequest request, CancellationToken cancellationToken) → Task<PagedResult<OrganizationMemberDto>>`. Tổ chức lấy từ `org_id`. Cờ tính theo TECH00 §4.6:
    - người gọi không có `organizations.members.manage` (đọc qua `LoadCallerAsync`): `Email` chỉ có ở dòng của chính họ, ba cờ `false`, `LockReason`, `LockRole` = `null`;
    - người gọi có quyền, dòng của chính mình: `CanChangeRoles = true`, `CanRemove = false`, không khoá;
    - người gọi có quyền, dòng khác: `denial = CheckTakeAwayAll(caller, roles)`; `CanChangeRoles = CanRemove = denial is null`; `LockReason`, `LockRole` lấy từ `denial`.
  - Route `GET api/organization/members`, `[Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMemberOrNotFound)]`, `[FromQuery] PageRequest`, 200 `PagedResult<OrganizationMemberDto>`.

- [ ] **Step 1: Viết test thất bại**

```csharp
// Application
ListAsync_PlainMember_SeesOnlyOwnEmailAndNoActions               // AC-261, AC-262
ListAsync_Manager_SeesEveryEmail                                  // AC-263
ListAsync_ManagerOwnRow_CanChangeRolesNotRemove                   // AC-339
ListAsync_ManagerLacksAdminPermissions_RowLockedWithAdminRole     // AC-338
ListAsync_AdminViewingOwnerRow_OwnerOnlyEvenIfAlsoLacking         // AC-341
ListAsync_NewOrganization_OnlyOwner                               // AC-265
// Infrastructure (Testcontainers)
ListActiveAsync_ExcludesInactiveMembers ; ListActiveAsync_SortsByNameIcuThenId ; ListActiveAsync_Pages
// API
Members_Anonymous_Returns401
[Theory] Members_PersonalContextOrNonMemberSystemAdministrator_Returns404   // AC-122, BR-45
Members_SuspendedOrganizationMember_Returns403Suspended
Members_PageSize101_Returns400
Members_PlainMemberResponse_HasNoOtherEmails                        // AC-262
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~MemberServiceListTests|FullyQualifiedName~OrganizationMemberQueriesTests|FullyQualifiedName~CurrentOrganizationMembersTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces. Vai trò của cả trang lấy bằng một lần `LoadRolesOfMembersAsync`, không truy vấn theo từng dòng (TECH00 §9).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(members): list members with email privacy and per-row permissions`

### Task 7: API-07 đổi vai trò, API-08 gỡ, API-09 tự rời

**Files:**
- Modify:
  - `MemberService.cs`, `IMemberService.cs`
  - `backend/src/FeedbackHub.Application/Services/Organizations/UpdateMemberRolesRequest.cs`
  - `CurrentOrganizationController.cs`
  - `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Organizations/MemberServiceWriteTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/MemberConcurrencyTests.cs`
  - `CurrentOrganizationMembersTests.cs`

**Interfaces:**
- Consumes: Task 3 (`IOrganizationLock`), Task 4, phần 3 (`IAuditTrail`, `AuditEvent.For`, `AuditRoleRef`).
- Produces:
  - `UpdateMemberRolesRequest(IReadOnlyList<Guid> RoleIds)` giữ tên. `null` hoặc rỗng → `DomainException(ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole)` (400; trước đây 409).
  - `IMemberService`:
    - `Task ChangeRolesAsync(Guid accountId, UpdateMemberRolesRequest request, CancellationToken cancellationToken)`. API-07: `PUT api/organization/members/{accountId:guid}/roles`, policy `organizations.members.manage`, 204;
    - `Task RemoveAsync(Guid accountId, CancellationToken cancellationToken)`. API-08: `DELETE api/organization/members/{accountId:guid}`, cùng policy, 204;
    - `Task LeaveAsync(CancellationToken cancellationToken)`. API-09: `POST api/organization/leave`, policy `ActiveOrganizationMember`, 204.
  - `ChangeRolesAsync`. Bước 1 trước giao dịch; bước 2–9 trong `ExecuteInTransactionAsync`:
    1. `RoleIds` rỗng → 400. Id trùng gộp làm một.
    2. `AcquireAsync(orgId)`.
    3. Thành viên `IsActive` có `AccountId` trong `orgId`, không có → 404.
    4. `LoadRolesAsync(orgId, roleIds)` thiếu id nào → 404. Kiểm trước mọi ghi.
    5. `caller = LoadCallerAsync`, `current` = vai trò hiện tại của thành viên.
    6. BR-58:
       - không phải chính mình: `CheckTakeAwayAll(caller, current)`;
       - rồi, với mọi trường hợp: `CheckGrantAll(caller, added)`, `added` = vai trò mới không có trong `current`;
       - không đạt → `ForbiddenException(ErrorCode(denial))`.
    7. `current` có Owner, tập mới không có, và không còn Chủ sở hữu đang hoạt động nào khác → 409 `OrganizationCannotRemoveLastOwner`.
    8. Tập mới bằng `current` → return thành công như thường (204): không ghi, không mục, không xoá cache. Các bước kiểm 3–7 (cổng, BR-58, 404, `OwnerOnlyOperation`) vẫn chạy trước. So sánh `roleIds` như tập, bỏ qua id trùng.
    9. Xoá dòng `organization_member_roles` bị bỏ, thêm dòng mới. Ghi `MemberRolesChanged { accountName, email, rolesBefore, rolesAfter }`. `SaveChangesAsync`.

    Sau commit: xoá cache của thành viên.
  - `RemoveAsync`, trong giao dịch:
    1. khoá;
    2. thành viên (404);
    3. BR-58 `CheckTakeAwayAll(caller, roles)` → 403;
    4. `accountId` là người gọi → 409 `CannotRemoveSelf` (bước 7, sau BR-58, AC-337);
    5. thành viên là Chủ sở hữu cuối cùng → 409 `OrganizationCannotRemoveLastOwner`;
    6. `member.Deactivate()`, xoá mọi `organization_member_roles` của họ (BR-30);
    7. ghi `MemberRemoved { accountName, email, roles }`, lưu.

    Sau commit: xoá cache.
  - `LeaveAsync`, trong giao dịch:
    1. khoá;
    2. dòng thành viên của người gọi;
    3. là Chủ sở hữu cuối cùng → 409 `LastOwnerCannotLeave` (AC-277);
    4. `Deactivate()`, xoá vai trò;
    5. ghi `MemberLeft { accountName, email }`, lưu.

    Sau commit: xoá cache.
  - `accountName`, `email` là giá trị hiện tại của tài khoản bị đổi; danh sách vai trò theo thứ tự hiển thị, dạng `AuditRoleRef`.
  - Key mới. Chữ:
    - `CannotRemoveSelf`: vi "Bạn không thể tự gỡ mình khỏi tổ chức. Hãy dùng Rời tổ chức.", en "You can't remove yourself from the organization. Use Leave organization instead."
    - `LastOwnerCannotLeave`: vi "Tổ chức cần ít nhất một Chủ sở hữu. Hãy gán vai trò Chủ sở hữu cho người khác trước khi rời.", en "The organization needs at least one Owner. Make someone else an Owner before you leave."

- [ ] **Step 1: Viết test thất bại**

```csharp
// Application (MemberServiceWriteTests)
ChangeRolesAsync_EmptyRoleIds_Throws400BeforeLock
ChangeRolesAsync_RoleOfOtherOrganization_Throws404AndWritesNothing
ChangeRolesAsync_DuplicateIds_TreatedAsOne
ChangeRolesAsync_AddMemberToAdminHolderLackingAdminPermissions_ThrowsInsufficient   // AC-336, AC-197
ChangeRolesAsync_SelfToAdminWithoutFeedbackHubAdmin_Throws403                       // AC-154
ChangeRolesAsync_AdminMakesMemberOwner_ThrowsOwnerOnly                              // AC-155
ChangeRolesAsync_AllPermissionsNonOwnerAddsRoleToOwner_ThrowsOwnerOnly              // AC-340
ChangeRolesAsync_ManagerRemovesOwnerRole_ThrowsOwnerOnly                            // AC-193
ChangeRolesAsync_OwnerMakesMemberOwner_Succeeds                                     // AC-157
ChangeRolesAsync_LastOwnerDropsOwner_Throws409
ChangeRolesAsync_StaleCacheGrantsButDatabaseDenies_Throws403
// IPermissionResolver trả mọi quyền (cache cũ), cơ sở dữ liệu không → 403; resolver DidNotReceive
ChangeRolesAsync_SameSet_NoWriteNoRecordNoInvalidate
ChangeRolesAsync_SameSetWithDuplicateIdsInDifferentOrder_NoWriteNoRecordNoInvalidate   // so như tập, bỏ qua trùng
ChangeRolesAsync_SameSetButCallerLacksPermission_StillThrows403                       // BR-58 chạy trước so sánh
ChangeRolesAsync_SameSetForMissingMember_StillThrows404
ChangeRolesAsync_RolesRenderedInDisplayOrder_CustomRolesByIcuCollationThenId           // AC-338, lockRole
ChangeRolesAsync_Success_RecordsBeforeAfterInDisplayOrderThenInvalidates           // AC-367
RemoveAsync_AdminRemovesOwner_ThrowsOwnerOnly                                       // AC-158
RemoveAsync_Self_Throws409CannotRemoveSelfAndRecordsNothing                         // AC-337
RemoveAsync_SelfWhenBr58Denies_Throws403Not409                                      // D2
RemoveAsync_Success_DeactivatesDeletesRolesRecordsRemoved                           // AC-69 (phần thành viên)
LeaveAsync_LastOwner_Throws409                                                      // AC-277
LeaveAsync_OneOfTwoOwners_SucceedsAndRecordsLeft                                    // AC-278, AC-279
// Infrastructure (Testcontainers, qua DI thật)
RemoveAsync_TwoOwnersRemoveEachOtherConcurrently_OneSucceedsOtherConflict           // Review Focus 2
// API
[Theory] MemberWrites_Anonymous_Return401
ChangeRoles_PlainMember_Returns403                                                  // AC-264
ChangeRoles_RoleIdOfOtherOrganization_Returns404AndNothingChanges
ChangeRoles_SameRoleSet_Returns204AndAuditLogHasNoNewRow
Remove_Self_Returns409WithMessageAndNoNewAuditRow                                   // AC-337
Leave_ThenNextRequestWithOldToken_Returns403AccessDenied                            // cache đã xoá
RemoveThenRefresh_ReturnsNullOrganization                                           // phiên của người bị gỡ (T6)
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~MemberServiceWriteTests|FullyQualifiedName~MemberConcurrencyTests|FullyQualifiedName~CurrentOrganizationMembersTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(members): change roles, remove and leave under BR-58 with audit entries`

### Task 8: API-14 danh sách chọn vai trò, API-15 phần Vai trò

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/Roles/RoleOptionDto.cs`
  - `backend/src/FeedbackHub.Application/Services/Roles/IRoleQueries.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/Queries/RoleQueries.cs`
- Modify: `RoleDto.cs`, `RoleService.cs`, `IRoleService.cs`, `CurrentOrganizationController.cs`, DI
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/RoleServiceReadTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/RoleQueriesTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/CurrentOrganizationRolesTests.cs`

**Interfaces:**
- Consumes: Task 4.
- Produces:
  - `public sealed record RoleRow(Guid Id, string Name, SystemRoleKind? SystemKind, IReadOnlyList<string> PermissionCodes, int HolderCount)`.
  - `IRoleQueries.ListAsync(Guid organizationId, CancellationToken cancellationToken) → Task<IReadOnlyList<RoleRow>>`:
    - thứ tự: vai trò hệ thống `Owner`, `Admin`, `Member`, rồi tự tạo theo `Collate(Name, "und-x-icu")`, rồi `Id`;
    - Owner mang `Permissions.All`;
    - `HolderCount` bằng một truy vấn `GROUP BY` trên `organization_member_roles` nối thành viên `IsActive` (TECH00 §9).
  - `public sealed record RoleOptionDto(Guid Id, string Name, SystemRoleKind? SystemKind, bool CanGrant)`. Không có quyền nào (AC-311).
  - `public enum RoleDeleteBlockedReason { SystemRole = 1, InsufficientRolePermissions = 2, HasHolders = 3 }`.
  - `RoleDto(Guid Id, string Name, SystemRoleKind? SystemKind, bool IsSystem, IReadOnlyList<string> PermissionCodes, int HolderCount, bool CanEdit, bool CanDelete, RoleDeleteBlockedReason? DeleteBlockedReason)`:
    - `CanEdit = !IsSystem && CanTakeAway(caller, role)`;
    - `DeleteBlockedReason` theo thứ tự `SystemRole`, `InsufficientRolePermissions`, `HasHolders`, rồi `null`;
    - `CanDelete = DeleteBlockedReason is null`.
  - `IRoleService`:
    - `Task<IReadOnlyList<RoleOptionDto>> ListOptionsAsync(CancellationToken cancellationToken)`. Route `GET api/organization/role-options`, policy `organizations.members.manage`;
    - `Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken)`. Route `GET api/organization/roles`, policy `organizations.roles.manage`.

    Cả hai lấy tổ chức từ `org_id`.

- [ ] **Step 1: Viết test thất bại**

```csharp
// Application
ListOptionsAsync_HrRoleCaller_OnlyMemberGrantable                    // AC-311
ListOptionsAsync_Owner_AllGrantable
ListAsync_FlagsAndBlockedReasons
// hệ thống → SystemRole; thiếu quyền → InsufficientRolePermissions; còn người giữ → HasHolders (AC-248, AC-257)
// Infrastructure
ListAsync_HolderCountIgnoresInactiveMembers                           // AC-252
ListAsync_SystemRolesFirstThenCustomByName ; ListAsync_OwnerHasAllPermissions
// API
[Theory] RoleReads_Anonymous_Return401
RoleOptions_WithoutMembersManage_Returns403 ; Roles_WithoutRolesManage_Returns403   // BR-77, DenyMode.Forbidden
RoleOptions_Response_HasNoPermissionCodes                                         // AC-311
Roles_PersonalContext_Returns403AccessDenied
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~RoleServiceReadTests|FullyQualifiedName~RoleQueriesTests|FullyQualifiedName~CurrentOrganizationRolesTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(roles): list roles with holder counts and grant options for the working organization`

### Task 9: API-16 tạo, API-17 sửa, API-18 xoá vai trò tự tạo

**Files:**
- Modify:
  - `RoleService.cs`, `IRoleService.cs`, `CreateRoleRequest.cs`, `UpdateRoleRequest.cs`
  - `CurrentOrganizationController.cs`
  - `ApplicationMessages.cs`, `Messages.resx`, `Messages.en.resx`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Roles/RoleServiceWriteTests.cs`
  - `CurrentOrganizationRolesTests.cs`

**Interfaces:**
- Consumes: Task 1 (`SystemRoleDisplayNames.IsReserved`), Task 2 (409 từ chỉ mục), Task 3, Task 4, Task 8 (`RoleDto`), phần 3 (`IAuditTrail`).
- Produces:
  - `IRoleService`:
    - `Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)`. Route `POST api/organization/roles`, 201;
    - `Task<RoleDto> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)`. Route `PUT api/organization/roles/{roleId:guid}`, 200. Trước đây `PATCH`;
    - `Task DeleteAsync(Guid roleId, CancellationToken cancellationToken)`. Route `DELETE api/organization/roles/{roleId:guid}`, 204.

    Policy `organizations.roles.manage`. `CreateRoleRequest`, `UpdateRoleRequest` giữ `[Required]`, `[MaxLength(100)]` cho tên (AC-287, AC-288).
  - Thứ tự `CreateAsync`:
    1. mã quyền lạ → 400 `UnknownPermissionCode`;
    2. trong giao dịch: khoá;
    3. BR-58: mọi mã ∈ `P(caller)` (`HasAllPermissions`) → nếu không, 403 `InsufficientRolePermissions` (AC-156);
    4. `IsReserved(name)` → 409 `RoleNameReservedForSystemRole` (AC-291);
    5. trùng tên không phân biệt hoa thường (so `lower` trong SQL) → 409 `OrganizationRoleNameAlreadyExists` (AC-290);
    6. tạo, ghi `RoleCreated { role, permissionCodes }`, lưu. Vi phạm chỉ mục lúc lưu cũng thành 409.
  - Thứ tự `UpdateAsync`:
    1. mã lạ → 400;
    2. khoá;
    3. vai trò trong `org_id` (404);
    4. hệ thống → 403 `OrganizationSystemRoleImmutable` (AC-55, AC-159);
    5. BR-58: `CanTakeAway(role)` và mọi mã mới ∈ `P(caller)` → 403 (AC-198, AC-295, AC-296);
    6. tên dành riêng hoặc trùng vai trò khác → 409 (AC-326);
    7. không đổi tên lẫn quyền → trả 200 với `RoleDto` hiện tại, không ghi, không mục, không xoá cache. Các bước 1–6 vẫn chạy trước. So sánh tên sau trim; so quyền như tập, bỏ qua mã trùng;
    8. ghi `RoleUpdated { nameBefore, nameAfter, permissionsAdded, permissionsRemoved }`, lưu.

    Sau commit: `IPermissionResolver.InvalidateOrganizationAsync`.
  - Thứ tự `DeleteAsync`:
    1. khoá;
    2. 404;
    3. hệ thống → 403;
    4. `CanTakeAway(role)` → 403 (AC-249, AC-257);
    5. còn người giữ `IsActive` → 409 `RoleHasHolders` (AC-248, AC-251, AC-256);
    6. ghi `RoleDeleted { role }` trước khi xoá;
    7. xoá vai trò. Cascade xoá `role_permissions`; phần 5 thêm cascade lời mời.

    Sau commit: xoá cache quyền của tổ chức.
  - Key mới. Chữ:
    - `RoleHasHolders`: vi "Vai trò này còn người giữ. Hãy đổi vai trò của họ trước khi xoá.", en "Someone still has this role. Change their roles before deleting it."
    - `RoleNameReservedForSystemRole`: vi "Tên này trùng tên một vai trò hệ thống.", en "This name is taken by a system role."

- [ ] **Step 1: Viết test thất bại**

```csharp
// Application (RoleServiceWriteTests)
CreateAsync_PermissionCallerLacks_Throws403AndCreatesNothing            // AC-156
[Theory] CreateAsync_ReservedName_Throws409                             // "Owner", "chủ sở hữu", NFD (AC-291)
CreateAsync_NameDiffersOnlyByCase_Throws409                             // AC-290
CreateAsync_SameNameInOtherOrganization_Succeeds                        // AC-292
CreateAsync_100CharName_Succeeds                                         // AC-289
CreateAsync_Success_RecordsRoleCreated
UpdateAsync_SystemRole_Throws403ImmutableBeforeBr58                      // AC-55, AC-159
UpdateAsync_RenameRoleWithPermissionCallerLacks_Throws403                // AC-295
UpdateAsync_AddPermissionCallerHasButRoleHasOneCallerLacks_Throws403     // AC-296
UpdateAsync_OwnerRenames_Succeeds                                        // AC-297
UpdateAsync_RenameToOtherRoleUppercase_Throws409                         // AC-326
UpdateAsync_RenameKeepsHoldersAndPermissions                             // AC-293 (phần vai trò)
UpdateAsync_NoChange_NoRecordNoInvalidate
UpdateAsync_NameChangesOnlyByTrailingSpaceAndSamePermissionSet_NoRecordNoInvalidate   // tên so sau trim, quyền so như tập
UpdateAsync_NoChangeButCallerLacksPermission_StillThrows403
UpdateAsync_NoChangeOnSystemRole_StillThrows403Immutable
UpdateAsync_Success_RecordsAddedAndRemovedThenInvalidates
DeleteAsync_HasActiveHolder_Throws409                                    // AC-248, AC-251, AC-256
DeleteAsync_OnlyInactiveHolders_Deletes                                  // AC-252
DeleteAsync_CallerLacksPermission_Throws403Before409                     // AC-257, AC-249
DeleteAsync_Success_RecordsRoleDeleted                                    // AC-247
// API
[Theory] RoleWrites_Anonymous_Return401 ; RoleWrites_WithoutRolesManage_Return403
UpdateRole_RoleOfOtherOrganization_Returns404
UpdateRole_NoChange_Returns200WithCurrentRoleDtoAndNoNewAuditRow
CreateRole_101CharName_Returns400                                         // AC-288
UpdateRole_OldPatchVerb_Returns405Or404
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~RoleServiceWriteTests|FullyQualifiedName~CurrentOrganizationRolesTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces. Tên đã chuẩn hoá (qua `Role.Update`) mới đem so với tập dành riêng và với vai trò khác.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(roles): create, edit and delete custom roles under BR-58 and BR-79`

### Task 10: Contract, frontend tối thiểu, feature map, gate

**Files:**
- Modify:
  - `shared/openapi/openapi.json` (export)
  - `frontend/src/views/OrganizationsView.vue` (bỏ hai nút *Thành viên*, *Vai trò* và hai dialog)
  - `docs/feature-map.md`
  - Delete:
  - `frontend/src/components/OrganizationMembersDialog.vue`
  - `frontend/src/components/OrganizationRolesDialog.vue`
  - `frontend/src/components/RoleForm.vue`
  - `frontend/src/stores/roles.ts`

  Cả bốn chỉ gọi route `{id}` đã bỏ; phần 8 dựng màn hình mới.

**Interfaces:**
- Consumes: Task 1–9.
- Produces:
  - `docs/feature-map.md`, mục mới `## Thành viên và vai trò của tổ chức`:
    - PRD: FR-23, FR-29, FR-52, FR-59, FR-60, FR-62, FR-64, FR-66;
    - vào bằng: chưa có màn hình (phần 8);
    - backend: route API-06–API-09, API-14–API-18, `RoleGrantPolicy`, `IOrganizationLock`;
    - test: tên các tệp test của phần này.

    Mục `## Tổ chức đang làm việc và admin hệ thống` của phần 3 bỏ dòng nhắc route `{id}` nếu có.
  - Mô tả PR có mục `## Changelog` với dòng bắt buộc:
    `- Removed: every api/organizations/{id}/... route, adding a member directly, and DELETE api/organizations/{id}; members and roles are now managed at api/organization/..., and members join by invitation.`
    (TECH00 §10.2 bước 3; cùng cách phần 6 xử lý `api/admin/audit-logs`.)

- [ ] **Step 1: Export contract và sinh lại client**

Run:
- `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
- `bun run --cwd frontend codegen`

Expected: `openapi.json` có các route `api/organization/members…`, `leave`, `role-options`, `roles…`; không còn `api/organizations/{id}`; `AddMemberRequest` biến mất.

- [ ] **Step 2: Sửa frontend cho biên dịch được**, rồi chạy:
- `bun run --cwd frontend build-only`
- `bun run --cwd frontend type-check`
- `bun run --cwd frontend test:run`

Expected: PASS.

- [ ] **Step 3: Ghi feature map** theo Interfaces. Quyết định API của module chỉ nằm ở TECH00 §12 (`AGENTS.md` § Decision Log), không ghi vào `.claude/decisions.md`.

- [ ] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [ ] **Step 5: Commit** `chore(contract): export the member and role management contract`

Mô tả PR khai báo lệch nhỏ: thứ tự vai trò của một dòng thành viên (`Owner`, `Admin`, `Member`, rồi vai trò tự tạo theo `COLLATE "und-x-icu"`, rồi `Id`, sắp trong truy vấn) là định nghĩa mà TECH00 §4.6 chưa nêu; ghi định nghĩa này vào TECH00 §4.6.

---

## Câu hỏi mở

- **Đã chốt (SA, 2026-10-23):** Lưu đổi vai trò (API-07) hoặc sửa vai trò (API-17) mà không đổi gì vẫn thành công như thường (API-07 trả 204, API-17 trả 200 với `RoleDto` hiện tại), không ghi mục nhật ký, không xoá cache. Mọi bước kiểm (cổng, BR-58, 404, `OwnerOnlyOperation`) chạy trước; `roleIds` và quyền so như tập bỏ qua trùng, tên so sau trim (Task 7, Task 9).
- **Đã chốt (SA, 2026-10-23):** Thứ tự vai trò của một dòng thành viên là `Owner`, `Admin`, `Member`, rồi vai trò tự tạo theo tên với `COLLATE "und-x-icu"` như danh sách vai trò, rồi `Id`. Sắp trong truy vấn tải vai trò; `lockRole` và mục nhật ký dùng thứ tự đó, không dùng bộ so sánh văn hoá trong bộ nhớ. PR khai báo đây là lệch nhỏ và ghi định nghĩa vào TECH00 §4.6 (Global Constraints, Task 4, Task 10).

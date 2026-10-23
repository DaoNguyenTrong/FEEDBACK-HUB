# Module 00 — Phần 1: Sửa bảo mật B1, B2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa hai lỗi bảo mật có sẵn mà appsec tìm ra, trước mọi phần khác của module 00:
- **B1**: đổi email ở hồ sơ phải qua email chờ xác nhận và hỏi mật khẩu hiện tại.
- **B2**: đọc đúng claim của token Microsoft và chỉ tin email đã xác minh.

Đi kèm là các thay đổi mà hai lỗi này cần:
- M7 và M8;
- kiểm `Status` một chỗ trong `TokenIssuer`;
- loại cột nhạy cảm khỏi snapshot audit;
- `SmtpEmailSender` không ghi người nhận vào log và có hạn 15 giây.

**Architecture:**
- **Domain**: `Account` thêm `PendingEmail` và ba phương thức đổi email. `EmailVerificationToken` ghi mục đích và địa chỉ mà token chứng minh.
- **Application**: `AccountService` (API-29–31) và `RegistrationService` (API-32, `verify-email`, `resend-verification`) dùng chung một lớp phát link. `AuthService.ExternalLoginAsync` chuyển việc kiểm "email đã xác minh" xuống đúng hai nhánh.
- **Infrastructure**:
  - `MicrosoftJwtValidator` đọc claim thô.
  - Migration M7 (cột) và M8 (dữ liệu).
  - `AppDbContext` loại thuộc tính nhạy cảm khỏi snapshot audit.
- **Frontend**: `ProfileDialog.vue` hiện tại được sửa tối thiểu. Phần sau (design system) sẽ chuyển nó thành `ProfilePanel`.

**Tech Stack:** .NET 10 (xUnit, NSubstitute, Testcontainers PostgreSQL, MailKit), EF Core + Npgsql, Vue 3 + vitest + MSW, orval.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0: §2.1, §3.7 (đoạn *Loại theo thuộc tính*), §3.8, §4.7, §4.9, §6.6, §6.7, §7.3, §10.1 (M7, M8), §10.2, §11 (các hàng B1, B2, M8, N-new-1, N-new-2, N-new-4, `SmtpEmailSender`), §12 QĐ-10, QĐ-12, QĐ-29, QĐ-30, QĐ-33, QĐ-35.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: US-26, FR-67, BR-82, D17, D18, AC-342–AC-358.

## Lộ trình module 00

Module 00 phát hành một lần, không có cờ tính năng (TECH00 §10.2). Mọi phần làm trên nhánh `feature/module-00`, và chỉ mở một PR vào `dev` sau phần cuối. Mỗi phần là một kế hoạch riêng, người dùng duyệt trước khi làm.

Bảng của phần 1, cập nhật theo quyết định của người điều phối 2026-10-23: lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục) chuyển từ phần 6 lên đầu phần 3, để mọi service dựng ở phần 3–5 tự ghi mục của mình. Hàng 5 ghi thêm API-23 (`accountCreated`), phần 5 làm cùng lời mời. Bảng này giống hệt nhau ở mọi kế hoạch phần 3–8.

| Phần | Nội dung | TECH00 |
|---|---|---|
| **1 (tệp này)** | Sửa bảo mật B1, B2; M7, M8; `TokenIssuer` kiểm `Status`; audit loại cột nhạy cảm; `SmtpEmailSender` | §3.8, §4.9, §6.6, §6.7, §7.3 |
| 2 | Sẵn sàng nhiều máy: giới hạn tần suất qua Redis, `ProductionTopologyValidator`, `EmailSettings:SecureSocket`, Data Protection | §4.8, §10.3 |
| 3 | Lõi nhật ký kiểm toán (M9, `AuditEvent`, `IAuditTrail`, danh mục 13 loại); cổng tổ chức, admin hệ thống, phiên: `GetAccessAsync`, `OrganizationContextAuthorizationHandler`, refresh `org_id = null`, `isSystemAdministrator`, CLI `grant`/`revoke-system-admin`, `SystemOrganizationService`, API-01–API-05, M1, M2 (`system_administrators`) | §3.1, §3.5, §3.7 (lõi), §4.1, §4.5, §5.1, §5.2, §6.1, §6.5 |
| 4 | Vai trò và thành viên: `RoleGrantPolicy`, `IOrganizationLock`, `MemberService`, `RoleService`, M3–M5 | §3.2, §3.3, §4.3, §5.3, §5.4, §6.4 |
| 5 | Lời mời: M2 (`organization_invitations`), API-10–API-13, API-20–API-23, email mời | §3.4, §4.2, §4.4, §6.2, §6.3 |
| 6 | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Nhánh và commit**
  - Làm trên nhánh `feature/module-00`, cắt từ `dev`.
  - Commit theo Conventional Commits, stage từng tệp theo đường dẫn.
  - Mỗi message kết thúc bằng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    Claude-Session: https://claude.ai/code/session_01HaxzoPfz2fU7WZC8dYVjBu
    ```
- **C#**: theo `code-conventions.md`:
  - không dùng `var`;
  - truyền `CancellationToken`;
  - entity chỉ tạo qua `static Create(XxxParams p)`;
  - lỗi Domain là `throw new DomainException(DomainMessages.X)`;
  - lỗi Application là `ConflictException`, `UnauthorizedException`… với key trong `ApplicationMessages`.
- **Mã lỗi**: mã lỗi là key resx.
  - Mỗi key mới phải có chữ ở cả `backend/src/FeedbackHub.Application/Resources/Messages.resx` (vi) và `Messages.en.resx` (en).
  - Key Domain đặt ở `DomainMessages`, chữ ở resource của Domain theo đúng chỗ các key Domain hiện có.
- **Test**: theo `testing.md`: tên `Method_Scenario_Expected`, dùng xUnit `Assert`. Test API dùng `[Collection(nameof(ApiCollection))]`.
- **Log**: không ghi email, mã link (thô hay hash), mật khẩu hay token vào log (TECH00 §7.3). Log chỉ dùng `accountId`.
- **Link đổi email**:
  - hạn 24 giờ, là hằng trong Application `EmailChangeLink.Lifetime = TimeSpan.FromHours(24)`, không đọc `EmailSettings:VerificationTokenExpiryHours`;
  - URL: `{FrontendBaseUrl}/#/confirm-email-change?token=<mã>`;
  - mã sinh bằng `RandomNumberGenerator.GetBytes(32)` rồi base64url, lưu `TokenHash.Compute`.
- **Microsoft**: tenant tài khoản cá nhân là `9188040d-6c67-4c5b-b112-36a304b66dad`.
- **Contract**: sau khi đổi controller hoặc DTO thì export OpenAPI rồi chạy `bun run --cwd frontend codegen` (`.claude/rules/commands.md` § OpenAPI Spec Export). Commit `shared/openapi/openapi.json`.
- **Chuỗi frontend**: mọi chuỗi người dùng nhìn thấy có cả `vi` và `en` trong `frontend/src/locales/{vi,en}.ts`.

## Review Focus

1. **Email gõ khác hoa thường hoặc có khoảng trắng** (`" A@X.com "` khi email hiện tại là `a@x.com`): phải coi là không đổi. Không hỏi mật khẩu, không phát link. Test ở Task 3.
2. **Link xác nhận bị mở hai lần** (bấm đúp, trình duyệt tải trước): lần đầu đổi xong, lần sau trả 409 `EmailChangeLinkNoLongerValid`, không 500. Test ở Task 5.
3. **Hai tài khoản cùng xác nhận một địa chỉ gần như đồng thời**: một bên đổi được, bên kia nhận 409 `AccountEmailAlreadyExists` do chỉ mục unique `Email` bắt, không 500. Test ở Task 5.
4. **Đăng nhập Google hoặc Microsoft bằng địa chỉ trùng `PendingEmail` của một tài khoản**: không được khớp vào tài khoản đó, vì chỉ khớp theo `Email`. Test ở Task 8.
5. **SMTP lỗi khi gửi link đổi email**: hồ sơ vẫn lưu, `PendingEmail` đặt, trả 200, log chỉ có `accountId`; người dùng bấm *Gửi lại* để nhận link mới. Test ở Task 3.

---

### Task 1: Domain, đổi email chờ xác nhận và token mang địa chỉ

**Files:**
- Modify: `backend/src/FeedbackHub.Domain/Entities/Account.cs`
- Modify: `backend/src/FeedbackHub.Domain/Entities/EmailVerificationToken.cs`
- Create: `backend/src/FeedbackHub.Domain/Entities/EmailVerificationPurpose.cs`
- Modify: `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs` (cùng resource chữ của Domain)
- Test: `backend/tests/FeedbackHub.Domain.Tests/Entities/AccountTests.cs`, `backend/tests/FeedbackHub.Domain.Tests/Entities/EmailVerificationTokenTests.cs`

**Interfaces:**
- Produces:
  - `enum EmailVerificationPurpose { VerifyEmail, ChangeEmail }`.
  - `Account`:
    - `string? PendingEmail { get; private set; }`;
    - `void RequestEmailChange(string email)`;
    - `void CancelEmailChange()`;
    - `void ApplyEmailChange()`;
    - `int FailedEmailChangeCount`, `DateTime? EmailChangeWindowStartedAt`, `DateTime? EmailChangeLockedUntil`, đều `{ get; private set; }`.
  - `EmailVerificationTokenParams(Guid AccountId, string TokenHash, DateTime ExpiresAt, EmailVerificationPurpose Purpose, string Email)`.
  - `EmailVerificationToken`:
    - `EmailVerificationPurpose? Purpose`, `string? Email` (null chỉ ở dòng cũ);
    - `bool IsActiveAt(DateTime utcNow)`, thay `IsActive`. Mọi chỗ đang gọi `IsActive` truyền `IDateTimeProvider.UtcNow`.
  - `DomainMessages.EmailChangeSameAsCurrent`, `DomainMessages.EmailChangeNotPending`.

- [ ] **Step 1: Viết test Domain thất bại**

```csharp
[Fact] public void RequestEmailChange_NewAddress_SetsNormalizedPendingAndKeepsEmail()
// Create(email "a@x.com"); RequestEmailChange(" B@X.com ") → PendingEmail == "b@x.com", Email == "a@x.com", EmailConfirmed giữ nguyên
[Fact] public void RequestEmailChange_SameAsCurrentIgnoringCase_Throws()
// RequestEmailChange("A@X.COM") → DomainException, Message == DomainMessages.EmailChangeSameAsCurrent
[Fact] public void CancelEmailChange_ClearsPending()
[Fact] public void ApplyEmailChange_MovesPendingToEmailAndConfirms()
// EmailConfirmed false trước → sau: Email == pending cũ, PendingEmail == null, EmailConfirmed == true
[Fact] public void ApplyEmailChange_NoPending_Throws()   // DomainMessages.EmailChangeNotPending
[Fact] public void Update_DoesNotTouchPendingEmail()
[Fact] public void Create_EmptyEmail_Throws()            // EmailVerificationToken
[Fact] public void Create_StoresPurposeAndNormalizedEmail()
[Fact] public void IsActiveAt_AfterExpiry_IsFalse()      // ExpiresAt = now+1h; IsActiveAt(now+2h) == false
[Fact] public void IsActiveAt_Consumed_IsFalse()
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Domain.Tests -m:1 --filter "FullyQualifiedName~AccountTests|FullyQualifiedName~EmailVerificationTokenTests"`
Expected: FAIL (các thành viên chưa tồn tại).

- [ ] **Step 3: Cài đặt**
  - `RequestEmailChange`:
    - chuẩn hoá bằng `NormalizeEmail`;
    - nếu bằng `Email` thì ném `EmailChangeSameAsCurrent`;
    - nếu không thì gán `PendingEmail`.
  - `ApplyEmailChange`: gán `Email = PendingEmail`, đặt `PendingEmail = null`, rồi gọi `ConfirmEmail()`.
  - `Update(AccountParams)` giữ nguyên chữ ký. Service từ nay luôn truyền `account.Email` hiện tại ở Task 3.
  - `EmailVerificationToken.Create`: giữ các kiểm tra hiện có, thêm kiểm `Email` không rỗng; lưu `NormalizeEmail` của `Email`.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(domain): add pending email change to Account and purpose to verification tokens`

### Task 2: Lưu trữ, M7, bộ đếm `EmailChange`, audit loại cột nhạy cảm

**Files:**
- Modify: `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/AccountConfiguration.cs`
- Modify: `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/EmailVerificationTokenConfiguration.cs`
- Modify: `backend/src/FeedbackHub.Application/Services/Accounts/PasswordAttemptPurpose.cs`
- Modify: `backend/src/FeedbackHub.Infrastructure/Persistence/Repositories/PasswordAttemptStore.cs`
- Modify: `backend/src/FeedbackHub.Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_AddEmailChange.cs` (sinh bằng `dotnet ef migrations add AddEmailChange`)
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/Repositories/PasswordAttemptStoreTests.cs`, `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/AuditExcludedPropertiesTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `PasswordAttemptPurpose.EmailChange`.
  - Cột M7:
    - `accounts.PendingEmail` varchar(200) null;
    - `accounts.FailedEmailChangeCount` int not null default 0;
    - `accounts.EmailChangeWindowStartedAt` timestamptz null;
    - `accounts.EmailChangeLockedUntil` timestamptz null;
    - `email_verification_tokens.Purpose` varchar(20) null, lưu tên enum dạng chuỗi;
    - `email_verification_tokens.Email` varchar(200) null.
  - `AppDbContext.AuditExcludedProperties`: tập `(Type, string)`, gồm `Account.PasswordHash` và chín cột khoá (Login ×3, PasswordChange ×3, EmailChange ×3).

- [ ] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
[Fact] public async Task ReserveAttemptAsync_EmailChange_UsesOwnCounter()
// Reserve(EmailChange) ×2 → FailedEmailChangeCount == 2; FailedLoginCount, FailedPasswordChangeCount == 0
[Fact] public async Task LockAsync_EmailChange_SetsOnlyEmailChangeLockedUntil()
[Fact] public async Task ClearAsync_EmailChange_ResetsOnlyEmailChangeColumns()
[Fact] public async Task UpdateAccount_ViaRepository_DoesNotOverwriteEmailChangeCounter()
// Reserve(EmailChange) rồi tải account (NoTracking), Update tên, SaveChanges → FailedEmailChangeCount vẫn 1
[Fact] public async Task SaveChanges_AccountSnapshot_ExcludesPasswordHashAndLockoutColumns()
// thêm, sửa, xoá Account → old_values/new_values không có khoá "PasswordHash", "FailedLoginCount", … "EmailChangeLockedUntil"; có "Email", "PendingEmail"
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~PasswordAttemptStoreTests|FullyQualifiedName~AuditExcludedPropertiesTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**
  - Ba cột `EmailChange` cấu hình `SetAfterSaveBehavior(PropertySaveBehavior.Ignore)`, giống sáu cột hiện có.
  - `Columns(PasswordAttemptPurpose)` và hai `switch` của `LockAsync`/`ClearAsync` thêm nhánh `EmailChange`.
  - `CaptureAuditEntries` bỏ qua thuộc tính có trong `AuditExcludedProperties`, cho cả `Added`, `Modified` và `Deleted`.
  - Sinh migration. Kiểm `Up` chỉ thêm cột, không đụng dữ liệu.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(persistence): add email change columns, its attempt counter and audit property exclusions`

### Task 3: API-29, lưu hồ sơ đổi email chờ xác nhận, hỏi mật khẩu hiện tại

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Accounts/AccountService.cs`
  - `backend/src/FeedbackHub.Application/Services/Accounts/UpdateProfileRequest.cs`
  - `backend/src/FeedbackHub.Application/Services/Accounts/ProfileDto.cs`
  - `backend/src/FeedbackHub.Application/Services/Accounts/AccountMapper.cs`
  - `backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs`
  - `Messages.resx`, `Messages.en.resx`
- Create: `backend/src/FeedbackHub.Application/Services/Accounts/EmailChangeLink.cs`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Accounts/AccountServiceTests.cs`, `backend/tests/FeedbackHub.Application.Tests/Services/Accounts/EmailChangeLinkTests.cs`

**Interfaces:**
- Consumes: Task 1, Task 2 (`PasswordAttemptPurpose.EmailChange`).
- Produces:
  - `internal sealed class EmailChangeLink`, đăng ký DI scoped:
    - constructor nhận `IUnitOfWork`, `IEmailSender`, `IOptions<EmailSettings>`, `IDateTimeProvider`, `ILogger<EmailChangeLink>`;
    - `public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24)`;
    - `Task IssueAsync(Account account, CancellationToken ct)`: `Consume()` mọi token `ChangeEmail` còn hoạt động của tài khoản, thêm token mới (`Purpose = ChangeEmail`, `Email = account.PendingEmail`), `SaveChangesAsync`, rồi gửi thư. Lỗi gửi thì log `"Failed to send email change link to account {AccountId}"` và không ném lại;
    - `Task ConsumeAllAsync(Guid accountId, EmailVerificationPurpose? purpose, CancellationToken ct)`: `null` nghĩa là mọi loại; không gọi `SaveChanges`.
  - `UpdateProfileRequest` thêm `[MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "FieldMaxLength")] string? CurrentPassword`.
  - `ProfileDto` thêm tham số cuối `string? PendingEmail = null`.
  - `ApplicationMessages.EmailChangeTemporarilyLocked`. Chữ: vi "Tạm khoá đổi email, thử lại sau.", en "Email change is temporarily locked. Try again later."

- [ ] **Step 1: Viết test Application thất bại**

```csharp
UpdateCurrentProfileAsync_EmailChangedWithCorrectPassword_SetsPendingSendsLinkKeepsEmail
// Email giữ "a@x.com", PendingEmail "b@x.com"; IEmailSender.SendAsync tới "b@x.com", thân chứa username và "Nếu không phải bạn, hãy bỏ qua thư này"; ClearAsync(EmailChange)
UpdateCurrentProfileAsync_EmailChangedWithoutPassword_Throws400AndReservesNothing
// currentPassword null, "", "   " → DomainException FieldRequired… (400); ReserveAttemptAsync DidNotReceive; SendAsync DidNotReceive; Name không đổi
UpdateCurrentProfileAsync_WrongPassword_Throws401AndSavesNothing
// UnauthorizedException InvalidCurrentPassword; PendingEmail null; không token, không thư, các trường khác không đổi
UpdateCurrentProfileAsync_WrongPasswordAtMaxAttempt_Locks
// LockAsync(EmailChange, …, CancellationToken.None)
UpdateCurrentProfileAsync_Locked_ThrowsTemporarilyLockedEvenWithCorrectPassword
// ReserveAttemptAsync trả null → EmailChangeTemporarilyLocked; PasswordHasher.Verify DidNotReceive
UpdateCurrentProfileAsync_WrongPasswordAndAddressTaken_Throws401Not409
UpdateCurrentProfileAsync_AddressIsOtherAccountsEmail_Throws409
UpdateCurrentProfileAsync_AddressIsOtherAccountsPendingEmail_Succeeds
UpdateCurrentProfileAsync_NoPasswordAccount_ChangesWithoutAsking
// PasswordHash null → không ReserveAttemptAsync, PendingEmail đặt
UpdateCurrentProfileAsync_EmailUnchangedIgnoringCaseAndSpaces_NoPasswordNoLink   // Review Focus 1
UpdateCurrentProfileAsync_EmailEqualsPending_NoNewLinkOldLinkKept
UpdateCurrentProfileAsync_ThirdAddressWhilePending_AsksPasswordAgainAndConsumesOldLink
UpdateCurrentProfileAsync_SendFails_StillSavesPendingAndLogsOnlyAccountId          // Review Focus 5
UpdateCurrentProfileAsync_LoginAndPasswordChangeCountersUntouched
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~EmailChangeLinkTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt `UpdateCurrentProfileAsync` theo TECH00 §6.7 bước 0–1**

Thứ tự trong `UpdateCurrentProfileAsync` là bắt buộc:
1. Chuẩn hoá `request.Email`. `emailChanges` = địa chỉ khác cả `Email` lẫn `PendingEmail`.
2. Nếu `emailChanges` và `PasswordHash != null`:
   - `currentPassword` trống hoặc chỉ có khoảng trắng → 400 với `code == "FieldRequired"`. Dùng cùng key resx `FieldRequired` mà DataAnnotations đang dùng, ném qua `DomainException("FieldRequired")`. Test API ở Task 4 ghim mã này. Kiểm việc này trước khi gọi `ReserveAttemptAsync`.
   - Rồi kiểm mật khẩu theo đúng mẫu của `ChangePasswordAsync` (L130–179), với `PasswordAttemptPurpose.EmailChange` và `EmailChangeTemporarilyLocked`.
3. Kiểm trùng `Email` của tài khoản khác → 409. Không so với `PendingEmail` của ai.
4. Gọi `account.Update(new AccountParams(request.Name, account.Username, account.Email, …))`. Email luôn là `account.Email` hiện tại.
5. Nếu `emailChanges` thì gọi `account.RequestEmailChange(email)` rồi `emailChangeLink.IssueAsync(account, ct)`. `IssueAsync` gồm cả lần save. Nếu không thì `SaveChangesAsync`.

Thư đổi email:
- tiêu đề `"Xác nhận đổi địa chỉ email"`;
- thân HTML tiếng Việt viết cứng, theo mẫu `RegistrationService.IssueVerificationTokenAsync`, mọi giá trị qua `WebUtility.HtmlEncode`;
- thân có tên đăng nhập, link, câu "Link có hiệu lực trong 24 giờ." và câu "Nếu không phải bạn, hãy bỏ qua thư này."

`AccountMapper` thêm `PendingEmail`.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(profile): require a confirmed link and the current password to change the email`

### Task 4: API-30 *Gửi lại*, API-31 *Huỷ*, giới hạn tần suất

**Files:**
- Modify:
  - `AccountService.cs`
  - `backend/src/FeedbackHub.API/Controllers/ProfileController.cs`
  - `ApplicationMessages.cs`
  - `Messages.resx`, `Messages.en.resx`
- Test: `AccountServiceTests.cs`, `backend/tests/FeedbackHub.API.Tests/Controllers/ProfileControllerTests.cs`

**Interfaces:**
- Consumes: Task 3 (`EmailChangeLink`).
- Produces:
  - `Task ResendEmailChangeAsync(CancellationToken ct)`: không có `PendingEmail` thì `ConflictException(EmailChangeNotPending)`, có thì gọi `IssueAsync`.
  - `Task CancelEmailChangeAsync(CancellationToken ct)`: `CancelEmailChange()`, rồi `ConsumeAllAsync(id, ChangeEmail)`, rồi save. Không có email chờ cũng không lỗi.
  - Route:
    - `POST api/profile/email/resend` trả 204, `[EnableRateLimiting("auth")]`;
    - `DELETE api/profile/email/pending` trả 204;
    - `PUT api/profile` thêm `[EnableRateLimiting("auth")]`.
  - `ApplicationMessages.EmailChangeNotPending`. Chữ: vi "Không có email nào đang chờ xác nhận.", en "There is no email change waiting for confirmation."

- [ ] **Step 1: Viết test thất bại**

```csharp
ResendEmailChangeAsync_Pending_ConsumesOldIssuesNew_NoPasswordAsked
ResendEmailChangeAsync_NothingPending_Throws409
CancelEmailChangeAsync_ClearsPendingAndConsumesChangeTokens
CancelEmailChangeAsync_NothingPending_Succeeds
// API
Resend_Anonymous_Returns401 ; Cancel_Anonymous_Returns401 ; Update_Anonymous_Returns401
Update_WrongCurrentPassword_Returns401WithCode   // body.code == "InvalidCurrentPassword"
Update_MissingCurrentPassword_Returns400
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests backend/tests/FeedbackHub.API.Tests -m:1 --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~ProfileControllerTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** hai action và hai phương thức service.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(profile): resend or cancel a pending email change`

### Task 5: API-32 xác nhận đổi email, siết `verify-email` và `resend-verification`

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Auth/RegistrationService.cs`
  - `backend/src/FeedbackHub.API/Controllers/AuthController.cs`
  - `ApplicationMessages.cs`
  - `Messages.resx`, `Messages.en.resx`
  - `.claude/rules/authentication.md` (danh sách `[AllowAnonymous]`)
- Create: `backend/src/FeedbackHub.Application/Services/Auth/ConfirmEmailChangeRequest.cs`
- Test:
  - `backend/tests/FeedbackHub.Application.Tests/Services/Auth/RegistrationServiceTests.cs`
  - `backend/tests/FeedbackHub.API.Tests/Controllers/AuthControllerTests.cs`
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/EmailChangeConcurrencyTests.cs`

**Interfaces:**
- Consumes: Task 3 (`EmailChangeLink.ConsumeAllAsync`).
- Produces:
  - `record ConfirmEmailChangeRequest([Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Token)`.
  - `Task ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct)`.
  - `POST api/auth/confirm-email-change`:
    - `[AllowAnonymous]`, `[EnableRateLimiting("auth")]`;
    - trả 204;
    - không đặt cookie, không trả token.
  - `ApplicationMessages.EmailChangeLinkNoLongerValid`. Chữ: vi "Liên kết đổi email không còn hiệu lực.", en "This email change link is no longer valid."
  - `IssueVerificationTokenAsync` ghi `Purpose = VerifyEmail`, `Email = account.Email`.

- [ ] **Step 1: Viết test thất bại**

```csharp
ConfirmEmailChangeAsync_ValidLink_AppliesChangeAndConsumesAllTokens
// Email == pending, EmailConfirmed true, PendingEmail null; mọi token VerifyEmail và ChangeEmail còn lại ConsumedAt != null
ConfirmEmailChangeAsync_UnknownUsedExpiredOrVerifyEmailToken_Throws409NoLongerValid
// bốn trường hợp; "hết hạn" dùng IDateTimeProvider giả UtcNow = ExpiresAt + 1 phút
ConfirmEmailChangeAsync_PendingChangedSinceLink_Throws409NoLongerValid
ConfirmEmailChangeAsync_TokenWithoutEmail_Throws409NoLongerValid
ConfirmEmailChangeAsync_AddressTakenMeanwhile_Throws409AccountEmailAlreadyExists_OtherAccountUntouched
ConfirmEmailChangeAsync_SecondOpenOfSameLink_Throws409                        // Review Focus 2
VerifyEmailAsync_ChangeEmailToken_Throws401NoSession
VerifyEmailAsync_TokenEmailDiffersFromAccountEmail_Throws401
VerifyEmailAsync_LegacyTokenWithoutEmail_Throws401
ResendVerificationEmailAsync_AccountWithoutPassword_SendsNothing
// API
ConfirmEmailChange_Valid_Returns204WithoutSetCookieOrBody
ConfirmEmailChange_IsTheOnlyNewAnonymousAction   // khẳng định theo cùng cách AuthControllerTests đang ghim danh sách ẩn danh
// Infrastructure (Testcontainers)
ConfirmEmailChange_TwoAccountsSameAddressConcurrently_OneSucceedsOtherConflict // Review Focus 3
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~RegistrationServiceTests|FullyQualifiedName~AuthControllerTests|FullyQualifiedName~EmailChangeConcurrencyTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt theo TECH00 §6.7 bước 2 và §4.9**
  - Không thấy token, token đã dùng, hết hạn (`IsActiveAt(clock.UtcNow)`), `Purpose != ChangeEmail`, `Email == null` hoặc `PendingEmail != token.Email` → cùng một `ConflictException(EmailChangeLinkNoLongerValid)`.
  - Kiểm trùng `Email` → `ConflictException(AccountEmailAlreadyExists)`.
  - Rồi `ApplyEmailChange()`, `ConsumeAllAsync(id, null)`, save.
  - Lỗi vi phạm unique của chỉ mục `Email` khi save ánh xạ thành `ConflictException(AccountEmailAlreadyExists)`. Repo chưa có cách ánh xạ nào, nên thêm vào `SaveChangesAsync` của unit of work ở Infrastructure:
    - bắt `DbUpdateException` có inner `PostgresException`, `SqlState == "23505"` và `ConstraintName` là tên chỉ mục unique `Email` của `accounts` (lấy từ snapshot model);
    - ném `ConflictException(ApplicationMessages.AccountEmailAlreadyExists)`;
    - mọi vi phạm khác ném lại nguyên trạng.
  - `verify-email` chỉ nhận token `Purpose` là `VerifyEmail` hoặc `null`, có `Email` bằng `Account.Email`. Mọi trường hợp khác trả 401 `EmailVerificationTokenInvalidOrExpired`.
  - `resend-verification`: tài khoản `PasswordHash == null` thì return như email không tồn tại.
  - Thêm `POST api/auth/confirm-email-change` vào danh sách `[AllowAnonymous]` của `.claude/rules/authentication.md` (TECH00 §10.2, nghĩa vụ N-new-3).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(auth): confirm email changes by link and bind verification tokens to their address`

### Task 6: `TokenIssuer` kiểm `Status` một chỗ (N-new-4)

**Files:**
- Modify: `backend/src/FeedbackHub.Application/Services/Auth/TokenIssuer.cs`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Auth/TokenIssuerTests.cs`, `AuthControllerTests.cs`

**Interfaces:**
- Produces: khi `account.Status == false`, `IssueTokensAsync` ném `UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword)`. Đây là đúng key mà `LoginAsync` (L39) đang dùng, nên không lộ thêm trạng thái tài khoản. Kiểm trước khi tạo refresh token.

- [ ] **Step 1: Viết test thất bại**

```csharp
IssueTokensAsync_LockedAccount_Throws401AndCreatesNoRefreshToken
// API, cùng một tài khoản đặt Status = false sau khi có phiên/token:
Login_LockedAccount_Returns401 ; Refresh_LockedAccount_Returns401 ; VerifyEmail_LockedAccount_Returns401
ExternalLogin_LinkedLockedAccount_Returns401 ; SwitchOrganization_LockedAccount_Returns401
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests -m:1 --filter "FullyQualifiedName~TokenIssuerTests|FullyQualifiedName~AuthControllerTests"`
Expected: FAIL (verify-email, external).

- [ ] **Step 3: Cài đặt**

Thêm kiểm `Status` vào `IssueTokensAsync`. Kiểm `Status` riêng ở `LoginAsync` và refresh giữ nguyên làm lưới. `register-with-invitation` (phần 5) sẽ tự đi qua `TokenIssuer`.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(auth): refuse every session for a locked account in TokenIssuer`

### Task 7: Đọc token Microsoft bằng claim thô (B2)

**Files:**
- Modify: `backend/src/FeedbackHub.Infrastructure/Services/Auth/External/IMicrosoftJwtValidator.cs`, `backend/src/FeedbackHub.Infrastructure/Services/Auth/External/MicrosoftAuthProvider.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Auth/External/MicrosoftJwtValidatorTests.cs`, `MicrosoftAuthProviderTests.cs`

**Interfaces:**
- Produces:
  - `record MicrosoftTokenPayload(string Subject, string? Email, string? Name, string TenantId, bool EmailDomainOwnerVerified)`.
  - `MicrosoftAuthProvider` trả `ExternalUserInfo(payload.Subject, email, payload.Name, EmailVerified: payload.TenantId == "9188040d-6c67-4c5b-b112-36a304b66dad" || payload.EmailDomainOwnerVerified)`.
  - Để test tiêm được khoá ký và cấu hình OIDC thật, validator nhận chúng qua một seam: overload hoặc constructor nhận `OpenIdConnectConfiguration`. Không mock bộ kiểm.

- [ ] **Step 1: Viết test thất bại**

Ký token cục bộ bằng khoá RSA của test, rồi gọi `ValidateAsync` thật:

```csharp
ValidateAsync_PersonalTenant_ReturnsTenantId
ValidateAsync_XmsEdov_BoolTrue_StringTrue_StringTRUE_AreVerified
ValidateAsync_XmsEdov_BoolFalse_OtherString_Missing_AreNotVerified
ValidateAsync_EmailMissingButPreferredUsernamePresent_EmailIsNull
ValidateAsync_MissingTid_Throws ; ValidateAsync_IssuerTenantDiffersFromTid_Throws
ValidateAsync_MissingSubEvenWithOid_Throws ; ValidateAsync_HasOidAndSub_SubjectIsSub
// Provider
ValidateAsync_PersonalTenant_EmailVerifiedTrue ; ValidateAsync_WorkTenantWithoutXmsEdov_EmailVerifiedFalse
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~Microsoft"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt theo TECH00 §6.6 bước 1–2**
  - Đặt `MapInboundClaims = false`, hoặc dùng `JsonWebTokenHandler`.
  - Đọc `sub`, `email`, `tid`, `xms_edov` theo tên thô. Bỏ `oid`, `preferred_username`, `ClaimTypes.NameIdentifier`.
  - Thiếu `sub` hoặc `tid` → `SecurityTokenException`.
  - Phần tenant của `iss` phải bằng `tid`: kiểm sau `IssuerValidator`, so chuỗi không phân biệt hoa thường.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(auth): read raw Microsoft claims and trust only verified emails`

### Task 8: `ExternalLoginAsync` chuyển kiểm xác minh xuống đúng nhánh, xác minh lại tài khoản M8

**Files:**
- Modify: `backend/src/FeedbackHub.Application/Services/Auth/AuthService.cs` (khoảng L201–L272), `.claude/decisions.md`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: Task 6 (`TokenIssuer` kiểm `Status`), Task 7 (`EmailVerified`).

- [ ] **Step 1: Viết test thất bại**

```csharp
ExternalLoginAsync_LinkedBySub_UnverifiedToken_SignsIn
ExternalLoginAsync_LinkedAccountUnconfirmed_VerifiedTokenSameEmail_ConfirmsEmail
ExternalLoginAsync_LinkedAccountUnconfirmed_DifferentEmailOrUnverified_StaysUnconfirmed
ExternalLoginAsync_EmailMatch_UnverifiedToken_ThrowsNotVerifiedByProviderAndNoLink
ExternalLoginAsync_EmailMatch_AccountUnconfirmed_ThrowsEmailNotConfirmedAndNoLink
ExternalLoginAsync_EmailMatch_LockedAccount_Throws401AndNoLink
ExternalLoginAsync_NewAccount_UnverifiedToken_ThrowsAndCreatesNothing
ExternalLoginAsync_TokenEmailEqualsSomeonesPendingEmail_DoesNotMatchThatAccount   // Review Focus 4
ExternalLoginAsync_Google_KeepsEmailVerifiedRuleOnBothBranches
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Application.Tests -m:1 --filter "FullyQualifiedName~AuthServiceTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt theo TECH00 §6.6 bước 3–4**
  - Bỏ kiểm ở đầu hàm (L214).
  - Nhánh đã liên kết: không kiểm `EmailVerified`. Nếu `!EmailConfirmed && EmailVerified` và email của token bằng `Account.Email` (đã chuẩn hoá) thì gọi `ConfirmEmail()`.
  - Nhánh khớp email, theo thứ tự:
    1. token chưa xác minh → `ExternalLoginEmailNotVerifiedByProvider`;
    2. tài khoản chưa xác minh → `ExternalLoginEmailNotConfirmed`;
    3. tài khoản bị khoá → 401.

    Cả ba trường hợp đều xảy ra trước khi tạo liên kết.
  - Nhánh tạo mới: token chưa xác minh → `ExternalLoginEmailNotVerifiedByProvider`.
  - Thêm vào `.claude/decisions.md` một mục mới có `Supersedes:` mục 2026-07-25 *Microsoft external login: multi-tenant issuer validated by regex, no email_verified check*.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(auth): check provider email verification only where an email is matched or an account is created`

### Task 9: M8, đánh dấu chưa xác minh các tài khoản chỉ có email Microsoft theo cách đọc cũ

**Files:**
- Create: `backend/src/FeedbackHub.Infrastructure/Migrations/<timestamp>_ReverifyMicrosoftOnlyAccounts.cs`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/Migrations/ReverifyMicrosoftOnlyAccountsTests.cs`

**Interfaces:**
- Produces: `public const string UpSql`, theo mẫu `20261016090000_NormalizeAccountIdentity.cs`. `Up` gọi `migrationBuilder.Sql(UpSql)`, `Down` để trống.

```sql
DO $$
DECLARE ids uuid[];
BEGIN
  WITH changed AS (
    UPDATE accounts a SET "EmailConfirmed" = false
    WHERE a."EmailConfirmed" = true AND a."PasswordHash" IS NULL
      AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
      AND NOT EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
    RETURNING a."Id")
  SELECT array_agg("Id") INTO ids FROM changed;
  RAISE NOTICE 'M8 marked % account(s) unverified: %', coalesce(array_length(ids, 1), 0), ids;
END $$;
```

Trước khi chép, kiểm lại tên cột thật của `external_logins` và `accounts` trong snapshot model (`account_id`, `provider`, `"Id"`, `"EmailConfirmed"`, `"PasswordHash"`).

- [ ] **Step 1: Viết test thất bại (Testcontainers)**

```csharp
UpSql_MarksOnlyPasswordlessMicrosoftWithoutGoogle
// seed 5 tài khoản: MS-only không mật khẩu (đổi); MS + mật khẩu; MS + Google; Google-only; MS-only đã false → chỉ cái đầu đổi
UpSql_RunTwice_ChangesNothingMore
UpSql_Notice_ContainsNoEmailOrUsername
ReviewQueries_Section10_2Step6_RunOnNewSchemaAndReturnSeededRows
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~ReverifyMicrosoftOnlyAccountsTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt migration**

Đặt các câu truy vấn §10.2 bước 6 làm hằng trong lớp test để ghim chúng.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(persistence): mark Microsoft-only accounts unverified after the claim fix (M8)`

### Task 10: `SmtpEmailSender` không ghi người nhận, một hạn 15 giây

**Files:**
- Modify: `backend/src/FeedbackHub.Infrastructure/Services/Email/SmtpEmailSender.cs`, lớp `EmailSettings` (giữ đúng chỗ hiện có), `appsettings.Example.json`
- Test: `backend/tests/FeedbackHub.Infrastructure.Tests/Services/Email/SmtpEmailSenderTests.cs`

**Interfaces:**
- Produces: `EmailSettings.SendTimeoutSeconds` (int, mặc định 15).

- [ ] **Step 1: Viết test thất bại**

Dùng `ISmtpClientFactory` giả. Client treo bằng `Task.Delay(Timeout.Infinite, ct)` ở từng bước. Đặt `SendTimeoutSeconds = 1`.

```csharp
SendAsync_Failure_LogsWithoutRecipient            // không entry log nào chứa địa chỉ người nhận
SendAsync_HangsAtConnect_StopsWithinTotalTimeout   // < 3 s
SendAsync_HangsAtAuthenticate_StopsWithinTotalTimeout
SendAsync_HangsAtSend_StopsWithinTotalTimeout
SendAsync_HangsAcrossSteps_TotalNotPerStep         // connect 0.6 s + auth 0.6 s → hết hạn ở auth
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests -m:1 --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: FAIL.

- [ ] **Step 3: Cài đặt**
  - Tạo một `CancellationTokenSource.CreateLinkedTokenSource(ct)` và gọi `CancelAfter(TimeSpan.FromSeconds(SendTimeoutSeconds))`.
  - Truyền token đó cho `ConnectAsync`, `AuthenticateAsync`, `SendAsync`.
  - `DisconnectAsync` trong `finally` dùng `CancellationToken.None` với hạn ngắn riêng.
  - Dòng log đổi thành `"Failed to send email"`, không có tham số người nhận.
  - Thêm `"SendTimeoutSeconds": 15` vào `appsettings.Example.json`. Không mở các `appsettings*.json` khác.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2.
Expected: PASS.

- [ ] **Step 5: Commit** `fix(email): drop recipients from SMTP logs and bound each send to one timeout`

### Task 11: Contract và frontend (hồ sơ, trang xác nhận đổi email)

**Files:**
- Modify:
  - `shared/openapi/openapi.json` (export)
  - `frontend/src/components/ProfileDialog.vue`
  - `frontend/src/router/index.ts`
  - `frontend/src/locales/vi.ts`, `frontend/src/locales/en.ts`
- Create: `frontend/src/views/ConfirmEmailChangeView.vue`
- Test: `frontend/src/components/__tests__/ProfileDialog.spec.ts` (theo chỗ test hiện có của component), `frontend/src/views/__tests__/ConfirmEmailChangeView.spec.ts`

**Interfaces:**
- Consumes: API-29–API-32 (Task 3–5), qua client orval sinh lại: `profileUpdate`, `profileResendEmailChange`, `profileCancelEmailChange`, `authConfirmEmailChange`. Dùng đúng tên orval sinh ra.
- Produces:
  - Route `/confirm-email-change`: không có `meta`, công khai như `/verify-email`.
  - Khoá locale:
    - `profile.pendingEmail`:
      - vi "Đang chờ xác nhận: {email}. Mở link trong thư để đổi."
      - en "Waiting for confirmation: {email}. Open the link in the email to switch."
    - `profile.resendEmailChange` (vi "Gửi lại", en "Resend").
    - `profile.cancelEmailChange` (vi "Huỷ", en "Cancel").
    - `profile.currentPassword` (vi "Mật khẩu hiện tại", en "Current password").
    - `profile.emailChangeTemporarilyLocked`:
      - vi "Tạm khoá đổi email, thử lại sau."
      - en "Email change is temporarily locked. Try again later."
    - `confirmEmailChange.success` (vi "Đã đổi email.", en "Your email has been changed.").
    - `confirmEmailChange.noLongerValid`:
      - vi "Liên kết đổi email không còn hiệu lực."
      - en "This email change link is no longer valid."
    - `confirmEmailChange.alreadyTaken`: dùng lại chữ của `AccountEmailAlreadyExists` nếu locale đã có, nếu chưa có thì thêm.

- [ ] **Step 1: Export contract và sinh lại client**

Run:
- `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
- `bun run --cwd frontend codegen`

Expected: `openapi.json` có `currentPassword`, `pendingEmail`, ba route mới.

- [ ] **Step 2: Viết test frontend thất bại (vitest + MSW)**

```ts
it('shows the pending line with Resend and Cancel when pendingEmail is set')
it('shows Current password only after the email field changes and hasPassword is true')
it('never shows Current password when hasPassword is false')
it('maps InvalidCurrentPassword, FieldRequired and EmailChangeTemporarilyLocked under the password field')
it('Resend calls the resend endpoint; Cancel calls the cancel endpoint and hides the line')
// ConfirmEmailChangeView
it('removes the token from the address bar before calling the API')   // history.replaceState
it('shows success on 204, noLongerValid on EmailChangeLinkNoLongerValid, alreadyTaken on AccountEmailAlreadyExists')
it('reloads the profile when signed in')
```

- [ ] **Step 3: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- ProfileDialog ConfirmEmailChangeView`
Expected: FAIL.

- [ ] **Step 4: Cài đặt**
  - Dòng chờ xác nhận là chữ thường, kèm hai nút chữ. Không mở hộp thoại.
  - Ô *Mật khẩu hiện tại* nằm ngay trong form.
  - Mã lỗi so bằng `error.problem.code`, như cách `ProfileDialog.vue` đang làm ở L225.

- [ ] **Step 5: Chạy test, type-check, phải qua**

Run:
- `bun run --cwd frontend test:run`
- `bun run --cwd frontend build-only`
- `bun run --cwd frontend type-check`

Expected: PASS.

- [ ] **Step 6: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [ ] **Step 7: Commit** `feat(profile): show pending email changes and confirm them by link`

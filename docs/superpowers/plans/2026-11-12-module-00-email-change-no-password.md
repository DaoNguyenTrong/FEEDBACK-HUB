# Module 00: No-Password Email Change Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An account without a password confirms an email change at its current address (button press on a page, never on page load) before any mail goes to the new address. An unverified one cannot start a change.

**Architecture:** The existing email-change machinery (`Account.PendingEmail`, `EmailVerificationToken`, `EmailChangeLink`, API-29–32) gains a stage column (`PendingEmailStage`), a token purpose (`ConfirmOldEmail`), a per-account row lock (`IAccountLock`), a mail cap, and two anonymous endpoints (API-34 confirm, API-35 read-only lookup). Every state change runs in one transaction behind the lock. Mail goes out after commit.

**Tech Stack:** .NET 10 (Clean Architecture, EF Core/PostgreSQL, xUnit + NSubstitute, Testcontainers), Vue 3 + naive-ui + vue-i18n, Vitest + MSW.

**Spec:**
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.6 (Approved 2026-11-12): FR-67, FR-69, BR-82, BR-85 (points 1–13), D21–D26, AC-342–358, AC-376–404.
- `docs/tech/00-nen-tang.md` v1.4 (Approved 2026-11-12, appsec: đạt): §3.8, §4.9 API-29–35, §5, §6.6 step 9, §6.7 steps 0–12, §7.2 T20–T25, §8.2, §10.1 M10, §10.2, §11 (rows "Đổi email của tài khoản không có mật khẩu" through "Danh sách gọi `Account.SetPasswordHash`"), §12 QĐ-37–45.
- Boards: `ProfileEmailNoPwLight/Dark` on https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn.

Where this plan says "per TECH00 §x", that section is the requirement: read it, don't guess.

**Approval:** solution-architect (thay người dùng), 2026-11-12, after one round of fixes. Approving the plan does not authorize a push; the user authorized pushing PR #32 updates on 2026-11-12.

**Branch and PR:** `feature/module-00`; this work lands in the open PR #32. Implementers commit locally, one commit per task, explicit paths only, and never push. The main session runs `scripts/test-gate.sh` and pushes. `.claude/agents/code-reviewer.md` has an unrelated uncommitted change: never stage it.

**Review:** every task here is security-critical (tokens, account lock, anonymous endpoints, account takeover). Each task gets its own reviewer on `opus`. Tasks are never batched. The implementer runs on `sonnet`. Mutation checks (TECH00 §11, targets a–g) run once in Task 7.

## Global Constraints

- Lifetime of every email-change link: `EmailChangeLink.Lifetime` (24 hours, already exists). Mail cap: `EmailChangeLink.MaxMailsPerHour = 5`, a constant (no setting), counted from `email_verification_tokens` rows with `Purpose ∈ {ChangeEmail, ConfirmOldEmail}` and `CreatedAt` in the last hour, under the account lock (QĐ-40).
- Error codes (each key goes in `ApplicationMessages`/`DomainMessages` + `Messages.resx` + `Messages.en.resx`, and the frontend maps them to `locales/{vi,en}.ts`):
  - `EmailNotConfirmed`: exists; 403 via `ForbiddenException`. The Domain safety net uses a `DomainMessages.EmailNotConfirmed` key.
  - `EmailChangeTooManyRequests`: new; 429.
  - `EmailChangeLinkNoLongerValid`: exists; 409. The body must be identical for every failure reason on API-34 and API-35.
  - `EmailChangeNotPending`: exists.
- Vietnamese copy, verbatim (write `en` equivalents yourself):
  - Pending line, stage 1: `Đang chờ xác nhận từ email hiện tại: mở link trong thư gửi tới {current} để tiếp tục đổi sang {pending}.`
  - Pending line, stage 2 (existing): `Đang chờ xác nhận: {pending}. Mở link trong thư để đổi.`
  - 403 under the email field: `Email hiện tại chưa được xác minh. Hãy đăng xuất rồi đăng nhập lại bằng Google hoặc Microsoft với email này, sau đó đổi email.`
  - 429 under the email field: `Bạn đã gửi quá nhiều yêu cầu đổi email. Hãy thử lại sau một giờ.`
  - Hint under the email field (no-password, verified account, while editing the email): `Chúng tôi sẽ gửi thư xác nhận tới {current} trước.`
  - Confirm page: heading `Đổi email sang {pending}?`, line `Bấm Xác nhận, chúng tôi sẽ gửi thư tới email này để bạn hoàn tất.`, button `Xác nhận`.
  - Stage-1 mail: subject `Xác nhận yêu cầu đổi địa chỉ email`. The body names the username and the new address. It includes the link `{FrontendBaseUrl}/#/confirm-current-email?token=<raw>`, the sentence `Nếu bạn bấm Xác nhận trên trang này, tài khoản sẽ chuyển sang địa chỉ {pending}.`, `Link có hiệu lực trong 24 giờ.` and `Nếu không phải bạn, hãy bỏ qua thư này.`. Every interpolated value goes through `WebUtility.HtmlEncode`, as in the existing `EmailChangeLink` mail.
- Logs carry only `accountId`: no email, no token (TECH00 §7.3).
- API-34 and API-35 never issue a session, never read or set a cookie, and never take `TokenIssuer`, `IHttpContextAccessor` or device info. API-35 writes nothing and sends `Cache-Control: no-store`.
- No new audit event type (QĐ-42). `PendingEmailStage` joins the automatic `accounts` snapshot (it must not appear in `AuditExcludedProperties`).

## Review Focus

1. **A mail scanner (or link preview) opens the stage-1 link:** nothing on the server changes until the button is pressed. Pinned in Task 6 (no API-34 call on mount, with fake timers flushed) and Task 5 (API-35 changes no row).
2. **A double click, or two tabs pressing Xác nhận at once:** exactly one 204 and one mail to the new address. Pinned in Task 6 (one call on a double click) and Task 5 (two parallel API-34 calls under Testcontainers).
3. **A stage-1 link opened after the user switched to a third address (g3) or cancelled:** 409, and no mail to anyone. Pinned in Task 4.
4. **A deploy while old instances still create `PendingEmail` with a `null` stage:** API-32 refuses a `null` stage (409), and the user can cancel and retry. M10 clears pending changes of every password-less account. Pinned in Task 2 (M10 test) and Task 5 (API-32 with a `null` stage → 409).
5. **The cap is reached partway through a burst:** request six → 429, state unchanged, no mail. Five tokens older than one hour do not count. Refusals for an unverified email (403) do not count. Pinned in Task 4.

---

### Task 1: Domain state machine for a two-stage email change

**Files:**
- Create: `backend/src/FeedbackHub.Domain/Entities/EmailChangeStage.cs`
- Modify: `backend/src/FeedbackHub.Domain/Entities/EmailVerificationPurpose.cs`, `backend/src/FeedbackHub.Domain/Entities/Account.cs:77-105`, `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs`, `backend/src/FeedbackHub.Application/Resources/Messages.resx`, `Messages.en.resx`
- Test: `backend/tests/FeedbackHub.Domain.Tests/Entities/AccountTests.cs`, `EmailVerificationTokenTests.cs`

**Interfaces:**
- Produces:
  - `enum EmailChangeStage { AwaitingCurrentEmail, AwaitingNewEmail }`.
  - `EmailVerificationPurpose.ConfirmOldEmail`, appended after `ChangeEmail`.
  - `Account.PendingEmailStage : EmailChangeStage?` (private set).
  - `EmailChangeStage Account.RequestEmailChange(string email)`.
  - `void Account.ConfirmCurrentEmail()`.
  - `ApplyEmailChange()` and `CancelEmailChange()` with new guards.
  - `DomainMessages.EmailNotConfirmed`.

- [ ] **Step 1: Write failing tests in `AccountTests`.** Use `// RequestEmailChange`, `// ConfirmCurrentEmail`, `// ApplyEmailChange` and `// CancelEmailChange` sections:
  - `RequestEmailChange_StageByPasswordAndConfirmation_ReturnsExpectedStage` (`[Theory]`):
    - (no hash, confirmed) → `AwaitingCurrentEmail`
    - (hash, confirmed) → `AwaitingNewEmail`
    - (hash, unconfirmed) → `AwaitingNewEmail`
    - Each case asserts the returned value, `PendingEmailStage` and `PendingEmail`.
  - `RequestEmailChange_NoPasswordUnconfirmed_ThrowsEmailNotConfirmed`: `DomainAssert` with `DomainMessages.EmailNotConfirmed`; `PendingEmail` and `PendingEmailStage` stay `null`.
  - `ConfirmCurrentEmail_AtStageOne_MovesToStageTwo`.
  - `ConfirmCurrentEmail_AtStageTwoOrNothingPending_ThrowsEmailChangeNotPending` (`[Theory]`).
  - `ApplyEmailChange_AtStageOne_ThrowsEmailChangeNotPending`: `Email` is unchanged. This is mutation target (c).
  - `ApplyEmailChange_AtStageTwo_SetsEmailConfirmsAndClearsBoth`.
  - `CancelEmailChange_AnyStage_ClearsPendingEmailAndStage`.
  - Invariant `PendingEmailStage is null ⇔ PendingEmail is null`: assert it in each test above.
  - `RequestEmailChange` on a pending account moves to the stage chosen now (AC-393: (no hash, confirmed) at stage 2 → back to stage 1).
  - Build accounts via `Account.Create`, `SetPasswordHash`, `ConfirmEmail`.
- [ ] **Step 2: Run** `dotnet test backend/tests/FeedbackHub.Domain.Tests --no-restore -m:1`. Expected: FAIL (compile errors on the missing members).
- [ ] **Step 3: Implement** per TECH00 §3.8.
  - The stage is chosen only inside `RequestEmailChange`; nothing re-reads `PasswordHash` later (D21).
  - `ApplyEmailChange` requires `AwaitingNewEmail` and clears both fields.
  - `EmailVerificationToken.Create` must accept `ConfirmOldEmail` with a non-null `Email`. Add a case to `EmailVerificationTokenTests` if `Create` validates the purpose.
  - Add the `EmailNotConfirmed` resx text in both files.
- [ ] **Step 4: Run** the Domain tests again. Expected: PASS. Then run `backend/scripts/test.sh` once; existing Application/API tests that call `RequestEmailChange` may need the returned stage ignored. Expected: PASS.
- [ ] **Step 5: Commit** `feat(accounts): add a two-stage email change to the account`, staging only the files above.

### Task 2: Account lock, stage column and migration M10

**Files:**
- Create:
  - `backend/src/FeedbackHub.Application/Services/Accounts/IAccountLock.cs`
  - `backend/src/FeedbackHub.Infrastructure/Persistence/AccountLock.cs`
  - a migration `AddAccountPendingEmailStage` (M10) under `backend/src/FeedbackHub.Infrastructure/Migrations/`
- Modify:
  - `backend/src/FeedbackHub.Infrastructure/Persistence/PersistenceExtensions.cs` (register next to `IOrganizationLock`, line ~45)
  - the `Account` EF configuration: map `PendingEmailStage` as a string, `varchar(30)`, nullable, the same way `Purpose` is stored
- Test:
  - `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/AccountLockTests.cs` (create, modelled on `OrganizationLockTests.cs`)
  - `AccountPersistenceTests.cs`
  - a M10 test under `Persistence/Migrations/` (follow the existing migration tests there)
  - `AuditExcludedPropertiesTests.cs`, if it lists audited columns

**Interfaces:**
- Produces: `interface IAccountLock { Task AcquireAsync(Guid accountId, CancellationToken cancellationToken); }`. It has the same contract and XML doc as `IOrganizationLock`: it throws `InvalidOperationException` when no transaction is open, and its SQL is `SELECT 1 FROM accounts WHERE "Id" = {id} FOR UPDATE`.

- [ ] **Step 1: Write failing tests**
  - `AcquireAsync_NoTransaction_Throws` and `AcquireAsync_TwoTransactionsSameAccount_RunOneAfterTheOther`: copy the shape of `OrganizationLockTests`, using the collection those tests use.
  - `PendingEmailStage_RoundTripsAsText`: store and reload, and assert that raw SQL reads the string `'AwaitingCurrentEmail'`.
  - M10 tests (TECH00 §10.1 M10, §11 row "Đồng thời, trần số thư, migration M10"):
    - A password account with `PendingEmail` → stage `AwaitingNewEmail`, and its tokens are untouched.
    - **Every** account with `PasswordHash IS NULL` that has `PendingEmail`, with `EmailConfirmed` either `true` or `false` → `PendingEmail = NULL`, stage `NULL`, and its unused `ChangeEmail` tokens get `ConsumedAt` set.
    - Rows without `PendingEmail` are unchanged.
    - Running the data step twice changes nothing more.
  - The audit snapshot includes `PendingEmailStage`.
- [ ] **Step 2: Run** `dotnet test backend/tests/FeedbackHub.Infrastructure.Tests --no-restore -m:1 --filter "FullyQualifiedName~AccountLock|FullyQualifiedName~AccountPersistence|FullyQualifiedName~Migration"`. Expected: FAIL.
- [ ] **Step 3: Implement**
  - Add the lock and register it.
  - Configure the column.
  - Create the migration with `dotnet ef migrations add AddAccountPendingEmailStage --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API`, then hand-write the data SQL in `Up` in this order: (1) clear password-less pending changes and consume their unused `ChangeEmail` tokens; (2) `UPDATE accounts SET "PendingEmailStage" = 'AwaitingNewEmail' WHERE "PendingEmail" IS NOT NULL AND "PendingEmailStage" IS NULL`.
  - `Down` drops the column only.
- [ ] **Step 4: Run** the same filter. Expected: PASS. Then run `backend/scripts/test.sh` once. Expected: PASS.
- [ ] **Step 5: Commit** `feat(accounts): lock an account row and store the email-change stage`.

### Task 3: EmailChangeLink: create in the transaction, send after commit, count for the cap

**Files:**
- Modify: `backend/src/FeedbackHub.Application/Services/Accounts/EmailChangeLink.cs`, `backend/src/FeedbackHub.API/Common/AppExceptionHttpMapper.cs`, `ApplicationMessages.cs`, both resx files
- Create: `backend/src/FeedbackHub.Domain/Exceptions/TooManyRequestsException.cs` (an `AppException` → 429, written like `ServiceUnavailableException`)
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Accounts/EmailChangeLinkTests.cs`, and the mapper's existing test if there is one

**Interfaces:**
- Consumes: Task 1's `EmailVerificationPurpose.ConfirmOldEmail` and `Account.PendingEmailStage`.
- Produces, replacing `IssueAsync`:
  - `public const int MaxMailsPerHour = 5;`
  - `Task<string> CreateAsync(Account account, CancellationToken ct)`: picks the purpose from `account.PendingEmailStage` (stage 1 → `ConfirmOldEmail`, stage 2 → `ChangeEmail`). It consumes the account's unused `ChangeEmail` and `ConfirmOldEmail` tokens, adds the new token with `Email = PendingEmail`, calls `Repository<Account>().Update(account)`, does **not** save, and returns the raw token.
  - `Task SendAsync(Account account, string rawToken, CancellationToken ct)`: stage 1 → the stage-1 mail to `account.Email`; stage 2 → the existing mail to `account.PendingEmail`. A send failure logs `accountId` only and does not throw.
  - `Task ConsumeAllAsync(Guid accountId, IReadOnlyCollection<EmailVerificationPurpose>? purposes, CancellationToken ct)`: `null` means every purpose, including `VerifyEmail`. It does not save. The `null` caller is API-32 (`RegistrationService.ConfirmEmailChangeAsync`, today at `RegistrationService.cs:156`), which consumes every unused token once the change applies (TECH00 §6.7 step 2); Task 5's full-path test asserts it.
  - `Task EnsureUnderCapAsync(Guid accountId, CancellationToken ct)`: throws `TooManyRequestsException(ApplicationMessages.EmailChangeTooManyRequests)` when `MaxMailsPerHour` or more change tokens were created in the last hour (consumed ones count).
  - `ApplicationMessages.EmailChangeTooManyRequests`.

- [ ] **Step 1: Write failing tests**
  - `CreateAsync_StageOne_CreatesConfirmOldEmailTokenForPendingAddress_DoesNotSave` (`SaveChangesAsync` `DidNotReceive`).
  - `CreateAsync_StageTwo_CreatesChangeEmailToken`.
  - `CreateAsync_ConsumesEarlierLinksOfBothPurposes_KeepsVerifyEmail`.
  - `SendAsync_StageOne_MailsCurrentAddressOnly`: assert the recipient, the subject, and that the body contains the username, the HTML-encoded pending address, the "bấm Xác nhận" sentence and `/#/confirm-current-email?token=`; `IEmailSender` `DidNotReceive` for the pending address.
  - `SendAsync_StageTwo_MailsPendingAddressOnly`.
  - `SendAsync_SenderThrows_LogsAndDoesNotThrow`.
  - `EnsureUnderCapAsync_FiveChangeTokensInLastHour_Throws429Key`.
  - `EnsureUnderCapAsync_FiveTokensOlderThanOneHour_Passes`.
  - `EnsureUnderCapAsync_VerifyEmailTokensDoNotCount`.
  - `ConsumeAllAsync_PurposesGiven_ConsumesOnlyThose`.
  - Keep `Lifetime_Always_Is24Hours`.
- [ ] **Step 2: Run** `dotnet test backend/tests/FeedbackHub.Application.Tests --no-restore -m:1 --filter FullyQualifiedName~EmailChangeLinkTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the methods above and map `TooManyRequestsException` to `(429, "Too Many Requests")` in `AppExceptionHttpMapper`. Callers are moved over in Task 4; to keep this commit compiling, keep `IssueAsync` as a thin `CreateAsync` + save + `SendAsync` wrapper for now.
- [ ] **Step 4: Run** the filter. Expected: PASS. Then run `backend/scripts/test.sh` once. Expected: PASS.
- [ ] **Step 5: Commit** `refactor(accounts): split email-change links into create, send and a mail cap`.

### Task 4: Profile email change (API-29, API-30, API-31) under the account lock

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Accounts/AccountService.cs:38-116`
  - `ProfileDto.cs` (add `EmailChangeStage? PendingEmailStage = null` after `PendingEmail`)
  - `AccountMapper.cs` (map it)
  - `EmailChangeLink.cs` (delete the `IssueAsync` wrapper)
  - `backend/src/FeedbackHub.API/Controllers/ProfileController.cs`: `[ProducesResponseType]` 403 and 429 on `Update`, 429 on `ResendEmailChange`
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Accounts/AccountServiceTests.cs`, `backend/tests/FeedbackHub.API.Tests/Controllers/ProfileControllerTests.cs`, `backend/tests/FeedbackHub.Infrastructure.Tests/Persistence/EmailChangeConcurrencyTests.cs`

**Interfaces:**
- Consumes: `IAccountLock` (Task 2) and `EmailChangeLink.CreateAsync`/`SendAsync`/`EnsureUnderCapAsync`/`ConsumeAllAsync` (Task 3). `IUnitOfWork.ExecuteInTransactionAsync` already exists.
- Produces: `ProfileDto.PendingEmailStage`, serialized as a string. `IAccountService` signatures are unchanged.

Flow per TECH00 §6.7 steps 0, 1, 6, 8, 9, 10:
- **Before the transaction:** the current-password check (step 0), which must stay outside so a wrong guess is still counted.
- **Inside `ExecuteInTransactionAsync`:** `IAccountLock.AcquireAsync`, then re-read the account, then in order:
  - password-less and unverified → 403 (before the cap);
  - `EnsureUnderCapAsync`;
  - the taken-email 409, **only** on the password branch (QĐ-41);
  - `Update` + `RequestEmailChange` + `CreateAsync`;
  - save.
- **After commit:** `SendAsync`.
- Resend (API-30) and cancel (API-31) take the same lock. Resend reads the stage from the account and checks the cap. Cancel consumes both purposes.

- [ ] **Step 1: Write failing Application tests.** Use a fake `IEmailSender`, a fake clock, and an `IAccountLock` substitute that records the call.
  - `UpdateCurrentProfileAsync_NoPasswordVerified_MailsCurrentAddressOnly_NoTakenCheck`: one mail to `Email`, none to the new address, `ReserveAttemptAsync` `DidNotReceive`, no 409 when the new address belongs to another account, `Email` unchanged, profile `PendingEmailStage = AwaitingCurrentEmail`. Covers AC-376 and BR-85 point 6.
  - `UpdateCurrentProfileAsync_NoPasswordUnverified_Throws403_NoTokenNoMail_NotCounted`: `ForbiddenException` with `ApplicationMessages.EmailNotConfirmed`, no token row, `IEmailSender` `DidNotReceive` for either address, and other fields in the same request **not** saved. Five refusals in a row still never return 429. Covers AC-398.
  - `UpdateCurrentProfileAsync_PasswordAccount_UnchangedBehaviour` (`[Theory]` over `EmailConfirmed` true and false): the stage goes straight to `AwaitingNewEmail`, the only mail goes to the new address, none goes to `Email`, there is no `ConfirmOldEmail` token, and the 409 check still runs. The `false` case is AC-402 (an unverified password account is not blocked); the `true` case is AC-385.
  - `UpdateCurrentProfileAsync_SixthRequestInOneHour_Throws429_StateUnchanged`: Review Focus 5.
  - `UpdateCurrentProfileAsync_SwitchToThirdAddressAtStageOneOrTwo_RestartsAtStageOne`: earlier links consumed, the mail goes to `Email` for g3. Covers AC-384 and AC-393.
  - `UpdateCurrentProfileAsync_EmailUnchanged_NoMailNoTokenStageKept`.
  - `UpdateCurrentProfileAsync_SaveFails_NoMailSent`: `SaveChangesAsync` throws, so `IEmailSender` `DidNotReceive`.
  - `ResendEmailChangeAsync_StageOne_NewConfirmOldEmailTokenToCurrentAddress` (AC-382, AC-401, including the case where the stage-1 link has expired).
  - `ResendEmailChangeAsync_StageTwo_NewChangeEmailTokenToPendingOnly_NoConfirmOldEmailToken` (AC-394, AC-397).
  - `ResendEmailChangeAsync_OverCap_Throws429`.
  - `CancelEmailChangeAsync_StageOne_ConsumesConfirmOldEmailAndChangeEmail_ClearsStage` (AC-383, AC-396).
  - Every write path calls `IAccountLock.AcquireAsync` exactly once.
- [ ] **Step 2: Write failing API and concurrency tests**
  - `ProfileControllerTests`: `Update_NoPasswordUnverified_Returns403ProblemWithCode`, which asserts `application/problem+json` and `code = EmailNotConfirmed`; `Update_OverCap_Returns429WithCode`; and the GET profile response contains `pendingEmailStage`.
  - `EmailChangeConcurrencyTests` (Testcontainers):
    - API-29 with a wrong password: the `EmailChange` counter still increments.
    - An exception during the transaction: no mail.
- [ ] **Step 3: Run** the Application and Infrastructure filters for these classes. Expected: FAIL.
- [ ] **Step 4: Implement** the flow above, move callers off `IssueAsync`, delete it, and re-export OpenAPI per `.claude/rules/api-contract.md`, then run `bun run --cwd frontend codegen`.
- [ ] **Step 5: Run** the filters, then `backend/scripts/test.sh` once. Expected: PASS.
- [ ] **Step 6: Commit** `feat(accounts): confirm a no-password email change at the current address first`, including `shared/openapi/openapi.json`.

### Task 5: Anonymous confirm (API-34), read-only lookup (API-35), stage guard on API-32

**Files:**
- Modify:
  - `backend/src/FeedbackHub.Application/Services/Auth/RegistrationService.cs` (`ConfirmEmailChangeAsync` at :112: the lock and the stage guard only)
  - `backend/src/FeedbackHub.API/Controllers/AuthController.cs` (next to `ConfirmEmailChange` at :110; inject the new service)
  - `backend/src/FeedbackHub.Application/DependencyInjection.cs` (register the new service)
  - `.claude/rules/authentication.md` (the `[AllowAnonymous]` list: add `confirm-current-email` and `confirm-current-email/lookup`, per TECH00 §10.2 N-new-3)
  - `shared/openapi/openapi.json`
- Create, all in `backend/src/FeedbackHub.Application/Services/Auth/`:
  - `ICurrentEmailConfirmationService.cs`
  - `CurrentEmailConfirmationService.cs`: its constructor takes `IUnitOfWork`, `IAccountLock`, `EmailChangeLink`, `IDateTimeProvider` and a logger, and never `ITokenIssuer`, `IHttpContextAccessor` or device info. `RegistrationService` already takes `ITokenIssuer` (constructor, line 20), so API-34/35 live here; declare this as a minor deviation from TECH00 §11's `RegistrationService.ConfirmCurrentEmailAsync`.
  - `ConfirmCurrentEmailRequest.cs` (same shape and attributes as `ConfirmEmailChangeRequest`)
  - `CurrentEmailChangeLookupDto.cs` (`sealed record CurrentEmailChangeLookupDto(string NewEmail)`)
- Test: `CurrentEmailConfirmationServiceTests.cs` (new, under `backend/tests/FeedbackHub.Application.Tests/Services/Auth/`), `RegistrationServiceTests.cs`, `AuthControllerTests.cs`, `EmailChangeConcurrencyTests.cs`, `ArchitectureTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces, on `ICurrentEmailConfirmationService`:
  - `Task ConfirmAsync(ConfirmCurrentEmailRequest request, CancellationToken ct)` (API-34)
  - `Task<CurrentEmailChangeLookupDto> LookupAsync(ConfirmCurrentEmailRequest request, CancellationToken ct)` (API-35)
  - Routes: `POST api/auth/confirm-current-email` → 204, and `POST api/auth/confirm-current-email/lookup` → 200. Both are `[AllowAnonymous]` with `[EnableRateLimiting("auth")]`; the lookup also sets `Cache-Control: no-store`.

Per TECH00 §4.9 API-32/34/35, §6.7 steps 2, 7, 10:
- **API-34:** read the token's `AccountId` outside the transaction with `AsNoTracking`. Inside the transaction: lock, re-read the token and account, and check all of `IsActiveAt`, `Purpose == ConfirmOldEmail`, `Email != null`, stage `AwaitingCurrentEmail` and `PendingEmail == token.Email`; any failure gives the same 409. Then `ConfirmCurrentEmail()`, consume every change token, `CreateAsync` (now stage 2, so a `ChangeEmail` token), save. After commit, `SendAsync` to the new address.
- **API-35:** the same checks without a lock or any write; it returns `PendingEmail` only.
- **API-32:** add the lock and require stage `AwaitingNewEmail`. A `null` stage gives 409.
- `CurrentEmailConfirmationService` never depends on `TokenIssuer`; an architecture test pins this.

- [ ] **Step 1: Write failing Application tests** (TECH00 §11 rows "Hết hạn…" and "Không phát phiên…"):
  - `ConfirmAsync_Valid_MovesToStageTwo_OneMailToNewAddress_EmailUnchanged` (AC-377).
  - `ConfirmAsync_ExpiredUsedReplacedCancelledWrongStageOrPendingMismatch_Throws409_NoMail` (`[Theory]`; AC-380, AC-381, AC-383, AC-384, AC-386).
  - `ConfirmAsync_WrongPurpose_Throws409` (`[Theory]` over `ChangeEmail`, `VerifyEmail` and a `null` purpose). This is mutation target (g).
  - `LookupAsync_Valid_ReturnsPendingOnly_WritesNothing`: the token stays unconsumed, the stage is unchanged, `SaveChangesAsync` and `IEmailSender` are both `DidNotReceive`; calling it twice and then API-34 still succeeds (AC-404).
  - `LookupAsync_AnyInvalid_Throws409` (the same `[Theory]` set plus the wrong purposes).
  - `ConfirmEmailChangeAsync_StageOneOrNullStage_Throws409_EmailUnchanged` (mutation target (c), Review Focus 4).
  - Full path: stage 1 → API-34 → API-32 → `Email` is new, `PendingEmail` and the stage are `null`, every token is consumed (AC-378).
  - A `ConfirmOldEmail` token sent to `verify-email` → 401, no session.
  - `CancelAtStageOne_ThenConfirmAsync_Throws409_NoMail`: call the real `AccountService.CancelEmailChangeAsync`, then `ConfirmAsync` with the stage-1 token → 409, and `IEmailSender` `DidNotReceive`. This is mutation target (d), covering AC-383.
  - `SwitchToThirdAddressAtStageTwo_ThenOldNewAddressLink_Throws409` (AC-393): G2's `ChangeEmail` link → API-32 409.
  - `CancelAtStageTwo_ThenNewAddressLink_Throws409` (AC-396).
  - `StageTwoLinkExpired_Resend_NewLinkApplies_OldLinkThrows409` (AC-394, AC-397).
  - `OldChangeEmailTokenOfNoPasswordAccount_ConfirmEmailChange_Throws409` (T21 regression).
  - `ConfirmAsync_NotBlockedByMailCap`: five change tokens in the last hour, then a valid `ConfirmAsync` → 204 and one mail.
  - `ArchitectureTests.CurrentEmailConfirmationService_DoesNotTakeSessionDependencies`: its constructor takes no `ITokenIssuer`, `IHttpContextAccessor` or device-info type.
- [ ] **Step 2: Write failing API tests** (`AuthControllerTests`):
  - API-34 → 204 with an empty body and no `Set-Cookie`; signed in or not, no cookie changes.
  - API-35 → 200 `{ "newEmail": … }` with only that property, `Cache-Control: no-store`, and no `Set-Cookie`.
  - Every API-34/35 failure returns a byte-identical 409 body: compare the raw strings across the `[Theory]` cases.
  - Both endpoints carry `[AllowAnonymous]`; extend the existing anonymous-list controller test.
- [ ] **Step 3: Write failing concurrency tests** (`EmailChangeConcurrencyTests`, Testcontainers):
  - Two parallel API-34 calls with one token → one 204, one 409, and **one** mail. This is mutation target (e).
  - API-34 in parallel with cancel → final state is consistent (`PendingEmail` null ⇒ no usable change token).
- [ ] **Step 4: Run** the filters. Expected: FAIL.
- [ ] **Step 5: Implement**, update `authentication.md`, re-export OpenAPI, and run `bun run --cwd frontend codegen`.
- [ ] **Step 6: Run** the filters, then `backend/scripts/test.sh` once. Expected: PASS.
- [ ] **Step 7: Commit** `feat(auth): confirm an email change at the current address by an explicit step`.

### Task 6: Frontend: profile pending lines, errors, and the confirm page

**Files:**
- Modify: `frontend/src/components/ProfilePanel.vue`, `frontend/src/router/index.ts` (next to `confirm-email-change` at :185), `frontend/src/locales/vi.ts`, `en.ts`
- Create: `frontend/src/views/ConfirmCurrentEmailView.vue` (layout copied from `ConfirmEmailChangeView.vue`, same not-signed-in look, no `requiresAuth`, no `guestOnly`)
- Test: `frontend/tests/components/ProfilePanel.test.ts`, `frontend/tests/views/ConfirmCurrentEmailView.test.ts`, the router meta test if routes are enumerated there

**Interfaces:**
- Consumes: the generated `getAuth().authConfirmCurrentEmail({ token })` and `getAuth().authConfirmCurrentEmailLookup({ token })` (names as orval generates them; check `src/api/generated/auth/auth.ts`), and `ProfileDto.pendingEmailStage`.
- Produces: the route `{ path: '/confirm-current-email', name: 'confirm-current-email' }`.

Behaviour per TECH00 §6.7 step 4, §8.2, and the `ProfileEmailNoPw` boards. Copy is in Global Constraints.

- [ ] **Step 1: Write failing `ProfilePanel` tests** (MSW `server.use`, copy asserted through `i18n.global.t`):
  - The stage-1 line with *Gửi lại*/*Huỷ* for `pendingEmailStage: 'AwaitingCurrentEmail'`; the stage-2 line for `'AwaitingNewEmail'`.
  - The hint under the email field appears only for `hasPassword: false` + `emailConfirmed: true` while the email is edited.
  - A 403 `EmailNotConfirmed` on save → the error line under the email field, no dialog, and no other field shown as saved.
  - A 429 `EmailChangeTooManyRequests` on save or resend → its line.
  - No *Mật khẩu hiện tại* field and no password tab when `hasPassword: false`.
- [ ] **Step 2: Write failing `ConfirmCurrentEmailView` tests** (`vi.useFakeTimers()`):
  - On mount it clears `token` from the URL and calls lookup once. After `await flushPromises()` and `vi.runAllTimers()`, the confirm handler is **not called**. This is mutation target (f) and Review Focus 1.
  - It shows the heading with the full new address, the line, and one `Xác nhận` button.
  - Clicking calls confirm exactly once; a double click still calls it once (the button is disabled while in flight).
  - After success it shows the "đã xác nhận, thư đã gửi tới email mới" state.
  - A lookup 409 → the no-longer-valid state with **no** button.
  - A confirm 409 → the no-longer-valid state.
  - A missing token → the missing-token state, and neither endpoint is called.
  - Never touches the `auth` store (assert `setAuth`/`clearAuth` are not called).
- [ ] **Step 3: Run** `bun run --cwd frontend test:run -- ProfilePanel ConfirmCurrentEmailView`. Expected: FAIL.
- [ ] **Step 4: Implement**, adding both `vi` and `en` keys. Any lookup/confirm network error other than 409 shows the generic failure and never retries API-34 by itself.
- [ ] **Step 5: Run** the tests, then `bun run --cwd frontend test:run`, `build-only`, `type-check` and `lint:boundaries`. Expected: all PASS.
- [ ] **Step 6: Commit** `feat(profile): show both email-change stages and confirm at the current address by a button`.

### Task 7: Takeover regressions, SetPasswordHash allowlist, mutation run, feature map

**Files:**
- Test: `backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs` (or the external-login test class already there), `backend/tests/FeedbackHub.API.Tests/Architecture/ArchitectureTests.cs`
- Modify: `docs/feature-map.md` (the profile/email-change entry: add FR-69 and AC-376–404 to its `PRD:` line, plus manual steps for the two-stage flow and the confirm page)

**Interfaces:**
- Consumes: everything above. There is no production code change unless a test exposes a bug; in that case stop and report it before fixing.

- [ ] **Step 1: Write the tests** (TECH00 §11 rows "Tự liên kết…" and "Danh sách gọi `Account.SetPasswordHash`"):
  - `ExternalLoginAsync_EmailEqualsVictimsPendingEmail_CreatesSeparateAccount` (`[Theory]` Google/Microsoft, at stage 1 and at stage 2): `accountCreated = true`, no `ExternalLogin` on G, and G's `Email` unchanged. Covers AC-391 and AC-399.
  - `ExternalLoginAsync_VictimSignsInAgain_ReachesOwnAccount` (AC-392).
  - `ExternalLoginAsync_LinkedSignInVerifiedMatchingEmail_ConfirmsEmail` and `..._ProviderNotVerified_LeavesUnconfirmed` (AC-400, AC-403).
  - `ChangePasswordAsync_NoPasswordAccount_Throws401` (BR-85 point 10).
  - Invitations and clashes while G waits at stage 1 or 2 (AC-388, AC-390):
    - An invitation sent to G's pending address and opened by G → `WrongAccount`, and G does not join the organization.
    - B registers that address → allowed.
    - At stage 2, B registers that address, then G opens the new-address link → 409 `AccountEmailAlreadyExists`, and G's `Email` is unchanged.
  - The account gets a password mid-change (AC-395). The test calls `SetPasswordHash` itself.
    - The pending stage's link still completes by that stage's rules.
    - A new change request after that requires `currentPassword`.
  - `ArchitectureTests.SetPasswordHash_CalledOnlyFromAllowlist`. The allowlist is exactly `RegistrationService.RegisterAsync`, `InvitationService.RegisterAsync`, `DevDataSeeder.EnsureAccountAsync` and `AccountService.ChangePasswordAsync`; test projects are excluded. The failure message is: `A new set-password flow must prove the current email's inbox (TECH00 QĐ-43) and be reviewed by appsec.` Check the caller list against the code first; if it differs, stop and report.
- [ ] **Step 2: Run** the tests. Expected: PASS. These pin existing behaviour. If one fails, stop and report: it is a real bug.
- [ ] **Step 3: Mutation run** (TECH00 §11 targets a–g). For each target, apply the mutation by hand, run the named test, confirm it turns **red**, then revert with `git checkout -- <file>`:
  - (a) Add `PendingEmail` to the auto-link predicate, or drop `EmailConfirmed` → `ExternalLoginAsync_EmailEqualsVictimsPendingEmail_CreatesSeparateAccount`.
  - (b) Change the stage mapping in `RequestEmailChange`, or remove its unverified guard → `RequestEmailChange_StageByPasswordAndConfirmation_ReturnsExpectedStage`, `RequestEmailChange_NoPasswordUnconfirmed_ThrowsEmailNotConfirmed` and `UpdateCurrentProfileAsync_NoPasswordUnverified_Throws403_NoTokenNoMail_NotCounted`.
  - (c) Remove the stage guard in `ApplyEmailChange` → `ApplyEmailChange_AtStageOne_ThrowsEmailChangeNotPending` and `ConfirmEmailChangeAsync_StageOneOrNullStage_Throws409_EmailUnchanged`.
  - (d) Make cancel consume only `ChangeEmail` → `CancelAtStageOne_ThenConfirmAsync_Throws409_NoMail`.
  - (e) Remove `IAccountLock.AcquireAsync` from `ConfirmAsync` → the two-parallel-API-34 test in `EmailChangeConcurrencyTests`.
  - (f) Call API-34 in `onMounted` → the no-call-on-mount test in `ConfirmCurrentEmailView.test.ts`.
  - (g) Remove the `Purpose == ConfirmOldEmail` check from `ConfirmAsync` and `LookupAsync` → `ConfirmAsync_WrongPurpose_Throws409` and the wrong-purpose rows of `LookupAsync_AnyInvalid_Throws409`.

   Record a table `target | mutation | test | red?` for the PR description. No mutation is ever committed.
- [ ] **Step 4: Update `docs/feature-map.md`.**
- [ ] **Step 5: Run** `backend/scripts/test.sh` and `bun run --cwd frontend test:run`. Expected: PASS.
- [ ] **Step 6: Commit** `test(auth): pin the no-password email-change takeover guards`, with the mutation table in the commit body.

## After the last task (main session)

- Run `scripts/test-gate.sh`.
- Add to PR #32's description:
  - in `## Changelog`: `- Security: Accounts that sign in only with Google or Microsoft must confirm an email change from their current address first.`;
  - under *Lệch nhỏ so với thiết kế*: any minor deviations reported by the tasks;
  - the mutation table.
- Push `feature/module-00`.
- Final whole-branch review: `opus`, once, scoped to this plan's commits.

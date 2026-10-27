# Feature map

Bản đồ tính năng theo góc nhìn người dùng: vào bằng đâu, code nằm ở đâu, kiểm tra thế nào. Mỗi tính năng là một mục `##`. Thêm tính năng mới theo cùng cấu trúc.

---

## Hồ sơ và phiên đăng nhập

- **PRD:** PRD00-FR-67 (đổi email, BR-82): AC-342 (dòng chờ xác nhận, thư tới email mới), AC-344 (link cũ hết hiệu lực), AC-352 (mật khẩu hiện tại khi đổi email).
- **Vào bằng:** avatar ở góc trên bên phải (`app-header-user-menu`) → **Profile** (`user-menu-profile`) → mở bảng `Profile` (panel bên phải từ 1200 px, sheet từ dưới lên bên dưới) → tab **Login sessions**. Vai trò: mọi người dùng đã đăng nhập (đã kiểm tra với tài khoản role `User`).
- **Frontend:**
  - Menu: `frontend/src/layouts/UserMenu.vue`
  - Bảng hồ sơ và tab sessions: `frontend/src/components/ProfilePanel.vue` (vỏ `AppSheet`)
  - Avatar: tab Personal information, testid `profile-avatar`, `profile-avatar-input`, `profile-avatar-change`, `profile-avatar-remove`
  - Avatar ở header: `user-menu-avatar` (nút) và `user-menu-panel-avatar` (trong menu) hiện ảnh từ `auth.account.avatarUrl` (`AccountDto.AvatarUrl`, do `TokenIssuer` dựng lúc đăng nhập/refresh); bảng Profile cập nhật lại giá trị này sau khi tải lên/xóa
  - Đổi email (tab Personal information):
    - Ô _Current password_ (testid `profile-email-current-password`) nằm ngay trong form, chỉ hiện khi tài khoản có mật khẩu và ô email khác email hiện tại và email đang chờ; lưu hồ sơ không đổi email thì không gửi mật khẩu.
    - Dòng chờ xác nhận là chữ thường dưới ô email (testid `profile-pending-email`) kèm hai nút chữ `profile-pending-email-resend` (Resend) và `profile-pending-email-cancel` (Cancel). Không mở hộp thoại và không hỏi mật khẩu.
    - Lỗi `InvalidCurrentPassword`, `FieldRequired`, `EmailChangeTemporarilyLocked` hiện ngay dưới ô mật khẩu, không bật toast. `api/client.ts` không refresh-và-gửi-lại cho các 401 cấp form `InvalidCurrentPassword`, `EmailChangeTemporarilyLocked`, `PasswordChangeTemporarilyLocked` (nếu không, phiên bị đăng xuất).
  - Trang xác nhận đổi email: `frontend/src/views/ConfirmEmailChangeView.vue`, route công khai `/#/confirm-email-change?token=...` (không có `meta`). Trang bỏ token khỏi thanh địa chỉ rồi mới gọi API; mở link không đăng nhập ai. Kết quả ở testid `confirm-email-change-result`.
  - Testid: `profile-sessions-list` (danh sách phiên), `app-header-user-menu`, `user-menu-profile`, `user-menu-logout`
  - Nút quan trọng: `Sign out other devices` (chỉ hiện khi có phiên khác), xác nhận bằng `Confirm` trong dialog cảnh báo của Naive UI
- **Backend:**
  - `GET /api/profile`, `PUT /api/profile`, `PUT /api/profile/password` → `ProfileController` → `IAccountService` (`Application/Services/Accounts/AccountService.cs`)
  - `PUT /api/profile/avatar` (multipart, PNG/JPEG/WebP ≤ 2 MB, loại ảnh lấy từ byte thật), `DELETE /api/profile/avatar` → `AccountService.UploadAvatarAsync` / `DeleteAvatarAsync`. Ảnh nằm ở `public/avatars/` của storage; `ProfileDto.avatarUrl` = `StorageSettings:PublicUrlBase` + đường dẫn, nên mọi provider phát file qua URL công khai thay vì qua API. Provider MinIO: app đặt policy đọc ẩn danh chỉ cho `public/*` của bucket `MinioSettings:BucketName` (mặc định `feedbackhub`) lúc khởi động; ở dev đặt `PublicUrlBase` thành `http://localhost:9010/feedbackhub` (MinIO, `docker compose --profile minio up -d minio`) hoặc `/storage` (Local). Production giữ `/storage`: nginx của `web` chuyển `/storage/public/` sang MinIO.
  - `PUT /api/profile` nhận `currentPassword` (bắt buộc với tài khoản có mật khẩu khi email đổi) và trả `ProfileDto.pendingEmail`; `POST /api/profile/email/resend` (204, hoặc 409 `EmailChangeNotPending`); `DELETE /api/profile/email/pending` (204); `POST /api/auth/confirm-email-change` `{ token }` (ẩn danh, 204, hoặc 409 `EmailChangeLinkNoLongerValid` / `AccountEmailAlreadyExists`). Link trong thư: `{FrontendBaseUrl}/#/confirm-email-change?token=...`, hạn 24 giờ.
  - `GET /api/auth/sessions`, `DELETE /api/auth/sessions/{id}`, `POST /api/auth/sessions/revoke-others` → `AuthController`
- **Test đã có:**
  - Backend: `backend/tests/FeedbackHub.API.Tests/Controllers/ProfileControllerTests.cs`, `AuthControllerTests.cs` (`GetSessions_ReturnsOnlyCallersOwnSessions`, `RevokeSession_NotOwned_ReturnsNotFound`, `RevokeOtherSessions_RevokesAllButCurrent`), `backend/tests/FeedbackHub.Application.Tests/Services/Accounts/AccountServiceTests.cs`
  - E2E: `frontend/e2e/profile-sessions.spec.ts` (chưa có E2E cho đổi email)
  - Frontend unit đổi email: `frontend/tests/components/ProfilePanel.test.ts` (nhóm `email change`), `frontend/tests/views/ConfirmEmailChangeView.test.ts`, `frontend/tests/unit/api/client.test.ts` (401 `InvalidCurrentPassword` không refresh)
  - Frontend unit: `frontend/tests/components/ProfilePanel.test.ts` (avatar: hiện, upload, chặn ảnh quá lớn/sai loại, lỗi server, xóa); `loadSessions`, `confirmRevokeOtherSessions` vẫn chưa được bao phủ
- **Dữ liệu test:**
  - Tài khoản role `User`: `E2E_USER_USERNAME` / `E2E_USER_PASSWORD` trong `frontend/.env.e2e` (mẫu ở `frontend/.env.e2e.example`)
  - Spec tự tạo một phiên "thiết bị khác" bằng cách đăng nhập rồi đóng context trong `beforeAll`, nên không cần seed phiên riêng
  - Chưa có tài liệu seed riêng cho tài khoản này (cần bổ sung khi dựng `dev-up.sh`)
- **Cách kiểm tra "đã chạy đúng":**
  1. Đăng nhập, mở Profile → Login sessions: thấy mục `This device` và nút `Sign out other devices` khi có phiên khác.
  2. Bấm `Sign out other devices` → `Confirm`: hiện thông báo `Other devices signed out`, chỉ còn `This device`, nút biến mất.
  3. Không có lỗi trong console; `GET /api/auth/sessions` trả về đúng số phiên của chính người dùng.
  4. Đổi email (AC-342): Profile → Personal information, sửa email → ô _Current password_ hiện ra; nhập sai mật khẩu thì lỗi nằm dưới ô mật khẩu và hồ sơ không đổi; nhập đúng thì dưới ô email có dòng `Waiting for confirmation: <email mới>...` kèm Resend và Cancel, email cũ vẫn là email hiện hành. Resend gửi lại thư; Cancel làm dòng biến mất.
  5. Mở link trong thư (`/#/confirm-email-change?token=...`): thanh địa chỉ mất `token`, trang báo `Your email has been changed.`; mở lại link hoặc link cũ báo `This email change link is no longer valid.` (AC-344); không ai bị đăng nhập hay đăng xuất.
- **Rủi ro hay gặp:**
  - Dialog cảnh báo của Naive UI cũng có `role="dialog"`, nên locator phải lấy `.last()` khi có hai dialog mở cùng lúc.
  - Chỉ phiên của chính người dùng được liệt kê và thu hồi (`RevokeSession_NotOwned_ReturnsNotFound`); thử nghiệm cần đúng tài khoản.
  - Đổi mật khẩu có khóa riêng sau số lần sai (`ChangePasswordAsync`), tránh khóa nhầm khi thử nhiều lần.
  - Chuỗi giao diện đang kiểm tra ở locale `en` (fixture đặt `app-locale`); đổi locale làm hỏng selector theo text.

---

## Tổ chức đang làm việc và admin hệ thống

- **PRD:** PRD00-FR-42, FR-44, FR-46, FR-53, FR-63, FR-38: bước 2 AC-126 (người không phải admin hệ thống bị từ chối xem danh sách mọi tổ chức); bước 3 AC-105 (vô hiệu), AC-106 (kích hoạt lại); bước 4 AC-185 (đổi tên), AC-226 (định danh không đổi), AC-367 (một mục nhật ký cho mỗi thao tác); tạo tổ chức không ghi nhật ký AC-364. Bước 1 (cấp admin hệ thống chỉ qua CLI, FR-38) không có AC riêng: AC-51 và AC-52 chỉ nói Hub không có thao tác cấp quyền.
- **Vào bằng:** màn hình nằm ở các mục _Tổ chức: tạo, chuyển, trang chào, tạm ngừng_, _Quản lý tổ chức_ và _Admin hệ thống: các tổ chức_ bên dưới; mục này chỉ ghi phần API. Gọi API bằng token của thành viên tổ chức, hoặc của admin hệ thống cho `api/system/*`. Cấp và thu hồi admin hệ thống chỉ qua CLI trên server: `dotnet run --project backend/src/FeedbackHub.API -- grant-system-admin --email <email>` và `revoke-system-admin --email <email>` (mã thoát 0 xong hoặc không có gì để làm, 1 sai cú pháp, 2 không có tài khoản, 3 email chưa xác minh).
- **Frontend:** xem ba mục _Tổ chức: tạo, chuyển, trang chào, tạm ngừng_, _Quản lý tổ chức_, _Admin hệ thống: các tổ chức_.
- **Backend:**
  - `GET /api/organization`, `PATCH /api/organization` (đổi tên, API-04; cùng tên thì 200 không ghi), `POST /api/organization/deactivate` → `CurrentOrganizationController` → `IOrganizationContextService`
  - `POST /api/organizations`, `GET /api/organizations` (API-01, API-02) → `OrganizationsController`
  - `GET /api/system/organizations` (phân trang `PageRequest`), `POST /api/system/organizations/{id}/deactivate`, `POST /api/system/organizations/{id}/reactivate` (API-24 đến API-26) → `SystemOrganizationsController` → `ISystemOrganizationService`; 404 là `EntityNotFound`
  - Cổng tổ chức: `OrganizationContextAuthorizationHandler` (thứ tự BR-77, mã riêng từng lý do); policy admin hệ thống tra qua `ISystemAdministratorResolver` (cache 60 giây)
  - Đổi trạng thái tổ chức qua `OrganizationStatusChanger` (khóa dòng bằng `IOrganizationLock`), ghi mục nhật ký qua `IAuditTrail`, rồi xóa cache truy cập và quyền
- **Test đã có:**
  - API: `CurrentOrganizationControllerTests`, `OrganizationsControllerTests`, `SystemOrganizationsControllerTests`, `AuthControllerTests` (refresh mất quyền vào tổ chức), `Commands/SystemAdminCommandsTests`, `Middleware/TenantAccessMiddlewareTests`, `Authorization/AuthorizationPolicyRegistrationTests`
  - Application: `OrganizationContextServiceTests`, `OrganizationServiceTests`, `SystemOrganizationServiceTests`, `SystemAdministratorActorTests`, `SystemAdministratorServiceTests`, `AuditEventCatalogTests`
  - Domain: `OrganizationTests`, `SystemAdministratorTests`, `AuditEventTests`
  - Infrastructure: `OrganizationContextAuthorizationHandlerTests`, `SystemAdministratorAuthorizationHandlerTests`, `SystemAdministratorResolverTests`, `TenantAccessServiceTests`, `AuditTrailTests`, `OrganizationLockTests`, `SystemOrganizationQueriesTests`
  - Frontend unit: `frontend/tests/unit/stores/organizations.test.ts`
- **Dữ liệu test:** admin hệ thống được cấp bằng lệnh CLI ở trên cho một tài khoản đã xác minh email; không có seed riêng.
- **Cách kiểm tra "đã chạy đúng":**
  1. `grant-system-admin --email <email>` in `Granted system administrator to ...`; chạy lại in `... is already a system administrator.` với mã thoát 0.
  2. Đăng nhập tài khoản đó rồi `GET /api/system/organizations` trả 200; với tài khoản thường trả 403.
  3. `POST /api/system/organizations/{id}/deactivate` kèm `reason`, rồi gọi API của thành viên tổ chức đó: 403 `OrganizationSuspended`. `reactivate` đưa về 200.
  4. `PATCH /api/organization` với tên mới trả 200 và có đúng một mục `OrganizationRenamed`; gửi lại cùng tên không thêm mục.
- **Rủi ro hay gặp:** thân `PATCH /api/organization` chỉ có `slug` trả 400 `OrganizationNameRequired`; lý do vô hiệu không nằm trong mục sự kiện (API-33) hay log ứng dụng; chỉ có trong snapshot `Change` của `audit_logs`.

## Thành viên và vai trò của tổ chức

- **PRD:** PRD00-FR-23, FR-29, FR-52, FR-59, FR-60, FR-62, FR-64, FR-66: bước 1 AC-263 (người có quyền quản lý thành viên thấy danh sách đầy đủ), AC-339 (dòng của chính mình không có *Gỡ*); bước 3 AC-277 (Chủ sở hữu duy nhất không rời được); bước 4 AC-290 (tên vai trò trùng không phân biệt hoa thường), AC-248 (vai trò còn người giữ không xoá được). Bước 2 (`roleIds` rỗng, Admin thêm Owner) chưa gắn AC: BR-58 không có AC khớp trọn từng mã lỗi.
- **Vào bằng:** màn hình nằm ở các mục _Thành viên và lời mời_ và _Vai trò tự tạo_ bên dưới. Trang _Tổ chức_ cũ cùng hai hộp thoại thành viên, vai trò cũ đã bỏ cùng route `api/organizations/{id}/...`. Gọi API bằng token của thành viên tổ chức đang làm việc.
- **Backend:**
  - API-06 đến API-09: `GET /api/organization/members`, `PUT /api/organization/members/{accountId}/roles`, `DELETE /api/organization/members/{accountId}`, `POST /api/organization/leave` → `CurrentOrganizationController` → `IMemberService`
  - API-14 đến API-18: `GET /api/organization/role-options`, `GET/POST /api/organization/roles`, `PUT/DELETE /api/organization/roles/{roleId}` → `CurrentOrganizationController` → `IRoleService`
  - BR-58 chỉ tính trong `RoleGrantPolicy` (đầu vào đọc qua `IRoleGrantReader`); mọi thao tác ghi khoá tổ chức bằng `IOrganizationLock` trước, kiểm lại từ cơ sở dữ liệu tư cách thành viên, trạng thái tổ chức và quyền của endpoint (`organizations.members.manage` hoặc `organizations.roles.manage`; rời tổ chức không cần quyền), ghi một mục nhật ký qua `IAuditTrail`, rồi xoá cache quyền và truy cập
- **Test đã có:**
  - API: `CurrentOrganizationMembersTests`, `CurrentOrganizationRolesTests`, `Middleware/TenantAccessMiddlewareTests`, `Architecture/ArchitectureTests`
  - Application: `MemberServiceListTests`, `MemberServiceWriteTests`, `RoleGrantPolicyTests`, `RoleServiceReadTests`, `RoleServiceWriteTests`, `SystemRoleDisplayNamesTests`, `SystemRoleDefaultsTests`
  - Domain: `OrganizationMemberTests`, `RoleTests`
  - Infrastructure: `OrganizationMemberQueriesTests`, `RoleQueriesTests`, `RoleGrantReaderTests`, `RoleDisplayOrderTests`, `MemberConcurrencyTests`, `RoleConcurrencyTests`, `Migrations/RoleMigrationsTests`
- **Dữ liệu test:** `dotnet run --project backend/src/FeedbackHub.API -- seed-dev` (không chạy ở Production, chạy lại không đổi gì) tạo tổ chức `Dev` (slug `dev`) với hai tài khoản đã xác thực email: `e2e-admin` / `E2e-Admin!2026` giữ vai trò **Owner** (tên có chữ "admin" nhưng không phải vai trò Admin) và `e2e-user` / `E2e-User!2026` giữ vai trò **Member**. Đăng nhập rồi `POST /api/auth/switch-organization` với id tổ chức `Dev` để có token của tổ chức. Bước 2 cần token Admin: dùng token Owner cho `e2e-user` vai trò Admin (`PUT .../members/{accountId}/roles`), rồi đăng nhập lại bằng `e2e-user`.
- **Cách kiểm tra "đã chạy đúng":**
  1. `GET /api/organization/members` trả 200 với `canChangeRoles`, `canRemove`, `lockReason` theo từng dòng; dòng của chính mình có `canRemove = false`.
  2. `PUT .../members/{accountId}/roles` với `roleIds: []` trả 400 `OrganizationMemberRequiresAtLeastOneRole`; thêm Owner bằng token Admin trả 403 `OwnerOnlyOperation`.
  3. `POST /api/organization/leave` khi là Owner cuối cùng trả 409 `LastOwnerCannotLeave`.
  4. Tạo vai trò tên trùng (không phân biệt hoa thường) trả 409; xoá vai trò còn người giữ trả 409 `RoleHasHolders`.
- **Rủi ro hay gặp:** route `api/organizations/{id}/...` không còn (404); `roleIds` hoặc `permissionCodes` thiếu trong thân trả 400.

## Lời mời

- **PRD:** PRD00-FR-25, FR-61, BR-81: bước 1 AC-273 (mời lại thay lời mời cũ); bước 2 AC-59, AC-117 (lời mời đã thu hồi báo không còn hiệu lực); bước 3 AC-57 (vào thẳng với vai trò đã mời), AC-182 (email coi là đã xác minh); bước 4 AC-306 (sai tài khoản), AC-351 (email chưa xác minh).
- **Vào bằng:** gửi và quản lý lời mời ở mục _Thành viên và lời mời_; người được mời mở link trong thư ở mục _Nhận lời mời_. Gọi API bằng token của thành viên tổ chức đang làm việc có quyền `organizations.members.manage`; hai API công khai (lookup, register-with-invitation) và accept (cần đăng nhập) do trang `#/invite?token=...` gọi.
- **Backend:**
  - API-10 đến API-13: `GET/POST /api/organization/invitations`, `POST /api/organization/invitations/{id}/resend`, `DELETE /api/organization/invitations/{id}` → `CurrentOrganizationController` → `IInvitationService`
  - API-20, API-21: `POST /api/invitations/lookup` (ẩn danh), `POST /api/invitations/accept` (cần đăng nhập) → `InvitationsController` → `IInvitationService`
  - API-22: `POST /api/auth/register-with-invitation` → `AuthController` → `IInvitationService`
  - Lookup và accept dùng giới hạn tốc độ `invitation`, register-with-invitation dùng `auth` (429). Danh sách đọc qua `IInvitationQueries` (kèm `pendingInvitationCount` ở `GET /api/organization/roles`). Gửi và gửi lại khoá tổ chức, kiểm BR-58, rồi gửi email; email lỗi trả 503 `InvitationEmailDeliveryFailed`: lời mời mới không được lưu, lời mời cũ vẫn mở. Gửi lại tới email đã là thành viên trả 409 `InvitationEmailAlreadyMember`.
  - Link trong thư: `{FrontendBaseUrl}/#/invite?token=...`, hạn 7×24 giờ; hạn ghi trong thư theo giờ Việt Nam (UTC+7).
- **Test đã có:**
  - API: `CurrentOrganizationInvitationsTests`, `InvitationsControllerTests`, `Architecture/ArchitectureTests`
  - Application: `InvitationServiceSendTests`, `InvitationServiceManageTests`, `InvitationServiceAcceptTests`, `InvitationServiceRegisterTests`, `InvitationEmailTests`, `OrganizationInvitationTests`
  - Infrastructure: `InvitationQueriesTests`, `OrganizationInvitationPersistenceTests`, `InvitationConcurrencyTests`
- **Dữ liệu test:** dùng tổ chức `Dev` và hai tài khoản của `seed-dev` như mục *Thành viên và vai trò của tổ chức*; token `e2e-admin` (Owner) gửi lời mời. Không có seed riêng cho lời mời; email thật cần cấu hình `EmailSettings`.
- **Cách kiểm tra "đã chạy đúng":**
  1. `POST /api/organization/invitations` với `email` và `roleId` trả 201; thư chứa link `/#/invite?token=...`. Mời lại cùng email thay lời mời cũ.
  2. `POST /api/invitations/lookup` với token trả 200 `status = Valid`; sau khi thu hồi trả `NoLongerValid`.
  3. `POST /api/auth/register-with-invitation` với token hợp lệ trả 200 `invitationOutcome = Joined` và tài khoản vào tổ chức với vai trò đã mời; token đã dùng trả 409.
  4. `POST /api/invitations/accept` bằng tài khoản đã đăng nhập có email khớp trả 200; email lệch trả 409 `InvitationWrongAccount`, email chưa xác minh trả 403 `EmailNotConfirmed`.
- **Rủi ro hay gặp:** thiếu `EmailSettings:FrontendBaseUrl` làm ứng dụng không khởi động; vượt giới hạn tốc độ trả 429; chữ `vi`/`en` của các mã lỗi lời mời nằm ở `frontend/src/locales`.
- **Frontend:** xem _Thành viên và lời mời_ (tab Lời mời) và _Nhận lời mời_ (`InviteView.vue`).

## Nhật ký kiểm toán

- **PRD:** PRD00-FR-68, BR-83, BR-84: bước 1 AC-359 (mới nhất trước), AC-371 (không IP hay thông tin trình duyệt); bước 2 và cách kiểm tra giao diện AC-372 (mục gửi lời mời); bước 3 AC-360 (*Xem thêm* tải trang kế); bước 4 AC-362 (không quyền xem nhật ký thì bị từ chối).
- **Vào bằng:** rail → nhóm **Organization** → **Audit log** (`/#/organization/audit-log`), chỉ hiện với người có quyền `auditlogs.view`; mở bằng URL khi thiếu quyền thấy _No permission_. API: token của thành viên tổ chức đang làm việc có quyền `auditlogs.view`; khoá API bị cổng từ chối 403.
- **Backend:**
  - API-33: `GET /api/organization/audit-log?cursor=&limit=` → `CurrentOrganizationController` → `AuditLogService` → `AuditLogRepository.ListEventsAsync`
  - Chính sách là `auditlogs.view` (`DenyMode.Forbidden`); `AuditLogService` còn đọc lại quyền này từ cơ sở dữ liệu qua `IRoleGrantReader`, nên cache quyền cũ không mở được nhật ký.
  - Chỉ trả các dòng `kind = 'Event'` của tổ chức đang làm việc, mới nhất trước (`timestamp DESC, id DESC`); dòng `Change` không bao giờ ra ngoài.
  - Phân trang bằng keyset qua `nextCursor` mờ (chuỗi không đọc được, máy khách chỉ chuyển lại làm `cursor`); `limit` mặc định 50, ngoài 1 đến 100 trả 400, cursor hỏng trả 400 `InvalidCursor`.
  - Một trang có thể ngắn hơn `limit`, thậm chí rỗng, khi dòng không dựng được bị bỏ; máy khách chỉ dừng khi `nextCursor` là null.
  - Mục trả về chỉ có `id`, `occurredAt`, `actorKind`, `actorName`, `type`, `parameters`; không có IP, user agent hay giá trị cũ/mới. Dòng bỏ ghi log Warning `Skipped audit entry {AuditLogId}`.
  - Route cũ `api/admin/audit-logs` đã bỏ.
- **Test đã có:**
  - API: `OrganizationAuditLogTests`, `AuditLogEndToEndTests`, `Architecture/ArchitectureTests`
  - Application: `AuditCursorTests`, `AuditLogServiceTests`, `AuditEventCatalogTests`
  - Infrastructure: `AuditLogRepositoryTests`, `AuditTrailTests`
- **Dữ liệu test:** dùng tổ chức `Dev` và token `e2e-admin` (Owner) như mục *Thành viên và vai trò của tổ chức*; mục `Event` xuất hiện sau khi chạy một thao tác trong danh mục (ví dụ gửi lời mời).
- **Cách kiểm tra "đã chạy đúng":**
  1. `GET /api/organization/audit-log` bằng token Owner trả 200, `items` mới nhất trước, mỗi mục có `type` thuộc danh mục.
  2. Gửi một lời mời rồi gọi lại: mục `InvitationSent` đứng đầu trang.
  3. `limit=1` trả `nextCursor` khác null; truyền nó làm `cursor` trả trang kế; `cursor=@@@` trả 400 `InvalidCursor`, `limit=0` trả 400.
  4. Token thành viên không có `auditlogs.view` hoặc khoá API trả 403; không token trả 401.
- **Rủi ro hay gặp:** `parameters` là object mở (client sinh ra kiểu `unknown`); `frontend/src/lib/audit-sentence.ts` thu hẹp theo `type` và dựng câu, nên thêm loại sự kiện ở backend thì phải thêm ở đó và ở khối `auditLog.events` của `vi`/`en`, nếu không dòng hiện là câu chung _performed an administrative action_.
- **Frontend:** `frontend/src/views/organization/AuditLogView.vue` (route `organization-audit-log`, `meta.permission = auditlogs.view`); câu mỗi mục dựng ở `frontend/src/lib/audit-sentence.ts`; danh sách cuộn theo cursor qua `useCursorList` (nút `audit-log-show-more`). Mục nhóm theo ngày (_Today_, _Yesterday_). Testid: `audit-log-row-<id>`, `audit-log-day`, `audit-log-skeleton`, `audit-log-empty`, `audit-log-error`, `audit-log-retry`.
- **Test (frontend):** `frontend/tests/views/organization/AuditLogView.test.ts`, `frontend/tests/unit/lib/audit-sentence.test.ts`; E2E: `frontend/e2e/invitation.spec.ts` (mục gửi lời mời và mục vào tổ chức hiện đúng câu).
- **Cách kiểm tra (giao diện):** đăng nhập `e2e-admin`, mở **Audit log**: mục mới nhất ở đầu, mỗi dòng là câu `<người làm> <việc>` kèm giờ; gửi một lời mời rồi tải lại trang thì dòng _sent an invitation to …_ đứng đầu.

## Khung ứng dụng và design system

- **PRD:** PRD00-FR-03, FR-30, FR-66: AC-73 (tổ chức tạm ngừng mờ trong bộ chọn, bấm không chuyển), AC-298 và AC-299 (phần Hub không có quyền bị ẩn khỏi menu), AC-362 (mục rail theo quyền). Mở bằng URL khi thiếu quyền thấy _Không đủ quyền_ (FR-66 "vẫn từ chối mọi yêu cầu gửi trực tiếp", TECH00 §8.2). BR-45: màn *Không tìm thấy* và *Không đủ quyền* chỉ hiện câu chung, không tên hay mã đối tượng.
- **Vào bằng:** mọi trang sau đăng nhập. Gõ một đường dẫn không có (`/#/no/such/page`) khi đã đăng nhập thì thấy _Không tìm thấy_ trong khung; khi chưa đăng nhập thì về đăng nhập rồi quay lại đúng đường dẫn đó.
- **Frontend:**
  - Token màu, chữ, bo góc: `frontend/src/theme/tokens.ts` (khai báo trong `frontend/src/assets/styles/tailwind.css`)
  - Khung: `frontend/src/layouts/AppLayout.vue`, `AppRail.vue` (rail rộng, rail biểu tượng, ngăn kéo trên điện thoại), `AppTopBar.vue`, `OrgSwitcher.vue`, `AppFooter.vue`
  - Quyền vào trang: `frontend/src/lib/route-access.ts` (một hàm cho guard, menu và `AppLayout`); trang `forbidden` được `AppLayout` thay cho `<RouterView>`, nên trang không được mount và không gọi API
  - Màn *Không tìm thấy* / *Không đủ quyền*: `frontend/src/components/AccessProblemState.vue`; `frontend/src/lib/access-problem.ts` (`accessProblemOf`: 404 là `not-found`; 403 `OrganizationAccessDenied` hoặc `SystemAdministratorRequired` là `forbidden`; còn lại `null`); `frontend/src/views/NotFoundView.vue` là route `not-found`, con của layout `/`, `requiresAuth`
  - Testid: `app-shell`, `app-sidebar`, `app-header-menu`, `app-header-palette`, `org-switcher-trigger`, `app-sheet`
- **Backend:** không có; màn hình chỉ là cổng giao diện, API vẫn kiểm quyền từng request (BR-75).
- **Test đã có:**
  - Frontend unit: `frontend/tests/unit/lib/access-problem.test.ts`, `route-access.test.ts`; `tests/components/AccessProblemState.test.ts`; `tests/layouts/AppLayout.test.ts` (trạng thái _Không đủ quyền_ không mount trang, 404 trong khung, không lộ `detail`), `AppRail.test.ts`, `OrgSwitcher.test.ts`; `tests/unit/router/guard-integration.test.ts`; `tests/unit/theme/*`
  - E2E: `frontend/e2e/guards.spec.ts` (đường dẫn lạ hiện _Not found_ trong khung)
- **Cách kiểm tra "đã chạy đúng":**
  1. Mở một đường dẫn lạ khi đã đăng nhập: thấy rail, thanh trên và chữ _Không tìm thấy_ kèm nút _Về trang chủ_; URL giữ nguyên.
  2. Với tài khoản thiếu quyền của một trang có `meta.permission`: mục menu ẩn; mở bằng URL thấy _Không đủ quyền_, URL giữ nguyên, tab Network không có request của trang đó.
  3. Ba khoảng màn hình (điện thoại dưới 768 px, máy tính bảng, từ 1200 px): rail rộng từ 1200 px, rail biểu tượng ở giữa, ngăn kéo và sheet từ dưới lên trên điện thoại.
  4. Chế độ sáng và tối (menu tài khoản, _Giao diện_): chữ và nền đủ tương phản, không có màu ngoài token.
  5. Bật giảm trong suốt của hệ điều hành (`prefers-reduced-transparency`): rail, thanh trên và panel thành nền đặc, không còn nền mờ.
  6. Bộ chọn tổ chức: tổ chức tạm ngừng mờ, có nhãn _Tạm ngừng_, bấm không chuyển.
- **Rủi ro hay gặp:** `AccessProblemState` không nhận tên, mã hay `detail`; đừng thêm prop hay slot truyền chúng vào. Trang con của tổ chức chưa có màn hình dùng `accessProblemOf` cho đến phần 8.

## Tổ chức: tạo, chuyển, trang chào, tạm ngừng

- **PRD:** PRD00-FR-03, FR-24, FR-63: AC-04 (chuyển sang tổ chức khác không đăng nhập lại), AC-56 (tạo tổ chức, vào thẳng tổ chức mới), AC-73 (tổ chức tạm ngừng mờ, bấm không chuyển), AC-281 (định danh gợi ý từ tên, sửa được).
- **Vào bằng:** góc trên bên trái, nút bộ chọn tổ chức (`org-switcher-trigger`) → danh sách tổ chức, _Back to welcome page_, _Create organization_ (`org-switcher-create`). Trang chào `/#/welcome` hiện khi đăng nhập mà tài khoản không có đúng một tổ chức đang hoạt động (`TokenIssuer`), hoặc sau khi tổ chức làm việc bị tạm ngừng; ở đó chọn một tổ chức (`welcome-org-<id>`) hoặc _Create a new organization_ (`welcome-create`). Admin hệ thống thấy thêm link _Organizations_ tới `/system/organizations` (`welcome-system-organizations`).
- **Frontend:**
  - Bộ chọn: `frontend/src/layouts/OrgSwitcher.vue` (popover từ khổ máy tính bảng, `AppSheet` ở khổ điện thoại), `OrgSwitcherPanel.vue`; mục tổ chức dùng chung với trang chào ở `frontend/src/components/OrganizationRows.vue`
  - Trang chào: `frontend/src/views/WelcomeView.vue` (route `welcome`, ngoài khung ứng dụng)
  - Tạo tổ chức: `frontend/src/components/CreateOrganizationSheet.vue` (testid `create-org-name`, `create-org-slug`, `create-org-submit`, `create-org-name-error`, `create-org-slug-error`, `create-org-unverified`); định danh gợi ý bằng `frontend/src/lib/slug-suggestion.ts`, sửa tay thì tên thôi ghi đè
  - Chuyển tổ chức: `frontend/src/composables/use-organization-switch.ts` → `switch-organization-call.ts` (gọi `POST /api/auth/switch-organization` rồi tải lại trang để không store nào giữ dữ liệu tổ chức cũ)
  - Tạm ngừng do người khác: `frontend/src/stores/organization-context.ts` (`OrganizationSuspended` hoặc `OrganizationAccessDenied` từ API → về trang chào, ghi tên vào `sessionStorage` qua `frontend/src/lib/suspended-notice.ts` để trang chào hiện `welcome-suspended-notice`)
  - Store: `frontend/src/stores/organizations.ts`
- **Backend:** API-01, API-02 (`GET/POST /api/organizations`) và `POST /api/auth/switch-organization`; chi tiết ở mục _Tổ chức đang làm việc và admin hệ thống_.
- **Test đã có:**
  - Frontend unit: `frontend/tests/layouts/OrgSwitcher.test.ts`, `tests/components/CreateOrganizationSheet.test.ts`, `tests/views/WelcomeView.test.ts`, `tests/unit/stores/organizations.test.ts`, `organization-context.test.ts`, `tests/unit/composables/use-organization-switch.test.ts`, `tests/unit/lib/slug-suggestion.test.ts`
  - E2E: `frontend/e2e/organizations.spec.ts` (tạo tổ chức với định danh gợi ý rồi vào thẳng tổ chức đó; chuyển giữa `Dev` và tổ chức mới từ bộ chọn)
- **Dữ liệu test:** `e2e-admin` / `E2e-Admin!2026` của `seed-dev`. Spec tạo tổ chức `E2E Org <mốc thời gian>`; không có API xoá tổ chức nên `afterAll` vô hiệu nó bằng _Deactivate organization_ để tài khoản chỉ còn một tổ chức đang hoạt động (nếu không lần đăng nhập sau rơi vào trang chào và `auth.spec.ts` hỏng). Các tổ chức `E2E Org …` tạm ngừng tích lại sau mỗi lần chạy; chúng không ảnh hưởng spec nào.
- **Cách kiểm tra "đã chạy đúng":**
  1. Đăng nhập `e2e-admin` (vào thẳng `Dev`). Mở bộ chọn → _Create organization_, gõ `Công ty Đông Á`: ô _Short identifier_ tự điền `cong-ty-dong-a` (AC-281); sửa ô đó rồi gõ tiếp tên thì ô không bị ghi đè.
  2. _Create organization_: trang tải lại, tên tổ chức mới hiện ở bộ chọn (AC-56). Tạo lại với định danh đã có thì lỗi nằm dưới ô định danh.
  3. Bộ chọn → `Dev`: tải lại, tên `Dev` hiện ở bộ chọn, không bị hỏi đăng nhập (AC-04).
  4. Bộ chọn → _Back to welcome page_: trang chào liệt kê các tổ chức; tổ chức tạm ngừng mờ, có nhãn _Suspended_, bấm không chuyển (AC-73).
- **Rủi ro hay gặp:** tài khoản có hai tổ chức đang hoạt động trở lên đăng nhập vào trang chào, không vào thẳng một tổ chức; test đăng nhập rồi chờ `#/` sẽ hỏng. Tài khoản chưa xác minh email tạo tổ chức thấy `create-org-unverified` kèm nút gửi lại thư (`EmailNotConfirmed`).

## Quản lý tổ chức

- **PRD:** PRD00-FR-53 (đổi tên), FR-66: AC-226 (đổi tên, định danh không đổi), AC-298 (thành viên thường không có phần Quản lý tổ chức).
- **Vào bằng:** rail → nhóm **Organization** → **Organization management** (`/#/organization/settings`), chỉ hiện với người có quyền `organizations.manage` (Chủ sở hữu, Quản trị). Người khác mở bằng URL thấy _No permission_.
- **Frontend:**
  - `frontend/src/views/organization/OrganizationSettingsView.vue` (route `organization-settings`, `meta.permission = organizations.manage`): ô tên `org-settings-name` + `org-settings-save`, định danh chỉ đọc `org-settings-slug`, khối vô hiệu `org-settings-deactivate` với nút `org-settings-deactivate-open`
  - Hộp thoại vô hiệu: `frontend/src/components/DeactivateOrganizationDialog.vue` (testid `deactivate-org-dialog`, `deactivate-org-reason`, `deactivate-org-submit`, `deactivate-org-cancel`); ô lý do bắt buộc với admin hệ thống (BR-54), không bắt buộc với người khác (BR-52); xong thì chuyển về trang chào qua `useOrganizationContextStore().leaveToWelcome()`
  - Đổi tên cập nhật ngay tên ở bộ chọn (`auth.organizationName`, `organizationsStore.applyRename`)
- **Backend:** API-03, API-04 (`GET`, `PATCH /api/organization`), API-05 (`POST /api/organization/deactivate`) → `CurrentOrganizationController`; xem _Tổ chức đang làm việc và admin hệ thống_.
- **Test đã có:** `frontend/tests/views/organization/OrganizationSettingsView.test.ts`, `tests/components/DeactivateOrganizationDialog.test.ts`; E2E: `frontend/e2e/organizations.spec.ts` (thành viên `e2e-user` không thấy mục này và mở bằng URL thấy _No permission_; `afterAll` dùng chính màn này để vô hiệu tổ chức vừa tạo).
- **Dữ liệu test:** `e2e-admin` (Chủ sở hữu `Dev`) vào được; `e2e-user` (Member) bị chặn.
- **Cách kiểm tra "đã chạy đúng":**
  1. `e2e-admin` mở **Organization management**: thấy tên, định danh không sửa được, khối _Deactivate organization_. Đổi tên rồi _Save_: toast `Renamed to …`, tên ở bộ chọn đổi theo, định danh giữ nguyên.
  2. `e2e-user` mở `/#/organization/settings`: thấy _No permission_, URL giữ nguyên, tab Network không có request của trang.
  3. _Deactivate organization_ → hộp thoại liệt kê hậu quả; xác nhận thì về trang chào, tổ chức đó mờ có nhãn _Suspended_.
- **Rủi ro hay gặp:** vô hiệu là một chiều với người dùng thường (chỉ admin hệ thống mở lại được); đừng thử trên `Dev` trong lúc chạy E2E.

## Thành viên và lời mời

- **PRD:** PRD00-FR-25, FR-29, FR-52, FR-59, FR-60, FR-61, FR-62, FR-66: AC-57 (người được mời vào thẳng với vai trò đã mời), AC-277 (Chủ sở hữu duy nhất không rời được), AC-298 (thành viên thường chỉ thấy danh sách, chỉ đọc).
- **Vào bằng:** rail → nhóm **Organization** → **Members** (`/#/organization/members`), mọi thành viên thấy. Tab **Invitations** (`tab-invitations`, `/#/organization/members/invitations`) và nút **Invite member** (`invite-member-btn`) chỉ hiện với người có `organizations.members.manage`; tab **Roles** xem mục _Vai trò tự tạo_. Mở tab bằng URL khi thiếu quyền thấy _No permission_.
- **Frontend:**
  - Vỏ hai tab: `frontend/src/components/OrganizationPeopleTabs.vue` (`tab-members`, `tab-invitations`, `tab-roles`, kèm số lượng)
  - Thành viên: `frontend/src/views/organization/MembersView.vue`, `frontend/src/components/MemberRow.vue` (`member-row-<accountId>`, `member-menu` → _Change roles_ / _Remove from organization_, `member-leave` ở dòng của chính mình, `member-lock-reason`; xác nhận bằng `InlineConfirm`: `inline-confirm-confirm` / `inline-confirm-cancel`)
  - Lời mời: `frontend/src/views/organization/InvitationsView.vue`, `frontend/src/components/InvitationRow.vue` (`invitation-row-<id>`, `invitation-resend`, `invitation-revoke`, `invitation-delete`), `frontend/src/components/InviteMemberSheet.vue` (`invite-email`, `role-option-<id>`, `invite-send`, `invite-error`)
  - Store: `frontend/src/stores/organization-people.ts`
- **Backend:** API-06 đến API-13 (thành viên, rời tổ chức, lời mời); xem _Thành viên và vai trò của tổ chức_ và _Lời mời_.
- **Test đã có:**
  - Frontend unit: `frontend/tests/views/organization/MembersView.test.ts`, `InvitationsView.test.ts`; `tests/components/MemberRow.test.ts`, `InvitationRow.test.ts`, `InviteMemberSheet.test.ts`, `OrganizationPeopleTabs.test.ts`; `tests/unit/stores/organization-people.test.ts`
  - E2E: `frontend/e2e/organizations.spec.ts` (Chủ sở hữu duy nhất của tổ chức mới bấm _Leave organization_ thấy câu `You are the only owner…` và không có nút xác nhận; `e2e-user` không thấy tab Invitations, Roles và mục Settings), `frontend/e2e/invitation.spec.ts` (mời → thư Mailpit → đăng ký → thấy ở tab Members → gỡ)
- **Dữ liệu test:**
  - Tổ chức `Dev` và hai tài khoản của `seed-dev` như mục _Thành viên và vai trò của tổ chức_: `e2e-admin` (Owner), `e2e-user` (Member).
  - Mailpit của `scripts/dev-up.sh` ở `E2E_MAILPIT_URL` (mặc định mẫu `http://localhost:8025/`, đặt trong `frontend/.env.e2e`). Helper `latestInvitationLink(email)` ở `frontend/e2e/mailpit.ts` đọc thư mới nhất gửi tới `email`, trả phần `#/invite?token=...` (bỏ gốc `FrontendBaseUrl`, vì Playwright mở bản preview ở cổng 4173), thử tối đa 10 lần cách 500 ms.
  - Spec mời `e2e+<mốc thời gian>@test.local` và tự dọn: gỡ thành viên hoặc thu hồi lời mời. Tài khoản đăng ký (`e2e-invitee-<mốc>`) còn lại sau mỗi lần chạy.
- **Cách kiểm tra "đã chạy đúng":**
  1. `e2e-admin` mở **Members**: thấy mình `(you)` với nút _Leave organization_, người khác có nút `…` (menu). Dòng bị khoá hiện lý do (`member-lock-reason`) thay cho nút.
  2. **Invite member**, nhập email, chọn _Member_, _Send invitation_: toast `Invitation sent to …`; tab **Invitations** có dòng mới với hạn 7 ngày; _Revoke_ rồi xác nhận thì dòng biến mất.
  3. Thư tới Mailpit chứa `/#/invite?token=...` (xem _Nhận lời mời_).
  4. Chủ sở hữu duy nhất bấm _Leave organization_ rồi _Confirm_: câu `You are the only owner…`, không còn nút xác nhận, vẫn ở trong tổ chức (AC-277).
  5. `e2e-user` mở **Members**: có danh sách, không có tab Invitations, Roles, không có nút _Invite member_ (AC-298).
- **Rủi ro hay gặp:** các trang Members, Invitations, Roles chỉ khác nhau ở phần sau dấu `#`, nên điều hướng giữa chúng bằng `page.goto` không tải lại và store giữ danh sách cũ; E2E gọi `page.reload()` sau khi thao tác ở trang khác. Dòng `member-change-roles`, `member-remove` ẩn (`hidden`) ở dòng người khác chỉ để test, người dùng thật dùng menu `member-menu`.

## Nhận lời mời

- **PRD:** PRD00-FR-25, FR-61, BR-81: AC-57, AC-182 (email coi là đã xác minh), AC-304 (form đăng ký, email điền sẵn không sửa được), AC-306 (tài khoản đang đăng nhập khác email chỉ thấy câu không dành cho tài khoản này).
- **Vào bằng:** link trong thư `…/#/invite?token=...` (route công khai `invite`, ngoài khung ứng dụng). Trang bỏ `token` khỏi thanh địa chỉ ngay khi mở (giữ trong `sessionStorage` qua lần đăng nhập), thêm thẻ `noindex` và `referrer: no-referrer`.
- **Frontend:** `frontend/src/views/InviteView.vue`. Trạng thái theo kết quả `POST /api/invitations/lookup`:
  - `Valid` + chưa đăng nhập: form `invite-form` (`#invite-name`, `#invite-username`, `#invite-password`, email chỉ đọc `#invite-email`), nút `invite-submit`, link `invite-login-link` tới đăng nhập rồi quay lại; đăng ký xong tải lại vào thẳng tổ chức
  - `Valid` + đã đăng nhập đúng email: thẻ tài khoản `invite-account-card` và nút `invite-accept`
  - Các trạng thái kết quả `invite-state-<status>` với tiêu đề `invite-state-title`: `WrongAccount` (kèm `invite-signout`, không hiện tên tổ chức, vai trò hay email của lời mời), `EmailNotConfirmed` (`invite-resend`), `Expired`, `NoLongerValid`, `OrganizationSuspended`; lookup lỗi mạng là `invite-load-error`
- **Backend:** API-20, API-21, API-22; xem mục _Lời mời_.
- **Test đã có:** frontend unit `frontend/tests/views/InviteView.test.ts`; E2E: `frontend/e2e/invitation.spec.ts` (người được mời đăng ký từ link trong thư vào `Dev` với vai trò Member, email điền sẵn không sửa được, `token=` biến khỏi URL; `e2e-user` đang đăng nhập mở link của người khác chỉ thấy `invite-state-WrongAccount`).
- **Dữ liệu test:** như mục _Thành viên và lời mời_; link lấy từ Mailpit bằng `latestInvitationLink`.
- **Cách kiểm tra "đã chạy đúng":**
  1. Gửi lời mời tới một email mới, mở link trong cửa sổ ẩn danh: tiêu đề `Invitation to Dev`, form đăng ký, ô email có sẵn địa chỉ và không sửa được (AC-304); thanh địa chỉ không còn `token=`.
  2. Điền tên, tên đăng nhập, mật khẩu, _Create account and join_: vào thẳng `Dev`; `e2e-admin` thấy người đó ở **Members** với vai trò Member (AC-57), và **Audit log** có hai dòng: _sent an invitation to … (role Member)_, _(… ) joined Dev with the role Member_ (AC-182).
  3. Đăng nhập `e2e-user` rồi mở link gửi cho email khác: chỉ thấy _This invitation isn't for the account you are using_ và nút đăng xuất; không có tên `Dev`, vai trò hay email lời mời (AC-306).
  4. Mở lại link đã dùng hoặc đã thu hồi: _This invitation can no longer be used_.
- **Rủi ro hay gặp:** link trong thư có gốc là `EmailSettings:FrontendBaseUrl` (`http://localhost:5175`), không phải bản preview E2E; chỉ phần `#/invite?token=...` dùng được. Gửi lời mời cần Mailpit (SMTP 1025) chạy, nếu không API trả 503 `InvitationEmailDeliveryFailed`.

## Vai trò tự tạo

- **PRD:** PRD00-FR-52, FR-64, FR-65, FR-66: bước 1 AC-55 (vai trò hệ thống không sửa được); bước 2 AC-290 (tên trùng không phân biệt hoa thường); bước 3 AC-248 (vai trò còn người giữ không xoá được).
- **Vào bằng:** **Members** → tab **Roles** (`tab-roles`, `/#/organization/members/roles`), chỉ hiện với người có `organizations.roles.manage`; mở bằng URL khi thiếu quyền thấy _No permission_.
- **Frontend:**
  - `frontend/src/views/organization/RolesView.vue` (route `organization-roles`, `activeMenu = organization-members`), nút `create-role-btn`
  - `frontend/src/components/RoleRow.vue` (`role-row-<id>`, `role-menu`, `role-delete-blocked` khi còn người giữ vai trò, `role-view-only-reason`), `frontend/src/components/RoleSheet.vue` tạo và sửa (`role-name`, `role-name-error`, `permission-option-<code>`, `role-submit`, `role-error`)
  - Store: `frontend/src/stores/organization-people.ts`
- **Backend:** API-14 đến API-18; xem _Thành viên và vai trò của tổ chức_.
- **Test đã có:** `frontend/tests/views/organization/RolesView.test.ts`, `tests/components/RoleRow.test.ts`, `RoleSheet.test.ts`; E2E: `e2e-user` không thấy tab Roles (`frontend/e2e/organizations.spec.ts`); chưa có E2E tạo, sửa, xoá vai trò.
- **Dữ liệu test:** `e2e-admin` (Owner `Dev`).
- **Cách kiểm tra "đã chạy đúng":**
  1. `e2e-admin` mở tab **Roles**: các vai trò hệ thống (Owner, Admin, Member) có sẵn và chỉ xem (BR-60).
  2. _Create role_, đặt tên, chọn quyền, lưu: dòng mới hiện; tên trùng (không phân biệt hoa thường) báo lỗi dưới ô tên.
  3. Gán vai trò cho một thành viên rồi xoá vai trò: bị chặn, dòng báo còn người giữ (`role-delete-blocked`).
- **Rủi ro hay gặp:** một người chỉ gán được vai trò mà mình có đủ mọi quyền của nó (BR-58); lựa chọn bị khoá hiện lý do thay vì ẩn.

## Admin hệ thống: các tổ chức

- **PRD:** PRD00-FR-44 (danh sách mọi tổ chức), FR-46 (lý do khi vô hiệu), FR-38 (cấp admin hệ thống chỉ qua CLI): bước 1 AC-124 (tên, số thành viên, nhãn đã vô hiệu); bước 2 AC-138 (bỏ trống lý do thì không vô hiệu được), AC-105 (vô hiệu); bước 3 AC-106 (kích hoạt lại); bước 4 AC-126 (người không phải admin hệ thống bị từ chối).
- **Vào bằng:** rail → nhóm **System** → **Organizations** (`/#/system/organizations`), chỉ hiện với tài khoản `isSystemAdministrator`; admin vào được cả khi chưa làm việc ở tổ chức nào (từ link _Organizations_ ở trang chào). Tài khoản khác mở bằng URL thấy _No permission_. Cấp quyền admin hệ thống chỉ qua CLI (`grant-system-admin`, xem _Tổ chức đang làm việc và admin hệ thống_).
- **Frontend:** `frontend/src/views/system/SystemOrganizationsView.vue` (route `system-organizations`, `meta.systemAdministrator`, không `requiresOrganization`): danh sách phân trang (`system-orgs-show-more`), dòng `system-org-row-<id>`, menu `system-org-actions-<id>`, `system-org-deactivate-<id>` / `system-org-reactivate-<id>` (khổ điện thoại dùng `system-org-sheet-deactivate` / `system-org-sheet-reactivate`), lỗi tải `system-orgs-error`; hộp thoại vô hiệu dùng `DeactivateOrganizationDialog.vue` với lý do bắt buộc
- **Backend:** API-24 đến API-26 → `SystemOrganizationsController` (policy `SystemAdministrator`); xem _Tổ chức đang làm việc và admin hệ thống_.
- **Test đã có:** `frontend/tests/views/system/SystemOrganizationsView.test.ts`, `tests/unit/lib/route-access.test.ts`; chưa có E2E (`seed-dev` không tạo admin hệ thống).
- **Dữ liệu test:** cấp admin hệ thống cho một tài khoản đã xác minh email bằng `dotnet run --project backend/src/FeedbackHub.API -- grant-system-admin --email <email>`.
- **Cách kiểm tra "đã chạy đúng":**
  1. Đăng nhập tài khoản admin hệ thống: **System → Organizations** liệt kê mọi tổ chức với số thành viên; tổ chức đang tạm ngừng có nhãn.
  2. _Deactivate_ một tổ chức: ô lý do bắt buộc (bỏ trống thì báo lỗi dưới ô); xong thì dòng đổi nhãn. Thành viên của tổ chức đó gọi API nhận 403 `OrganizationSuspended` và được đưa về trang chào.
  3. _Reactivate_ rồi xác nhận: tổ chức hoạt động lại.
  4. Đăng nhập tài khoản thường, mở `/#/system/organizations`: _No permission_.
- **Rủi ro hay gặp:** lý do vô hiệu chỉ nằm trong snapshot `Change` của `audit_logs`, không hiện ở _Audit log_ của tổ chức.

# Feature map

Bản đồ tính năng theo góc nhìn người dùng: vào bằng đâu, code nằm ở đâu, kiểm tra thế nào. Mỗi tính năng là một mục `##`. Thêm tính năng mới theo cùng cấu trúc.

---

## Hồ sơ và phiên đăng nhập

- **PRD:** PRD00-FR-67 (đổi email, BR-82): AC-342 (dòng chờ xác nhận, thư tới email mới), AC-344 (link cũ hết hiệu lực), AC-352 (mật khẩu hiện tại khi đổi email).
- **Vào bằng:** avatar ở góc trên bên phải (`app-header-user-menu`) → **Profile** (`user-menu-profile`) → mở dialog `Profile` → tab **Login sessions**. Vai trò: mọi người dùng đã đăng nhập (đã kiểm tra với tài khoản role `User`).
- **Frontend:**
  - Menu: `frontend/src/layouts/UserMenu.vue`
  - Dialog và tab sessions: `frontend/src/components/ProfileDialog.vue`
  - Avatar: tab Personal information, testid `profile-avatar`, `profile-avatar-input`, `profile-avatar-change`, `profile-avatar-remove`
  - Avatar ở header: `user-menu-avatar` (nút) và `user-menu-panel-avatar` (trong menu) hiện ảnh từ `auth.account.avatarUrl` (`AccountDto.AvatarUrl`, do `TokenIssuer` dựng lúc đăng nhập/refresh); dialog Profile cập nhật lại giá trị này sau khi tải lên/xóa
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
  - Frontend unit đổi email: `frontend/tests/components/ProfileDialog.test.ts` (nhóm `email change`), `frontend/tests/views/ConfirmEmailChangeView.test.ts`, `frontend/tests/unit/api/client.test.ts` (401 `InvalidCurrentPassword` không refresh)
  - Frontend unit: `frontend/tests/components/ProfileDialog.test.ts` (avatar: hiện, upload, chặn ảnh quá lớn/sai loại, lỗi server, xóa); `loadSessions`, `confirmRevokeOtherSessions` vẫn chưa được bao phủ
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

- **PRD:** PRD00-FR-42, FR-44, FR-46, FR-53, FR-63, FR-38. Đối chiếu AC cụ thể (đổi tên API-04 AC-226 (slug không đổi), tạo tổ chức không ghi nhật ký AC-364) theo test trong mục Test.
- **Vào bằng:** chưa có màn hình (phần 8 của kế hoạch). Gọi API bằng token của thành viên tổ chức, hoặc của admin hệ thống cho `api/system/*`. Cấp và thu hồi admin hệ thống chỉ qua CLI trên server: `dotnet run --project backend/src/FeedbackHub.API -- grant-system-admin --email <email>` và `revoke-system-admin --email <email>` (mã thoát 0 xong hoặc không có gì để làm, 1 sai cú pháp, 2 không có tài khoản, 3 email chưa xác minh).
- **Frontend:** phần 3 không thêm màn hình; phần 8 dựng màn hình tổ chức.
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

- **PRD:** PRD00-FR-23, FR-29, FR-52, FR-59, FR-60, FR-62, FR-64, FR-66.
- **Vào bằng:** chưa có màn hình (phần 8 của kế hoạch). Trang Tổ chức chỉ còn danh sách và nút chuyển tổ chức; hai hộp thoại thành viên, vai trò cũ đã bỏ cùng route `api/organizations/{id}/...`. Gọi API bằng token của thành viên tổ chức đang làm việc.
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

- **PRD:** PRD00-FR-25, FR-61, BR-81.
- **Vào bằng:** chưa có màn hình (phần 8 của kế hoạch). Gọi API bằng token của thành viên tổ chức đang làm việc có quyền `organizations.members.manage`; ba API công khai (lookup, accept, register-with-invitation) gọi từ trang `#/invite?token=...` khi phần 8 dựng xong.
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
- **Rủi ro hay gặp:** thiếu `EmailSettings:FrontendBaseUrl` làm ứng dụng không khởi động; vượt giới hạn tốc độ trả 429; mã lỗi lời mời mới mới chỉ có chữ trong resx, chữ `vi`/`en` ở frontend thuộc phần 8.
- **Frontend:** chưa có; phần 8 dựng màn hình *Lời mời* và *InviteAccept*.

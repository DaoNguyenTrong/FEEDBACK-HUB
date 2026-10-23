# TECH-00 — Nền tảng: đa tổ chức, thành viên, lời mời, vai trò, admin hệ thống, quyền theo sản phẩm, design system mới

| Mục | Giá trị |
|---|---|
| Mã | `TECH00` |
| PRD | `PRD00` v1.3 (Approved 2026-10-23), `docs/prd/00-tong-quan-va-nen-tang.md`. PRD-00 v1.4 (câu 118, D18, AC-352–AC-358: xác thực lại khi đổi email; câu 119, US-27, FR-68 (Must), BR-83, BR-84, D19, AC-359–AC-375: màn hình nhật ký kiểm toán) đã viết xong, đang chờ người dùng duyệt; TECH00 chỉ được duyệt sau khi v1.4 được duyệt |
| Thiết kế giao diện | https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn — hệ thiết kế: *SystemLight/Dark*, *ComponentsLight/Dark*; module 00: *AppShell*, *OrgContext*, *Members*, *InviteAccept*, *Roles*, *OrgSettings*, *SysOrgs* (sáng, tối; 1280, 834, 390 px) |
| Trạng thái | In review |
| Phiên bản | 0.9 |
| Cần appsec review | Có (định danh, token lời mời, phiên, quyền, đường mở lời mời ẩn danh, admin hệ thống). Đạt v0.9 (appsec kiểm lại nhật ký kiểm toán; C1–C3 không chặn, đã áp) |
| Người duyệt | Người dùng (sau review của `appsec`) |
| Cập nhật | 2026-10-23 |
| Phụ thuộc | Không có. TECH01–TECH07 phụ thuộc tài liệu này (cổng tổ chức §5.1, phạm vi theo sản phẩm §5.5, quy tắc file §7.5) |
| Kế hoạch triển khai | Chưa có |

## 1. Phạm vi và truy vết

Tài liệu này thiết kế phần PRD-00 thuộc nền tảng: tổ chức (tạo với định danh ngắn, đổi tên, vô hiệu, kích hoạt lại), ngữ cảnh tổ chức và trang chào, thành viên (danh sách, đổi vai trò, gỡ, tự rời), lời mời (gửi, danh sách, thu hồi, mời lại, xoá, mở, đăng ký và chấp nhận), vai trò tự tạo, chống leo thang quyền, admin hệ thống, mô hình quyền theo sản phẩm dùng chung cho các module sau, các yêu cầu chạy nhiều máy API, việc đưa design system "Lớp kính" vào frontend, và sửa hai lỗi bảo mật có sẵn trong code mà review `appsec` tìm ra: đổi email ở hồ sơ không xác minh lại (B1, §6.7) và đọc sai claim của token Microsoft (B2, §6.6). Hai lỗi này là việc đầu tiên của kế hoạch module 00, không làm hotfix riêng (§10.2).

**Bao phủ trực tiếp** (thiết kế và kiểm thử ở đây): FR-02, FR-03, FR-12, FR-21, FR-23, FR-24, FR-25, FR-29, FR-30 (phần chặn chuyển tổ chức và vô hiệu), FR-31, FR-37 (với dữ liệu quản trị tổ chức), FR-38, FR-42, FR-44, FR-45, FR-46, FR-52, FR-53, FR-54, FR-56 (quy tắc nền), FR-59 đến FR-68.

**Đặt nền, hành vi kiểm ở module khác**: FR-01, FR-04 đến FR-09, FR-11 dùng cổng tổ chức (§5.1) và phạm vi theo sản phẩm (§5.5) mà tài liệu này định nghĩa; màn hình và endpoint của chúng thuộc TECH01 (sản phẩm, gán Phụ trách/Viewer), TECH02 (widget), TECH03 (hộp feedback), TECH06 (Tổng quan).

**Cố ý không làm ở đây**:

- Vòng đời feedback (FR-13 đến FR-17, FR-20, FR-26 đến FR-28, FR-35): TECH03. Link riêng của khách (FR-10): TECH04. Kết quả khảo sát (FR-49): TECH07.
- Nội dung và người nhận email (vô hiệu, kích hoạt lại, thu hồi lời mời, rời tổ chức, vào tổ chức): PRD05/TECH05. Riêng email mời bắt buộc có từ bây giờ vì FR-25; dùng mẫu tạm, xem §14 câu P3.
- Widget tạm ngừng, link riêng hết hiệu lực khi tổ chức bị vô hiệu (AC-71, AC-72, AC-129, AC-130, AC-145): TECH02, TECH04 đọc trạng thái tổ chức theo §3.1.

Bảng truy vết đầy đủ ở §13.

## 2. Kiến trúc

### 2.1 Thành phần mới hoặc thay đổi

| Tầng | Thành phần | Việc |
|---|---|---|
| Domain | `Organization` | Thêm trường vô hiệu (§3.1); `Create` kiểm định dạng định danh ngắn (BR-78); `Update` chỉ đổi tên; `Deactivate(DeactivationParams)`, `Reactivate()` |
| Domain | `OrganizationInvitation` (mới) | Lời mời; `Create` kiểm email, vai trò, hạn |
| Domain | `SystemAdministrator` (mới) | Danh sách admin hệ thống |
| Domain | `Product`, `ProductMember` (thiết kế ở đây, dựng ở kế hoạch TECH01) | Mô hình sản phẩm và vai trò theo sản phẩm (§3.6) |
| Domain | `Role`, `OrganizationMember` | Cài `IHasOrganization`; `Role` chuẩn hoá tên (NFC, trim) |
| Application | `OrganizationContextService` | Đọc, đổi tên, vô hiệu tổ chức đang làm việc |
| Application | `MemberService` (thay `OrganizationMembershipService`) | Danh sách, đổi vai trò, gỡ, rời. Bỏ `AddMemberAsync` |
| Application | `InvitationService` (mới) | Gửi, mời lại, thu hồi/xoá, danh sách, tra cứu, chấp nhận |
| Application | `RoleService` | Hai đường đọc, tạo/sửa/xoá theo BR-58, BR-60, BR-79 |
| Application | `RoleGrantPolicy` (mới) | Một chỗ duy nhất tính BR-58 điểm 1–3 (§5.3); khi ghi, đầu vào đọc thẳng từ cơ sở dữ liệu sau khi khoá tổ chức |
| Application | `SystemOrganizationService` (mới) | Danh sách tổ chức, vô hiệu, kích hoạt lại cho admin hệ thống |
| Application | Cổng `IAuditTrail` (mới) | Ghi mục nhật ký kiểm toán ngữ nghĩa (thu hồi, xoá lời mời, rời tổ chức) |
| Application | Cổng `IOrganizationLock` (mới) | Khoá dòng tổ chức trong một giao dịch (§5.4) |
| Application | Cổng `IProductAccessScope` (hợp đồng ở đây, hiện thực ở kế hoạch TECH01) | Phạm vi theo sản phẩm cho TECH01–TECH06 (§5.5) |
| Application | Cổng `ISystemAdministratorResolver` (mới) | Tra cứu admin hệ thống có cache |
| Infrastructure | `OrganizationContextAuthorizationHandler` (thay hai handler `ActiveOrganization*` và hai handler theo route `{id}`) | Cổng tổ chức, thứ tự BR-77 bước 1–4 (§5.1) |
| Application | `ITenantAccessService` | `HasActiveAccessAsync` (bool) thay bằng `GetAccessAsync` trả `NotMember`, `Suspended`, `Active`, kiểm thành viên trước trạng thái tổ chức (§5.1) |
| Infrastructure | `SystemAdministratorAuthorizationHandler` | Policy `SystemAdministrator` |
| Infrastructure | `RedisFixedWindowRateLimiter` | Đếm giới hạn tần suất chung giữa các máy (§12 QĐ-02) |
| Infrastructure | `MicrosoftJwtValidator`, `MicrosoftAuthProvider` | Tắt ánh xạ claim (`MapInboundClaims = false`, §6.6); `ProviderUserId` = `sub` thô; đọc `tid`, `xms_edov`, `email` theo tên thô; phần tenant của issuer phải bằng `tid`; tính `EmailVerified` theo §6.6; bỏ `preferred_username` và phương án dự phòng `NameIdentifier` (B2) |
| Application | `AuthService.ExternalLoginAsync` | Kiểm "email đã xác minh" chỉ ở nhánh khớp email và nhánh tạo tài khoản, không còn ở đầu hàm; kiểm `account.Status` ở nhánh đã liên kết và nhánh khớp email; xác minh lại tài khoản cũ khi token chứng nhận đúng email (§6.6) |
| Application | `AuthService.RefreshTokenAsync` | Mất quyền vào tổ chức (bị gỡ, tổ chức tạm ngừng) → phát token mới với `org_id = null` thay vì 401; tài khoản bị khoá vẫn 401 (§6.5) |
| Application | `TokenIssuer` | Chỗ duy nhất đặt `isSystemAdministrator` cho mọi `LoginResponse` (API-27), và chỗ duy nhất kiểm `account.Status` trước khi phát phiên: tài khoản bị khoá → 401, cho cả sáu đường phát phiên (login, refresh, verify-email, external, register-with-invitation, switch-organization) |
| Infrastructure | `AppDbContext` (audit) | Loại cột nhạy cảm của `Account` khỏi snapshot audit ở mức thuộc tính (§3.7) |
| Domain, Application | `Account` (`PendingEmail`), `EmailVerificationToken` (`Email`), `AccountService`, `RegistrationService` | Đổi email ở hồ sơ qua email chờ xác nhận (B1, §3.8, §6.7) |
| API | `ProfileController`, `AuthController` | API-29–API-32: đổi email chờ xác nhận, gửi lại, huỷ, xác nhận (§4.9) |
| Infrastructure | `SmtpEmailSender` | Không ghi người nhận vào log; một hạn 15 giây cho kết nối, xác thực, gửi; production bắt buộc StartTls hoặc SSL (§6.2, §10.3) |
| Infrastructure | `ProductionTopologyValidator` | Khởi động thất bại nếu cấu hình production không đủ cho nhiều máy hoặc thiếu điều kiện bảo mật (proxy tin cậy, cookie `Secure`, SMTP mã hoá) (§10.3) |
| API | `CurrentOrganizationController` (`api/organization`) | Tổ chức đang làm việc, thành viên, lời mời, vai trò |
| API | `InvitationsController` (`api/invitations`) | Tra cứu, chấp nhận lời mời |
| API, Application | `CurrentOrganizationController` (`GET api/organization/audit-log`), `AuditLogService`, `AuditLogRepository` | Màn hình nhật ký kiểm toán (API-33, §6.8); thay `AuditLogsController` (`api/admin/audit-logs`), bỏ route cũ |
| API | `SystemOrganizationsController` (`api/system/organizations`) | Admin hệ thống |
| API | `AuthController` | Thêm `register-with-invitation`; `LoginResponse` thêm `isSystemAdministrator`; `external/{provider}` thêm `accountCreated` |
| API | `OrganizationsController` | Chỉ còn `GET` (tổ chức của tôi) và `POST` (tạo). Bỏ mọi route `api/organizations/{id}/...` |
| API | Lệnh CLI `grant-system-admin`, `revoke-system-admin` | Đường duy nhất cấp, bỏ admin hệ thống (BR-20). `grant` cần tài khoản có email đã xác minh và in username, id; `revoke` xoá cache của `ISystemAdministratorResolver` |
| Infrastructure | Migration M8 | Đánh dấu chưa xác minh các tài khoản chỉ có email do Microsoft chứng nhận theo cách đọc claim cũ (§10.1) |
| Frontend | Design system "Lớp kính", khung ứng dụng mới, các màn hình module 00 | §8 |

### 2.2 Luồng dữ liệu chính

```
Trình duyệt ──cookie access_token (JWT: sub, org_id)──► nginx ──► một trong N máy API
   │                                                              │
   │                         UseAuthentication → UseAuthorization (cổng tổ chức §5.1, policy quyền)
   │                                                              │ → TenantAccessMiddleware (lưới cuối cho endpoint chỉ có [Authorize])
   │                                                              ▼
   │                                       Controller → Service (BR-58 §5.3, khoá tổ chức §5.4)
   │                                                              │
   │                       PostgreSQL (dữ liệu, Hangfire, khoá Data Protection)   Redis (cache quyền, giới hạn tần suất, backplane SignalR)
   │                                                              │
   └─────────────── email mời (SMTP) chứa  {FrontendBaseUrl}/#/invite?token=<mã>  (mã nằm trong fragment, không tới máy chủ)
```

Hướng phụ thuộc giữ Domain ← Application ← Infrastructure/API. Cổng mới nằm cạnh service dùng nó, không trong `Common/` (quy tắc không có vòng phụ thuộc namespace).

## 3. Mô hình dữ liệu

Mọi entity tạo qua `static Create(XxxParams)`, kiểm hợp lệ trong `Update`. Thời gian là `timestamptz` UTC.

### 3.1 `organizations` (sửa)

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `Name` | varchar(200) | Có | Không trống sau trim (BR-65, AC-187) |
| `Slug` | varchar(100) | Có | Chỉ đặt trong `Create`. Định dạng BR-78: `^[a-z0-9]+(-[a-z0-9]+)*$`, dài 3–50, đầu vào đổi về chữ thường. Không có phương thức đổi; `Update` không nhận slug (BR-65, AC-226). Slug cũ không đúng định dạng giữ nguyên, không kiểm lại. Chỉ mục unique đã có |
| `Status` | bool | Có | Giữ nguyên nghĩa: `false` = đã vô hiệu |
| `DeactivatedAt` | timestamptz | Không | Mới |
| `DeactivatedByAccountId` | uuid | Không | Mới; không FK (tài khoản có thể bị xoá) |
| `DeactivatedBySystemAdministrator` | bool | Có, mặc định `false` | Mới; `true` khi người vô hiệu là admin hệ thống **không** là thành viên đang hoạt động của tổ chức lúc thao tác (cùng phép tra với nhãn nhật ký, §3.7). PRD05 dùng để ghi "admin hệ thống FeedbackHub" thay tên người (câu 69 của người dùng) |
| `DeactivationReason` | varchar(500) | Không | Mới; bắt buộc khi người vô hiệu là admin hệ thống (BR-54), tối đa 500 (BR-66). Kích hoạt lại xoá bốn trường này; lịch sử nằm trong nhật ký kiểm toán |

`Organization` không cài `IHasOrganization` (nó là tổ chức, không thuộc tổ chức).

### 3.2 `organization_members`, `organization_member_roles` (sửa)

- `OrganizationMember` cài `IHasOrganization`: thay đổi của nó vào nhật ký kiểm toán của tổ chức (snapshot chỉ có `AccountId`, `IsActive`, không có email).
- Gỡ hoặc rời: đặt `IsActive = false`, **xoá** mọi dòng `organization_member_roles` của thành viên đó trong cùng giao dịch (BR-30). Việc xoá `product_members` thêm vào khi TECH01 dựng bảng đó (nghĩa vụ ở §5.5). Dòng `organization_members` giữ lại để lời mời sau này kích hoạt lại đúng dòng đó (chỉ mục unique `(OrganizationId, AccountId)` đã có).
- Mọi phép đếm người giữ vai trò lọc `IsActive = true` (BR-60, AC-252), kể cả với dữ liệu cũ còn sót (migration M4 dọn dữ liệu cũ, §10.1).

### 3.3 `roles`, `role_permissions` (sửa)

- `Role` cài `IHasOrganization`. `Name` được trim và chuẩn hoá Unicode NFC trong `Role.Update` (chặn "chủ sở hữu" viết dạng tổ hợp lọt qua so sánh).
- Bỏ chỉ mục unique `(OrganizationId, Name)`, thay bằng chỉ mục unique biểu thức `(OrganizationId, lower("Name"))` (BR-79, AC-290, AC-326). Vì ba vai trò hệ thống lưu tên `Owner`, `Admin`, `Member` trong cùng bảng, chỉ mục này cũng chặn vai trò tự tạo trùng tên tiếng Anh của vai trò hệ thống. Tên tiếng Việt "Chủ sở hữu" kiểm ở Application (§5.3, AC-291).
- Danh mục quyền thêm `feedbackhub.admin` (quyền Quản trị FeedbackHub, FR-23). `SystemRoleDefaults[Admin] = [organizations.members.manage, feedbackhub.admin]` (BR-60). Owner vẫn là `Permissions.All` tính động.
- FK `RoleId` của `organization_member_roles`, `role_permissions` giữ `ON DELETE CASCADE`; việc xoá vai trò còn người giữ bị chặn ở Application trước đó (BR-60).

### 3.4 `organization_invitations` (mới)

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `Id` | uuid v7 | Có | |
| `OrganizationId` | uuid | Có | FK `organizations` ON DELETE CASCADE; cài `IHasOrganization` |
| `Email` | varchar(200) | Có | Chuẩn hoá bằng `Account.NormalizeEmail` |
| `RoleId` | uuid | Có | FK `roles` ON DELETE CASCADE: xoá vai trò thì lời mời biến mất, mở link thấy "không còn hiệu lực" (BR-63 điểm 5, AC-181, AC-250) |
| `TokenHash` | char(64) | Có | SHA-256 hex của mã (dùng lại `TokenHash.Compute`); unique |
| `ExpiresAt` | timestamptz | Có | Lúc tạo + đúng 7×24 giờ, không làm tròn (BR-24, D4) |
| `CreatedAt`, `CreatedBy`, … | | | Trường audit chung |

- Chỉ mục unique `(OrganizationId, Email)`: mỗi email có tối đa một lời mời trong tổ chức (BR-63 §9).
- Trạng thái không lưu: dòng tồn tại và `ExpiresAt > now` là *đang chờ*; dòng tồn tại và đã quá hạn là *Hết hạn* (BR-75). Dùng, thu hồi, bị thay, bị xoá đều **xoá hẳn dòng**, nên mã cũ tra ra "không tìm thấy" và được báo "không còn hiệu lực" (AC-59, AC-179, AC-272). Không lưu mã thô ở đâu cả.
- Loại khỏi audit tự động (`AuditExcludedEntityTypes`), giống `RefreshToken`, `EmailVerificationToken`: snapshot sẽ chứa `TokenHash`. Mục nhật ký về lời mời ghi bằng `IAuditTrail` (§3.7).
- Không có job dọn: lời mời hết hạn nằm lại đến khi được mời lại hoặc bị xoá (BR-75).

### 3.5 `system_administrators` (mới)

`Id` uuid v7, `AccountId` uuid unique (FK `accounts` ON DELETE CASCADE), `CreatedAt`, `CreatedBy`. Chỉ lệnh CLI ghi bảng này (BR-20, FR-38). Không thuộc tổ chức nào; mục audit tự động có `OrganizationId = null`, nên không tổ chức nào thấy.

### 3.6 Mô hình sản phẩm lõi (thiết kế; dựng ở kế hoạch TECH01, TECH01 bổ sung trường)

| Bảng | Trường lõi | Ràng buộc |
|---|---|---|
| `products` | `Id`, `OrganizationId` (FK cascade, `IHasOrganization`), `Name` varchar(200), `ArchivedAt` timestamptz null | TECH01 thêm khoá công khai, domain, chế độ khách… |
| `product_members` | `Id`, `OrganizationId` (`IHasOrganization`, chỉ mục), `ProductId` (FK cascade), `OrganizationMemberId` (FK cascade), `Role` varchar(20) (`Handler` = Phụ trách, `Viewer`) | Unique `(ProductId, OrganizationMemberId)`: tối đa một vai trò theo sản phẩm mỗi người (BR-03, AC-09) |

Hai bảng này là thiết kế; dựng bảng, hiện thực `IProductAccessScope` và các test của chúng thuộc kế hoạch TECH01 (§14 T1, SA thay người dùng 2026-10-23). Kế hoạch TECH00 không tạo bảng sản phẩm nào.

### 3.7 Nhật ký kiểm toán

Bảng `audit_logs` thêm cột `kind` varchar(10) not null default `'Change'` (M9): `Change` là snapshot tự động hiện có (chỉ người vận hành đọc trong cơ sở dữ liệu, không endpoint nào trả nữa), `Event` là mục ngữ nghĩa. Enum `AuditLogKind { Change = 1, Event = 2 }` (không giá trị 0, nên một dòng quên gán không thành `Event`); `AuditEntry.ToAuditLog` gán `Kind = Change` tường minh, `IAuditTrail` gán `Event` tường minh. Cổng mới `IAuditTrail.RecordAsync(AuditEvent)` ghi một dòng `kind = 'Event'` trong cùng `SaveChanges` với thay đổi nó mô tả, nên không có mục cho thao tác bị từ chối hay rollback (AC-337). Chỉ mục mới `(organization_id, "timestamp" DESC, id DESC) WHERE kind = 'Event'` cho API-33.

Danh mục sự kiện là danh sách đóng (`AuditEventType` + khai báo tham số cho từng loại, một chỗ trong Application): `RecordAsync` từ chối (ném lỗi lập trình) tham số không khai báo cho loại đó, nên không module nào vô tình ghi nội dung feedback hay snapshot vào mục ngữ nghĩa (BR-59, BR-83 điểm 4). `EntityName`, `Action` lấy từ loại; tham số lưu trong `new_values`. Tên vai trò hệ thống lưu kèm `systemKind` để hiện theo ngôn ngữ người xem.

`AuditEvent { OrganizationId, ActorAccountId, ActorKind, Type, Parameters }` mang **tường minh** tổ chức và người làm (A1):

- `OrganizationId` lấy từ đối tượng của sự kiện, không từ JWT: `invitation.OrganizationId` (API-11–API-13, API-21, API-22), `organization.Id` (API-04, API-05, API-25, API-26), `member.OrganizationId` hoặc `role.OrganizationId` (API-07–API-09, API-16–API-18). Nhờ vậy nhận lời mời vào A khi đang làm việc ở B hoặc ở ngữ cảnh cá nhân, đăng ký ẩn danh qua lời mời, và admin hệ thống thao tác trên A khi đang làm việc ở C đều ghi vào A. `IAuditTrail` không phụ thuộc `ICurrentTenantProvider`; `RecordAsync` ném lỗi lập trình khi `OrganizationId` là `Guid.Empty`.
- **Lưới bằng code (C1, chọn phương án a):** `AuditEvent` không có constructor công khai nhận `Guid` tổ chức; chỉ dựng được qua `AuditEvent.For(IHasOrganization subject, …)` (chép `subject.OrganizationId`) và `AuditEvent.For(Organization organization, …)` (chép `organization.Id`), `OrganizationId` chỉ có `private init`. Service vì vậy phải có trong tay entity đã tra (theo `org_id` hoặc theo route của policy `SystemAdministrator`) mới ghi được mục; không có đường nào đưa một `Guid` lấy từ thân hay query request vào. Lý do không chọn (b) (kiểm trong `SaveChanges` rằng tổ chức của mục trùng một entity `IHasOrganization` hay `Organization` đổi cùng lần lưu): một số sự kiện chỉ đổi bảng nối không cài `IHasOrganization` (`organization_member_roles`, `role_permissions`), nên (b) hoặc báo sai, hoặc phải mở ngoại lệ, và nó chỉ bắt lỗi lúc chạy; (a) bắt lúc biên dịch, không tốn gì lúc chạy.
- `ActorAccountId` do service truyền: người gọi ở mọi endpoint có phiên; ở API-22 là tài khoản vừa tạo (request ẩn danh, chưa có `sub`). Ghi vào `user_id`.
- `ActorKind` chọn theo **tư cách của người làm**, không theo nơi thao tác (PRD-00 v1.4 §3.1, BR-84 điểm 12, BR-53; cùng quy tắc với thư báo vô hiệu tổ chức, câu 69 của người dùng):
  - `SystemAdministrator` (hiện "admin hệ thống FeedbackHub", không tên, không email, không lý do) **chỉ** khi sự kiện là vô hiệu hoặc kích hoạt lại tổ chức X và người làm là admin hệ thống **không** là thành viên đang hoạt động của X lúc thao tác (AC-368).
  - `Account`, có tên, trong mọi trường hợp khác: người làm là thành viên của X lúc thao tác, kể cả admin hệ thống kiêm thành viên vô hiệu hay kích hoạt lại X từ danh sách tổ chức (API-25, API-26; AC-375), và mọi thao tác khác của thành viên như API-05 hay gửi lời mời (BR-53, AC-369).
  - Một phép tra duy nhất `SystemAdministratorActor.ResolveAsync(organizationId, accountId)` (Application, cạnh `SystemOrganizationService`): đọc dòng `organization_members` đang hoạt động của chính X trong cùng giao dịch (không qua cache), trả `SystemAdministrator` hoặc `Account`. API-25, API-26 gọi nó để chọn `ActorKind`; API-25 dùng cùng kết quả để đặt `organizations.DeactivatedBySystemAdministrator` (§3.1) mà thư báo vô hiệu của PRD05/TECH05 đọc, nên nhật ký và thư không lệch nhau. API-05 không gọi (người gọi đã qua cổng thành viên, luôn `Account`).
  - Tư cách được xét lúc ghi và chốt vào mục (`actor_kind`); người đó vào hay rời X sau này, mục cũ không đổi. Phép tra chỉ đọc danh sách thành viên của chính X nên không mở thêm đường lộ dữ liệu.
- Nhất quán người làm (C2): `RecordAsync` từ chối (lỗi lập trình) khi `ActorKind ∈ {Account, SystemAdministrator}` mà `ActorAccountId` rỗng, và khi `ActorKind = System` mà có `ActorAccountId`. `System` dành cho thao tác không có người làm (chưa có ở module 00). `ActorKind` lưu trong cột riêng của dòng (`actor_kind`, thêm ở M9), không trong `new_values`; `new_values` chỉ chứa tham số đã khai báo.
- API-25, API-26 không đổi gì (tổ chức đã vô hiệu, đã hoạt động; trả 204) thì không ghi mục.
- Mục `Event` **không** lưu `ip_address`, `user_agent` (để `null`): PRD không cho hiện hay trả hai trường này (BR-83, AC-371), và dòng snapshot `Change` của cùng giao dịch vẫn giữ chúng cho người vận hành điều tra (B4).

| Loại (`AuditEventType`) | Ghi khi | Tham số | Nguồn |
|---|---|---|---|
| `InvitationSent` | API-11, API-12 tạo lời mời mới. Không nhắc lời mời bị thay (D4, AC-325) | `email`, `role` | BR-84 (mời lại là một mục mới), AC-369, AC-372, AC-373 |
| `InvitationRevoked` | API-13 với lời mời còn hạn | `email`, `role` | BR-84, BR-75, AC-272 |
| `InvitationDeleted` | API-13 với lời mời hết hạn | `email`, `role` | BR-84, BR-75, D5, AC-268 |
| `MemberJoined` | API-21, API-22 vào tổ chức (người làm là người vào; ở API-22 là tài khoản vừa tạo) | `accountName`, `email`, `role` | BR-84, AC-374 |
| `MemberRolesChanged` | API-07 | `accountName`, `email`, `rolesBefore[]`, `rolesAfter[]` | BR-84, AC-367 |
| `MemberRemoved` | API-08 | `accountName`, `email`, `roles[]` | BR-84, AC-367 |
| `MemberLeft` | API-09 | `accountName`, `email` | BR-84, BR-80, AC-279 |
| `RoleCreated` | API-16 | `role`, `permissionCodes[]` | BR-84, AC-367 |
| `RoleUpdated` | API-17 | `nameBefore`, `nameAfter`, `permissionsAdded[]`, `permissionsRemoved[]` | BR-84, AC-367 |
| `RoleDeleted` | API-18 | `role` | BR-84, AC-367 |
| `OrganizationRenamed` | API-04 | `nameBefore`, `nameAfter` | BR-84, AC-367 |
| `OrganizationDeactivated` | API-05, API-25 | — (không ghi lý do, BR-84) | BR-84, AC-368 |
| `OrganizationReactivated` | API-26 | — | BR-84, AC-368 |

Danh mục khớp đúng BR-84 (PRD-00 v1.4; FR-68: "gồm các mục các PRD quy định, không gì khác"). Không ghi mục khi tạo tổ chức (AC-364), khi seed vai trò hệ thống, khi thao tác bị từ chối (AC-370), và cho lời mời bị thay (chỉ mục `InvitationSent` của lời mời mới; BR-84, AC-325). Module khác thêm loại của mình vào cùng danh mục (TECH01: §6.8 nghĩa vụ).

Loại theo thuộc tính (mới, cạnh `AuditExcludedEntityTypes` trong `AppDbContext`): `AuditExcludedProperties` gồm `Account.PasswordHash` và các cột khoá đăng nhập `FailedLoginCount`, `FailedLoginWindowStartedAt`, `LockedUntil`, `PasswordChangeLockedUntil` cùng mọi cột đếm của khoá đổi mật khẩu và của khoá xác thực lại khi đổi email (§6.7). Hash mật khẩu không bao giờ vào `old_values`/`new_values`; cột khoá chỉ cho biết ai đang bị dò mật khẩu, không có giá trị kiểm toán và vốn chỉ được ghi bằng câu lệnh riêng của `PasswordAttemptStore`. Thuộc tính bị loại không xuất hiện trong snapshot của mục `Added`, `Modified` lẫn `Deleted`.

Mục audit có `organization_id = null` (tài khoản, admin hệ thống, mọi entity không cài `IHasOrganization`) không được endpoint nào trả về. Hiện tại endpoint duy nhất đọc `audit_logs` là `GET api/admin/audit-logs[/{id}]`, lọc `OrganizationId == org_id` của JWT ở cả hai truy vấn (đã kiểm code 2026-10-23). Bản này thay nó bằng API-33, chỉ đọc `kind = 'Event'` và cũng lọc `organization_id = org_id` (§6.8); không endpoint nào trả mục `Change` hay mục không có tổ chức. Mọi endpoint audit mới (kể cả của module sau) phải giữ quy tắc này; test ở §11.

Với mục `Change` (snapshot tự động), `user_id` là người gọi và `organization_id` lấy như hiện nay. Với mục `Event`, `user_id` = `AuditEvent.ActorAccountId` và `organization_id` = `AuditEvent.OrganizationId`, không lấy từ JWT (A1). Email trong các mục ngữ nghĩa là ngoại lệ D12 (BR-74, BR-83 điểm 4): chỉ người có `auditlogs.view` của tổ chức đó đọc được (AC-335). Thay lời mời (mời lại, gửi lời mời mới cùng email) không có mục riêng (D4, AC-325). Không mục nào chứa mã hay `TokenHash`.

### 3.8 `accounts`, `email_verification_tokens` (sửa, B1)

| Bảng | Trường | Kiểu | Ghi chú |
|---|---|---|---|
| `accounts` | `PendingEmail` | varchar(200) null | Email mới đang chờ xác nhận, chuẩn hoá bằng `Account.NormalizeEmail`. Không unique, không kiểm trùng với `PendingEmail` của người khác (hai người cùng chờ một địa chỉ không lộ gì cho nhau; chỉ người mở được link mới đổi được) |
| `email_verification_tokens` | `Purpose` | varchar(20) null | `VerifyEmail` (xác minh email hiện tại, luồng đăng ký có sẵn) hoặc `ChangeEmail` (xác nhận đổi email). Dòng cũ để `null`, xử lý như `VerifyEmail` |
| `email_verification_tokens` | `Email` | varchar(200) null | Địa chỉ mà token chứng minh, ghi trên **mọi** token mới (N-new-1): `VerifyEmail` ghi `Account.Email` lúc phát, `ChangeEmail` ghi `PendingEmail`. `EmailVerificationToken.Create` bắt buộc cả hai trường; cột nullable chỉ vì dòng cũ và cửa sổ cuốn chiếu (mở rộng rồi thu hẹp, QĐ-03) |

- `Account.RequestEmailChange(string email)` đặt `PendingEmail` (từ chối nếu bằng `Email` hiện tại); `Account.CancelEmailChange()` xoá nó; `Account.ApplyEmailChange()` chép `PendingEmail` sang `Email`, xoá `PendingEmail`, gọi `ConfirmEmail()`; service gọi nó cùng lúc `Consume()` **mọi** token chưa dùng của tài khoản, cả `VerifyEmail` lẫn `ChangeEmail` (N-new-1). `Email` chỉ đổi qua `ApplyEmailChange`; `UpdateProfile` không nhận email nữa.
- `Account` đang được audit tự động (trừ các cột ở §3.7); `PendingEmail` vào snapshot như `Email` hiện nay, cùng mức riêng tư (mục có `organization_id = null`, không endpoint nào trả); nhờ đó người vận hành thấy được lịch sử đổi email trong cơ sở dữ liệu (§10.2 bước 6). `EmailVerificationToken` vẫn loại khỏi audit.
- Mọi nơi cần "email này của chủ tài khoản" dùng một vị từ duy nhất `Account.EmailConfirmed`: API-02, API-21, nhánh khớp email của đăng nhập ngoài, lệnh `grant-system-admin` (§12 QĐ-29).
- Không còn cột `external_logins.EmailVerified` (bản 0.3 đề xuất, đã bỏ): sau B1 và M8, `Account.EmailConfirmed` là đủ, không chỗ nào khác đọc cột đó.

## 4. Hợp đồng API

Mọi route viết thường. Lỗi dùng `CodedProblemDetails` (`code` + `detail` theo ngôn ngữ request); frontend ánh xạ `code` sang `locales/{vi,en}.ts`. Thời gian trả UTC, chuyển theo `X-TimeZone` như mọi endpoint. Sau khi đổi, xuất lại `shared/openapi/openapi.json` và chạy `codegen`.

### 4.1 Tổ chức của tôi và tổ chức đang làm việc

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-01 | `GET api/organizations` | Tổ chức mình là thành viên, kể cả đang bị vô hiệu (bộ chọn tổ chức) | — | 200 `OrganizationDto[]` | 401 | `SignedInUser` |
| API-02 | `POST api/organizations` | Tạo tổ chức, người tạo là Chủ sở hữu | `{ name ≤200, slug }` | 201 `OrganizationDto` | 400 `FieldRequired`, `OrganizationSlugInvalid`; 403 `EmailNotConfirmed` (mã có sẵn); 409 `OrganizationSlugAlreadyExists` (không nêu tổ chức đang dùng) | `SignedInUser` và `Account.EmailConfirmed` kiểm ở service (BR-23; tài khoản bị M8 đánh dấu vẫn đăng nhập được nên không dựa vào việc đã đăng nhập, §3.8) |
| API-03 | `GET api/organization` | Thông tin tổ chức đang làm việc | — | 200 `CurrentOrganizationDto { id, name, slug, status }` | 401, 403 | Cổng tổ chức |
| API-04 | `PATCH api/organization` | Đổi tên | `{ name ≤200 }` | 200 `CurrentOrganizationDto` | 400 `OrganizationNameRequired`; 401, 403 | `organizations.manage` |
| API-05 | `POST api/organization/deactivate` | Vô hiệu tổ chức đang làm việc | `{ reason?: string ≤500 }` | 204 | 400 `DeactivationReasonRequired` (người gọi là admin hệ thống, BR-54, AC-147), `FieldMaxLength`; 401, 403 | `organizations.manage` |
| API-06 | `GET api/organization/members` | Danh sách thành viên | `pageNumber`, `pageSize` (mặc định 50, tối đa 100) | 200 `PagedResult<OrganizationMemberDto>` | 401; 403 `OrganizationSuspended`; 404 (ngữ cảnh cá nhân, không phải thành viên đang hoạt động — kể cả admin hệ thống, AC-122) | Cổng tổ chức, `DenyMode.NotFound` (mọi thành viên, BR-74) |
| API-07 | `PUT api/organization/members/{accountId}/roles` | Đổi vai trò tổ chức | `{ roleIds: uuid[] ≥1 }` | 204 | 400 `OrganizationMemberRequiresAtLeastOneRole`; 403 `OwnerOnlyOperation` (người được đổi đang là Chủ sở hữu, hoặc thêm/bỏ vai trò Chủ sở hữu, mà người gọi không phải Chủ sở hữu; AC-340), `InsufficientRolePermissions`; 404 (thành viên, hoặc bất kỳ phần tử nào của `roleIds` không thuộc tổ chức đang làm việc); 409 `OrganizationCannotRemoveLastOwner` | `organizations.members.manage` |
| API-08 | `DELETE api/organization/members/{accountId}` | Gỡ thành viên | — | 204 | 403 (như trên); 404; 409 `OrganizationCannotRemoveLastOwner`, `CannotRemoveSelf` (`accountId` là chính người gọi, kiểm ở bước 7, sau BR-58; AC-337) | `organizations.members.manage` |
| API-09 | `POST api/organization/leave` | Tự rời tổ chức đang làm việc | — | 204 | 401, 403; 409 `LastOwnerCannotLeave` | Cổng tổ chức |

### 4.2 Lời mời (quản lý)

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-10 | `GET api/organization/invitations` | Lời mời đang chờ và hết hạn | `pageNumber`, `pageSize` (50/100) | 200 `PagedResult<InvitationDto>` | 401, 403 | `organizations.members.manage` |
| API-11 | `POST api/organization/invitations` | Gửi lời mời; nếu email đã có lời mời (chờ hoặc hết hạn) thì thay nó | `{ email ≤200, roleId }` | 201 `InvitationDto` | 400 `FieldEmailAddress`; 403 BR-58; 404 vai trò; 409 `InvitationEmailAlreadyMember`; 503 `InvitationEmailDeliveryFailed` | `organizations.members.manage` |
| API-12 | `POST api/organization/invitations/{id}/resend` | Mời lại cùng vai trò (lời mời hết hạn: *Mời lại*; lời mời đang chờ: gửi lời mời mới thay nó, BR-63 điểm 3) | — | 201 `InvitationDto` (id mới) | 403 BR-58; 404; 503 | `organizations.members.manage` |
| API-13 | `DELETE api/organization/invitations/{id}` | Thu hồi (đang chờ) hoặc xoá (hết hạn); ghi nhật ký | — | 204 | 403 BR-58; 404 | `organizations.members.manage` |
| API-14 | `GET api/organization/role-options` | Danh sách chọn vai trò khi mời, đổi vai trò | — | 200 `RoleOptionDto[]` | 401, 403 | `organizations.members.manage` |

### 4.3 Vai trò

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-15 | `GET api/organization/roles` | Phần Vai trò: mọi vai trò kèm quyền | — | 200 `RoleDto[]` | 401, 403 | `organizations.roles.manage` |
| API-16 | `POST api/organization/roles` | Tạo vai trò tự tạo | `{ name ≤100, permissionCodes[] }` | 201 `RoleDto` | 400 `FieldRequired`, `FieldMaxLength`, `UnknownPermissionCode`; 403 `InsufficientRolePermissions`; 409 `OrganizationRoleNameAlreadyExists`, `RoleNameReservedForSystemRole` | `organizations.roles.manage` |
| API-17 | `PUT api/organization/roles/{roleId}` | Sửa tên và quyền | như trên | 200 `RoleDto` | như trên; 403 `OrganizationSystemRoleImmutable`; 404 | `organizations.roles.manage` |
| API-18 | `DELETE api/organization/roles/{roleId}` | Xoá vai trò tự tạo | — | 204 | 403 `OrganizationSystemRoleImmutable`, `InsufficientRolePermissions`; 404; 409 `RoleHasHolders` | `organizations.roles.manage` |
| API-19 | `GET api/permissions` | Danh mục mã quyền (thêm `feedbackhub.admin`) | — | 200 `PermissionDto[]` | 401 | `[Authorize]` (không đổi) |

### 4.4 Mở và chấp nhận lời mời

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-20 | `POST api/invitations/lookup` | Tra lời mời theo mã, không tiêu thụ | `{ token }` | 200 `InvitationLookupDto` (§4.6) | 400; 429 | `[AllowAnonymous]` (có đọc phiên nếu có cookie); rate `invitation` |
| API-21 | `POST api/invitations/accept` | Chấp nhận bằng tài khoản đang đăng nhập | `{ token }` | 200 `{ organizationId, organizationName }` | 403 `EmailNotConfirmed` (tài khoản chưa xác minh email, §6.3); 409 `InvitationExpired`, `InvitationNoLongerValid`, `InvitationWrongAccount`, `InvitationOrganizationSuspended`; 401; 429 | `SignedInUser`; rate `invitation` |
| API-22 | `POST api/auth/register-with-invitation` | Đăng ký bằng email được mời; email coi là đã xác minh; đăng nhập luôn | `{ token, name, username, password, phone?, position?, address? }` (không có `email`: lấy từ lời mời) | 200 `LoginResponse` + `invitationOutcome: Joined \| OrganizationSuspended`; phiên phát qua `TokenIssuer` với `org_id` = tổ chức vừa vào (`null` khi `OrganizationSuspended`), `isPersistent = false`; đặt cookie như `login` | 400; 409 `InvitationExpired`, `InvitationNoLongerValid`, `AccountUsernameAlreadyExists`, `AccountEmailAlreadyExists`; 429 | `[AllowAnonymous]`; rate `auth` |
| API-23 | `POST api/auth/external/{provider}` (sửa) | Không đổi hành vi; phản hồi thêm `accountCreated: bool` để trang lời mời tự chấp nhận cho tài khoản vừa tạo (AC-178) | như cũ | như cũ | như cũ | như cũ |

### 4.5 Admin hệ thống và phiên

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-24 | `GET api/system/organizations` | Mọi tổ chức: tên, số thành viên đang hoạt động, dấu đã vô hiệu; sắp theo tên không phân biệt hoa thường | `pageNumber`, `pageSize` (50/100) | 200 `PagedResult<SystemOrganizationDto { id, name, memberCount, isDeactivated }>` | 401, 403 | `SystemAdministrator` |
| API-25 | `POST api/system/organizations/{id}/deactivate` | Vô hiệu | `{ reason: string 1–500 }` | 204 (đã vô hiệu thì vẫn 204, không đổi gì) | 400 `DeactivationReasonRequired`; 404 | `SystemAdministrator` |
| API-26 | `POST api/system/organizations/{id}/reactivate` | Kích hoạt lại | — | 204 (đang hoạt động thì 204) | 404 | `SystemAdministrator` |
| API-27 | `LoginResponse` (login, refresh, verify-email, external, register-with-invitation, switch-organization) | Thêm `isSystemAdministrator: bool`, đặt ở một chỗ duy nhất là `TokenIssuer` — chỉ để hiện nhóm "Hệ thống" ở giao diện; máy chủ vẫn kiểm từng request | | | | |
| API-28 | `POST api/auth/switch-organization` (sửa) | Cùng thứ tự với cổng §5.1: không phải thành viên đang hoạt động (kể cả tổ chức đang bị vô hiệu) → 403 `OrganizationAccessDenied`, không kèm tên; là thành viên mà tổ chức bị vô hiệu → 403 `OrganizationSuspended` kèm `organizationName` (trước đây `OrganizationAccessDenied`) | | | 403 | `[Authorize]` |

**Bỏ** (FR-59, BR-45, AC-300): `POST api/organizations/{id}/members` (thêm thẳng), `GET api/organizations/{id}/members`, `PATCH`/`DELETE api/organizations/{id}/members/{accountId}`, `GET`/`POST`/`PATCH`/`DELETE api/organizations/{id}/roles[...]`, `DELETE api/organizations/{id}`. Gọi lại các route này nhận 404 (route không tồn tại, người đã đăng nhập). Lý do và cách triển khai: §12 QĐ-06, §10.2.

### 4.6 Hình dạng DTO

- `OrganizationDto { id, name, slug, status, myRoleNames[] (giữ để tương thích), myRoles: RoleRefDto[], myPermissionCodes[] }`.
- `RoleRefDto { id, name, systemKind: "Owner" | "Admin" | "Member" | null }` — frontend hiện tên vai trò hệ thống theo ngôn ngữ giao diện từ `systemKind`, không từ `name`.
- `OrganizationMemberDto { accountId, accountName, email: string | null, roleIds[], roleNames[] (giữ để tương thích), roles: RoleRefDto[], isSelf, canChangeRoles, canRemove, lockReason: "OwnerOnly" | "InsufficientRolePermissions" | null, lockRole: RoleRefDto | null }`. `email` là `null` với người gọi không có `organizations.members.manage`, trừ dòng của chính họ (BR-74, AC-261–263). Ba cờ `can*` luôn `false` với người không có quyền quản lý thành viên; với người có quyền, tính theo §5.3: `canChangeRoles` = `CanTakeAway` với mọi vai trò hiện tại của người đó (BR-58 điểm 1 và 3, D14, AC-336, AC-340), `canRemove` = cùng điều kiện và người đó không phải chính người gọi (BR-80, D16, AC-339). Dòng của chính mình: `canChangeRoles = true` (người gọi luôn có mọi quyền của vai trò mình đang giữ; vai trò thêm vào theo `canGrant` của API-14, BR-58 điểm 2), `canRemove = false`; giao diện hiện *Rời tổ chức* và *Đổi vai trò*, không *Gỡ* (D16). Không đạt thì hàng khoá với `lockReason`: người đó là Chủ sở hữu và người gọi không phải → `OwnerOnly`, **kể cả khi** người gọi cũng thiếu quyền của vai trò khác của họ (lý do Chủ sở hữu thắng, BR-74, AC-341); ngược lại `InsufficientRolePermissions` với `lockRole` = vai trò đầu tiên (theo thứ tự hiển thị vai trò của dòng) mà người gọi thiếu quyền, để giao diện viết "Bạn không có đủ quyền của vai trò <tên>" (AC-338); `lockRole = null` khi `lockReason` khác `InsufficientRolePermissions`. Cờ chỉ để hiện; API-07, API-08 kiểm lại.
- `InvitationDto { id, email, role: RoleRefDto, expiresAt, status: "Pending" | "Expired", canRevokeOrDelete, canResend, lockReason }`.
- `RoleOptionDto { id, name, systemKind, canGrant }` — không có quyền nào (BR-77, AC-311).
- `RoleDto { id, name, systemKind, isSystem, permissionCodes[], holderCount, pendingInvitationCount: number | null, canEdit, canDelete, deleteBlockedReason: "SystemRole" | "InsufficientRolePermissions" | "HasHolders" | null }`. `holderCount` chỉ đếm thành viên đang hoạt động. `pendingInvitationCount` chỉ trả khi người gọi có thêm `organizations.members.manage` (BR-75: người khác không thấy lời mời nào), ngược lại `null`.
- `ProfileDto` thêm `pendingEmail: string | null` (§4.9) và `hasPassword: bool` (để form biết có hỏi mật khẩu hiện tại không, §6.7 bước 0; máy chủ vẫn tự kiểm).
- `InvitationLookupDto { status, organizationName?, role?: RoleRefDto, expiresAt?, email? }`, `status` thuộc `Valid | Expired | NoLongerValid | WrongAccount | EmailNotConfirmed | OrganizationSuspended`. Trường nào có theo từng trạng thái: §6.3. Phản hồi API-20 kèm `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`.

### 4.7 Mã lỗi mới (vi và en trong `Messages*.resx` và `locales/{vi,en}.ts`)

`OrganizationSuspended` (403, extension `organizationName`), `OwnerOnlyOperation` (403), `InsufficientRolePermissions` (403), `RoleHasHolders` (409), `RoleNameReservedForSystemRole` (409), `LastOwnerCannotLeave` (409), `CannotRemoveSelf` (409, BR-80, AC-337; câu báo "Bạn không thể tự gỡ mình khỏi tổ chức. Hãy dùng Rời tổ chức."), `OrganizationSlugInvalid` (400), `OrganizationNameRequired` (400), `DeactivationReasonRequired` (400), `InvitationEmailAlreadyMember` (409), `InvitationEmailDeliveryFailed` (503), `InvitationExpired`, `InvitationNoLongerValid`, `InvitationWrongAccount`, `InvitationOrganizationSuspended` (409), `SystemAdministratorRequired` (403), `EmailChangeLinkNoLongerValid` (409, §4.9), `EmailChangeNotPending` (409, §4.9), `EmailChangeSameAsCurrent` (400, chỉ ở Domain, §4.9), `EmailChangeTemporarilyLocked` (401, §6.7), `InvalidCursor` (400, API-33). Dùng lại `InvalidCurrentPassword` (401) cho mật khẩu hiện tại sai ở API-29. Dùng lại mã có sẵn: `EmailNotConfirmed` (403 ở API-02, API-21), `EmailVerificationTokenInvalidOrExpired` (chỉ `verify-email`), `AccountEmailAlreadyExists`, `ExternalLoginEmailNotVerifiedByProvider`. `OrganizationAccessDenied` giữ cho "không đủ quyền" của cổng tổ chức.

### 4.8 Phân trang và giới hạn tần suất

- Danh sách dùng `PagedResult<T>` (frontend chuẩn hoá bằng `toPagedResult()`), mặc định 50, tối đa 100, "Xem thêm" ở giao diện. Thứ tự ổn định: tên theo collation ICU (`COLLATE "und-x-icu"`, không phân biệt hoa thường, đúng thứ tự có dấu), rồi `Id` (D9, AC-302). Lời mời: `ExpiresAt` giảm dần rồi `Id`. Ngoại lệ: API-33 dùng keyset với `nextCursor`, không `PagedResult` (§6.8, QĐ-36).
- Chính sách giới hạn, đếm chung qua Redis (§12 QĐ-02), khoá theo IP thật (sau `UseForwardedHeaders`): `auth` (giữ cấu hình `AuthPermitLimit`, áp thêm cho API-22), `auth-refresh` (giữ), `invitation` mới cho API-20, API-21 (`RateLimiterSettings:InvitationPermitLimit`, mặc định 30 mỗi phút).

### 4.9 Đổi email ở hồ sơ (B1, §6.7)

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-29 | `PUT api/profile` (sửa) | Lưu hồ sơ. `email` khác cả `Email` lẫn `PendingEmail` hiện tại → ghi `PendingEmail`, vô hiệu mọi link đổi email trước đó, gửi link mới tới địa chỉ mới; `Email` không đổi (BR-82 điểm 1, 3). `email` bằng `Email` → không đụng tới email. `email` bằng `PendingEmail` → không gửi thư, không vô hiệu link (gửi lại là việc riêng của API-30). Khi email đổi và tài khoản có mật khẩu, phải kèm mật khẩu hiện tại (§6.7 bước 0) | như cũ, thêm `currentPassword?: string ≤ PasswordPolicy.MaxLength` (bắt buộc khi email đổi và tài khoản có mật khẩu; bỏ qua trong mọi trường hợp khác) | 200 `ProfileDto` (có `pendingEmail`) | 400 như cũ, `FieldRequired` (`currentPassword` thiếu khi bắt buộc); 401 `InvalidCurrentPassword` (sai), `EmailChangeTemporarilyLocked` (đang bị khoá); 409 `AccountEmailAlreadyExists` (địa chỉ là `Email` của tài khoản khác, kiểm như hiện nay; không so với `PendingEmail` của ai); 429 | `[Authorize]` người dùng; thêm rate `auth` |
| API-30 | `POST api/profile/email/resend` | *Gửi lại*: vô hiệu mọi link đổi email còn lại của tài khoản, phát link mới 24 giờ cho cùng `PendingEmail`; chỉ link mới nhất dùng được (BR-82 điểm 3, D17, AC-350) | — | 204 | 409 `EmailChangeNotPending` (không có email chờ); 429 | `[Authorize]` người dùng; rate `auth` |
| API-31 | `DELETE api/profile/email/pending` | *Huỷ*: xoá `PendingEmail`, vô hiệu mọi link đổi email của tài khoản (AC-347) | — | 204 (không có email chờ vẫn 204) | 401 | `[Authorize]` người dùng |
| API-32 | `POST api/auth/confirm-email-change` | Xác nhận đổi email bằng mã trong link; **không** phát phiên, không đặt cookie | `{ token }` | 204 | 409 `EmailChangeLinkNoLongerValid` (không có, đã dùng, đã bị vô hiệu, quá 24 giờ, `PendingEmail ≠ token.Email`, hoặc là token `VerifyEmail`; AC-344, AC-347, AC-349, AC-350), `AccountEmailAlreadyExists` (AC-345); 429 | `[AllowAnonymous]`; rate `auth` |

- `POST api/auth/verify-email` (có sẵn) chỉ nhận token `Purpose` = `VerifyEmail` (hoặc `null`) có `Email` **bằng** `Account.Email` hiện tại; token đổi email, token cũ không có `Email`, và token phát cho một địa chỉ mà tài khoản đã bỏ đều trả 401 `EmailVerificationTokenInvalidOrExpired`. Link đổi email vì vậy không bao giờ phát phiên, và một link xác minh cũ không xác minh được địa chỉ khác (N-new-1). Phiên do `verify-email` phát vẫn qua `TokenIssuer`, nên tài khoản bị khoá nhận 401.
- `POST api/auth/resend-verification` (có sẵn) không gửi gì cho tài khoản không có mật khẩu (`PasswordHash = null`, gồm mọi tài khoản M8): trả 204 như với email không tồn tại, không phát token. Tài khoản đó chỉ xác minh lại bằng đăng nhập Microsoft (§6.6 bước 4). Lý do: kẻ tấn công biết địa chỉ của một tài khoản M8 không được dùng thư xác minh (mà người nhận dễ tin) để đẩy nạn nhân mở link phát phiên cho tài khoản mang email đó (N-new-1).
- API-29 và API-30 tách bạch: API-29 chỉ phát link khi địa chỉ chờ **đổi**; API-30 là đường duy nhất phát lại cho cùng địa chỉ. Nhờ vậy lưu hồ sơ (sửa tên, số điện thoại) trong lúc đang chờ không vô tình làm chết link người dùng vừa nhận. `EmailChangeSameAsCurrent` chỉ dùng ở `Account.RequestEmailChange` như lưới cho Domain; service không gọi nó khi email không đổi.
- Hạn link đổi email cố định 24 giờ theo PRD (D17), là hằng trong Application, không dùng `EmailSettings:VerificationTokenExpiryHours` của thư xác minh đăng ký.
- Khoá API không gọi được API-29–API-31 (không có `sub` người dùng), như các endpoint hồ sơ hiện có.

### 4.10 Nhật ký kiểm toán (FR-68, §6.8)

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-33 | `GET api/organization/audit-log` | Mục ngữ nghĩa của tổ chức đang làm việc, mới nhất trước | `cursor?: string` (mờ, lấy từ `nextCursor` của trang trước), `limit` (mặc định 50; ngoài 1–100 → 400 kiểm hợp lệ `[Range(1, 100)]`, không kẹp) | 200 `AuditEntryPage { items: AuditEntryDto[], nextCursor: string \| null }` | 400 `InvalidCursor`, lỗi kiểm hợp lệ của `limit`; 401; 403 `OrganizationAccessDenied` (ngữ cảnh cá nhân, thiếu quyền, khoá API), `OrganizationSuspended` | `auditlogs.view`, `DenyMode.Forbidden` (BR-77, BR-83 điểm 1, AC-362, AC-366) |

- `AuditEntryDto { id, occurredAt, actorKind: "Account" | "SystemAdministrator" | "System", actorName: string | null, type: AuditEventType, parameters: Record<string, string | string[] | RoleRefDto | RoleRefDto[]> }`. `actorName` là tên hiện tại của tài khoản người làm (`null` khi tài khoản đã xoá hoặc `actorKind ≠ Account`). API-33 dựng `parameters` **chỉ** từ các khoá đã khai báo cho `type` trong danh mục, bỏ mọi khoá khác có trong `new_values` (lưới thứ hai cho BR-59, sau kiểm ở `RecordAsync`; B1). Dòng `Event` có `action` không ánh xạ được về `AuditEventType`, hoặc `actor_kind` null hay không ánh xạ được về `ActorKind` (C2), thì bị bỏ qua (không trả), ghi log cảnh báo chỉ kèm `id` của dòng; không bao giờ mặc định `actor_kind` thành `Account`. DTO không có `ipAddress`, `userAgent`, `entityId`, `oldValues`, `newValues` (BR-83). `occurredAt` UTC, chuyển theo `X-TimeZone` như mọi endpoint.
- Không có tham số lọc hay tìm kiếm (BR-83 điểm 6, D19, AC-365). Không có `totalCount` (không cần cho *Xem thêm*, và đếm toàn bảng tốn khi nhật ký dài).
- **Bỏ** `GET api/admin/audit-logs` và `GET api/admin/audit-logs/{id}` (trả snapshot JSON thô, có tìm kiếm): frontend chưa có màn hình nào gọi chúng; PR triển khai ghi một dòng `- Removed:` (QĐ-36).

## 5. Phân quyền

### 5.1 Cổng tổ chức (thứ tự BR-77 bước 1–4)

Một requirement `OrganizationContextRequirement(PermissionCode?, DenyMode)` thay `ActiveOrganizationMembershipRequirement`, `ActiveOrganizationPermissionRequirement`, `OrganizationMembershipRequirement`, `OrganizationPermissionRequirement`. Policy `AuthorizationPolicies.ActiveOrganizationMember` và mọi mã trong `Permissions` (qua `OrganizationPermissionPolicyProvider`) đều dựng requirement này. Handler kiểm theo đúng thứ tự, dừng ở điều kiện đầu tiên không đạt:

1. Chưa đăng nhập → 401 (challenge mặc định).
2. JWT không có `org_id` (ngữ cảnh cá nhân), hoặc `org_id` trỏ tới tổ chức mà người gọi không phải thành viên đang hoạt động (đã bị gỡ, đã rời, chưa từng vào) → `DenyMode.Forbidden`: 403 `OrganizationAccessDenied`; `DenyMode.NotFound`: 404 (BR-45). Bước này đứng trước bước 3, nên người không phải thành viên của một tổ chức đang bị vô hiệu cũng nhận `OrganizationAccessDenied` hoặc 404, không bao giờ thấy tên tổ chức.
3. Người gọi là thành viên đang hoạt động và tổ chức đang bị vô hiệu → 403 `OrganizationSuspended` kèm tên tổ chức (BR-64, AC-301, AC-320, AC-321).
4. Có `PermissionCode` mà người gọi không có → 403 `OrganizationAccessDenied`, trước mọi truy vấn dữ liệu (AC-316).

Bước 5–7 nằm trong service: (5) đối tượng tra theo `id` **và** `OrganizationId = org_id` của JWT, không thấy → 404 giống hệt khi không tồn tại (AC-310, AC-317, AC-318); (6) BR-58 → 403; (7) điều kiện còn lại → 409. Thứ tự (6) trước (7) là D2 (AC-257).

- Bước 2–3 đọc một lần qua `ITenantAccessService.GetAccessAsync(accountId, orgId)` trả `NotMember`, `Suspended` hoặc `Active`; service tra dòng thành viên đang hoạt động **trước**, chỉ khi có mới đọc trạng thái tổ chức. Thay `HasActiveAccessAsync` (bool, không phân biệt được hai lý do). API-28, `RefreshTokenAsync` (§6.5) và `TenantAccessMiddleware` dùng cùng hàm này. Bước 4 đọc qua `IPermissionResolver`. Cả hai cache Redis TTL 60 giây, xoá ngay khi đổi, nên mọi đổi quyền có hiệu lực ở request tiếp theo trên mọi máy (BR-67, AC-190). Cache chỉ dùng cho cổng; BR-58 khi ghi đọc thẳng cơ sở dữ liệu (§5.3).
- `AuthorizationResultHandler` đọc `AuthorizationFailureReason` của handler để ghi đúng mã (`OrganizationSuspended`, `OrganizationAccessDenied`, `SystemAdministratorRequired`) thay vì luôn `OrganizationAccessDenied`.
- `DenyMode.Forbidden` dùng cho các phần chỉ dành cho một quyền (BR-77, ngoại lệ ở BR-45): Vai trò, Lời mời, Quản lý tổ chức, Nhật ký kiểm toán (API-33, BR-83 điểm 1) của tài liệu này, và phần Sản phẩm của TECH01 (quyền Quản trị FeedbackHub, cả đọc lẫn quản lý; thứ tự kiểm chi tiết theo PRD01 BR-18), cùng các thao tác khác của tài liệu này trừ API-06. Ở các phần đó, ngữ cảnh cá nhân hoặc thiếu quyền nhận "không đủ quyền" trước khi tra đối tượng; admin hệ thống mở phần Sản phẩm cũng vậy, không tên sản phẩm nào được trả về (BR-21). `DenyMode.NotFound` dùng cho đọc dữ liệu bên trong tổ chức không thuộc các phần trên: API-06 (danh sách thành viên; người ngoài, kể cả admin hệ thống, nhận "không tìm thấy", BR-74, AC-122) và feedback, Tổng quan của TECH03, TECH06, để người đã rời hay bị gỡ thấy "không tìm thấy" (BR-45, AC-70, AC-280). PRD-00 v1.3 câu 115.
- `TenantAccessMiddleware` giữ làm lưới cuối cho endpoint chỉ có `[Authorize]` mà vẫn mang `org_id` (thông báo, push), nhưng ghi `CodedProblemDetails` (`OrganizationSuspended` hoặc `OrganizationAccessDenied`) thay vì `ProblemDetails` không mã. Danh sách miễn trừ: `/api/auth`, `/api/organizations` (tổ chức của tôi, tạo), `/api/permissions`, `/api/invitations`, `/api/system`.
- Mở và chấp nhận lời mời không qua cổng này (BR-77, BR-81); thứ tự riêng ở §6.3.

### 5.2 Bảng quyền theo endpoint

| Endpoint | Ai được gọi | Phạm vi máy chủ | Từ chối |
|---|---|---|---|
| API-01 | Mọi người đã đăng nhập (không nhận khoá API) | `organization_members.AccountId = sub`, `IsActive` | 401 |
| API-02 | Như trên, email tài khoản đã xác minh | — | 401; 403 `EmailNotConfirmed` |
| API-03, API-09 | Thành viên đang hoạt động của tổ chức đang làm việc | `org_id` của JWT | Cổng §5.1 (`Forbidden`) |
| API-06 | Như trên | `org_id` của JWT | Cổng §5.1 (`NotFound`: 404 cho người không phải thành viên) |
| API-04, API-05 | Có `organizations.manage` | `org_id` | Cổng §5.1; API-05 kiểm thêm admin hệ thống phải có lý do |
| API-07, API-08, API-10–API-14 | Có `organizations.members.manage` | `org_id`; đối tượng ngoài tổ chức → 404, kể cả từng `roleIds[]` của API-07 | Cổng; 404; BR-58 403 |
| API-15–API-18 | Có `organizations.roles.manage` | `org_id` | Cổng; 404; BR-58 403 |
| API-33 | Có `auditlogs.view` | `organization_id = org_id`, `kind = 'Event'` trong SQL | Cổng §5.1 (`Forbidden`) |
| API-20 | Bất kỳ ai có mã | Theo `TokenHash` | Không có 401/403; trạng thái trả trong thân (§6.3) |
| API-21 | Người đã đăng nhập bằng JWT người dùng, email tài khoản đã xác minh | `TokenHash`; email tài khoản phải bằng email lời mời | 401; 403 `EmailNotConfirmed`; 409 theo §6.3 |
| API-22 | Ẩn danh có mã | `TokenHash`; email lấy từ lời mời | 409 |
| API-24–API-26 | Admin hệ thống (JWT người dùng, không nhận khoá API, không phụ thuộc `org_id`) | Không đọc gì bên trong tổ chức ngoài tên, số thành viên, trạng thái (BR-21) | 403 `SystemAdministratorRequired` (AC-126, AC-131) |
| API-29–API-31 | Chính chủ tài khoản (JWT người dùng) | Chỉ tài khoản `sub`; không nhận id tài khoản nào | 401 |
| API-32 | Bất kỳ ai có mã | Theo `TokenHash`; chỉ tác động tài khoản của token | 401, 409 |
| Lệnh CLI `grant-system-admin`, `revoke-system-admin` | Người vận hành máy chủ | — | Không có endpoint HTTP nào ghi `system_administrators` (AC-51, AC-52) |

Khoá API (`X-Api-Key`) không có `sub` người dùng: mọi endpoint của §4.1–§4.5 dùng cổng tổ chức hoặc `SignedInUser` nên khoá API nhận 403 hoặc 401, không gọi được thao tác thành viên, lời mời, vai trò.

### 5.3 Chống leo thang quyền (`RoleGrantPolicy`, BR-58)

Đầu vào: tập quyền hiệu lực `P(caller)` (Owner = `Permissions.All`), `isOwner(caller)`; với mỗi vai trò `r`: `P(r)` (Owner = toàn bộ danh mục).

- `CanGrant(r)` = `r` là Owner ? `isOwner(caller)` : `P(r) ⊆ P(caller)` (điểm 1, 2).
- `CanTakeAway(r)` = cùng công thức (điểm 3: phải có mọi quyền của vai trò bị lấy đi hoặc làm mất hiệu lực).

| Thao tác | Điều kiện BR-58 (bước 6) | Điều kiện khác (bước 7) |
|---|---|---|
| Đổi vai trò (API-07), cả của chính mình | `CanTakeAway` với **mọi** vai trò người đó đang giữ, kể cả khi chỉ thêm vai trò (cùng điều kiện như gỡ khỏi tổ chức, BR-58 điểm 3, D14), và `CanGrant` với mỗi vai trò thêm (điểm 2). Người đó đang giữ Owner → `CanTakeAway(Owner)` = `isOwner(caller)`, nên mọi thay đổi vai trò của một Chủ sở hữu, kể cả chỉ thêm hay bỏ vai trò khác, cần người gọi là Chủ sở hữu dù họ có mọi quyền (điểm 1, D15); lỗi trả `OwnerOnlyOperation`. Với chính mình, `CanTakeAway` luôn đúng nên chỉ còn điểm 2 (AC-154, AC-155, AC-157, AC-193, AC-197, AC-336, AC-340) | Còn ít nhất một Chủ sở hữu đang hoạt động; ít nhất một vai trò |
| Gỡ thành viên (API-08) | `CanTakeAway` mọi vai trò tổ chức của người bị gỡ; vai trò theo sản phẩm không tính (AC-158, AC-258, AC-259) | `accountId` là chính người gọi → 409 `CannotRemoveSelf` (rời chỉ qua API-09; BR-80, AC-337); không gỡ Chủ sở hữu cuối cùng |
| Gửi lời mời (API-11) | `CanGrant(vai trò mới)`; nếu email đã có lời mời (chờ hoặc hết hạn): thêm `CanTakeAway(vai trò lời mời cũ)` (BR-63 điểm 3, D4, AC-153, AC-194, AC-254, AC-271) | Email đã là thành viên đang hoạt động → 409 (AC-180) |
| Mời lại (API-12) | `CanGrant(vai trò)` (cùng vai trò nên bằng `CanTakeAway`) | — |
| Thu hồi, xoá lời mời (API-13) | `CanTakeAway(vai trò lời mời)` (AC-253, AC-255, AC-269, AC-270) | — |
| Tạo vai trò (API-16) | Mọi quyền chọn `∈ P(caller)` (AC-156) | Tên theo BR-79 |
| Sửa vai trò, kể cả chỉ đổi tên (API-17) | `CanTakeAway(vai trò hiện tại)` và mọi quyền mới `∈ P(caller)` (AC-198, AC-295–297) | Tên theo BR-79 |
| Xoá vai trò (API-18) | `CanTakeAway(vai trò)` (AC-249, AC-257) | Không còn người giữ đang hoạt động → nếu còn: 409 `RoleHasHolders` (AC-248, AC-251, AC-256) |
| Rời tổ chức (API-09) | Không áp dụng (BR-58: người rời chỉ bỏ quyền của mình) | Không phải Chủ sở hữu cuối cùng (AC-277) |

- Mã lỗi khi BR-58 từ chối: `OwnerOnlyOperation` khi nhánh Chủ sở hữu (điểm 1) không đạt, kể cả khi điều kiện quyền cũng không đạt; ngược lại `InsufficientRolePermissions`. Cùng thứ tự với `lockReason` (§4.6), nên giao diện và lỗi khi gửi trực tiếp nêu cùng một lý do (BR-74, AC-341).
- Vai trò hệ thống: API-17, API-18 trên vai trò có `SystemRoleKind` trả 403 `OrganizationSystemRoleImmutable` **trước** BR-58, vì AC-55 yêu cầu báo đúng lý do "vai trò hệ thống không sửa được" (§12 QĐ-27).
- Tên vai trò (BR-79, D8): sau trim và NFC, không trống, ≤100 ký tự; không trùng không phân biệt hoa thường với vai trò khác của tổ chức (chỉ mục §3.3 bảo đảm cả khi hai request đồng thời; vi phạm chỉ mục ánh xạ về 409 `OrganizationRoleNameAlreadyExists`); không trùng `Owner`, `Admin`, `Member`, `Chủ sở hữu` không phân biệt hoa thường → 409 `RoleNameReservedForSystemRole`. Danh sách tên hệ thống vi/en đặt một chỗ trong Application (`SystemRoleDisplayNames`) và có test so với `locales/{vi,en}.ts`.
- Nguồn đầu vào: khi ghi (API-07, API-08, API-11–API-13, API-16–API-18), `P(caller)`, `isOwner(caller)`, `P(r)` và người giữ vai trò được đọc thẳng từ cơ sở dữ liệu **sau** `IOrganizationLock.AcquireAsync`, trong cùng giao dịch, không qua `IPermissionResolver`. Nhờ vậy một quyết định BR-58 không dựa trên cache có thể cũ tới lúc bị xoá, và hai thao tác song song (Owner A bỏ quyền của B trong lúc B cấp vai trò) được xếp hàng bởi khoá rồi tính trên dữ liệu đã cập nhật.
- Cờ trên DTO (`canGrant`, `canChangeRoles`, `canRemove`, `canRevokeOrDelete`, `canEdit`, `canDelete`) gọi cùng `RoleGrantPolicy` với đầu vào đọc từ cơ sở dữ liệu trong truy vấn danh sách (không khoá; cờ chỉ để hiện), nên giao diện và máy chủ không lệch nhau.

### 5.4 Đồng thời

Mọi thao tác đổi tập thành viên, vai trò của thành viên, vai trò, lời mời chạy trong `ExecuteInTransactionAsync` và bắt đầu bằng `IOrganizationLock.AcquireAsync(orgId)` (`SELECT 1 FROM organizations WHERE "Id" = @id FOR UPDATE`). Nhờ vậy hai Chủ sở hữu không gỡ nhau cùng lúc thành tổ chức không còn Chủ sở hữu, xoá vai trò không chạy song song với việc gán vai trò đó hay chấp nhận lời mời có vai trò đó, và hai lời mời cùng email không cùng được tạo (chỉ mục unique là lưới thứ hai). Đọc đầu vào BR-58 (§5.3) và kiểm Chủ sở hữu cuối cùng đều diễn ra sau khi có khoá. Giữ khoá trong lúc gửi email mời (§6.2) chấp nhận được ở quy mô ≤20 tổ chức.

### 5.5 Phạm vi theo sản phẩm (hợp đồng cho TECH01–TECH06)

`IProductAccessScope.GetAsync(orgId, accountId) → ProductAccess { IsAdministrator, HandlerProductIds, ViewerProductIds }`:

- `IsAdministrator` = có `feedbackhub.admin` trong `P(caller)` (BR-18, FR-05, AC-53). Admin hệ thống không có ngoại lệ nào (BR-21, BR-22).
- Cache Redis theo `(orgId, accountId)`, TTL 60 giây; xoá khi gán, bỏ gán, đổi vai trò theo sản phẩm, đổi vai trò tổ chức, sửa vai trò tự tạo (qua `InvalidateOrganizationAsync`), gỡ và rời (BR-67, AC-12).
- Quy tắc cho mọi module dùng: lọc danh sách bằng phạm vi này trong SQL, không lọc sau khi tải; truy cập theo `id` ngoài phạm vi → 404 như không tồn tại (FR-37, BR-45); thấy được nhưng không được làm → 403 (BR-45). Không bao giờ dựa vào việc giao diện ẩn nút (FR-12).
- Bốn quyền khác (`apikeys.manage`, `auditlogs.view`, `files.manage`, `settings.manage`) không tham gia phạm vi này (BR-59, FR-56).
- Mục này là thiết kế (định nghĩa phạm vi, quy tắc 404/403, xoá cache). Bảng §3.6, hiện thực `IProductAccessScope` và test của chúng thuộc kế hoạch TECH01 (§14 T1).
- **Nghĩa vụ của TECH01**: khi thêm `product_members`, `MemberService` xoá mọi dòng `product_members` của người bị gỡ hoặc tự rời trong cùng giao dịch với việc đặt `IsActive = false` (BR-30), và xoá cache `IProductAccessScope` của họ; kèm test: gỡ một Phụ trách, mời lại, chấp nhận → người đó vào lại tổ chức mà không còn vai trò theo sản phẩm nào (phần sản phẩm của AC-69, AC-259, AC-276).

## 6. Luồng chính

### 6.1 Tạo tổ chức và chuyển tổ chức

1. Frontend gợi ý định danh từ tên theo BR-78 (a)–(e) (`lib/slug-suggestion.ts`), đổi chữ hoa gõ vào thành chữ thường (AC-281, AC-329, AC-331).
2. API-02: `Organization.Create` kiểm định dạng và độ dài (AC-283, AC-284, AC-328); kiểm trùng, vi phạm chỉ mục unique đồng thời cũng trả 409 (AC-286, AC-330); seed ba vai trò hệ thống, thêm người tạo với vai trò Owner, trong một giao dịch.
3. Frontend gọi `switch-organization` với tổ chức mới rồi tải lại trang (FR-24, AC-56).
4. Đăng nhập, xác minh email, đăng nhập ngoài: tổ chức mặc định chỉ chọn khi tài khoản có **đúng một** tổ chức đang hoạt động (bỏ qua tổ chức bị vô hiệu), ngược lại vào ngữ cảnh cá nhân (AC-184).

### 6.2 Gửi, mời lại, thu hồi lời mời

```
API-11 → cổng (members.manage) → giao dịch + khoá tổ chức
  → tìm vai trò trong org_id (404) → tìm lời mời cũ cùng email (nếu có)
  → BR-58: CanGrant(vai trò mới) và CanTakeAway(vai trò cũ) (403)
  → email là thành viên đang hoạt động? (409)
  → xoá lời mời cũ; tạo lời mời mới: mã 32 byte ngẫu nhiên (RandomNumberGenerator, base64url), lưu SHA-256, ExpiresAt = now + 168 giờ
  → SaveChanges → gửi email (link {FrontendBaseUrl}/#/invite?token=<mã>)
      lỗi SMTP hoặc quá 15 giây → ném lỗi → giao dịch rollback: không lời mời mới, lời mời cũ còn nguyên → 503
  → commit → 201
```

- Email mời dùng mẫu tạm chỉ tiếng Việt, viết cứng như các email thông báo hiện có (`localization.md`): tên tổ chức, tên vai trò (vai trò hệ thống theo `vi`), hạn, link; không có tên người mời (§12 PRD05 *Email mời nêu tổ chức và vai trò*; §14 P3). Mọi giá trị đưa vào HTML được `WebUtility.HtmlEncode`, như `RegistrationService`. PRD05 quyết mẫu chính thức và ngôn ngữ cho người nhận chưa có tài khoản.
- Mã thô chỉ tồn tại trong bộ nhớ của request và trong email; không ghi log, không vào job Hangfire, không vào nhật ký kiểm toán.
- Điều kiện của việc gửi đồng bộ (§14 T3):
  1. Timeout SMTP tối đa 15 giây (`EmailSettings:SendTimeoutSeconds`, mặc định 15), ngắn hơn timeout của nginx và bộ cân bằng tải, vì khoá dòng tổ chức (§5.4) được giữ trong lúc gửi. Đây là **một** hạn chung cho cả kết nối, xác thực và gửi: `SmtpEmailSender` tạo `CancellationTokenSource` liên kết với token của request, `CancelAfter(15 s)`, truyền nó cho `ConnectAsync`, `AuthenticateAsync`, `SendAsync`, và đặt `client.Timeout` bằng phần thời gian còn lại; không phải 15 giây cho từng bước.
  2. Frontend khoá nút trong lúc "Đang gửi…" và không bao giờ tự thử lại khi gặp 503 hay hết thời gian chờ. Người dùng thử lại bằng tay thì lời mời được thay (BR-63 điểm 3), nên không cần khoá idempotency.
  3. Lỗi SMTP ghi log chỉ kèm `invitationId`: không email, không mã. `SmtpEmailSender` bỏ tham số người nhận khỏi mọi dòng log của nó (hiện ghi `"Failed to send email to {ToEmail}"`); service gọi nó tự ghi id của đối tượng (lời mời, tài khoản).
  4. Rủi ro còn lại: SMTP đã nhận thư nhưng commit lỗi, người nhận có link chết (báo "không còn hiệu lực"). Hiếm, chấp nhận.
- API-12: như trên với vai trò của lời mời cũ. API-13: khoá tổ chức → tìm lời mời (404) → `CanTakeAway` (403) → `IAuditTrail` (`Revoked` nếu còn hạn, `Deleted` nếu hết hạn) → xoá dòng → commit.

### 6.3 Mở lời mời (API-20) và chấp nhận (API-21, API-22)

Thứ tự kiểm BR-81 điểm 3, dừng ở điều kiện đầu tiên không đạt:

| Bước | Điều kiện | `InvitationLookupDto` (API-20) | API-21 | API-22 |
|---|---|---|---|---|
| 1 | Không có dòng với `TokenHash` (không tồn tại, đã dùng, đã thu hồi, đã bị thay, vai trò bị xoá) | `NoLongerValid`, không trường nào khác (D10) | 409 `InvitationNoLongerValid` | 409, không tạo tài khoản |
| 1 | `ExpiresAt ≤ now` | `Expired`, kèm `expiresAt` (không tên tổ chức, vai trò, email) | 409 `InvitationExpired` | 409, không tạo tài khoản |
| 2 | Có phiên và `Account.Email` ≠ email lời mời (`PendingEmail` không được tính, BR-51, AC-348) | `WrongAccount`, không trường nào khác (BR-81 điểm 4, AC-306, AC-308, AC-348) | 409 `InvitationWrongAccount` | Không thể xảy ra (email lấy từ lời mời) |
| 2 | Có phiên, `Account.Email` = email lời mời nhưng `EmailConfirmed = false` (§3.8) | `EmailNotConfirmed`, không trường nào khác (BR-51, AC-351) | 403 `EmailNotConfirmed`, lời mời giữ nguyên | Không thể xảy ra (không có phiên) |
| 3 | Có phiên đúng tài khoản, tổ chức bị vô hiệu | `OrganizationSuspended`, kèm `organizationName`, `expiresAt` | 409 `InvitationOrganizationSuspended`, lời mời giữ nguyên (AC-151) | Tạo tài khoản đã xác minh, không vào tổ chức, `invitationOutcome = OrganizationSuspended`, lời mời giữ nguyên (AC-333) |
| — | Không có phiên, lời mời còn hiệu lực (kể cả tổ chức bị vô hiệu) | `Valid` với đúng bốn điều: `organizationName`, `role`, `expiresAt`, `email` (BR-81 điểm 5, 6; AC-309, AC-332) | — | — |
| — | Có phiên đúng tài khoản, tổ chức hoạt động | `Valid` với bốn điều | Vào tổ chức: kích hoạt lại dòng thành viên cũ hoặc tạo mới, gán vai trò của lời mời, xoá lời mời, xoá cache quyền; 200 | Tạo tài khoản đã xác minh (`ConfirmEmail`), vào tổ chức như API-21, đăng nhập; `Joined` (AC-57, AC-182) |

- API-20 không tiêu thụ lời mời, nên trình quét link trong email không làm hỏng nó. Tra cứu luôn qua `POST` thân JSON: mã không nằm trong URL nên không vào log truy cập của nginx hay log request của API.
- Đăng nhập Google/Microsoft từ trang lời mời dùng `POST api/auth/external/{provider}` có sẵn (không truyền mã), rồi gọi API-21. Tài khoản Google/Microsoft khác email lời mời bị API-21 từ chối `InvitationWrongAccount`; trang không nhắc lại tên tổ chức (AC-305). Tài khoản vừa tạo (`accountCreated = true`) được trang tự chấp nhận (AC-178); tài khoản có sẵn thấy nút *Chấp nhận lời mời* (AC-177). Tài khoản Microsoft có email chưa xác minh theo §6.6 không bao giờ được tạo hay liên kết; tài khoản Microsoft cũ bị M8 đánh dấu chưa xác minh dừng ở bước 2 (`EmailNotConfirmed`) cho đến khi xác minh lại (§6.6 bước 4, §10.1). API-21 chỉ dùng một vị từ `Account.EmailConfirmed`, không đọc gì riêng của nhà cung cấp.
- Đã là thành viên đang hoạt động khi chấp nhận (chỉ xảy ra khi hai tab cùng chấp nhận): xoá lời mời và trả 200, không đổi vai trò.
- Người đã có tài khoản chưa xác minh không đăng ký lại được (`AccountEmailAlreadyExists`); họ xác minh email theo luồng có sẵn (*Gửi lại email xác thực*; tài khoản không có mật khẩu thì đăng nhập lại bằng Microsoft, §4.9) rồi chấp nhận.
- API-22 phát phiên qua `TokenIssuer.IssueTokensAsync` như mọi đường đăng nhập: `org_id` = tổ chức vừa vào, hoặc `null` khi `OrganizationSuspended`; `isPersistent = false` (phiên không "ghi nhớ", vì người đăng ký chưa từng chọn); `isSystemAdministrator` do `TokenIssuer` đặt. Rủi ro link bị chuyển tiếp: §12 QĐ-11.

### 6.4 Đổi vai trò, gỡ, rời

- API-07, API-08: cổng → khoá tổ chức → tìm thành viên đang hoạt động trong `org_id` (404); API-07 tìm thêm mọi `roleIds[]` trong `org_id`, thiếu bất kỳ cái nào → 404 → đọc đầu vào BR-58 từ cơ sở dữ liệu → BR-58 (403) → bước 7: `CannotRemoveSelf` (API-08), Chủ sở hữu cuối cùng (409) → ghi → xoá cache `ITenantAccessService`, `IPermissionResolver`, `IProductAccessScope` của người bị đổi.
- Gỡ và rời: `IsActive = false`, xoá `organization_member_roles` của họ (BR-30, AC-69, AC-259, AC-276); phần `product_members` do TECH01 thêm (§5.5).
- API-08 với `accountId` là chính người gọi: 409 `CannotRemoveSelf` ở bước 7, sau BR-58 (D2: 403 trước 409), giống mọi điều kiện 409 khác; tự rời chỉ qua API-09 (BR-80, AC-337); không ghi nhật ký kiểm toán. Feedback họ đã gửi không đổi (TECH03).
- API-09 thêm mục nhật ký `Left` (AC-279). Frontend sau 204 gọi `switch-organization` với `null` rồi tải lại về trang chào (BR-80, AC-276). Nếu bước đó lỗi, request kế tiếp mang `org_id` cũ nhận `OrganizationAccessDenied` và frontend xử lý như §6.5.

### 6.5 Tổ chức bị vô hiệu trong lúc làm việc

1. API-05 hoặc API-25 đặt `Status = false` và trường vô hiệu, xoá cache `tenant-access:{org}` và `permissions:{org}` (Redis, mọi máy thấy ngay).
2. Request kế tiếp của thành viên mang `org_id` đó nhận 403 `OrganizationSuspended` + `organizationName`; thao tác không có tác dụng (AC-183, AC-320).
3. Frontend (interceptor trong `api/client.ts` báo sự kiện; store `organization-context` xử lý) ghi thông báo vào `sessionStorage`, gọi `switch-organization(null)`, tải lại về trang chào; trang chào hiện "<tên> vừa tạm ngừng hoạt động. Việc bạn vừa làm chưa được thực hiện" và bộ chọn tổ chức (AC-199, AC-200, AC-301). `OrganizationAccessDenied` từ cổng (thành viên đã bị gỡ) cũng đưa về ngữ cảnh cá nhân, không kèm thông báo tạm ngừng.
   Làm mới phiên (`POST api/auth/refresh`) gặp cùng tình huống: `RefreshTokenAsync` hỏi `ITenantAccessService.GetAccessAsync` cho `org_id` của refresh token; kết quả `NotMember` hoặc `Suspended` → vẫn phát cặp token mới, cùng `familyId`, với `org_id = null`, thay vì 401 như hiện nay (đẩy người dùng ra màn hình đăng nhập). Tài khoản bị khoá (`account.Status = false`) và refresh token không hợp lệ vẫn 401. Hợp đồng không đổi (`LoginResponse.organizationId` đã nullable). Frontend: khi `organizationId` đổi từ `X` sang `null` sau một lần refresh, interceptor **không** phát lại request gốc (nó được gửi trong ngữ cảnh `X`), mà đi theo bước 3: tải API-01; `X` còn trong danh sách với `status = false` → thông báo tạm ngừng kèm tên; ngược lại (bị gỡ, đã rời) → trang chào không thông báo.
4. Bộ chọn tổ chức hiện tổ chức bị vô hiệu làm mờ, không chọn được (API-01 trả `status`, AC-73); gọi thẳng API-28 vào tổ chức đó bị 403 (FR-30).
5. API-26 kích hoạt lại: `Status = true`, xoá trường vô hiệu, xoá cache; thành viên, vai trò, vai trò theo sản phẩm, lời mời còn hạn còn nguyên (BR-49, BR-55, AC-106, AC-128, AC-152, AC-334). Hạn lời mời không được cộng thêm (AC-144).

### 6.6 Độ tin cậy email của tài khoản Microsoft (§14 T2, quyết định của người dùng 2026-10-23; sửa B2 theo review `appsec`)

Áp cho mọi đăng nhập Microsoft, không riêng lời mời.

1. **Đọc claim (sửa B2).** Code hiện tại dùng `JwtSecurityTokenHandler` với `MapInboundClaims = true` mặc định, nên claim bị đổi tên trước khi đọc: `FindFirst("oid")` không bao giờ thấy gì và rơi xuống `NameIdentifier` (tức `sub`), còn `FindFirst("email")` không thấy và rơi xuống `preferred_username`. Liên kết hiện có vì vậy đã được khoá theo `sub`, và email thực ra lấy từ `preferred_username`. Sửa:
   - Đặt `MapInboundClaims = false` (hoặc đổi sang `JsonWebTokenHandler`, không ánh xạ claim); đọc claim theo tên thô từ token đã kiểm chữ ký, issuer, audience.
   - `ProviderUserId` = `sub` thô. **Không** đọc `oid` trước. Giữ `sub` làm khoá liên kết nên mọi liên kết đã có vẫn khớp, không cần migration, không cần cột `tid`.
   - Thiếu `sub` → từ chối (`SecurityTokenException`). Bỏ phương án dự phòng `NameIdentifier`.
   - `email` chỉ lấy từ claim `email`; **bỏ** `preferred_username` (người dùng tự sửa được).
   - `tid` bắt buộc; thiếu → từ chối. Phần tenant trong `iss` (`https://login.microsoftonline.com/{tenant}/v2.0`) phải bằng đúng `tid` của cùng token, không chỉ khớp mẫu `{tenantid}` như `IssuerValidator` hiện nay; khác → từ chối.
   - `xms_edov`: nhận cả dạng bool và chuỗi; chỉ bool `true` hoặc chuỗi `"true"` (không phân biệt hoa thường) được tính; thiếu claim hay giá trị khác đều là chưa xác minh.
2. `EmailVerified = (tid = 9188040d-6c67-4c5b-b112-36a304b66dad, tài khoản cá nhân) || (xms_edov = true)`.
3. `AuthService.ExternalLoginAsync`:
   - Tìm liên kết theo `Provider + ProviderUserId` (`sub`) **trước**; có liên kết thì đăng nhập (tài khoản bị khoá bị `TokenIssuer` trả 401), không kiểm `EmailVerified` (người đã liên kết vẫn đăng nhập được).
   - Nhánh khớp tài khoản có sẵn theo email: `EmailVerified = false` → từ chối `ExternalLoginEmailNotVerifiedByProvider`; tài khoản khớp chưa xác minh → `ExternalLoginEmailNotConfirmed` (có sẵn); tài khoản khớp bị khoá → 401 trước khi tạo liên kết (kiểm ở đây vì liên kết được thêm trước khi gọi `TokenIssuer`); không tạo liên kết trong cả ba trường hợp.
   - Nhánh tạo tài khoản mới: `EmailVerified = false` → từ chối `ExternalLoginEmailNotVerifiedByProvider`, không tạo tài khoản, không tạo liên kết. Frontend hiện gợi ý đăng ký bằng email.
   - Google giữ quy tắc `email_verified` hiện có, nhưng cũng chuyển xuống hai nhánh trên cho thống nhất.
4. **Xác minh lại tài khoản cũ (M8).** Ở nhánh đã liên kết, nếu `account.EmailConfirmed = false`, `EmailVerified = true` và email của token (chuẩn hoá) bằng `Account.Email` → `account.ConfirmEmail()` trong cùng lần đăng nhập. Khác email hoặc chưa xác minh → vẫn đăng nhập, tài khoản giữ chưa xác minh. Đây là đường duy nhất: `resend-verification` không gửi cho tài khoản không có mật khẩu (§4.9, N-new-1). Trong lúc chưa xác minh, tài khoản đăng nhập được nhưng không tạo được tổ chức (API-02) và không chấp nhận được lời mời (API-21).
5. Không lưu cờ xác minh theo từng liên kết: API-21, API-02, nhánh khớp email và `grant-system-admin` chỉ đọc `Account.EmailConfirmed` (§3.8, QĐ-29).
6. App registration của Microsoft phải bật optional claim `xms_edov` trong ID token trước khi bản này chạy (§10.2).
7. Thay quyết định 2026-07-25 trong `.claude/decisions.md` (*Microsoft external login: multi-tenant issuer validated by regex, no email_verified check*); `implementer` ghi mục mới có `Supersedes:` trong PR triển khai.
8. Câu hỏi `appsec` ở bản 0.3 (khoá liên kết có cần kèm `tid` không) đã đóng theo B2 phương án (a), SA (thay người dùng): giữ `sub`. `sub` của Microsoft là định danh theo cặp (người dùng, ứng dụng), không trùng giữa hai người ở hai tenant cho cùng một ứng dụng; kiểm issuer bằng `tid` (bước 1) chặn token của tenant này mang issuer tenant khác.

### 6.7 Đổi email ở hồ sơ (sửa B1)

Hiện tại `PUT api/profile` ghi thẳng `Email` mới và `EmailConfirmed` vẫn `true`: ai đăng nhập được vào một tài khoản (hoặc chủ tài khoản gõ sai) có ngay một email "đã xác minh" chưa từng được chứng minh, dùng được để chấp nhận lời mời gửi tới địa chỉ đó hoặc để chiếm tài khoản khi người thật đăng nhập Google/Microsoft khớp theo email (§7.2 T15).

0. **Xác thực lại (N-new-2, quyết định của người dùng 2026-10-23; PRD-00 v1.4 BR-82 điểm 7, D18, AC-352–AC-358).** API-29 làm email đổi (khác cả `Email` lẫn `PendingEmail`) và `account.PasswordHash ≠ null`:
   - Thiếu `currentPassword`, hoặc chuỗi rỗng hay chỉ khoảng trắng → 400 `FieldRequired`, kiểm **trước** `ReserveAttemptAsync` nên không tính là lần sai (AC-356).
   - Kiểm theo đúng cơ chế của `authentication.md` § Temporary lockout, trên bộ đếm **riêng** `PasswordAttemptPurpose.EmailChange` (mục đích mới; ba cột mới trên `accounts`: `FailedEmailChangeCount`, `EmailChangeWindowStartedAt`, `EmailChangeLockedUntil`, chỉ `PasswordAttemptStore` ghi, `SetAfterSaveBehavior(Ignore)` như hai bộ đếm hiện có; cùng giới hạn `LoginLockoutSettings`; đạt giới hạn chỉ khoá việc đổi email, người dùng vẫn đăng nhập và đổi mật khẩu được, AC-355): `ReserveAttemptAsync` **trước** khi so mật khẩu; đang khoá hoặc vượt giới hạn → 401 `EmailChangeTemporarilyLocked`, báo trước khi so để khoá không xác nhận lần đoán nào; sai → 401 `InvalidCurrentPassword`, lần sai thứ `MaxFailedAttempts` gọi `LockAsync` với `CancellationToken.None`; đúng → `ClearAsync`. Bộ đếm riêng nên kẻ cầm phiên đánh cắp không dò được mật khẩu ở đây và không khoá được chủ tài khoản khỏi đăng nhập hay đổi mật khẩu, và ngược lại.
   - Thất bại ở bất kỳ nhánh nào: cả request bị từ chối, không trường hồ sơ nào được lưu (kể cả trường khác email gửi cùng), không `PendingEmail`, không token, không thư (AC-353, AC-355). Mật khẩu đúng → đổi chờ xác nhận như AC-342 (AC-352).
   - Kiểm mật khẩu đứng **trước** việc kiểm trùng email (409), để người cầm phiên đánh cắp không dò được địa chỉ nào đã có tài khoản.
   - Tài khoản không có mật khẩu (chỉ đăng nhập Google/Microsoft): không xác thực lại, không có trường nào phải nhập (AC-354); rủi ro còn lại ở §7.2 T20, §12 (PRD-00 §11 ghi cùng rủi ro).
   - Lưu hồ sơ mà email không đổi (AC-357), API-30 *Gửi lại* (AC-358) và API-31 *Huỷ* không cần mật khẩu: không cái nào gắn một địa chỉ mới vào tài khoản. Đổi sang một địa chỉ khác `PendingEmail` đang chờ thì hỏi lại (D18).
   - Thư báo cho địa chỉ cũ khi có yêu cầu đổi: PRD-00 v1.4 §10, §12 giao nội dung cho PRD05; thiết kế và gửi ở TECH05, chưa làm ở đây.
1. API-29 với `email` khác `Email`, sau bước 0: kiểm địa chỉ không phải `Email` của tài khoản khác (409, như hiện nay) → `Account.RequestEmailChange` đặt `PendingEmail` → `Consume()` mọi token `ChangeEmail` chưa dùng của tài khoản → tạo token mới `Purpose = ChangeEmail`, `Email = PendingEmail` (32 byte ngẫu nhiên, lưu SHA-256, hạn 24 giờ, D17) → SaveChanges → gửi thư tới **địa chỉ mới**: `{FrontendBaseUrl}/#/confirm-email-change?token=<mã>`. Thư nêu username của tài khoản và câu "Nếu không phải bạn, hãy bỏ qua thư này"; mọi giá trị qua `WebUtility.HtmlEncode`; mẫu tiếng Việt viết cứng như thư xác minh hiện có. Lỗi gửi thư: ghi log chỉ `accountId`, vẫn trả 200 (như `RegistrationService`); người dùng bấm *Gửi lại*.
2. API-32: tra token theo `TokenHash` → không có, đã dùng, hết hạn, `Purpose ≠ ChangeEmail`, hoặc (sau khi tải tài khoản) `PendingEmail ≠ token.Email` → cùng một 409 `EmailChangeLinkNoLongerValid` (trang báo link không còn hiệu lực) → kiểm lại không tài khoản nào khác có `Email` này (người khác có thể đã đăng ký địa chỉ đó trong lúc chờ, BR-82 điểm 2) → 409 `AccountEmailAlreadyExists`, không đổi gì, tài khoản kia không bị đụng tới (AC-345) → `Account.ApplyEmailChange()` (đặt `Email`, xoá `PendingEmail`, `ConfirmEmail()`), `Consume()` mọi token chưa dùng của tài khoản, cả hai loại → SaveChanges (chỉ mục unique `Email` có sẵn là lưới cho hai xác nhận đồng thời, vi phạm ánh xạ về 409). Không phát phiên, không đổi cookie: người mở link có thể không phải người đang đăng nhập.
3. Mỗi tài khoản có tối đa một link đổi email dùng được: đổi sang địa chỉ khác (API-29), *Gửi lại* (API-30) và *Huỷ* (API-31) đều `Consume()` mọi token đổi email chưa dùng của tài khoản trước khi (nếu có) phát token mới (AC-344, AC-347, AC-350). Điều kiện `PendingEmail = token.Email` ở bước 2 là lưới thứ hai.
4. Giao diện trong bảng hồ sơ (`ProfilePanel.vue`, thay `ProfileDialog.vue`: bảng bên ở máy tính, sheet ở máy tính bảng và điện thoại, hành vi không đổi): khi `pendingEmail` khác `null`, một dòng chữ thường "Đang chờ xác nhận: <email mới>. Mở link trong thư để đổi." kèm hai nút chữ *Gửi lại* (API-30) và *Huỷ* (API-31); không hộp thoại. Ô email vẫn hiện `Email` hiện tại. Khi người dùng sửa ô email và tài khoản có mật khẩu (`ProfileDto` thêm `hasPassword: bool`), bảng hồ sơ hiện thêm ô *Mật khẩu hiện tại* ngay trong form, không mở hộp thoại mới; lỗi `FieldRequired`, `InvalidCurrentPassword`, `EmailChangeTemporarilyLocked` ("tạm khoá, thử lại sau") hiện dưới ô đó theo locale. Ô này không hiện với tài khoản không có mật khẩu. Route `/confirm-email-change` (không `requiresAuth`, không `guestOnly`) đọc mã, `history.replaceState` xoá mã khỏi thanh địa chỉ, gọi API-32, hiện kết quả; nếu đang đăng nhập thì tải lại hồ sơ. Chuỗi vi và en.
5. Quy tắc nghiệp vụ: PRD-00 v1.3 US-26, FR-67, BR-82, D17, AC-342–AC-350. Thiết kế không thêm hành vi nào PRD chưa nêu: không báo cho địa chỉ cũ (PRD05 quyết, PRD-00 §12), không thu hồi phiên khi đổi email.

### 6.8 Nhật ký kiểm toán (FR-68, BR-83)

1. API-33: cổng (`auditlogs.view`, `Forbidden`) → giải mã `cursor` (base64url của `"{timestamp ticks UTC}:{id}"`). Mọi lỗi giải mã đều thành 400 `InvalidCursor`, không bao giờ 500: base64 sai, thiếu dấu `:`, ticks không phải số hoặc ngoài `DateTime.MinValue.Ticks`–`DateTime.MaxValue.Ticks`, `id` không phải số hoặc `≤ 0` (B5) → truy vấn keyset `WHERE organization_id = @org AND kind = 'Event' AND ("timestamp", id) < (@t, @id) ORDER BY "timestamp" DESC, id DESC LIMIT @limit + 1`, cùng thứ tự `Timestamp DESC, Id DESC` mà `ListPagedAsync` hiện dùng, nối `accounts` để lấy tên người làm. Có dòng thứ `limit + 1` thì `nextCursor` = khoá của dòng thứ `limit`, ngược lại `null`. Keyset thay `Skip/Take` để *Xem thêm* không lặp hay sót mục khi có mục mới chen vào đầu danh sách (AC-360).
2. Frontend (`views/organization/AuditLogView.vue`, route `/organization/audit-log`, `meta.permission = auditlogs.view`, mục *Nhật ký kiểm toán* trong nhóm "Tổ chức" của rail chỉ hiện khi có quyền): mỗi mục là một câu thường dựng bằng `t('auditLog.events.<type>', parameters)` trong `locales/{vi,en}.ts` (ai làm gì, với cái gì) cộng thời điểm theo múi giờ người xem (AC-359). Vai trò hệ thống hiện theo `systemKind`; danh sách quyền hiện bằng tên quyền theo locale; `actorKind = SystemAdministrator` hiện "admin hệ thống FeedbackHub"; `actorName = null` hiện "một tài khoản đã xoá". Loại chưa có câu trong locale (module mới hơn frontend) hiện câu chung "<người làm> đã thực hiện một thao tác quản trị", không bao giờ in `parameters` thô. Không có ô lọc, ô tìm kiếm (AC-365); *Xem thêm* gọi tiếp với `nextCursor`; không có mục nào thì trạng thái trống (AC-364). Câu chỉ dùng nội suy văn bản của vue-i18n: không `v-html`, không bật HTML trong `<i18n-t>`, nên tên người, tên vai trò hay email chứa thẻ HTML hiện nguyên văn (B6). Khi đổi tổ chức (API-28), danh sách và `nextCursor` đã tải bị xoá trước khi tải lại.
3. Email trong `parameters` hiện cho mọi người xem nhật ký theo D12, kể cả người không có quyền quản lý thành viên (AC-335); danh sách thành viên vẫn theo BR-74.
4. **Nghĩa vụ của TECH01 trở đi**: `AuditEvent.OrganizationId` lấy từ đối tượng đã tra theo `org_id`, hoặc (với route admin hệ thống) theo route của policy `SystemAdministrator`; không bao giờ lấy từ thân hay query của request (C1; `AuditEvent.For` ở §3.7 ép điều này). Mục nhật ký của PRD01 BR-19 (và AC-361: tạo sản phẩm) ghi bằng `IAuditTrail` với loại khai báo trong danh mục, không bằng snapshot; mục về gán, bỏ gán, đổi vai trò theo sản phẩm có tên thành viên và vai trò trước, sau (`accountName`, `productName`, `productRoleBefore`, `productRoleAfter` hoặc tương đương TECH01 quyết). TECH03 trở đi theo cùng quy tắc và không khai báo tham số chứa tiêu đề, nội dung, ảnh, file, bình luận hay ngữ cảnh kỹ thuật của feedback (BR-59, T10).

## 7. Bảo mật và quyền riêng tư

### 7.1 Tài sản

Mã lời mời; quyền và vai trò của thành viên (đặc biệt Owner); danh sách admin hệ thống; email thành viên và người được mời; tách biệt dữ liệu giữa tổ chức; tính toàn vẹn của nhật ký kiểm toán; phiên đăng nhập.

### 7.2 Mô hình đe doạ

| # | Kẻ tấn công và cách tấn công | Biện pháp |
|---|---|---|
| T1 | Người ngoài đoán hoặc dò mã lời mời | Mã 256 bit ngẫu nhiên; chỉ lưu SHA-256; tra theo hash có chỉ mục; giới hạn `invitation` đếm chung qua Redis theo IP; mã sai và mã đã thu hồi cho cùng một kết quả `NoLongerValid` |
| T2 | Mã lộ qua log, Referer, lịch sử trình duyệt, chỉ mục tìm kiếm | Mã nằm trong fragment (`#/invite?token=`) nên không tới nginx, không vào Referer; tra cứu bằng thân POST; API-20 trả `no-store`, `no-referrer`, `noindex`; route `/invite` chèn `<meta name="robots" content="noindex">` và `<meta name="referrer" content="no-referrer">`, đọc mã rồi `history.replaceState` xoá khỏi thanh địa chỉ, giữ trong `sessionStorage` của tab đến khi xong; font tự host nên trang không gọi bên thứ ba ngoài SDK Google/Microsoft khi người dùng bấm. Không log thân request; thông điệp ngoại lệ không chứa mã; `OrganizationInvitation` loại khỏi audit tự động |
| T3 | Lời mời bị chuyển tiếp cho người khác | Chỉ tài khoản có đúng email được mời chấp nhận được (BR-51); đăng ký qua lời mời lấy email từ lời mời, không từ request; người chưa đăng nhập chỉ thấy bốn điều PRD cho phép (BR-81 điểm 5) |
| T4 | Người có quyền quản lý thành viên hoặc vai trò tự nâng quyền, tự thành Owner, gỡ Owner, xoá vai trò để gỡ quyền người khác | `RoleGrantPolicy` duy nhất (§5.3), kiểm ở máy chủ cho cả việc tự đổi vai trò mình; khoá tổ chức chống tranh chấp (§5.4); không còn endpoint thêm thẳng (FR-59); test cho từng nhánh từ chối (§11) |
| T5 | Kẻ tấn công dựng tenant Microsoft Entra đặt email (hoặc `preferred_username`) trùng email nạn nhân ("nOAuth") để chấp nhận lời mời, khớp tài khoản sẵn có theo email, hoặc tạo tài khoản mang email nạn nhân | §6.6: chỉ tin email khi `tid` là tài khoản cá nhân hoặc `xms_edov = true`, đọc từ token đã kiểm chữ ký; bỏ `preferred_username`; email chưa xác minh không khớp tài khoản, không tạo tài khoản; tắt ánh xạ claim, đọc `sub`, `tid`, `email` theo tên thô; phần tenant của issuer phải bằng `tid`; tài khoản cũ có email chỉ do cách đọc sai chứng nhận bị đánh dấu chưa xác minh (M8) nên không chấp nhận được lời mời; người đã liên kết vẫn khớp theo `sub` (QĐ-12, QĐ-30) |
| T6 | Thành viên đã bị gỡ hoặc đã rời dùng phiên cũ (JWT còn `org_id`), kể cả qua refresh | Cổng §5.1 kiểm thành viên đang hoạt động mỗi request (cache bị xoá ngay khi gỡ); dữ liệu bên trong trả 404; refresh phát token `org_id = null` (§6.5), không bao giờ mang lại `org_id` cũ |
| T7 | Admin hệ thống tìm đường vào dữ liệu của tổ chức | Policy `SystemAdministrator` chỉ mở API-24–API-26, chỉ trả tên, số thành viên, trạng thái; không route nào nhận `organizationId` khác `org_id` để đọc dữ liệu bên trong; vào tổ chức chỉ qua lời mời (BR-53, AC-135) |
| T8 | Cấp quyền admin hệ thống từ bên trong Hub | Không endpoint HTTP nào ghi `system_administrators`; chỉ lệnh CLI chạy trong máy chủ; test API khẳng định không có route nào như vậy; policy không nhận khoá API |
| T9 | Người thường đọc email đồng nghiệp hoặc lời mời bằng gọi thẳng | `email = null` trong API-06 cho người không có quyền quản lý thành viên; API-10 cần quyền đó; `pendingInvitationCount = null` cho người chỉ có quyền vai trò (BR-74, BR-75) |
| T10 | Người xem nhật ký kiểm toán đọc nội dung feedback qua snapshot hoặc qua tham số của mục ngữ nghĩa | Quy tắc cho TECH02/TECH03: entity chứa nội dung feedback, bình luận, ảnh, ngữ cảnh kỹ thuật phải nằm trong `AuditExcludedEntityTypes` hoặc chỉ ghi mục ngữ nghĩa không có nội dung (BR-59, AC-161). API-33 chỉ trả mục `Event`, và danh mục sự kiện đóng chặn tham số không khai báo (§3.7, §6.8). BR-59 đã đủ; TECH03 áp T10 (§14 P4) |
| T11 | Ảnh feedback đọc được qua quyền quản lý file hoặc qua URL công khai | Quy tắc §7.5 |
| T12 | CSRF trên chấp nhận lời mời, rời tổ chức, vô hiệu | Middleware CSRF có sẵn (header bắt buộc với cookie); API-21 còn cần mã |
| T13 | Liệt kê định danh ngắn hay email qua thông báo lỗi | Thông báo trùng định danh không nêu tổ chức (BR-78); `InvitationEmailAlreadyMember` chỉ người có quyền quản lý thành viên của chính tổ chức đó thấy |
| T14 | Cửa sổ cập nhật cuốn chiếu: máy cũ còn endpoint thêm thẳng và gán vai trò không giới hạn | Triển khai API trước, cuốn chiếu nhanh, ghi rõ cửa sổ vài phút được chấp nhận (§10.2) |
| T15 | Người có phiên của một tài khoản (hoặc chủ tài khoản gõ nhầm) đổi email hồ sơ sang địa chỉ của người khác để: chấp nhận lời mời gửi tới địa chỉ đó; nhận tài khoản của nạn nhân khi nạn nhân đăng nhập Google/Microsoft lần đầu (khớp theo email); được cấp admin hệ thống theo email (B1) | Email chỉ đổi sau khi mở link gửi tới địa chỉ mới (§6.7); trong lúc chờ, `Email` cũ giữ nguyên nên không khớp được gì; xác nhận kiểm `PendingEmail = token.Email` và kiểm trùng lần nữa; link xác nhận không phát phiên; một vị từ `Account.EmailConfirmed` cho API-02, API-21, khớp email, `grant-system-admin` (QĐ-29). Tài khoản có thể đã bị đổi email trước bản sửa: bước rà soát một lần §10.2 bước 6 |
| T16 | Kẻ tấn công gửi `X-Forwarded-For` giả để né giới hạn tần suất theo IP, hoặc cookie phiên đi qua HTTP | Production dừng khởi động nếu không khai `KnownProxies` hoặc `KnownNetworks`, hoặc `AuthCookieSettings:Secure ≠ true` (§10.3) |
| T17 | SMTP chạy không mã hoá làm lộ mã lời mời, link xác nhận đổi email; log lộ địa chỉ người nhận | Production bắt buộc `SslOnConnect` hoặc `StartTls` (không `StartTlsWhenAvailable`); `SmtpEmailSender` không ghi người nhận (§6.2, §10.3) |
| T18 | Kẻ tấn công biết địa chỉ của một tài khoản không mật khẩu (M8) gọi *Gửi lại email xác thực* để nạn nhân nhận một thư thật, mở link và vô tình xác minh, đăng nhập tài khoản mang email đó; hoặc dùng một link xác minh cũ sau khi email đã đổi | `resend-verification` không gửi cho tài khoản không mật khẩu; mọi token ghi địa chỉ nó chứng minh, `verify-email` từ chối token có địa chỉ khác `Account.Email`; xác nhận đổi email tiêu thụ mọi token của tài khoản (§3.8, §4.9, N-new-1) |
| T19 | Đọc hash mật khẩu hay nhịp dò mật khẩu qua snapshot audit; đọc mục audit không thuộc tổ chức nào | `AuditExcludedProperties` cho `PasswordHash` và cột khoá; mục `organization_id = null` không endpoint nào trả (§3.7, N-new-5) |
| T20 | Kẻ cầm phiên đánh cắp đổi email hồ sơ sang địa chỉ của chính họ rồi xác nhận, để giữ tài khoản lâu dài (ví dụ đăng nhập Google bằng địa chỉ đó sẽ khớp vào tài khoản nạn nhân theo email) | Tài khoản có mật khẩu: phải nhập mật khẩu hiện tại, đếm trên bộ đếm khoá riêng (§6.7 bước 0, N-new-2). Tài khoản chỉ đăng nhập ngoài: **không có** biện pháp xác thực lại (rủi ro chấp nhận, §12); còn lại: cần phiên còn hiệu lực, chủ tài khoản thấy và thu hồi được phiên lạ, thư báo cho địa chỉ cũ chờ PRD05/TECH05 |

### 7.3 Không được ghi log

Mã lời mời và mã xác nhận đổi email (thô hoặc hash), mật khẩu, JWT, refresh token, thân request của `api/invitations/*` và `api/auth/*`. Email (kể cả người nhận trong `SmtpEmailSender`) chỉ được ghi trong mục nhật ký kiểm toán của §3.7, không trong log ứng dụng (log dùng `accountId`, `invitationId`).

### 7.4 Riêng tư email

BR-74, BR-75, D12 áp dụng như §4.6 và §3.7. `OrganizationMemberDto.email` chuyển thành nullable thay vì bỏ trường, để hợp đồng tương thích; frontend coi `null` là "không hiện".

### 7.5 File (giải quyết phần TECH00 của ràng buộc *Quyền đọc và tải file*)

- Production không dùng provider `Local` (máy khởi động thất bại, §10.3), nên `UseStaticFiles` không phát file nào trên production; chỉ tiền tố `public/` của S3/SeaweedFS đọc ẩn danh được (quyết định 2026-10-21).
- Ảnh và file của feedback **không** lưu thành `StoredFile`, không đi qua `FilesController`, không nằm dưới `public/`, không có `PublicUrl`. TECH02 thiết kế entity và endpoint riêng, đọc qua `IProductAccessScope` (§5.5), trả 404 ngoài phạm vi (AC-195). `files.manage` vì vậy không thấy chúng (AC-160).
- `FilesController` hiện tại giữ nguyên cho file quản trị của tổ chức.

## 8. Giao diện: ghép với thiết kế

### 8.1 Đưa design system "Lớp kính" vào frontend

- Nguồn token duy nhất `frontend/src/theme/tokens.ts` (sáng, tối): màu theo vai trò, chữ, viền, vòng focus, kính mỏng/dày, nền nổi, màu đặc thay thế, bo góc, khoảng cách lưới 4px, chuyển động, màu trạng thái/loại/ưu tiên, lấy đúng từ bảng *SystemLight/Dark*. `tailwind.css` (`@theme`, biến `.dark`) và `naive-theme.ts` (overrides sáng, tối) lấy giá trị từ đây; một test unit so `tailwind.css` với `tokens.ts` để hai bên không lệch (thay ghi chú "keep in sync" hiện có).
- Font Plus Jakarta Sans và JetBrains Mono tự host (gói `@fontsource-variable`), không gọi Google Fonts (§12 QĐ-24).
- Kính: `backdrop-filter` chỉ cho rail, thanh trên, panel; popover, menu, toast dùng nền đặc. `@media (prefers-reduced-transparency: reduce)` đổi mọi lớp kính sang màu đặc ghi trên bảng và tắt ánh nền; `prefers-reduced-motion` tắt chuyển động (đã có quy ước).
- Thành phần mới theo bảng *Components*: nút ba cỡ (40/44/32), ô nhập, chọn, ô tick, rail rộng và rail biểu tượng, thanh trên, bộ chọn tổ chức (popover ở máy tính, sheet từ dưới ở điện thoại), tấm trượt (sheet) cho tạo tổ chức, mời, tạo/sửa vai trò, khối xác nhận ngay dưới dòng (gỡ, thu hồi, xoá vai trò, rời), hộp thoại chỉ cho vô hiệu tổ chức (đúng thiết kế, ít hộp thoại), toast, trạng thái trống, nhãn "Tạm ngừng", "Hết hạn".
- Ba khoảng: điện thoại < 768 px (thanh trên + menu trượt + sheet dưới), máy tính bảng 768–1199 px (rail biểu tượng), máy tính ≥ 1200 px (rail rộng); vẽ ở 390, 834, 1280 px. Vùng bấm ≥ 44 px trên điện thoại.
- Bố cục chỉ còn rail của bảng *AppShell* (§14 T6, quyết định của người dùng 2026-10-23): rail rộng trên máy tính, rail biểu tượng trên máy tính bảng, thanh trên với menu trượt trên điện thoại. Bỏ năm chế độ menu (Static, Slim, Slim+, Drawer, Horizontal: `MenuMode` trong `lib/layout-preferences.ts`, `AppTopMenu`, `MenuModePreview`, `drawerPinned`) và hai màu menu. Phần *Giao diện* chỉ còn sáng/tối, ngôn ngữ, cỡ chữ. Lựa chọn cũ trong `localStorage` bị bỏ qua khi đọc, không có bước chuyển dữ liệu (QĐ-28).

### 8.2 Màn hình và API

| Màn hình (bảng) | Route (hash) | API | Trạng thái phía khách |
|---|---|---|---|
| Khung ứng dụng (*AppShell*) | mọi route có tổ chức | API-01 (bộ chọn), API-02 (tạo), API-27 (nhóm "Hệ thống") | Store `organizations` (của tôi), `auth` (`organizationId`, `permissions`, `isSystemAdministrator`). Nhóm "Tổ chức": *Thành viên* (mọi thành viên), *Quản lý tổ chức* (`organizations.manage`), *Nhật ký kiểm toán* (`auditlogs.view`); nhóm "Hệ thống": *Các tổ chức* khi `isSystemAdministrator` |
| Trang chào (*OrgContext*) | `/welcome` (trang chủ khi không có tổ chức) | API-01, API-02, API-28 | Thông báo tạm ngừng từ `sessionStorage` (§6.5) |
| Mẫu lỗi chung "Không đủ quyền" / "Không tìm thấy" (review thiết kế module 01) | trong khung có rail: route 404 (`NotFoundView.vue`) và mọi trang mà API trả 404 hoặc 403 `OrganizationAccessDenied` khi mở (thành viên, lời mời, vai trò, quản lý tổ chức, nhật ký kiểm toán, và trang của TECH01 trở đi) | — | Một thành phần dùng chung (`AccessProblemState`, hai biến thể theo `code`/status), biểu tượng trung tính, một nút *Về trang chủ*. **Không bao giờ** hiện tên, mã hay đường dẫn của đối tượng, kể cả trong breadcrumb (breadcrumb chỉ còn mục chung) và `document.title`; không hiện `detail` hay extension nào của `CodedProblemDetails`, chỉ câu chung theo locale — để không lộ đối tượng có tồn tại (BR-45). `OrganizationSuspended` vẫn theo §6.5, không dùng mẫu này. Lỗi của thao tác trong danh sách (404 từ API-07/08/13/17/18) vẫn hiện tại chỗ và tải lại danh sách |
| Thành viên (*Members*) | `/organization/members` | API-06, API-07, API-08, API-09, API-14 | Tab *Thành viên* (mọi thành viên), *Lời mời* (`organizations.members.manage`), *Vai trò* (`organizations.roles.manage`) — tab ẩn hẳn khi thiếu quyền (BR-77, AC-298, AC-299, AC-314). Hàng khoá theo `canChangeRoles`, `canRemove`, `lockReason`, `lockRole` (một dòng lý do, không mở danh sách chọn vai trò; AC-338, AC-341); email chỉ hiện khi khác `null`; dòng "Bạn" có *Rời tổ chức* với khối xác nhận dưới dòng (D6) và *Đổi vai trò*, không *Gỡ* (D16, AC-339) |
| Lời mời (*Members*) | `/organization/members/invitations` | API-10–API-14 | "Đang gửi…" trong lúc chờ API-11/API-12, nút bị khoá, không tự thử lại khi 503 hay hết thời gian chờ (§6.2); nhãn *Hết hạn* với *Mời lại*, *Xoá*; trạng thái trống (AC-274) |
| Vai trò (*Roles*) | `/organization/members/roles` | API-15–API-19 | Quyền không có thì ô tick khoá ("Chỉ chọn được quyền mà bạn có"); câu mô tả quyền lấy từ `locales/{vi,en}.ts` `permissions.<code>.description` (FR-65); vai trò hệ thống, vai trò thiếu quyền: chỉ xem; xác nhận xoá nêu số lời mời đang chờ khi có |
| Quản lý tổ chức (*OrgSettings*) | `/organization/settings` (`meta.permission = organizations.manage`) | API-03, API-04, API-05 | Lý do bắt buộc khi `isSystemAdministrator` (BR-54), đếm 0/500 |
| Nhận lời mời (*InviteAccept*) | `/invite` (không `requiresAuth`, không `guestOnly`) | API-20, API-21, API-22, API-23 | Các trạng thái của §6.3; form đăng ký với email chỉ đọc (AC-304); "Đăng xuất để dùng tài khoản khác" khi `WrongAccount`; khi `EmailNotConfirmed` trang báo cần xác minh email trước, kèm *Gửi lại email xác thực* (AC-351) |
| Nhật ký kiểm toán (review thiết kế module 01) | `/organization/audit-log` (`meta.permission = auditlogs.view`) | API-33 | Mục rail ẩn hẳn khi thiếu quyền (BR-77, AC-362); danh sách câu thường mới nhất trước, *Xem thêm*, trạng thái trống, không lọc (§6.8) |
| Các tổ chức (*SysOrgs*) | `/system/organizations` (`meta.systemAdministrator`) | API-24, API-25, API-26 | "Xem thêm"; xác nhận kích hoạt lại trong dòng; hộp thoại vô hiệu với lý do bắt buộc |
| Hồ sơ (bảng hồ sơ `ProfilePanel.vue`, thay `ProfileDialog.vue`; theo review thiết kế module 01) | bảng bên ở máy tính (≥ 1200 px), sheet ở máy tính bảng và điện thoại; mở từ menu tài khoản, không có route riêng | API-29, API-30, API-31 | Dòng chữ thường "Đang chờ xác nhận: <email mới>. Mở link trong thư để đổi." với *Gửi lại*, *Huỷ* khi `pendingEmail` khác `null`; không thêm hộp thoại (§6.7). Tài khoản chưa xác minh gặp 403 `EmailNotConfirmed` ở tạo tổ chức, chấp nhận lời mời: thông báo theo locale kèm *Gửi lại email xác thực* |
| Xác nhận đổi email | `/confirm-email-change` (không `requiresAuth`, không `guestOnly`) | API-32 | Xoá mã khỏi thanh địa chỉ; kết quả thành công hoặc lỗi theo `code` |

- Bỏ `OrganizationsView`, `OrganizationMembersDialog`, `OrganizationRolesDialog`, `OrganizationList`, `OrganizationForm` cũ; store `roles` đổi sang route tổ chức đang làm việc (không còn tham số `organizationId`).
- `lib/permissions.ts` thêm `FeedbackHubAdmin: 'feedbackhub.admin'`; test unit khẳng định mọi mã có câu mô tả vi và en, không trống, ≤ 120 ký tự (AC-294, AC-327).
- Tên vai trò hệ thống hiện theo `systemKind` và ngôn ngữ giao diện ở mọi nơi (bộ chọn tổ chức, danh sách, trang lời mời, AC-309).
- Lỗi: ánh xạ `code` sang locale; `OrganizationSuspended` và `OrganizationAccessDenied` từ cổng xử lý chung (§6.5); 404 từ API-07/08/13/17/18 hiện "không tìm thấy" và tải lại danh sách.
- Mọi chuỗi mới có trong `vi.ts` và `en.ts`.

## 9. Hiệu năng và quy mô

Quy mô (`docs/process/system-context.md`): ≤ 20 tổ chức, ≤ 500 tài khoản, ≤ 100 người dùng đồng thời; mục tiêu p95 < 1 giây.

- API-06: một truy vấn, nối `organization_members` (lọc `OrganizationId`, `IsActive`) với `accounts`, vai trò gom bằng truy vấn thứ hai theo danh sách id trang hiện tại; bỏ vòng lặp `GetByIdAsync` từng tài khoản của `GetMembersAsync` cũ. API-01 cũng bỏ vòng lặp từng tổ chức.
- API-15: số người giữ bằng một truy vấn `GROUP BY` trên `organization_member_roles` nối `organization_members` đang hoạt động.
- API-24: `COUNT` thành viên đang hoạt động theo tổ chức trong một truy vấn gom.
- API-33: một truy vấn keyset trên chỉ mục một phần `audit_logs (organization_id, "timestamp" DESC, id DESC) WHERE kind = 'Event'`, không `COUNT`.
- Chỉ mục mới: `organization_invitations (OrganizationId, Email)` unique, `(TokenHash)` unique, `(RoleId)`; `roles (OrganizationId, lower("Name"))` unique; `system_administrators (AccountId)` unique.
- Cache Redis: quyền, truy cập tổ chức, admin hệ thống (phạm vi sản phẩm thêm ở TECH01); TTL 60 giây, xoá chủ động; Redis `noeviction` (quyết định 2026-10-16).
- Gửi email mời đồng bộ (thường 1–2 giây, tối đa 15 giây) chỉ ảnh hưởng thao tác mời.
- Không có job nền mới.

## 10. Di chuyển dữ liệu và triển khai

### 10.1 Migration (chạy được trên dữ liệu đang có, không đổi tên hay xoá cột code cũ còn dùng)

| # | Nội dung | An toàn với code cũ |
|---|---|---|
| M1 | Thêm cột vô hiệu vào `organizations` (nullable, `DeactivatedBySystemAdministrator` mặc định `false`) | Code cũ không đọc |
| M2 | Tạo `organization_invitations`, `system_administrators` (bảng sản phẩm: migration của TECH01) | Bảng mới |
| M3 | Đổi tên vai trò tự tạo trùng nhau không phân biệt hoa thường, hoặc trùng `owner`/`admin`/`member`/`chủ sở hữu` (sau NFC), bằng cách thêm hậu tố ` (2)`, ` (3)`…, ghi danh sách vào log migration; sau đó bỏ chỉ mục `(OrganizationId, Name)`, tạo chỉ mục unique `(OrganizationId, lower("Name"))` | Code cũ kiểm trùng chính xác, chỉ mục mới chặt hơn: trong cửa sổ cuốn chiếu, máy cũ có thể gặp lỗi 500 khi tạo tên trùng khác hoa thường — chấp nhận |
| M4 | Xoá `organization_member_roles` của thành viên `IsActive = false` (BR-30) | Code cũ đã lọc thành viên đang hoạt động khi tính quyền và Owner cuối cùng |
| M5 | Thêm `role_permissions (feedbackhub.admin)` cho mọi vai trò `SystemRoleKind = 'Admin'` chưa có (idempotent) | Code cũ hiện mã lạ ở danh sách quyền, không lỗi |
| M6 | Không động tới `Slug` (cột giữ varchar(100); slug cũ giữ nguyên) | — |
| M7 | Thêm `accounts.PendingEmail` varchar(200) null, ba cột đếm `accounts.FailedEmailChangeCount` int not null default 0, `EmailChangeWindowStartedAt` timestamptz null, `EmailChangeLockedUntil` timestamptz null (§6.7 bước 0), `email_verification_tokens.Purpose` varchar(20) null và `email_verification_tokens.Email` varchar(200) null (§3.8) | Code cũ không đọc. Token cũ (không có `Email`) không còn dùng được ở `verify-email` mới; người dùng bấm *Gửi lại email xác thực* (chưa có người dùng production thật) |
| M9 | Thêm `audit_logs.kind` varchar(10) not null default `'Change'`, `audit_logs.actor_kind` varchar(20) null (chỉ mục `Event` dùng) và chỉ mục một phần `(organization_id, "timestamp" DESC, id DESC) WHERE kind = 'Event'`; dòng cũ có `action` `Revoked`, `Deleted` (`OrganizationInvitation`) hay `Left` không tồn tại trước bản này nên không cần chuyển | Code cũ không đọc; mặc định giữ nghĩa snapshot |
| M8 | Dữ liệu: `UPDATE accounts SET "EmailConfirmed" = false WHERE "EmailConfirmed" = true AND "PasswordHash" IS NULL AND` có liên kết `external_logins.provider = 'Microsoft'` `AND` không có liên kết `'Google'` (§6.6 bước 4). Viết trong khối `DO` dùng `RETURNING`, rồi `RAISE NOTICE` chỉ số lượng và danh sách `Id`; lệnh `migrate` chuyển các NOTICE này vào log. Không ghi email, username. Câu lệnh idempotent (chỉ chạm dòng còn `true`), chạy một lần theo lịch sử migration của EF; chạy lại bằng tay sau khi người dùng đã xác minh lại sẽ đánh dấu họ lần nữa (chỉ chặt hơn, không mở thêm quyền) | Code cũ không kiểm `EmailConfirmed` ở nhánh đã liên kết nên người dùng vẫn đăng nhập được trong cửa sổ cuốn chiếu |

### 10.2 Thứ tự triển khai

0. Bật optional claim `xms_edov` (ID token) trong app registration Microsoft. Thiếu bước này, mọi tài khoản Microsoft công ty chưa liên kết bị từ chối khi đăng nhập lần đầu (§6.6).
1. Chạy lệnh `migrate` một lần (service hoặc job riêng) trước khi cập nhật máy API; `DatabaseSettings:MigrateOnStartup = false` trên production nhiều máy (§12 QĐ-03).
2. Cập nhật lần lượt từng máy API (route mới có, route `{id}` cũ và thêm thẳng không còn).
3. **Quy tắc cứng: frontend mới chỉ được phát hành sau khi máy API cuối cùng đã cập nhật.** Trong vài phút giữa bước 2 và 3, tab đang mở của bản frontend cũ gặp 404 ở màn hình tổ chức cũ cho đến khi tải lại (chấp nhận theo system-context: "mất vài phút khi cập nhật là chấp nhận được").
   Việc bỏ route `{id}` cũ ngay trong bản phát hành này đã chốt (§14 T4); PR triển khai ghi một dòng `- Removed:` trong `## Changelog`. Tab frontend cũ gặp 404 cho đến khi tải lại; điều này, cùng cửa sổ T14, nằm trong mức "mất vài phút khi cập nhật" của system-context.
4. Người vận hành chạy `grant-system-admin --email <email>` cho admin hệ thống đầu tiên.
5. Quay lại: image cũ cộng bản sao lưu (system-context). M1–M5, M7, M8 không phá code cũ nên image cũ chạy được trên schema mới, trừ chỉ mục M3 chặt hơn. Quay lại image cũ đưa B1, B2 trở lại; dữ liệu M8 giữ nguyên.
6. **Rà soát một lần (người vận hành production, tức người dùng)** sau bước 1. Hai truy vấn chỉ đọc; việc làm gì với kết quả là quyết định của người dùng, tài liệu này không định trước:

   (1) Tài khoản vừa bị M8 đánh dấu chưa xác minh:

   ```sql
   SELECT a."Id", a."Username", a."Email"
   FROM accounts a
   WHERE a."EmailConfirmed" = false AND a."PasswordHash" IS NULL
     AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
     AND NOT EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
   ORDER BY a."Username";
   ```

   (2) Tài khoản có mật khẩu và có liên kết Microsoft, tài khoản có cả liên kết Microsoft lẫn Google (M8 bỏ qua chúng vì có Google; liên kết Microsoft có thể đã được gắn qua khớp email bằng email chưa xác minh, N-new-6), cùng tài khoản mà nhật ký kiểm toán cho thấy `Email` đã đổi (qua hồ sơ, trước bản sửa B1). Nhật ký ghi snapshot đầy đủ ở mỗi lần sửa, nên phép so là giữa hai mục liên tiếp của cùng tài khoản, không giữa `old_values` và `new_values` của một mục:

   ```sql
   SELECT a."Id", a."Username", a."Email", 'PasswordAndMicrosoftLink' AS reason, NULL::text AS previous_email, NULL::timestamptz AS changed_at
   FROM accounts a
   WHERE a."PasswordHash" IS NOT NULL
     AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
   UNION ALL
   SELECT a."Id", a."Username", a."Email", 'MicrosoftAndGoogleLinks', NULL, NULL
   FROM accounts a
   WHERE EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
     AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
   UNION ALL
   SELECT a."Id", a."Username", a."Email", 'EmailChangedInAudit', t.previous_email, t."timestamp"
   FROM (
     SELECT entity_id, "timestamp", new_values ->> 'Email' AS email,
            LAG(new_values ->> 'Email') OVER (PARTITION BY entity_id ORDER BY "timestamp", id) AS previous_email
     FROM audit_logs
     WHERE entity_name = 'Account' AND new_values ? 'Email'
   ) t
   JOIN accounts a ON a."Id"::text = t.entity_id
   WHERE t.previous_email IS NOT NULL AND t.previous_email <> t.email
   ORDER BY 2, 6;
   ```

   Nhật ký chỉ có từ khi bật audit tự động; thay đổi trước đó không thấy được. Kết quả chứa email, nên chỉ chạy trong phiên của người vận hành, không lưu vào ticket hay kênh chat.

**Không hotfix** (quyết định của người dùng, 2026-10-23: chưa có người dùng production thật): B1 (§6.7) và B2 (§6.6 bước 1) được sửa trong chính bản phát hành module 00, là các task **đầu tiên** của kế hoạch module 00 (cùng M7, M8 và các thay đổi `ExternalLoginAsync`, `SmtpEmailSender` mà chúng cần), trước mọi phần tổ chức, lời mời, vai trò. Thứ tự giữa chúng do kế hoạch quyết.

**Nghĩa vụ của PR triển khai (N-new-3):** thêm `POST api/invitations/lookup` (API-20), `POST api/auth/register-with-invitation` (API-22) và `POST api/auth/confirm-email-change` (API-32) vào danh sách `[AllowAnonymous]` của `.claude/rules/authentication.md`, mỗi endpoint có một test controller khẳng định nó ẩn danh (và không endpoint nào khác của các controller đó ẩn danh); thiếu dòng hoặc test thì PR không đạt.

Không dùng cờ tính năng: toàn bộ module 00 bật cùng một bản phát hành, vì nửa vời (ví dụ còn thêm thẳng mà đã có giới hạn BR-58) để lại lỗ hổng.

### 10.3 Sẵn sàng nhiều máy API (bắt buộc)

`ProductionTopologyValidator` chạy lúc khởi động khi `ASPNETCORE_ENVIRONMENT = Production` (không chạy khi xuất OpenAPI hay test) và dừng máy nếu:

- `CacheSettings:Provider ≠ Redis`, `RealtimeSettings:Backplane ≠ Redis`, hoặc thiếu `ConnectionStrings:Redis`;
- `StorageSettings:Provider = Local`;
- `RateLimiterSettings:Store ≠ Redis`;
- `DatabaseSettings:MigrateOnStartup = true`;
- `ForwardedHeadersSettings:KnownProxies` và `ForwardedHeadersSettings:KnownNetworks` đều trống (giới hạn tần suất khoá theo IP thật, §4.8, T16);
- `AuthCookieSettings:Secure ≠ true`;
- chế độ mã hoá SMTP không phải `SslOnConnect` hoặc `StartTls` (T17). Thêm `EmailSettings:SecureSocket` (`SslOnConnect` | `StartTls` | `StartTlsWhenAvailable`); khi để trống, suy từ `UseSsl` như hiện nay (`true` → `SslOnConnect`, `false` → `StartTlsWhenAvailable`), nên cấu hình dev không phải sửa; production phải khai rõ một trong hai giá trị được nhận.

Ngoài ra: Data Protection đặt `SetApplicationName("FeedbackHub")` cố định để mọi máy giải được cookie và dữ liệu bảo vệ của nhau (khoá đã lưu trong PostgreSQL); client SignalR dùng `transport: WebSockets`, `skipNegotiation: true` nên bộ cân bằng tải không cần sticky session; API không giữ trạng thái nào trong bộ nhớ ngoài cache có TTL; Hangfire dùng PostgreSQL (đã có), job và seeder idempotent (quy tắc hiện có). Môi trường dev và test vẫn được dùng `Memory`, `None`, `Local`.

## 11. Chiến lược kiểm thử

| Nhóm | Tầng | AC |
|---|---|---|
| Định danh ngắn, tên tổ chức | Domain (`Organization.Create`, `Update`); frontend unit `slug-suggestion` | AC-281, AC-283, AC-284, AC-285, AC-187, AC-328, AC-329, AC-331 |
| Tạo tổ chức, trùng định danh, chuyển vào | API; frontend view | AC-56, AC-282, AC-286, AC-330 |
| Tên vai trò | Domain (`Role`, NFC); Application; Infrastructure (chỉ mục lower, migration M3 trên dữ liệu trùng) | AC-287–AC-293, AC-326 |
| `RoleGrantPolicy` | Application: mọi nhánh cho phép và từ chối, Owner, tự đổi vai trò mình; thêm vai trò cho người giữ vai trò mình thiếu quyền → từ chối; người có mọi quyền nhưng không phải Owner thêm vai trò cho Owner → `OwnerOnlyOperation`; `lockReason`/`lockRole` (Owner thắng khi cả hai lý do cùng đúng; vai trò thiếu quyền đầu tiên); dòng của chính mình `canChangeRoles = true`, `canRemove = false` | AC-336, AC-338–AC-341, AC-153–AC-158, AC-193, AC-194, AC-197, AC-198, AC-249, AC-253–AC-255, AC-257–AC-259, AC-269–AC-271, AC-295–AC-297, AC-311 |
| Đổi vai trò, gỡ, Owner cuối cùng, gỡ chính mình, đồng thời | Application (gỡ chính mình mà thiếu quyền lấy vai trò mình → 403 trước 409 `CannotRemoveSelf`; BR-58 đọc từ cơ sở dữ liệu: quyền của người gọi bị bỏ ngay trước đó trong cache còn cũ vẫn bị từ chối); Infrastructure (khoá `FOR UPDATE` với hai giao dịch song song); API (`roleIds` chứa vai trò của tổ chức khác → 404, không ghi gì) | AC-69 (phần thành viên), AC-125, AC-157, AC-264, AC-277, AC-278, AC-336, AC-337 (câu báo đúng, không mục nhật ký mới), AC-340 |
| Xoá vai trò, người giữ, lời mời theo vai trò | Application; Infrastructure (cascade lời mời); API | AC-55, AC-159, AC-247–AC-252, AC-256, AC-257 |
| Danh sách thành viên, email | API (gọi thẳng với ba loại người gọi; admin hệ thống không phải thành viên và ngữ cảnh cá nhân → 404) | AC-261–AC-265, AC-335 |
| Lời mời: gửi, thay, mời lại, thu hồi, xoá, danh sách, nhật ký | Application (email lỗi hoặc hết 15 giây → rollback, `DidNotReceive`; log không có email, mã); frontend (nút khoá khi đang gửi, không tự thử lại); API; Infrastructure (unique, mục audit không có `TokenHash`) | AC-60, AC-115–AC-117, AC-179, AC-180, AC-196, AC-266–AC-275, AC-310, AC-312, AC-316, AC-324, AC-325 |
| Mở và chấp nhận lời mời, thứ tự BR-81 | Application (bảng §6.3 thành `[Theory]`, kể cả hai nhánh của bước 2: `WrongAccount` trước `EmailNotConfirmed`); API (ẩn danh, đúng tài khoản, sai tài khoản; API-22 phát phiên `org_id` = tổ chức vừa vào hoặc `null`, cookie không "ghi nhớ", `isSystemAdministrator` đúng; header `no-store`, `no-referrer`, `noindex`; 429); frontend view `/invite`; e2e cục bộ cho đăng ký qua lời mời | AC-57–AC-59, AC-118, AC-119, AC-144, AC-151, AC-152, AC-177, AC-178, AC-181, AC-182, AC-303–AC-309, AC-332–AC-334 |
| Thêm thẳng đã bỏ | API: route cũ trả 404; không route nào tạo `OrganizationMember` ngoài tạo tổ chức và chấp nhận | AC-135, AC-260 |
| Cổng tổ chức, thứ tự BR-77 | Infrastructure (handler: mọi lý do từ chối); API (ngữ cảnh cá nhân, tạm ngừng trước thiếu quyền, đối tượng tổ chức khác 404) | AC-107, AC-186, AC-275, AC-298–AC-300, AC-313, AC-316–AC-321 |
| Phần ẩn theo quyền | Frontend (tab, menu, route guard) | AC-298, AC-299, AC-314, AC-315 |
| Vô hiệu, kích hoạt lại, tạm ngừng giữa chừng | Application; API; frontend (interceptor, trang chào với thông báo) | AC-73, AC-105, AC-106, AC-128, AC-131, AC-138, AC-139, AC-141, AC-146–AC-148, AC-183, AC-184, AC-199–AC-201, AC-301 |
| Đổi tên tổ chức | API; frontend | AC-185–AC-187, AC-225, AC-226 |
| Admin hệ thống | API (policy, không nhận khoá API, không route cấp quyền); CLI command test (`grant` cần email đã xác minh, in username và id; `revoke` xoá cache resolver: request kế tiếp trên máy khác bị 403); Infrastructure (sắp xếp ICU) | AC-51, AC-52, AC-122, AC-124, AC-126, AC-127, AC-302 |
| Rời tổ chức | Application; API; frontend (khối xác nhận) | AC-276–AC-280, AC-321–AC-323 |
| Bộ chọn tổ chức, trang chào | Frontend | AC-04, AC-05, AC-74, AC-201 |
| Hiệu lực ngay | Infrastructure (xoá cache Redis thấy ở instance khác: hai `RedisCacheService` cùng một Redis Testcontainers) | AC-12, AC-190 (phần cache) |
| Câu mô tả quyền | Frontend unit | AC-294, AC-327 |
| Đọc token Microsoft (B2, N2) | Infrastructure: `MicrosoftJwtValidator.ValidateToken` thật trên token ký cục bộ bằng khoá RSA của test, khoá và cấu hình OIDC tiêm vào (không mock bộ kiểm). Trường hợp: `tid` cá nhân; `xms_edov` bool `true`, chuỗi `"true"`, `"TRUE"`, bool `false`, chuỗi khác, thiếu; thiếu `email` mà có `preferred_username` → email `null`; thiếu `tid` → từ chối; tenant của `iss` khác `tid` → từ chối; thiếu `sub` (dù có `oid`) → từ chối; token có cả `oid` và `sub` → `ProviderUserId == sub` | QĐ-12 |
| Đăng nhập Microsoft (§6.6, N3) | Application (`ExternalLoginAsync`): liên kết cũ theo `sub` vẫn đăng nhập; đã liên kết + chưa xác minh → đăng nhập được; đã liên kết + tài khoản bị khoá → 401; khớp email + chưa xác minh → từ chối, không liên kết; khớp email + tài khoản bị khoá → 401, không liên kết; tài khoản mới + chưa xác minh → từ chối, không tạo; tài khoản M8 + token xác minh đúng email → `EmailConfirmed = true`; tài khoản M8 + email khác hoặc chưa xác minh → vẫn `false`; Google giữ hành vi | AC-305; QĐ-12, QĐ-30 |
| M8 và rà soát | Infrastructure (Testcontainers PostgreSQL): M8 chỉ đổi tài khoản không mật khẩu, có Microsoft, không Google; chạy hai lần không đổi thêm; NOTICE không chứa email; hai truy vấn §10.2 bước 6 chạy được trên schema mới và trả đúng dòng mẫu | — (QĐ-30) |
| Đổi email hồ sơ (B1) | Domain (`RequestEmailChange`, `CancelEmailChange`, `ApplyEmailChange`); Application: đổi email → `Email` không đổi, `PendingEmail` đặt, thư tới địa chỉ mới có username và câu "Nếu không phải bạn…", mã không vào log; trùng `Email` người khác → 409; trùng `PendingEmail` người khác → được; xác nhận đúng → `Email` mới, `EmailConfirmed = true`, `PendingEmail = null`; đổi tiếp sang địa chỉ thứ ba rồi mở link đầu → 409 `EmailChangeLinkNoLongerValid`; *Huỷ* rồi mở link → 409; *Gửi lại* rồi mở link đầu → 409, mở link mới → đổi; quá 24 giờ (đồng hồ giả) → 409; lưu hồ sơ với `email` = `PendingEmail` → không thư mới, link cũ còn dùng được; địa chỉ bị người khác đăng ký trong lúc chờ → 409 `AccountEmailAlreadyExists`, tài khoản kia không đổi; token `Email = null` gửi tới API-32 → 409; token đổi email gửi tới `verify-email` → 401, không phiên; lời mời tới địa chỉ đang chờ → `WrongAccount`. API: API-32 không đặt cookie, không trả token, trình duyệt đăng xuất vẫn đăng xuất; 429; API-29–API-31 401. Frontend: dòng chờ xác nhận trong `ProfileDialog`, *Gửi lại*, *Huỷ*; view `/confirm-email-change` | AC-342–AC-350 (QĐ-29, T15) |
| Xác thực lại khi đổi email (N-new-2) | Application (`UpdateCurrentProfileAsync`): tài khoản có mật khẩu, email đổi, mật khẩu đúng → `PendingEmail` đặt, thư gửi, bộ đếm `EmailChange` được xoá; thiếu `currentPassword` hoặc chuỗi rỗng → 400, `ReserveAttemptAsync` `DidNotReceive` (không tính lần sai), không thư; mật khẩu sai → 401 `InvalidCurrentPassword`, không `PendingEmail`, không token, không thư, không trường hồ sơ nào đổi, `ReserveAttemptAsync(EmailChange)` được gọi; lần sai thứ `MaxFailedAttempts` → `LockAsync(EmailChange)`, lần kế tiếp (kể cả mật khẩu đúng) → 401 `EmailChangeTemporarilyLocked`; bộ đếm `Login` và `PasswordChange` không bị đụng; mật khẩu sai với địa chỉ đã có người dùng → 401, không 409; tài khoản không mật khẩu, email đổi, không `currentPassword` → được, không gọi `ReserveAttemptAsync`; email không đổi → không hỏi; đổi sang địa chỉ thứ ba khi đang chờ → hỏi lại; API-30 → không hỏi. Infrastructure (Testcontainers): ba cột mới chỉ `PasswordAttemptStore` ghi, cập nhật hồ sơ thường không ghi đè chúng, không vào snapshot audit. API: 401, 400 qua HTTP; frontend: ô *Mật khẩu hiện tại* chỉ hiện khi sửa email và `hasPassword` | AC-352–AC-358 (QĐ-35, T20) |
| Một vị từ email đã xác minh | API: tài khoản `EmailConfirmed = false` (đăng nhập qua Microsoft liên kết cũ) gọi API-02 → 403, API-20 → `EmailNotConfirmed` không trường nào khác; API-21 → 403 `EmailNotConfirmed` và lời mời còn nguyên; tài khoản chưa xác minh mở lời mời gửi tới email khác → `WrongAccount` (bước 2 so email trước); CLI `grant-system-admin` từ chối tài khoản chưa xác minh | AC-351 (QĐ-29) |
| Token xác minh ghi địa chỉ (N-new-1) | Application: `resend-verification` cho tài khoản không mật khẩu → 204, không token, không thư; `verify-email` với token mà `Email` ≠ `Account.Email` hiện tại → 401; token cũ không có `Email` → 401; token `ChangeEmail` → 401; xác nhận đổi email → mọi token `VerifyEmail` và `ChangeEmail` còn lại của tài khoản đã dùng | — (T18, QĐ-30) |
| Kiểm `Status` một chỗ (N-new-4) | Application (`TokenIssuer.IssueTokensAsync`: tài khoản bị khoá → 401, không tạo refresh token); API: tài khoản bị khoá gọi lần lượt login, refresh, verify-email, external, register-with-invitation (tài khoản vừa tạo không thể bị khoá: chỉ khẳng định không có đường vòng), switch-organization → đều 401 | — (QĐ-33) |
| Audit theo thuộc tính và mục không có tổ chức (N-new-5) | Infrastructure: tạo, sửa, xoá `Account` → `old_values`/`new_values` không có `PasswordHash` và các cột khoá; API: mục audit có `organization_id = null` và mục `Change` không ra ở API-33 với mọi người gọi; route `api/admin/audit-logs` cũ trả 404 | — (T19, QĐ-34) |
| Nhật ký kiểm toán (FR-68) | Application: mỗi thao tác của danh mục §3.7 ghi đúng một mục `Event` cùng giao dịch (AC-367), thao tác bị từ chối hay rollback không ghi (AC-337); mời lại thay lời mời cũ chỉ có `InvitationSent` (AC-325); tạo tổ chức không ghi (AC-364); `RecordAsync` với tham số không khai báo → lỗi, với `OrganizationId` rỗng → lỗi, với `Account`/`SystemAdministrator` mà không có `ActorAccountId` → lỗi, với `System` mà có `ActorAccountId` → lỗi (C2); `IAuditTrail` không nhận `ICurrentTenantProvider`; `AuditEvent.For(subject)` chép đúng `OrganizationId` của entity, `For` với entity có `OrganizationId` rỗng → lỗi, và test kiến trúc khẳng định `AuditEvent` không có constructor hay factory công khai nhận `Guid` tổ chức (C1). Gửi lời mời (AC-372), *Mời lại* đúng một mục (AC-373), nhận lời mời với người làm là người vào (AC-374); tự gỡ và mời trực tiếp khi thiếu quyền bị từ chối, mục mới nhất không đổi (AC-370). Đúng tổ chức (A1, qua HTTP): K nhận lời mời vào A khi đang làm việc ở B → có `MemberJoined` ở A, không có ở B; API-21 từ ngữ cảnh cá nhân → mục ở A; API-22 → mục ở A, người làm là tài khoản vừa tạo; API-25, API-26 do admin hệ thống đang làm việc ở C → (admin không là thành viên A) mục ở A với `actorKind = SystemAdministrator`, không tên, email, lý do, `DeactivatedBySystemAdministrator = true`, không có ở C (AC-368); admin hệ thống kiêm thành viên X vô hiệu X qua API-25 rồi kích hoạt lại qua API-26 → cả hai mục `actorKind = Account`, có tên, không lý do, `DeactivatedBySystemAdministrator = false` (AC-375); mục cũ giữ `actor_kind` sau khi người đó vào hoặc rời X; API-25 với tổ chức đã vô hiệu (204, không đổi gì) → không ghi mục; admin hệ thống gọi API-05 hoặc gửi lời mời trong tổ chức của mình → `actorKind = Account`, có tên (B3, AC-369). Mục `Event` có `ip_address`, `user_agent` là `null` (B4). Infrastructure (Testcontainers): keyset đúng thứ tự `timestamp DESC, id DESC` kể cả nhiều mục cùng `timestamp`; *Xem thêm* không lặp, không sót khi có mục mới chen vào; chỉ mục `Event` của `org_id`; snapshot của một entity `IHasOrganization` (ví dụ `ApiKey`) có `kind = 'Change'` và không ra ở API-33 (B2); dòng `Event` có khoá lạ trong `new_values` → khoá đó không có trong `parameters`; dòng `Event` có `action` lạ → bị bỏ qua, có log cảnh báo (B1); dòng `Event` có `actor_kind` null hoặc lạ → bị bỏ qua, có log, không hiện thành `Account` (C2). API: 401; 403 cho ngữ cảnh cá nhân (AC-366) và người thiếu `auditlogs.view` (AC-362); 403 khi gọi bằng `X-Api-Key` (B7); chỉ mục của X khi làm việc ở X (AC-363); cursor hỏng → 400 cho từng dạng (base64 sai, ticks ngoài khoảng, `id ≤ 0`), không 500; `limit` 0 hoặc 101 → 400; phản hồi không có trường IP hay user agent (AC-371); mục có email hiện cho người chỉ có `auditlogs.view` (AC-335); không trả `old_values`/`new_values` thô. Frontend: câu theo locale vi/en cho mọi loại, thời điểm theo múi giờ (AC-359), *Xem thêm* (AC-360), trạng thái trống (AC-364), không ô lọc (AC-365), loại lạ hiện câu chung; tên người làm chứa `<img src=x onerror=…>` hiện nguyên văn, không có phần tử `img` trong DOM (B6); đổi tổ chức xoá danh sách đã tải; mục rail ẩn khi thiếu quyền | AC-161, AC-268, AC-272, AC-279, AC-325, AC-335, AC-359–AC-375 |
| Mẫu lỗi chung | Frontend: route lạ và trang có API 404/403 hiện cùng thành phần trong khung có rail, một nút *Về trang chủ*; tên, mã, đường dẫn của đối tượng không có trong DOM kể cả breadcrumb; `detail` và mọi extension của `CodedProblemDetails` không được hiện; `document.title` không chứa tên đối tượng (B6); `OrganizationSuspended` không dùng mẫu này | AC-310, AC-317, AC-318 (phần giao diện) |
| Bảng hồ sơ | Frontend: bảng bên ở ≥ 1200 px, sheet ở khoảng nhỏ hơn; các test hồ sơ hiện có (đổi email, mật khẩu hiện tại, dòng chờ xác nhận) chạy trên `ProfilePanel` | AC-342, AC-352–AC-358 (phần giao diện) |
| Danh sách ẩn danh (N-new-3) | API: test controller cho API-20, API-22, API-32 khẳng định `[AllowAnonymous]`; `ArchitectureTests` vẫn đạt; dòng tương ứng có trong `.claude/rules/authentication.md` (nghĩa vụ §10.2) | — |
| Refresh khi mất quyền vào tổ chức (N10) | Application (`RefreshTokenAsync`: bị gỡ → token `org_id = null`, cùng `familyId`; tổ chức tạm ngừng → như trên; tài khoản bị khoá → 401); frontend (sau refresh `X` → `null`: không phát lại request, tạm ngừng → thông báo có tên, bị gỡ → trang chào không thông báo) | AC-183, AC-199, AC-200 |
| Thứ tự thành viên trước tạm ngừng (N5) | Infrastructure (`TenantAccessService.GetAccessAsync`: ba kết quả); API: người không phải thành viên gọi vào tổ chức tạm ngừng → `OrganizationAccessDenied`, thân không có `organizationName`; API-28 cùng thứ tự | AC-301, AC-320 |
| `SmtpEmailSender` (N4) | Infrastructure: log lỗi không chứa người nhận; máy chủ SMTP giả treo ở bước kết nối, ở bước xác thực, ở bước gửi → cả ba dừng trong khoảng 15 giây tổng, không phải 15 giây mỗi bước | — (QĐ-10) |
| Bố cục chỉ còn rail | Frontend (layout ở ba khoảng; `localStorage` có `menuMode` cũ bị bỏ qua; *Giao diện* chỉ ba lựa chọn) | — (QĐ-28) |
| Giới hạn tần suất chung | Infrastructure (Redis Testcontainers: hai limiter cùng khoá dùng chung đếm; Redis lỗi thì về limiter cục bộ) | — (QĐ-02) |
| Kiểm cấu hình production | API host test với môi trường Production: từng điều kiện của §10.3 (kể cả proxy tin cậy trống, cookie không `Secure`, SMTP `StartTlsWhenAvailable`) làm khởi động thất bại | — (§10.3, T16, T17) |
| Token design system | Frontend unit (drift `tailwind.css` ↔ `tokens.ts`) | — (QĐ-24) |

`API.Tests` vẫn chạy `CacheSettings:Provider=Memory` theo `ApiFactoryFixture`; các test cần Redis nằm ở `Infrastructure.Tests` với Testcontainers Redis (thêm vào fixture). Mọi endpoint có test 401 và 403 theo `testing.md`.

## 12. Quyết định, rủi ro, phương án đã loại

| # | Quyết định | Vì sao | Phương án đã loại và lý do | Phạm vi |
|---|---|---|---|---|
| QĐ-01 | Production bắt buộc Redis cho cache và backplane SignalR; máy dừng khi khởi động nếu không đúng (§10.3). Thay quyết định 2026-08-25 "Redis is not hard-required" cho production (cần dòng `Supersedes:`; người dùng đã quyết trong system-context 2026-10-23) | Nhiều máy là yêu cầu cứng; cache trong bộ nhớ làm thu hồi quyền chậm tới hết TTL ở máy khác | Giữ tuỳ chọn và chỉ ghi tài liệu: một cấu hình sai chạy được mà âm thầm sai | Nền tảng |
| QĐ-02 | Giới hạn tần suất đếm chung qua Redis (cửa sổ cố định, một script Lua `INCR` + `PEXPIRE`), cắm vào tên policy `AddRateLimiter` có sẵn; Redis lỗi thì về limiter trong bộ nhớ của máy đó và ghi log cảnh báo | Giới hạn `auth` 5/phút nhân theo số máy làm yếu chống dò mật khẩu và mã; con số phải không phụ thuộc số máy | Chấp nhận nhân theo số máy: giới hạn thật thay đổi mỗi lần thêm máy. Gói `RedisRateLimiting`: thêm phụ thuộc cho ~100 dòng code. Fail-open khi Redis lỗi: mất giới hạn đúng lúc hệ thống yếu | Nền tảng |
| QĐ-03 | Migration chạy bằng lệnh `migrate` một lần trước khi cuốn chiếu; `MigrateOnStartup = false` trên production; mọi migration theo mở rộng rồi thu hẹp (không đổi tên, xoá cột trong cùng bản phát hành) | Nhiều máy khởi động cùng lúc sẽ chạy migration song song | Migration lúc khởi động có khoá: phụ thuộc hành vi khoá của provider, khó kiểm | Nền tảng |
| QĐ-04 | Data Protection `SetApplicationName` cố định; SignalR client chỉ WebSockets, `skipNegotiation` | Mọi máy giải được dữ liệu của nhau; không cần sticky session | Sticky session ở bộ cân bằng tải: thêm cấu hình hạ tầng, một máy hỏng làm rớt phiên SignalR | Nền tảng |
| QĐ-05 | `OrganizationMemberDto.email` nullable, `null` cho người không có quyền quản lý thành viên trừ chính mình | BR-74, giữ hợp đồng tương thích | Bỏ trường: phá client cũ; DTO riêng cho từng loại người gọi: hai schema cho cùng màn hình | Module |
| QĐ-06 | Mọi tài nguyên quản trị tổ chức dưới `api/organization/...` lấy tổ chức từ `org_id` của JWT; bỏ hẳn các route `api/organizations/{id}/...` và hai handler theo route, cùng bản phát hành | BR-45 cần 404 cho đối tượng tổ chức khác, route `{id}` trả 403; mở rộng quyết định 2026-08-02 cho Files, ApiKeys, AuditLogs | Giữ route `{id}` và đổi 403 thành 404: vẫn lộ một tham số chọn tổ chức; mở rộng rồi thu hẹp qua hai bản: route cũ cũng phải được vá BR-58, BR-74, gấp đôi bề mặt | Nền tảng |
| QĐ-07 | Một requirement cổng tổ chức kiểm bước 1–4 của BR-77 theo thứ tự, hai chế độ từ chối (`Forbidden` cho các phần chỉ dành cho một quyền theo BR-77 và BR-45, kể cả phần Sản phẩm của TECH01; `NotFound` cho đọc dữ liệu bên trong tổ chức còn lại, kể cả API-06; PRD-00 v1.3 câu 115); lý do từ chối mang mã riêng; `ITenantAccessService` trả ba trạng thái, kiểm thành viên trước trạng thái tổ chức, dùng chung cho cổng, API-28, refresh, middleware | "Tạm ngừng" phải báo trước "thiếu quyền"; middleware chạy sau policy nên không làm được thứ tự đó; người ngoài không được biết tên hay trạng thái của tổ chức mình không thuộc về | Giữ `TenantAccessMiddleware` làm cổng chính: chạy sau policy quyền, sai thứ tự | Nền tảng |
| QĐ-08 | BR-58 tính ở một `RoleGrantPolicy` trên quyền hiệu lực; mọi thao tác đổi thành viên, vai trò, lời mời khoá dòng tổ chức (`FOR UPDATE`) trong giao dịch, rồi đọc quyền và người giữ vai trò thẳng từ cơ sở dữ liệu (không qua `IPermissionResolver`) | Một chỗ cho máy chủ và cờ giao diện; chặn hai Owner gỡ nhau cùng lúc; quyết định cấp quyền không dựa trên cache có thể cũ | Kiểm rải rác từng service: dễ lệch; khoá tư vấn Postgres theo hash: khó đọc hơn, cùng tác dụng; dùng `IPermissionResolver` trong giao dịch: cache Redis có thể cũ trong khoảng giữa ghi và xoá cache | Module |
| QĐ-09 | Mã lời mời 256 bit, chỉ lưu SHA-256, một dòng mỗi (tổ chức, email), xoá hẳn khi dùng, thu hồi, thay, xoá; mã trong fragment URL, tra bằng POST | Mã sai và mã đã thu hồi không phân biệt được; không lưu dấu vết mã đã chết; mã không vào log | Lưu trạng thái `Revoked`/`Accepted`: giữ hash vô ích và thêm trạng thái phải lọc ở mọi truy vấn; mã trong query string: vào log nginx | Module |
| QĐ-10 | Gửi email mời đồng bộ trong giao dịch, lỗi SMTP thì rollback và trả 503; timeout SMTP ≤ 15 giây; frontend khoá nút, không tự thử lại; log lỗi chỉ kèm `invitationId`; chấp nhận rủi ro SMTP nhận thư mà commit lỗi (§6.2). SA (thay người dùng), 2026-10-23 | "Không email nào được gửi" và "lời mời cũ vẫn còn" giữ đúng khi lỗi; mã không bao giờ nằm trong job; khoá tổ chức không bị giữ quá timeout của nginx | Hangfire: mã thô nằm trong tham số job trong PostgreSQL và dashboard, và người gửi không được báo khi gửi lỗi; Hangfire với mã được bảo vệ bằng `ISecretProtector`: thêm phần chuyển động, vẫn không báo lỗi được; khoá idempotency: thử lại bằng tay đã thay lời mời (BR-63 điểm 3) | Module |
| QĐ-11 | Đăng ký bằng email qua lời mời là endpoint riêng `register-with-invitation`; Google/Microsoft dùng đăng nhập có sẵn rồi API-21. API-22 phát phiên qua `TokenIssuer` (`org_id` = tổ chức vừa vào hoặc `null`, `isPersistent = false`). **Rủi ro chấp nhận**: ai nhận được link chuyển tiếp (người được mời chuyển thư, hộp thư dùng chung) đăng ký được tài khoản mang email người được mời, coi như đã xác minh, và vào tổ chức với vai trò của lời mời; đó là hệ quả trực tiếp của BR-63 điểm 6 (sở hữu link = sở hữu email), giới hạn bởi hạn 7 ngày và việc lời mời bị xoá khi dùng | Email được xác minh nhờ sở hữu link (BR-63 điểm 6) chỉ ở một chỗ; không thêm nhánh vào đăng nhập ngoài; một chỗ phát phiên nên cờ và `org_id` không lệch giữa các đường đăng nhập | Thêm `invitationToken` tuỳ chọn vào `register` và `external`: nhánh bảo mật trộn vào luồng chung | Module |
| QĐ-12 | Microsoft: tắt ánh xạ claim, đọc `sub`, `tid`, `xms_edov`, `email` theo tên thô; `ProviderUserId` = `sub` (giữ khoá của liên kết hiện có, không đọc `oid`); `tid` bắt buộc và phải bằng tenant của `iss`; chỉ tin email khi `tid` là tài khoản cá nhân hoặc `xms_edov = true` (nhận bool và chuỗi); áp ở khớp email và tạo tài khoản, cho cả đăng nhập Microsoft đang có; người đã liên kết vẫn khớp theo `sub`; bỏ `preferred_username` và `NameIdentifier`; bật `xms_edov` trong app registration (§6.6). Thay quyết định 2026-07-25 *Microsoft external login: multi-tenant issuer validated by regex, no email_verified check* (`implementer` ghi mục `Supersedes:`). Quyết định của người dùng, 2026-10-23; cách đọc claim (B2, phương án a): SA (thay người dùng), 2026-10-23. **Để người dùng lưu ý khi duyệt**: chữ trong quyết định T2 đổi từ "khớp theo `oid`" sang "khớp theo `sub`"; ý định không đổi (người đã liên kết vẫn đăng nhập được), vì liên kết hiện có thực ra đã khoá theo `sub` | Chặn chiếm lời mời và tài khoản bằng tenant giả ("nOAuth"); kiểm ở đầu hàm như hiện nay sẽ khoá người đã liên kết; đổi khoá liên kết sang `oid` sẽ làm mọi liên kết cũ không khớp | Tin claim `email` như hiện tại (quyết định 2026-07-25): bị khai thác được; chỉ áp cho lời mời: tài khoản vẫn bị chiếm qua khớp email khi đăng nhập; gọi Graph: không chứng minh được miền; khoá `oid` (+ `tid`) với migration chuyển khoá: không có cách nối `sub` cũ với `oid` mà không chờ người dùng đăng nhập lại | Nền tảng |
| QĐ-13 | `OrganizationInvitation` loại khỏi audit tự động; mục ngữ nghĩa qua cổng `IAuditTrail`; `Role`, `OrganizationMember` cài `IHasOrganization` | Snapshot sẽ chứa `TokenHash`; PRD cần email và vai trò trong mục, không cần snapshot | Snapshot tự động rồi lọc trường: dễ quên khi thêm trường | Nền tảng (cổng audit) |
| QĐ-14 | Hai đường đọc vai trò: API-15 (có quyền, cần quản lý vai trò) và API-14 (tên + `canGrant`, cần quản lý thành viên) | BR-77, AC-300, AC-311 | Một endpoint lọc trường theo quyền: hợp đồng mơ hồ | Module |
| QĐ-15 | Admin hệ thống trong bảng `system_administrators`, chỉ lệnh CLI ghi; `grant-system-admin` chỉ nhận tài khoản có email đã xác minh và in username, id để người vận hành đối chiếu; `revoke-system-admin` xoá cache của `ISystemAdministratorResolver`; policy `SystemAdministrator` chỉ nhận JWT người dùng, tra có cache, không nằm trong JWT | BR-20; thu hồi có hiệu lực trong vài giây; có dấu thời gian cấp | Danh sách email trong cấu hình: phải khởi động lại, lệch giữa các máy; cột trên `accounts`: lệnh cập nhật hồ sơ ghi đè mọi cột, có thể xoá mất cờ | Nền tảng |
| QĐ-16 | Gỡ và rời xoá vai trò tổ chức và vai trò theo sản phẩm, giữ dòng thành viên `IsActive = false` để lời mời kích hoạt lại | BR-30; chỉ mục unique (tổ chức, tài khoản) | Xoá hẳn dòng thành viên: mất `CreatedAt` và tham chiếu lịch sử feedback của TECH03 | Module |
| QĐ-17 | Bỏ thêm thẳng: xoá endpoint, `AddMemberAsync`, `AddMemberRequest`; giữ hằng `OrganizationMemberAdded` và mẫu hiển thị để thông báo cũ trong cơ sở dữ liệu vẫn hiện, không phát thông báo mới | FR-59, BR-73 | Xoá luôn kiểu thông báo: thông báo cũ hiện lỗi | Module |
| QĐ-18 | Tên vai trò: trim, NFC, chỉ mục unique `lower("Name")`, tên hệ thống vi/en kiểm ở Application; migration đổi tên trùng có sẵn | BR-79 không phân biệt hoa thường, an toàn khi đồng thời | Chỉ kiểm ở Application: hai request song song lọt; collation `citext`: thêm extension, không bắt tên tiếng Việt | Module |
| QĐ-19 | Định dạng định danh kiểm trong `Organization.Create`; `Update` chỉ đổi tên; slug cũ không kiểm lại | BR-65 không bao giờ đổi; đổi tên tổ chức có slug cũ không được lỗi | Kiểm trong `Update` như mẫu chung: đổi tên tổ chức có slug cũ sẽ hỏng | Module |
| QĐ-20 | Danh sách dùng `PagedResult`, 50 mặc định, tối đa 100, sắp theo collation ICU rồi `Id` | Thiết kế có "Xem thêm" và tổng số; D9 đúng với chữ có dấu | Trả hết: không chặn trên; `lower()` theo byte: "Ánh" xếp sau "Bình" | Module |
| QĐ-21 | Hợp đồng `IProductAccessScope` (phạm vi, quy tắc 404/403, xoá cache) được thiết kế ở TECH00; bảng `products`, `product_members`, hiện thực và việc xoá `product_members` khi gỡ, rời thuộc kế hoạch TECH01, với nghĩa vụ ở §5.5. SA (thay người dùng), 2026-10-23 | FR-05–FR-07 và BR-03 dùng chung cho TECH01–TECH06; một định nghĩa phạm vi; không dựng bảng chưa có màn hình nào dùng | Mỗi module tự lọc: lệch quy tắc 404/403 giữa các màn hình; dựng bảng ở kế hoạch TECH00: bảng không có endpoint, trường sẽ đổi khi TECH01 thiết kế | Nền tảng |
| QĐ-22 | Tệp feedback không đi qua `StoredFile`/`FilesController`, không dưới `public/`; production cấm provider `Local` | BR-59, AC-195: quyền quản lý file và URL công khai không mở đường tới feedback | Dùng `StoredFile` kèm lọc theo sản phẩm: mọi đường đọc file chung phải biết phạm vi sản phẩm | Nền tảng |
| QĐ-23 | Route `/invite` xoá mã khỏi thanh địa chỉ, giữ trong `sessionStorage` của tab, chèn meta `noindex` và `no-referrer`; API-20 trả `no-store`, `no-referrer`, `noindex` | Hash routing nên nginx không phân biệt được route để đặt header | Đường dẫn riêng ngoài hash do nginx phục vụ: thêm cấu hình nginx cho một trang | Module |
| QĐ-24 | Token design system một nguồn `theme/tokens.ts`, test drift với `tailwind.css`; font tự host; kính đổi màu đặc theo `prefers-reduced-transparency` | Naive UI cần giá trị màu thật, Tailwind cần CSS: một nguồn cộng test thay việc giữ tay | Biến CSS cho cả Naive: Naive không tính được màu hover từ biến; Google Fonts: gửi IP người dùng cho bên thứ ba (NĐ 13/2023), thêm nguồn ngoài cho CSP. Đoán "máy yếu" bằng phần cứng: không tin cậy | Nền tảng |
| QĐ-25 | Bỏ route cũ trong cùng bản phát hành; thứ tự migrate → cập nhật từng máy API → frontend, frontend mới chỉ phát hành sau máy API cuối cùng (§10.2); PR có dòng `- Removed:`. SA (thay người dùng), 2026-10-23 | Ít bề mặt phải vá; tab cũ gặp 404 đến khi tải lại, trong mức "vài phút khi cập nhật" | Giữ route cũ một bản: xem QĐ-06 | Module |
| QĐ-26 | Tổ chức mặc định khi đăng nhập chỉ chọn trong tổ chức đang hoạt động | Tài khoản mà mọi tổ chức đều tạm ngừng phải thấy trang chào, không thấy lỗi (AC-184) | Giữ như cũ: người dùng vào tổ chức tạm ngừng rồi bị đẩy ra | Module |
| QĐ-28 | Chỉ còn bố cục rail của bảng *AppShell*; bỏ năm chế độ menu và hai màu menu; *Giao diện* còn sáng/tối, ngôn ngữ, cỡ chữ; bỏ qua lựa chọn cũ trong `localStorage`, không chuyển dữ liệu (§8.1). Quyết định của người dùng, 2026-10-23 | Thiết kế đã duyệt chỉ có rail; mỗi chế độ menu nhân đôi số bố cục phải thiết kế và kiểm ở ba khoảng màn hình | Giữ các chế độ cũ song song: bố cục không có trong thiết kế, phải tự vẽ lại theo "Lớp kính"; chuyển lựa chọn cũ sang bố cục mới: không có tương ứng một-một | Module |
| QĐ-27 | Sửa, xoá vai trò hệ thống báo `OrganizationSystemRoleImmutable` trước BR-58 | AC-55 yêu cầu đúng lý do này; vai trò hệ thống không phải đối tượng sửa được nên xếp cùng bước 5 | Theo D2 báo "không đủ quyền": trái AC-55 | Module |
| QĐ-29 | Đổi email ở hồ sơ qua `accounts.PendingEmail` và token xác nhận mang `Email`; `Email` chỉ đổi khi mở link gửi tới địa chỉ mới (`PendingEmail = token.Email`, kiểm trùng lần nữa); link hết hạn sau 24 giờ, mỗi tài khoản tối đa một link dùng được (đổi, *Gửi lại*, *Huỷ* vô hiệu link cũ); link xác nhận không phát phiên; một vị từ `Account.EmailConfirmed` cho API-02, API-21, khớp email ngoài, `grant-system-admin`; bỏ cột `external_logins.EmailVerified` của bản 0.3 (§3.8, §6.7). Sửa B1, không hotfix. SA (thay người dùng), 2026-10-23; khớp PRD-00 v1.3 BR-82, D17 | Một email chưa chứng minh không được mở lời mời, khớp tài khoản ngoài hay cấp admin hệ thống; `Email` cũ giữ nguyên trong lúc chờ nên tài khoản không mất đường đăng nhập | Đổi `Email` ngay và đặt `EmailConfirmed = false`: người gõ nhầm bị khoá khỏi đăng nhập mật khẩu, còn địa chỉ chưa chứng minh vẫn chiếm chỗ unique; bảng yêu cầu đổi email riêng: thêm một entity cho đúng một trường; cờ xác minh theo từng liên kết ngoài: hai vị từ cho cùng câu hỏi | Nền tảng (định danh) |
| QĐ-30 | Tài khoản chỉ có email do Microsoft chứng nhận theo cách đọc claim cũ (không mật khẩu, có Microsoft, không Google) bị M8 đánh dấu chưa xác minh; xác minh lại chỉ khi đăng nhập Microsoft với token chứng nhận đúng `Account.Email` (`resend-verification` không gửi cho tài khoản không có mật khẩu, N-new-1); trong lúc đó đăng nhập được nhưng không tạo tổ chức, không chấp nhận lời mời; người vận hành rà soát một lần bằng hai truy vấn §10.2 bước 6. SA (thay người dùng), 2026-10-23 | Email của các tài khoản này có thể đến từ `preferred_username` hoặc tenant chưa xác minh miền (B2), nên không còn đáng tin; không khoá ai khỏi đăng nhập | Giữ nguyên: email chưa chứng minh tiếp tục mở lời mời; khoá hẳn tài khoản: người dùng thật mất truy cập; cột `EmailVerified` theo liên kết (bản 0.3): vị từ thứ hai; cho phép xác minh lại bằng thư: thư xác minh tới địa chỉ chưa ai chứng minh là mồi lừa phát phiên (N-new-1) | Module |
| QĐ-31 | Refresh gặp thành viên bị gỡ hoặc tổ chức tạm ngừng phát token mới với `org_id = null` thay vì 401; tài khoản bị khoá vẫn 401; frontend không phát lại request gốc khi `organizationId` chuyển sang `null` mà đi theo §6.5 bước 3. SA (thay người dùng), 2026-10-23 | 401 đẩy người dùng ra màn hình đăng nhập và mất thông báo tạm ngừng (AC-199, AC-200); phát lại request trong ngữ cảnh khác có thể ghi nhầm tổ chức | Giữ 401: trải nghiệm sai so với PRD; phát lại request sau khi đổi ngữ cảnh: không an toàn | Nền tảng (phiên) |
| QĐ-32 | `ProductionTopologyValidator` còn kiểm điều kiện bảo mật: proxy tin cậy khai báo, cookie `Secure`, SMTP `SslOnConnect` hoặc `StartTls`; `SmtpEmailSender` không ghi người nhận, một hạn 15 giây chung cho kết nối, xác thực, gửi. SA (thay người dùng), 2026-10-23 | Một cấu hình sai chạy được mà âm thầm yếu đi (giới hạn tần suất theo IP của proxy, cookie qua HTTP, mã trong thư đi không mã hoá) | Chỉ ghi vào tài liệu triển khai: không ai bị chặn khi quên | Nền tảng |
| QĐ-33 | Kiểm `account.Status` một lần trong `TokenIssuer.IssueTokensAsync` cho cả sáu đường phát phiên; `ExternalLoginAsync` chỉ giữ thêm kiểm ở nhánh khớp email (trước khi tạo liên kết). `appsec` (N-new-4), 2026-10-23 | `VerifyEmailAsync` hiện phát phiên mà không kiểm; một chỗ thì đường phát phiên mới không quên | Kiểm ở từng service: đã quên một lần | Nền tảng (phiên) |
| QĐ-34 | Loại thuộc tính khỏi audit (`AuditExcludedProperties`, bắt đầu với `PasswordHash` và cột khoá của `Account`); mục audit không có `organization_id` không bao giờ được endpoint nào trả. `appsec` (N-new-5), 2026-10-23 | Loại cả entity thì mất lịch sử đổi email (§10.2 bước 6); hash mật khẩu không có giá trị kiểm toán | Che giá trị lúc trả về: hash vẫn nằm trong cơ sở dữ liệu và bản sao lưu; loại cả `Account`: mất dấu vết đổi email | Nền tảng (audit) |
| QĐ-35 | Đổi email (API-29) với tài khoản có mật khẩu cần `currentPassword`, kiểm trên mục đích riêng `PasswordAttemptPurpose.EmailChange` (ba cột đếm mới, cùng cơ chế và giới hạn với đăng nhập và đổi mật khẩu), trước kiểm trùng email; thất bại thì cả request không lưu gì; tài khoản không mật khẩu không bị hỏi; API-30, API-31 không hỏi. Quyết định của người dùng, 2026-10-23 (N-new-2); PRD-00 v1.4 BR-82 điểm 7, D18 | Link xác nhận gửi tới địa chỉ mới chỉ chứng minh người đổi sở hữu địa chỉ mới, không chứng minh họ là chủ tài khoản; mật khẩu hiện tại chứng minh điều đó | Dùng chung bộ đếm đăng nhập: kẻ cầm phiên khoá được chủ tài khoản khỏi đăng nhập, trái `authentication.md`; xác thực lại bằng đăng nhập Google/Microsoft lại cho tài khoản không mật khẩu: thêm một luồng OAuth giữa chừng cho trường hợp hiếm, người dùng chưa chọn; thư báo cho địa chỉ cũ: thuộc PRD05 | Module |
| QĐ-36 | Màn hình nhật ký kiểm toán đọc riêng mục ngữ nghĩa (`audit_logs.kind = 'Event'`) qua API-33, phân trang keyset `(timestamp, id)` cùng thứ tự hiện có, trả `type` + tham số khai báo trước, frontend dựng câu theo locale; danh mục sự kiện đóng; `AuditEvent` mang tổ chức và người làm tường minh, không đọc JWT; nhãn "admin hệ thống FeedbackHub" chọn theo tư cách người làm (admin hệ thống không là thành viên của X lúc vô hiệu hay kích hoạt lại), chốt vào mục, dùng chung phép tra với `DeactivatedBySystemAdministrator` (§3.7; PRD-00 v1.4 §3.1, BR-84 điểm 12, BR-53; SA thay người dùng, 2026-10-23, theo câu 69 của người dùng); mục `Event` không lưu IP, user agent; bỏ `api/admin/audit-logs` (snapshot thô, chưa có màn hình dùng). SA (thay người dùng) từ review thiết kế module 01, 2026-10-23; PRD-00 v1.4 câu 119, BR-83, BR-84; review `appsec` v0.9 (A1, B1–B7). **Rủi ro chấp nhận**: `audit_logs.id` là chuỗi tăng chung cho mọi tổ chức, nên khoảng cách giữa các `id` liên tiếp mà người xem thấy (qua `id` trong DTO và cursor) tiết lộ sơ lượng hoạt động của tổ chức khác; chấp nhận ở quy mô ≤ 20 tổ chức | BR-83 cần câu thường, không JSON; danh mục đóng là chỗ chặn nội dung feedback (BR-59) bằng code thay vì quy ước; keyset không lặp mục khi nhật ký tăng giữa hai lần *Xem thêm* | Diễn giải snapshot `Change` thành câu: mỗi entity, mỗi cột cần luật dịch, dễ lộ trường mới thêm; máy chủ dựng sẵn câu theo `Accept-Language`: trộn hai hệ thống dịch (`localization.md`); bảng riêng `audit_events`: tách hai nơi ghi nhật ký cho cùng một khái niệm, phải nhân đôi quy tắc `organization_id`; `Skip/Take`: lặp, sót mục khi có mục mới; giữ route cũ song song: hai đường đọc phải cùng giữ BR-59, D12; lấy `organization_id` từ JWT như snapshot: ghi sai tổ chức khi nhận lời mời từ tổ chức khác, đăng ký ẩn danh, admin hệ thống thao tác từ xa; ghi IP cho mục `Event`: trùng với dòng `Change` cùng giao dịch, thêm một chỗ phải giữ ngoài DTO; chọn nhãn theo route (mọi thao tác từ API-25/26 đều ghi nhãn): lệch với thư báo vô hiệu của câu 69, giấu tên một thành viên với chính tổ chức của họ; tính nhãn lúc hiện thay vì chốt lúc ghi: mục cũ đổi nghĩa khi người đó vào hay rời tổ chức | Nền tảng (audit) |

**N-new-2 (review `appsec` v0.5), đã quyết (PRD-00 v1.4 BR-82 điểm 7, D18):** đổi email ở hồ sơ cần mật khẩu hiện tại với tài khoản có mật khẩu, kiểm trên bộ đếm khoá riêng `PasswordAttemptPurpose.EmailChange`; thất bại thì không có gì chờ xác nhận và không thư nào được gửi (§6.7 bước 0, QĐ-35). Tài khoản chỉ đăng nhập ngoài không bị hỏi. **Rủi ro chấp nhận**: với tài khoản đó, ai cầm được một phiên còn hiệu lực đổi được email sang địa chỉ của mình và giữ tài khoản qua khớp email khi đăng nhập ngoài (T20); thư báo cho địa chỉ cũ thuộc PRD05/TECH05. Nguồn: Quyết định của người dùng, 2026-10-23; quy tắc nghiệp vụ ở PRD-00 v1.4 câu 118, D18, AC-352–AC-358 (chờ người dùng duyệt).

**Rủi ro còn lại**: cửa sổ vài phút khi cuốn chiếu (T14); email mời phụ thuộc SMTP đồng bộ, và hiếm khi người nhận có link chết nếu commit lỗi sau khi SMTP đã nhận thư (QĐ-10); mẫu email tạm chỉ tiếng Việt trước PRD05; người dùng Microsoft thuộc tenant chưa xác minh miền (hoặc khi app registration chưa bật `xms_edov`) không tạo tài khoản, không khớp tài khoản theo email, phải đăng ký bằng email; link lời mời bị chuyển tiếp mở được đăng ký thay người được mời (QĐ-11, hệ quả của BR-63 điểm 6); tài khoản đã bị đổi email hoặc gắn liên kết Microsoft giả trước bản sửa chỉ được phát hiện qua rà soát một lần (§10.2 bước 6), việc xử lý do người dùng quyết.

## 13. Truy vết

| Mã PRD | Phần thiết kế đáp ứng | Test (§11) |
|---|---|---|
| FR-01 | §5.1, §5.5, §7.2 T7 | AC-121, AC-122, AC-136 ở đây (phần dữ liệu quản trị); AC-01, AC-02 ở TECH01/TECH03 |
| FR-02 | §3.2, §3.6, §5.1 | AC-03 (TECH03) |
| FR-03 | API-01, API-28, §6.1, §8.2 | AC-04, AC-05 |
| FR-04 | §5.1 (`org_id`), §5.5 | AC-06 (TECH03) |
| FR-05, FR-07 | §5.5 | AC-07, AC-08, AC-11, AC-12, AC-53, AC-86, AC-87 (TECH03, TECH06) |
| FR-06 | §3.6 (BR-03), §5.5 (thiết kế; dựng ở TECH01) | AC-09, AC-10 (TECH01) |
| FR-08, FR-09 | §5.5 (quy tắc 404/403) | AC-14–AC-18, AC-91, AC-170–AC-172 (TECH03) |
| FR-11 | §5.5 (thành viên theo tổ chức sở hữu sản phẩm) | AC-22, AC-23 (TECH02) |
| FR-12 | §5.1, §5.2, §5.5 | AC-12, AC-43, AC-190, AC-214–AC-217, AC-223, AC-239, AC-241, AC-245 (phần cổng ở đây, hành vi ở TECH03/TECH07) |
| FR-21 | §5.2 API-24–API-26, §7.2 T7 | AC-122, AC-124, AC-126, AC-136, AC-246 (TECH02) |
| FR-23 | §3.3, §5.5 | AC-53, AC-54 (TECH03), AC-55, AC-159 |
| FR-24 | API-02, §6.1 | AC-56 |
| FR-25 | §3.4, §4.2, §4.4, §6.2, §6.3 | AC-57–AC-60, AC-115–AC-119, AC-144, AC-151, AC-152, AC-177–AC-182, AC-196, AC-303–AC-309, AC-312, AC-332–AC-334, AC-348, AC-351 |
| FR-29 | API-08, §6.4, §5.5 (nghĩa vụ TECH01) | AC-69, AC-259 (phần thành viên; phần vai trò theo sản phẩm ở truy vết TECH01), AC-70 (404 phần dữ liệu ở TECH03), AC-318, AC-320, AC-337, AC-339 |
| FR-30 | API-05, API-25, API-28, §6.5 | AC-73, AC-107; AC-71, AC-72 (TECH02, TECH04) |
| FR-31 | §8.2 trang chào | AC-74, AC-201, AC-303 |
| FR-37 | §5.1 bước 5, §5.5 | AC-85, AC-121–AC-123, AC-134, AC-136, AC-176, AC-195, AC-280, AC-310, AC-317, AC-318 (phần feedback ở TECH03) |
| FR-38 | §3.5, §5.2, T8 | AC-51, AC-52 |
| FR-42 | API-26, §6.5 | AC-105, AC-106, AC-128, AC-131, AC-144, AC-152, AC-334; AC-129, AC-130, AC-142, AC-143, AC-145 (TECH02–TECH04) |
| FR-44 | API-24, §4.8 | AC-124–AC-127, AC-302 |
| FR-45 | §6.3 (admin hệ thống vào qua lời mời như mọi tài khoản), §5.2 | AC-132–AC-134 (TECH03), AC-135 |
| FR-46 | API-05, API-25, §3.1 | AC-138, AC-139, AC-141, AC-146–AC-148 |
| FR-52 | §5.3, §5.4 | AC-55, AC-153–AC-158, AC-193, AC-194, AC-197, AC-198, AC-247–AC-259, AC-269–AC-271, AC-295–AC-297, AC-311, AC-317, AC-319, AC-336, AC-338, AC-340, AC-341 |
| FR-53 | API-04, §3.1 | AC-185–AC-187, AC-225, AC-226 |
| FR-54 | §5.1 bước 3, §6.5, §8.2 | AC-73, AC-151, AC-183, AC-184, AC-199–AC-201, AC-301, AC-308, AC-320, AC-321, AC-332, AC-333 |
| FR-56 | §5.5, §6.8, §7.2 T10, T11, §7.5 | AC-160, AC-161, AC-195 (TECH02, TECH03) |
| FR-59 | §4.5 (bỏ), QĐ-17 | AC-135, AC-260 |
| FR-60 | API-06, §4.6 (`lockReason`, `lockRole`), §7.4 | AC-261–AC-265, AC-335, AC-338, AC-339, AC-341 |
| FR-61 | API-10–API-13, §3.4, §3.7 | AC-266–AC-275, AC-310, AC-316, AC-324, AC-325, AC-335 |
| FR-62 | API-09, §6.4, §5.5 (nghĩa vụ TECH01) | AC-276 (phần thành viên; phần vai trò theo sản phẩm ở truy vết TECH01), AC-277–AC-280, AC-321–AC-323, AC-337 |
| FR-63 | API-02, §3.1, §6.1 | AC-281–AC-286, AC-328–AC-331 |
| FR-64 | §3.3, §5.3 | AC-287–AC-293, AC-295, AC-297, AC-326 |
| FR-65 | §8.2 (locale, test) | AC-294, AC-327 |
| FR-66 | §4.2 API-14, §4.3, §5.1, §8.2 | AC-275, AC-298–AC-300, AC-311, AC-313–AC-315 |
| FR-68, BR-83, BR-84, BR-53 (PRD-00 v1.4, chờ duyệt) | §3.1 `DeactivatedBySystemAdministrator`, §3.7 (danh mục sự kiện khớp BR-84, `ActorKind` theo tư cách, M9), §4.10 API-33, §5.1, §6.8, §8.2, QĐ-36 | AC-161 (phần nhật ký), AC-279, AC-335, AC-359–AC-375 |
| FR-67 | §3.8, §4.9 API-29–API-32, §6.7, §8.2 (hồ sơ, `/confirm-email-change`) | AC-342–AC-350 |
| FR-67 (PRD-00 v1.4 BR-82 điểm 7, D18; chờ duyệt) | §6.7 bước 0, API-29, §4.6 `hasPassword`, M7, QĐ-35, T20 | AC-352–AC-358 |
| FR-24, BR-23, BR-51 (email đã xác minh) | §3.8, §6.3 bước 2, §6.6 bước 4, API-02, API-20, API-21 | AC-351; test "một vị từ", M8 (§11) |

Ngoài phạm vi, thuộc module khác: FR-10 (TECH04: AC-20, AC-21, AC-92, AC-191, AC-192); FR-13–FR-17, FR-20, FR-26–FR-28, FR-35 (TECH03: AC-24–AC-36, AC-40, AC-41, AC-61–AC-68, AC-82, AC-83, AC-90, AC-120, AC-173, AC-188, AC-189, AC-210, AC-211, AC-243, AC-244); FR-49 (TECH07: AC-43).

Phần thiết kế không có yêu cầu PRD trực tiếp, giữ vì quy tắc repo, system-context hoặc review `appsec`: §10.3 và QĐ-01–QĐ-04 (system-context: nhiều máy), QĐ-32 và §6.6 bước 1 (review `appsec` v0.3: N2, N4, N12, B2), QĐ-31 (N10, phục vụ AC-199, AC-200), `IHasOrganization` cho `Role`, `OrganizationMember` (quy tắc kiến trúc), QĐ-24 (catalog TECH00: chuyển design system).

## 14. Câu hỏi mở

### Câu hỏi kỹ thuật (gửi `solution-architect`)

| # | Câu hỏi | Đề xuất trả lời | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|
| T2 | Có áp QĐ-12 cho cả đăng nhập Microsoft đang có, không chỉ lời mời? | — | — | Đã trả lời: có, theo đề xuất; thêm các chi tiết `tid` từ token đã kiểm, `xms_edov` bool/chuỗi, liên kết `oid` vẫn đăng nhập, bỏ `preferred_username`, bật claim khi triển khai, `appsec` kiểm ba điểm (§6.6, §7.2 T5, §10.2, §11, QĐ-12). Nguồn: Quyết định của người dùng, 2026-10-23. Bản 0.4: "liên kết `oid`" thành "liên kết `sub`" theo B2 (SA thay người dùng), ý định không đổi; nêu lại khi người dùng duyệt |
| T6 | Chỉ giữ rail của *AppShell*, bỏ chế độ menu? | — | — | Đã trả lời: bỏ năm chế độ menu và hai màu menu; *Giao diện* còn sáng/tối, ngôn ngữ, cỡ chữ; bỏ qua lựa chọn cũ trong `localStorage` (§8.1, QĐ-28). Nguồn: Quyết định của người dùng, 2026-10-23 |
| T1 | Bảng sản phẩm và `IProductAccessScope` dựng trong kế hoạch TECH00 hay TECH01? | — | — | Đã trả lời: không dựng trong kế hoạch TECH00. Giữ §5.5 làm thiết kế; bảng, hiện thực và việc xoá `product_members` khi gỡ, rời thuộc kế hoạch TECH01 kèm nghĩa vụ ở §5.5; bỏ hai bảng khỏi M2; phần sản phẩm của AC-69, AC-259 sang truy vết TECH01 (§3.6, §5.5, §10.1, §13, QĐ-21). Nguồn: SA (thay người dùng), 2026-10-23 |
| T3 | Gửi email mời đồng bộ trong giao dịch? | — | — | Đã trả lời: đồng bộ, với bốn điều kiện (timeout ≤ 15 giây; frontend khoá nút, không tự thử lại; log chỉ `invitationId`; chấp nhận rủi ro link chết). Hangfire bị loại (§6.2, QĐ-10). Nguồn: SA (thay người dùng), 2026-10-23 |
| T4 | Bỏ route `{id}` cũ cùng bản phát hành? | — | — | Đã trả lời: bỏ, theo thứ tự §10.2; frontend mới chỉ phát hành sau máy API cuối cùng; PR có dòng `- Removed:` (§10.2, QĐ-25). Nguồn: SA (thay người dùng), 2026-10-23 |

### Câu hỏi cho PRD (gửi `product-manager`)

| # | Câu hỏi | Đề xuất trả lời | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|
| P1 | Người có quyền quản lý thành viên có được **thêm** vai trò cho một thành viên đang giữ vai trò mà mình không đủ quyền không? | — | — | Đã đóng: PRD-00 v1.3 câu 113 (BR-58 điểm 3, D14, AC-336), kèm D15 (BR-58 điểm 1: mọi thay đổi vai trò của Chủ sở hữu chỉ Chủ sở hữu làm, AC-340; lý do Chủ sở hữu thắng, AC-341) (§4.6, §5.3). Nguồn: Quyết định của người dùng (duyệt PRD-00 v1.3), 2026-10-23 |
| P2 | Gỡ chính mình bằng *Gỡ*? | — | — | Đã đóng: PRD-00 v1.3 câu 114 (BR-80, D16, AC-337, AC-339); API-08 trả 409 `CannotRemoveSelf` ở bước 7; dòng "Bạn" có *Đổi vai trò*, không *Gỡ* (§4.1, §4.6, §5.3, §6.4). Nguồn: Quyết định của người dùng (duyệt PRD-00 v1.3), 2026-10-23 |
| P3 | Mẫu email mời tạm trước PRD05? | — | — | Đã trả lời: mẫu tạm chỉ tiếng Việt, viết cứng như email thông báo hiện có; tên tổ chức, tên vai trò (vai trò hệ thống theo `vi`), hạn, link; không tên người mời; mã hoá HTML mọi giá trị; không đổi PRD; PRD05 quyết ngôn ngữ cho người nhận chưa có tài khoản (§6.2). Nguồn: SA (thay người dùng), 2026-10-23 |
| P5 | Quy tắc nghiệp vụ của đổi email ở hồ sơ và tài khoản chưa xác minh dùng lời mời | — | — | Đã đóng: PRD-00 v1.3 câu 117 (US-26, FR-67, BR-82, D17, AC-342–AC-350) và BR-51 (AC-351); hạn link 24 giờ, chỉ link mới nhất dùng được, API-20 báo `EmailNotConfirmed` ở bước kiểm tài khoản (§4.9, §6.3, §6.7). Nguồn: Quyết định của người dùng (duyệt PRD-00 v1.3), 2026-10-23 |
| P6 | Xác thực lại khi đổi email (N-new-2) | — | — | Đã trả lời: PRD-00 v1.4 câu 118, BR-82 điểm 7, D18, AC-352–AC-358 (bộ đếm riêng theo quy tắc đăng nhập, chỉ khoá việc đổi email; chỉ hỏi khi email đổi; sai thì cả lần lưu bị từ chối; để trống không tính; *Gửi lại*, *Huỷ* không hỏi). v1.4 chờ người dùng duyệt; TECH00 được duyệt sau đó (§6.7 bước 0, QĐ-35). Nguồn: Quyết định của người dùng, 2026-10-23 |
| P7 | Mục nào của module 00 vào nhật ký kiểm toán | — | — | Đã đóng: PRD-00 v1.4 BR-84 liệt kê các hành động tạo mục, khớp danh mục §3.7; admin hệ thống hiện "admin hệ thống FeedbackHub"; không mục khi tạo tổ chức, khi bị từ chối, khi lời mời bị thay; không ghi lý do vô hiệu; US-27, FR-68 lên Must; AC-366 (ngữ cảnh cá nhân bị từ chối) và AC-367. Nguồn: SA (thay người dùng), 2026-10-23 |
| P8 | Nhãn người làm khi admin hệ thống vô hiệu hoặc kích hoạt lại tổ chức | — | — | Đã trả lời: theo tư cách lúc thao tác, không theo nơi thao tác; là thành viên X thì ghi tên, không là thành viên thì ghi "admin hệ thống FeedbackHub"; chốt vào mục; dùng chung phép tra với thư báo vô hiệu (§3.1, §3.7, QĐ-36; PRD-00 v1.4 §3.1, BR-84 điểm 12, BR-53, AC-375 do `product-manager` thêm). Nguồn: SA (thay người dùng), 2026-10-23, độ tin vừa |
| P4 | Ghi cho PRD03 về BR-59 và snapshot nhật ký kiểm toán | — | — | Đã đóng: BR-59 đã đủ; TECH03 áp T10 (§7.2). Không cần dòng chuyển giao PRD. Nguồn: SA (thay người dùng), 2026-10-23 |

## 15. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| 2026-10-23 | 0.9 (sửa 3) | C3 thay theo quyết định mới: `ActorKind` chọn theo tư cách người làm lúc thao tác (admin hệ thống không là thành viên X mới ghi nhãn, kể cả từ API-25/26), chốt vào mục; một phép tra `SystemAdministratorActor.ResolveAsync` dùng chung cho nhật ký và `DeactivatedBySystemAdministrator` (§3.1, §3.7, §11, §12 QĐ-36, §13, §14 P8; AC-375). Không cần `appsec` xem lại. Giữ phiên bản 0.9 | SA (thay người dùng), 2026-10-23 |
| 2026-10-23 | 0.9 (sửa 2) | `appsec` đạt v0.9; áp C1–C3. C1: §6.8 bước 4 thêm quy tắc lấy `OrganizationId`; chọn lưới (a) `AuditEvent.For(entity)`, không constructor nhận `Guid`, kèm lý do không chọn (b) và test. C2: `RecordAsync` kiểm `ActorKind`/`ActorAccountId` nhất quán; API-33 bỏ qua dòng có `actor_kind` null hoặc lạ. C3: dẫn BR-84 điểm 12, BR-53. Gắn AC-367 (đã chốt), AC-368–AC-374 vào §3.7, §11, §13, header; "AC-359–AC-366" thành "AC-359–AC-374". Giữ phiên bản 0.9 | Review `appsec` v0.9 (lần 2); PRD-00 v1.4 |
| 2026-10-23 | 0.9 (sửa) | Áp review `appsec` v0.9. A1 (chặn): `AuditEvent` mang `OrganizationId` lấy từ đối tượng và `ActorAccountId` tường minh, `IAuditTrail` không đọc `ICurrentTenantProvider`, `OrganizationId` rỗng thì lỗi; API-25/26 không đổi gì thì không ghi. B1 `parameters` chỉ từ khoá khai báo, `action` lạ bị bỏ qua có log. B2 `AuditLogKind` không dùng 0, gán tường minh. B3 `SystemAdministrator` chỉ ở API-25/26. B4 mục `Event` không lưu IP, user agent; `actor_kind` thành cột riêng (M9). B5 cursor hỏng luôn 400, `limit` ngoài 1–100 trả 400, rủi ro khoảng cách `id` ở QĐ-36. B6 không `v-html`, mẫu lỗi không hiện `detail` hay đặt tên vào `document.title`, đổi tổ chức xoá danh sách. B7 khoá API nhận 403. Khớp PRD-00 v1.4 BR-84, AC-366, AC gộp (AC-367); đóng P7 (§3.7, §4.10, §6.8, §8.2, M9, §11, §12 QĐ-36, §13, §14). Giữ phiên bản 0.9, vẫn In review | Review `appsec` v0.9; SA (thay người dùng) |
| 2026-10-23 | 0.9 | Từ review thiết kế module 01 và PRD-00 v1.4 câu 119: màn hình nhật ký kiểm toán — `audit_logs.kind`, danh mục sự kiện đóng của module 00, API-33 keyset, bỏ `api/admin/audit-logs`, nghĩa vụ TECH01 (§2.1, §3.7, §4.7, §4.10, §5.1, §5.2, §6.8, §7.2 T10, §8.2, M9, §11, §12 QĐ-36, §13, §14 P7). Bảng hồ sơ thay hộp thoại hồ sơ (§6.7 bước 4, §8.2). Mẫu lỗi chung "không đủ quyền" / "không tìm thấy" trong khung có rail, không lộ tên, mã, đường dẫn đối tượng, dùng cho cả route 404 (§8.2, §11). Khoá công khai và đoạn nhúng thuộc TECH02. Vẫn In review | SA (thay người dùng), 2026-10-23; PRD-00 v1.4 |
| 2026-10-23 | 0.8 | Khớp PRD-00 v1.4 (câu 118, D18): mật khẩu để trống là lỗi kiểm hợp lệ, không tính lần sai; đạt giới hạn chỉ khoá việc đổi email; đổi sang địa chỉ khác khi đang chờ thì hỏi lại; *Gửi lại*, *Huỷ* không hỏi; thư báo cho địa chỉ cũ thuộc PRD05/TECH05 (§6.7 bước 0, §7.2 T20, §11, §12, QĐ-35). Điền AC-352–AC-358 vào §11, §13, §14 P6. Chuyển *In review*; appsec: đạt v0.5, v0.6–v0.8 áp các điểm không chặn và quyết định N-new-2. Duyệt sau PRD-00 v1.4 | Phiên chính; quyết định của người dùng |
| 2026-10-23 | 0.7 | Áp N-new-2: đổi email cần mật khẩu hiện tại với tài khoản có mật khẩu, mục đích khoá riêng `PasswordAttemptPurpose.EmailChange`, thất bại không lưu gì; tài khoản không mật khẩu không bị hỏi, rủi ro chấp nhận T20; báo cho địa chỉ cũ hoãn sang PRD05 (§4.6 `hasPassword`, §4.7, §4.9 API-29, §6.7 bước 0, §3.7, M7, §7.2 T20, §11, §12 QĐ-35, §13, §14 P6). Thêm tham chiếu PRD-00 v1.4 câu 118 (đang viết); TECH00 duyệt sau v1.4. Vẫn Draft | Quyết định của người dùng |
| 2026-10-23 | 0.6 | `appsec` đạt v0.5; áp các điểm không chặn. N-new-1: token xác minh có `Purpose` và ghi địa chỉ, `verify-email` từ chối địa chỉ khác `Account.Email`, `resend-verification` không gửi cho tài khoản không mật khẩu (M8 chỉ xác minh lại qua Microsoft), xác nhận đổi email tiêu thụ mọi token (§3.8, §4.9, §6.3, §6.6 bước 4, §6.7, M7, T18, QĐ-30). N-new-3: nghĩa vụ thêm ba endpoint vào danh sách `[AllowAnonymous]` kèm test (§10.2, §11). N-new-4: kiểm `Status` một chỗ trong `TokenIssuer` (§2.1, §6.6, QĐ-33). N-new-5: loại `PasswordHash` và cột khoá khỏi audit; mục audit không có tổ chức không endpoint nào trả, đã kiểm code (§3.7, T19, QĐ-34). N-new-6: truy vấn rà soát (2) thêm tài khoản có cả Microsoft và Google (§10.2). N-new-2: chỗ trống chờ người dùng (§12). Vẫn Draft | Review `appsec` v0.5 |
| 2026-10-23 | 0.5 | Khớp PRD-00 v1.3. Đóng P1, P2, P5 (§14). D15, BR-58 điểm 1: thay đổi vai trò của Chủ sở hữu chỉ Chủ sở hữu làm, mã `OwnerOnlyOperation`, lý do Chủ sở hữu thắng; thêm `lockRole`; dòng của chính mình `canChangeRoles = true`, `canRemove = false` (§4.1, §4.6, §5.3, §8.2). Câu 115: phần Sản phẩm của TECH01 dùng `DenyMode.Forbidden` (§5.1, QĐ-07). D17: link đổi email 24 giờ, tối đa một link dùng được, API-29 và API-30 tách bạch, một mã `EmailChangeLinkNoLongerValid` cho mọi link không còn hiệu lực, `EmailChangeNotPending` cho API-30 (§4.7, §4.9, §6.7, QĐ-29). BR-51, AC-351: API-20 thêm trạng thái `EmailNotConfirmed`, bước tài khoản so email trước rồi kiểm xác minh; `PendingEmail` không được tính (§4.6, §6.3, §8.2). M8 giữ như đề xuất (EF migration, SQL idempotent, ghi id). Truy vết AC-336–AC-351 (§11, §13); thêm FR-67. Vẫn Draft, chờ `appsec` kiểm lại | Quyết định của người dùng (duyệt PRD-00 v1.3); phiên chính |
| 2026-10-23 | 0.4 | Áp review `appsec` v0.3. B1: đổi email hồ sơ qua `PendingEmail` và token mang `Email`, API-29–API-32, một vị từ `Account.EmailConfirmed`, T15 (§2.1, §3.8 thay cột `external_logins.EmailVerified` đã bỏ, §4.9, §6.7, M7 mới, QĐ-29). B2: tắt ánh xạ claim, `ProviderUserId` = `sub`, đọc `tid`/`xms_edov`/`email` thô, bỏ `preferred_username` và `NameIdentifier`, "khớp theo `oid`" thành "khớp theo `sub`" (§6.6, T5, QĐ-12). N1: M8 đánh dấu chưa xác minh, xác minh lại, rà soát một lần (§6.6 bước 4, §10.1, §10.2 bước 6, QĐ-30). N2 issuer = `tid`; N3 kiểm `Status`; N4 `SmtpEmailSender`; N5 `ITenantAccessService` ba trạng thái, API-28; N6 BR-58 đọc cơ sở dữ liệu sau khoá; N7 `roleIds` ngoài tổ chức 404; N8 `CannotRemoveSelf` ở bước 7; N9 API-22 qua `TokenIssuer`, rủi ro link chuyển tiếp; N10 refresh `org_id = null`; N11 CLI; N12 proxy tin cậy, cookie `Secure`; N13 ngày quyết định bị thay 2026-07-25 (§4, §5, §6, §7.2 T6, T16, T17, §10.3, §11, §12, §13). API-06 dùng `DenyMode.NotFound`, quản lý sản phẩm TECH01 dùng `Forbidden` (§5.1, §5.2, QĐ-07). Không hotfix: B1, B2 là task đầu của kế hoạch module 00 (§10.2). Thêm P5. Vẫn Draft | Review `appsec`; SA (thay người dùng); quyết định của người dùng (không hotfix) |
| 2026-10-23 | 0.3 | Áp T2: độ tin cậy email Microsoft cho mọi đăng nhập Microsoft và lời mời (§2.1, §3.8 và M7 mới: `external_logins.EmailVerified` để áp quy tắc khi chấp nhận lời mời, §6.3, §6.6 mới, §7.2 T5, §10.2 bước 0, §11, QĐ-12, rủi ro còn lại). Áp T6: chỉ còn bố cục rail, bỏ năm chế độ menu và hai màu menu (§8.1, §11, QĐ-28). Hết câu hỏi kỹ thuật mở; còn chờ PRD-00 v1.3 (P1, P2) và review `appsec`. Vẫn Draft | Quyết định của người dùng |
| 2026-10-23 | 0.2 | Áp câu trả lời T1, T3, T4, P1–P4: bảng sản phẩm và hiện thực `IProductAccessScope` chuyển sang kế hoạch TECH01 kèm nghĩa vụ ở §5.5, bỏ khỏi M2; bốn điều kiện gửi email mời đồng bộ (§6.2, QĐ-10); quy tắc cứng frontend sau máy API cuối cùng và dòng `- Removed:` (§10.2, QĐ-25); đổi vai trò cần `CanTakeAway` mọi vai trò hiện có (§4.6, §5.3, chờ PRD-00 v1.3); `CannotRemoveSelf` (§4.1, §5.3, §6.4); mẫu email mời tạm tiếng Việt (§6.2); P4 đóng (§7.2). T2, T6 còn mở, chờ người dùng. Vẫn Draft | SA (thay người dùng) |
| 2026-10-23 | 0.1 | Bản nháp đầu từ PRD-00 v1.2, thiết kế module 00, system-context và code hiện có; giải quyết các ràng buộc *Quyền gán vai trò*, *Quyền sửa vai trò tự tạo*, *Xoá vai trò tự tạo* và phần TECH00 của *Quyền đọc và tải file* | — |

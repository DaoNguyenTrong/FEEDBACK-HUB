# TECH-00 — Nền tảng: đa tổ chức, thành viên, lời mời, vai trò, admin hệ thống, quyền theo sản phẩm, design system mới

| Mục | Giá trị |
|---|---|
| Mã | `TECH00` |
| PRD | `PRD00` v1.2 (Approved 2026-10-23), `docs/prd/00-tong-quan-va-nen-tang.md` |
| Thiết kế giao diện | https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn — hệ thiết kế: *SystemLight/Dark*, *ComponentsLight/Dark*; module 00: *AppShell*, *OrgContext*, *Members*, *InviteAccept*, *Roles*, *OrgSettings*, *SysOrgs* (sáng, tối; 1280, 834, 390 px) |
| Trạng thái | Draft |
| Phiên bản | 0.2 |
| Cần appsec review | Có (định danh, token lời mời, phiên, quyền, đường mở lời mời ẩn danh, admin hệ thống) |
| Người duyệt | Người dùng (sau review của `appsec`) |
| Cập nhật | 2026-10-23 |
| Phụ thuộc | Không có. TECH01–TECH07 phụ thuộc tài liệu này (cổng tổ chức §5.1, phạm vi theo sản phẩm §5.5, quy tắc file §7.5) |
| Kế hoạch triển khai | Chưa có |

## 1. Phạm vi và truy vết

Tài liệu này thiết kế phần PRD-00 thuộc nền tảng: tổ chức (tạo với định danh ngắn, đổi tên, vô hiệu, kích hoạt lại), ngữ cảnh tổ chức và trang chào, thành viên (danh sách, đổi vai trò, gỡ, tự rời), lời mời (gửi, danh sách, thu hồi, mời lại, xoá, mở, đăng ký và chấp nhận), vai trò tự tạo, chống leo thang quyền, admin hệ thống, mô hình quyền theo sản phẩm dùng chung cho các module sau, các yêu cầu chạy nhiều máy API, và việc đưa design system "Lớp kính" vào frontend.

**Bao phủ trực tiếp** (thiết kế và kiểm thử ở đây): FR-02, FR-03, FR-12, FR-21, FR-23, FR-24, FR-25, FR-29, FR-30 (phần chặn chuyển tổ chức và vô hiệu), FR-31, FR-37 (với dữ liệu quản trị tổ chức), FR-38, FR-42, FR-44, FR-45, FR-46, FR-52, FR-53, FR-54, FR-56 (quy tắc nền), FR-59 đến FR-66.

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
| Application | `RoleGrantPolicy` (mới) | Một chỗ duy nhất tính BR-58 điểm 1–3 (§5.3) |
| Application | `SystemOrganizationService` (mới) | Danh sách tổ chức, vô hiệu, kích hoạt lại cho admin hệ thống |
| Application | Cổng `IAuditTrail` (mới) | Ghi mục nhật ký kiểm toán ngữ nghĩa (thu hồi, xoá lời mời, rời tổ chức) |
| Application | Cổng `IOrganizationLock` (mới) | Khoá dòng tổ chức trong một giao dịch (§5.4) |
| Application | Cổng `IProductAccessScope` (hợp đồng ở đây, hiện thực ở kế hoạch TECH01) | Phạm vi theo sản phẩm cho TECH01–TECH06 (§5.5) |
| Application | Cổng `ISystemAdministratorResolver` (mới) | Tra cứu admin hệ thống có cache |
| Infrastructure | `OrganizationContextAuthorizationHandler` (thay hai handler `ActiveOrganization*` và hai handler theo route `{id}`) | Cổng tổ chức, thứ tự BR-77 bước 1–4 (§5.1) |
| Infrastructure | `SystemAdministratorAuthorizationHandler` | Policy `SystemAdministrator` |
| Infrastructure | `RedisFixedWindowRateLimiter` | Đếm giới hạn tần suất chung giữa các máy (§12 QĐ-02) |
| Infrastructure | `ProductionTopologyValidator` | Khởi động thất bại nếu cấu hình production không đủ cho nhiều máy (§10.3) |
| API | `CurrentOrganizationController` (`api/organization`) | Tổ chức đang làm việc, thành viên, lời mời, vai trò |
| API | `InvitationsController` (`api/invitations`) | Tra cứu, chấp nhận lời mời |
| API | `SystemOrganizationsController` (`api/system/organizations`) | Admin hệ thống |
| API | `AuthController` | Thêm `register-with-invitation`; `LoginResponse` thêm `isSystemAdministrator`; `external/{provider}` thêm `accountCreated` |
| API | `OrganizationsController` | Chỉ còn `GET` (tổ chức của tôi) và `POST` (tạo). Bỏ mọi route `api/organizations/{id}/...` |
| API | Lệnh CLI `grant-system-admin`, `revoke-system-admin` | Đường duy nhất cấp, bỏ admin hệ thống (BR-20) |
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
| `DeactivatedBySystemAdministrator` | bool | Có, mặc định `false` | Mới; PRD05 cần để ghi "admin hệ thống FeedbackHub" thay tên người |
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

Bảng `audit_logs` không đổi. Thêm cổng `IAuditTrail.RecordAsync(AuditRecord)` ghi một dòng ngữ nghĩa trong cùng `SaveChanges`:

| Sự kiện | `EntityName` | `Action` | `NewValues` (JSON) | Nguồn |
|---|---|---|---|---|
| Thu hồi lời mời đang chờ | `OrganizationInvitation` | `Revoked` | `{ email, roleId, roleName, roleSystemKind }` | BR-75, AC-272 |
| Xoá lời mời hết hạn | `OrganizationInvitation` | `Deleted` | như trên | BR-75, D5, AC-268 |
| Tự rời tổ chức | `OrganizationMember` | `Left` | `{ accountId, accountName, email }` | BR-80, AC-279 |

`UserId` là người làm, `OrganizationId` là tổ chức đang làm việc. Email trong các mục này là ngoại lệ D12: chỉ người có `auditlogs.view` của tổ chức đó đọc được (AC-335). Thay lời mời (mời lại, gửi lời mời mới cùng email) không có mục riêng (D4, AC-325). Không mục nào chứa mã hay `TokenHash`.

## 4. Hợp đồng API

Mọi route viết thường. Lỗi dùng `CodedProblemDetails` (`code` + `detail` theo ngôn ngữ request); frontend ánh xạ `code` sang `locales/{vi,en}.ts`. Thời gian trả UTC, chuyển theo `X-TimeZone` như mọi endpoint. Sau khi đổi, xuất lại `shared/openapi/openapi.json` và chạy `codegen`.

### 4.1 Tổ chức của tôi và tổ chức đang làm việc

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-01 | `GET api/organizations` | Tổ chức mình là thành viên, kể cả đang bị vô hiệu (bộ chọn tổ chức) | — | 200 `OrganizationDto[]` | 401 | `SignedInUser` |
| API-02 | `POST api/organizations` | Tạo tổ chức, người tạo là Chủ sở hữu | `{ name ≤200, slug }` | 201 `OrganizationDto` | 400 `FieldRequired`, `OrganizationSlugInvalid`; 409 `OrganizationSlugAlreadyExists` (không nêu tổ chức đang dùng) | `SignedInUser` (đăng nhập được nghĩa là email đã xác minh, BR-23) |
| API-03 | `GET api/organization` | Thông tin tổ chức đang làm việc | — | 200 `CurrentOrganizationDto { id, name, slug, status }` | 401, 403 | Cổng tổ chức |
| API-04 | `PATCH api/organization` | Đổi tên | `{ name ≤200 }` | 200 `CurrentOrganizationDto` | 400 `OrganizationNameRequired`; 401, 403 | `organizations.manage` |
| API-05 | `POST api/organization/deactivate` | Vô hiệu tổ chức đang làm việc | `{ reason?: string ≤500 }` | 204 | 400 `DeactivationReasonRequired` (người gọi là admin hệ thống, BR-54, AC-147), `FieldMaxLength`; 401, 403 | `organizations.manage` |
| API-06 | `GET api/organization/members` | Danh sách thành viên | `pageNumber`, `pageSize` (mặc định 50, tối đa 100) | 200 `PagedResult<OrganizationMemberDto>` | 401, 403 | Cổng tổ chức (mọi thành viên, BR-74) |
| API-07 | `PUT api/organization/members/{accountId}/roles` | Đổi vai trò tổ chức | `{ roleIds: uuid[] ≥1 }` | 204 | 400 `OrganizationMemberRequiresAtLeastOneRole`; 403 `OwnerOnlyOperation`, `InsufficientRolePermissions`; 404; 409 `OrganizationCannotRemoveLastOwner` | `organizations.members.manage` |
| API-08 | `DELETE api/organization/members/{accountId}` | Gỡ thành viên | — | 204 | 403 (như trên); 404; 409 `OrganizationCannotRemoveLastOwner`, `CannotRemoveSelf` (`accountId` là chính người gọi; §14 P2, chờ PRD-00 v1.3) | `organizations.members.manage` |
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
| API-21 | `POST api/invitations/accept` | Chấp nhận bằng tài khoản đang đăng nhập | `{ token }` | 200 `{ organizationId, organizationName }` | 409 `InvitationExpired`, `InvitationNoLongerValid`, `InvitationWrongAccount`, `InvitationOrganizationSuspended`; 401; 429 | `SignedInUser`; rate `invitation` |
| API-22 | `POST api/auth/register-with-invitation` | Đăng ký bằng email được mời; email coi là đã xác minh; đăng nhập luôn | `{ token, name, username, password, phone?, position?, address? }` (không có `email`: lấy từ lời mời) | 200 `LoginResponse` + `invitationOutcome: Joined \| OrganizationSuspended`; đặt cookie như `login` | 400; 409 `InvitationExpired`, `InvitationNoLongerValid`, `AccountUsernameAlreadyExists`, `AccountEmailAlreadyExists`; 429 | `[AllowAnonymous]`; rate `auth` |
| API-23 | `POST api/auth/external/{provider}` (sửa) | Không đổi hành vi; phản hồi thêm `accountCreated: bool` để trang lời mời tự chấp nhận cho tài khoản vừa tạo (AC-178) | như cũ | như cũ | như cũ | như cũ |

### 4.5 Admin hệ thống và phiên

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| API-24 | `GET api/system/organizations` | Mọi tổ chức: tên, số thành viên đang hoạt động, dấu đã vô hiệu; sắp theo tên không phân biệt hoa thường | `pageNumber`, `pageSize` (50/100) | 200 `PagedResult<SystemOrganizationDto { id, name, memberCount, isDeactivated }>` | 401, 403 | `SystemAdministrator` |
| API-25 | `POST api/system/organizations/{id}/deactivate` | Vô hiệu | `{ reason: string 1–500 }` | 204 (đã vô hiệu thì vẫn 204, không đổi gì) | 400 `DeactivationReasonRequired`; 404 | `SystemAdministrator` |
| API-26 | `POST api/system/organizations/{id}/reactivate` | Kích hoạt lại | — | 204 (đang hoạt động thì 204) | 404 | `SystemAdministrator` |
| API-27 | `LoginResponse` (login, refresh, verify-email, external, register-with-invitation, switch-organization) | Thêm `isSystemAdministrator: bool` — chỉ để hiện nhóm "Hệ thống" ở giao diện; máy chủ vẫn kiểm từng request | | | | |
| API-28 | `POST api/auth/switch-organization` (sửa) | Đích đang bị vô hiệu: 403 `OrganizationSuspended` (trước đây `OrganizationAccessDenied`), kèm `organizationName` | | | 403 | `[Authorize]` |

**Bỏ** (FR-59, BR-45, AC-300): `POST api/organizations/{id}/members` (thêm thẳng), `GET api/organizations/{id}/members`, `PATCH`/`DELETE api/organizations/{id}/members/{accountId}`, `GET`/`POST`/`PATCH`/`DELETE api/organizations/{id}/roles[...]`, `DELETE api/organizations/{id}`. Gọi lại các route này nhận 404 (route không tồn tại, người đã đăng nhập). Lý do và cách triển khai: §12 QĐ-06, §10.2.

### 4.6 Hình dạng DTO

- `OrganizationDto { id, name, slug, status, myRoleNames[] (giữ để tương thích), myRoles: RoleRefDto[], myPermissionCodes[] }`.
- `RoleRefDto { id, name, systemKind: "Owner" | "Admin" | "Member" | null }` — frontend hiện tên vai trò hệ thống theo ngôn ngữ giao diện từ `systemKind`, không từ `name`.
- `OrganizationMemberDto { accountId, accountName, email: string | null, roleIds[], roleNames[] (giữ để tương thích), roles: RoleRefDto[], isSelf, canChangeRoles, canRemove, lockReason: "OwnerOnly" | "InsufficientRolePermissions" | null }`. `email` là `null` với người gọi không có `organizations.members.manage`, trừ dòng của chính họ (BR-74, AC-261–263). Ba cờ `can*` luôn `false` với người không có quyền quản lý thành viên; với người có quyền, tính theo §5.3: `canChangeRoles` = `CanTakeAway` với mọi vai trò hiện tại của người đó (§14 P1), `canRemove` = cùng điều kiện và người đó không phải chính người gọi (§14 P2). Dòng của chính mình vẫn tính `canChangeRoles` theo cùng điều kiện; giao diện chỉ hiện *Rời tổ chức* trên dòng đó, theo thiết kế. Không đạt thì hàng khoá với `lockReason` (`OwnerOnly` khi người đó là Chủ sở hữu và người gọi không phải, ngược lại `InsufficientRolePermissions`), đúng bảng *Members*. Cờ chỉ để hiện; API-07, API-08 kiểm lại.
- `InvitationDto { id, email, role: RoleRefDto, expiresAt, status: "Pending" | "Expired", canRevokeOrDelete, canResend, lockReason }`.
- `RoleOptionDto { id, name, systemKind, canGrant }` — không có quyền nào (BR-77, AC-311).
- `RoleDto { id, name, systemKind, isSystem, permissionCodes[], holderCount, pendingInvitationCount: number | null, canEdit, canDelete, deleteBlockedReason: "SystemRole" | "InsufficientRolePermissions" | "HasHolders" | null }`. `holderCount` chỉ đếm thành viên đang hoạt động. `pendingInvitationCount` chỉ trả khi người gọi có thêm `organizations.members.manage` (BR-75: người khác không thấy lời mời nào), ngược lại `null`.
- `InvitationLookupDto { status, organizationName?, role?: RoleRefDto, expiresAt?, email? }`, `status` thuộc `Valid | Expired | NoLongerValid | WrongAccount | OrganizationSuspended`. Trường nào có theo từng trạng thái: §6.3. Phản hồi API-20 kèm `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`.

### 4.7 Mã lỗi mới (vi và en trong `Messages*.resx` và `locales/{vi,en}.ts`)

`OrganizationSuspended` (403, extension `organizationName`), `OwnerOnlyOperation` (403), `InsufficientRolePermissions` (403), `RoleHasHolders` (409), `RoleNameReservedForSystemRole` (409), `LastOwnerCannotLeave` (409), `CannotRemoveSelf` (409, §14 P2), `OrganizationSlugInvalid` (400), `OrganizationNameRequired` (400), `DeactivationReasonRequired` (400), `InvitationEmailAlreadyMember` (409), `InvitationEmailDeliveryFailed` (503), `InvitationExpired`, `InvitationNoLongerValid`, `InvitationWrongAccount`, `InvitationOrganizationSuspended` (409), `SystemAdministratorRequired` (403). `OrganizationAccessDenied` giữ cho "không đủ quyền" của cổng tổ chức.

### 4.8 Phân trang và giới hạn tần suất

- Danh sách dùng `PagedResult<T>` (frontend chuẩn hoá bằng `toPagedResult()`), mặc định 50, tối đa 100, "Xem thêm" ở giao diện. Thứ tự ổn định: tên theo collation ICU (`COLLATE "und-x-icu"`, không phân biệt hoa thường, đúng thứ tự có dấu), rồi `Id` (D9, AC-302). Lời mời: `ExpiresAt` giảm dần rồi `Id`.
- Chính sách giới hạn, đếm chung qua Redis (§12 QĐ-02), khoá theo IP thật (sau `UseForwardedHeaders`): `auth` (giữ cấu hình `AuthPermitLimit`, áp thêm cho API-22), `auth-refresh` (giữ), `invitation` mới cho API-20, API-21 (`RateLimiterSettings:InvitationPermitLimit`, mặc định 30 mỗi phút).

## 5. Phân quyền

### 5.1 Cổng tổ chức (thứ tự BR-77 bước 1–4)

Một requirement `OrganizationContextRequirement(PermissionCode?, DenyMode)` thay `ActiveOrganizationMembershipRequirement`, `ActiveOrganizationPermissionRequirement`, `OrganizationMembershipRequirement`, `OrganizationPermissionRequirement`. Policy `AuthorizationPolicies.ActiveOrganizationMember` và mọi mã trong `Permissions` (qua `OrganizationPermissionPolicyProvider`) đều dựng requirement này. Handler kiểm theo đúng thứ tự, dừng ở điều kiện đầu tiên không đạt:

1. Chưa đăng nhập → 401 (challenge mặc định).
2. JWT không có `org_id` (ngữ cảnh cá nhân), hoặc `org_id` trỏ tới tổ chức mà người gọi không còn là thành viên đang hoạt động (đã bị gỡ, đã rời) → `DenyMode.Forbidden`: 403 `OrganizationAccessDenied`; `DenyMode.NotFound`: 404 (BR-45).
3. Tổ chức đang bị vô hiệu → 403 `OrganizationSuspended` kèm tên tổ chức (BR-64, AC-301, AC-320, AC-321).
4. Có `PermissionCode` mà người gọi không có → 403 `OrganizationAccessDenied`, trước mọi truy vấn dữ liệu (AC-316).

Bước 5–7 nằm trong service: (5) đối tượng tra theo `id` **và** `OrganizationId = org_id` của JWT, không thấy → 404 giống hệt khi không tồn tại (AC-310, AC-317, AC-318); (6) BR-58 → 403; (7) điều kiện còn lại → 409. Thứ tự (6) trước (7) là D2 (AC-257).

- Thành viên, trạng thái tổ chức và quyền đọc qua `ITenantAccessService` và `IPermissionResolver` có sẵn (cache Redis TTL 60 giây, xoá ngay khi đổi), nên mọi đổi quyền có hiệu lực ở request tiếp theo trên mọi máy (BR-67, AC-190).
- `AuthorizationResultHandler` đọc `AuthorizationFailureReason` của handler để ghi đúng mã (`OrganizationSuspended`, `OrganizationAccessDenied`, `SystemAdministratorRequired`) thay vì luôn `OrganizationAccessDenied`.
- `DenyMode.Forbidden` dùng cho mọi endpoint quản trị của tài liệu này. `DenyMode.NotFound` dành cho endpoint đọc dữ liệu bên trong tổ chức (sản phẩm, feedback) của TECH01, TECH03, TECH06, để người đã rời hay bị gỡ thấy "không tìm thấy" (AC-70, AC-280).
- `TenantAccessMiddleware` giữ làm lưới cuối cho endpoint chỉ có `[Authorize]` mà vẫn mang `org_id` (thông báo, push), nhưng ghi `CodedProblemDetails` (`OrganizationSuspended` hoặc `OrganizationAccessDenied`) thay vì `ProblemDetails` không mã. Danh sách miễn trừ: `/api/auth`, `/api/organizations` (tổ chức của tôi, tạo), `/api/permissions`, `/api/invitations`, `/api/system`.
- Mở và chấp nhận lời mời không qua cổng này (BR-77, BR-81); thứ tự riêng ở §6.3.

### 5.2 Bảng quyền theo endpoint

| Endpoint | Ai được gọi | Phạm vi máy chủ | Từ chối |
|---|---|---|---|
| API-01 | Mọi người đã đăng nhập (không nhận khoá API) | `organization_members.AccountId = sub`, `IsActive` | 401 |
| API-02 | Như trên | — | 401 |
| API-03, API-06, API-09 | Thành viên đang hoạt động của tổ chức đang làm việc | `org_id` của JWT | Cổng §5.1 |
| API-04, API-05 | Có `organizations.manage` | `org_id` | Cổng §5.1; API-05 kiểm thêm admin hệ thống phải có lý do |
| API-07, API-08, API-10–API-14 | Có `organizations.members.manage` | `org_id`; đối tượng ngoài tổ chức → 404 | Cổng; 404; BR-58 403 |
| API-15–API-18 | Có `organizations.roles.manage` | `org_id` | Cổng; 404; BR-58 403 |
| API-20 | Bất kỳ ai có mã | Theo `TokenHash` | Không có 401/403; trạng thái trả trong thân (§6.3) |
| API-21 | Người đã đăng nhập bằng JWT người dùng | `TokenHash`; email tài khoản phải bằng email lời mời | 401; 409 theo §6.3 |
| API-22 | Ẩn danh có mã | `TokenHash`; email lấy từ lời mời | 409 |
| API-24–API-26 | Admin hệ thống (JWT người dùng, không nhận khoá API, không phụ thuộc `org_id`) | Không đọc gì bên trong tổ chức ngoài tên, số thành viên, trạng thái (BR-21) | 403 `SystemAdministratorRequired` (AC-126, AC-131) |
| Lệnh CLI `grant-system-admin`, `revoke-system-admin` | Người vận hành máy chủ | — | Không có endpoint HTTP nào ghi `system_administrators` (AC-51, AC-52) |

Khoá API (`X-Api-Key`) không có `sub` người dùng: mọi endpoint của §4.1–§4.5 dùng cổng tổ chức hoặc `SignedInUser` nên khoá API nhận 403 hoặc 401, không gọi được thao tác thành viên, lời mời, vai trò.

### 5.3 Chống leo thang quyền (`RoleGrantPolicy`, BR-58)

Đầu vào: tập quyền hiệu lực `P(caller)` (Owner = `Permissions.All`), `isOwner(caller)`; với mỗi vai trò `r`: `P(r)` (Owner = toàn bộ danh mục).

- `CanGrant(r)` = `r` là Owner ? `isOwner(caller)` : `P(r) ⊆ P(caller)` (điểm 1, 2).
- `CanTakeAway(r)` = cùng công thức (điểm 3: phải có mọi quyền của vai trò bị lấy đi hoặc làm mất hiệu lực).

| Thao tác | Điều kiện BR-58 (bước 6) | Điều kiện khác (bước 7) |
|---|---|---|
| Đổi vai trò (API-07), cả của chính mình | `CanTakeAway` với **mọi** vai trò người đó đang giữ, kể cả khi chỉ thêm vai trò (cùng điều kiện như gỡ khỏi tổ chức), và `CanGrant` với mỗi vai trò thêm (AC-154, AC-155, AC-157, AC-193, AC-197; §14 P1, chờ PRD-00 v1.3) | Còn ít nhất một Chủ sở hữu đang hoạt động; ít nhất một vai trò |
| Gỡ thành viên (API-08) | `CanTakeAway` mọi vai trò tổ chức của người bị gỡ; vai trò theo sản phẩm không tính (AC-158, AC-258, AC-259) | `accountId` là chính người gọi → 409 `CannotRemoveSelf` (rời chỉ qua API-09; §14 P2); không gỡ Chủ sở hữu cuối cùng |
| Gửi lời mời (API-11) | `CanGrant(vai trò mới)`; nếu email đã có lời mời (chờ hoặc hết hạn): thêm `CanTakeAway(vai trò lời mời cũ)` (BR-63 điểm 3, D4, AC-153, AC-194, AC-254, AC-271) | Email đã là thành viên đang hoạt động → 409 (AC-180) |
| Mời lại (API-12) | `CanGrant(vai trò)` (cùng vai trò nên bằng `CanTakeAway`) | — |
| Thu hồi, xoá lời mời (API-13) | `CanTakeAway(vai trò lời mời)` (AC-253, AC-255, AC-269, AC-270) | — |
| Tạo vai trò (API-16) | Mọi quyền chọn `∈ P(caller)` (AC-156) | Tên theo BR-79 |
| Sửa vai trò, kể cả chỉ đổi tên (API-17) | `CanTakeAway(vai trò hiện tại)` và mọi quyền mới `∈ P(caller)` (AC-198, AC-295–297) | Tên theo BR-79 |
| Xoá vai trò (API-18) | `CanTakeAway(vai trò)` (AC-249, AC-257) | Không còn người giữ đang hoạt động → nếu còn: 409 `RoleHasHolders` (AC-248, AC-251, AC-256) |
| Rời tổ chức (API-09) | Không áp dụng (BR-58: người rời chỉ bỏ quyền của mình) | Không phải Chủ sở hữu cuối cùng (AC-277) |

- Vai trò hệ thống: API-17, API-18 trên vai trò có `SystemRoleKind` trả 403 `OrganizationSystemRoleImmutable` **trước** BR-58, vì AC-55 yêu cầu báo đúng lý do "vai trò hệ thống không sửa được" (§12 QĐ-27).
- Tên vai trò (BR-79, D8): sau trim và NFC, không trống, ≤100 ký tự; không trùng không phân biệt hoa thường với vai trò khác của tổ chức (chỉ mục §3.3 bảo đảm cả khi hai request đồng thời; vi phạm chỉ mục ánh xạ về 409 `OrganizationRoleNameAlreadyExists`); không trùng `Owner`, `Admin`, `Member`, `Chủ sở hữu` không phân biệt hoa thường → 409 `RoleNameReservedForSystemRole`. Danh sách tên hệ thống vi/en đặt một chỗ trong Application (`SystemRoleDisplayNames`) và có test so với `locales/{vi,en}.ts`.
- Cờ trên DTO (`canGrant`, `canChangeRoles`, `canRemove`, `canRevokeOrDelete`, `canEdit`, `canDelete`) gọi cùng `RoleGrantPolicy`, nên giao diện và máy chủ không lệch nhau.

### 5.4 Đồng thời

Mọi thao tác đổi tập thành viên, vai trò của thành viên, vai trò, lời mời chạy trong `ExecuteInTransactionAsync` và bắt đầu bằng `IOrganizationLock.AcquireAsync(orgId)` (`SELECT 1 FROM organizations WHERE "Id" = @id FOR UPDATE`). Nhờ vậy hai Chủ sở hữu không gỡ nhau cùng lúc thành tổ chức không còn Chủ sở hữu, xoá vai trò không chạy song song với việc gán vai trò đó hay chấp nhận lời mời có vai trò đó, và hai lời mời cùng email không cùng được tạo (chỉ mục unique là lưới thứ hai). Giữ khoá trong lúc gửi email mời (§6.2) chấp nhận được ở quy mô ≤20 tổ chức.

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
  1. Timeout SMTP tối đa 15 giây (`EmailSettings:SendTimeoutSeconds`, mặc định 15), ngắn hơn timeout của nginx và bộ cân bằng tải, vì khoá dòng tổ chức (§5.4) được giữ trong lúc gửi.
  2. Frontend khoá nút trong lúc "Đang gửi…" và không bao giờ tự thử lại khi gặp 503 hay hết thời gian chờ. Người dùng thử lại bằng tay thì lời mời được thay (BR-63 điểm 3), nên không cần khoá idempotency.
  3. Lỗi SMTP ghi log chỉ kèm `invitationId`: không email, không mã.
  4. Rủi ro còn lại: SMTP đã nhận thư nhưng commit lỗi, người nhận có link chết (báo "không còn hiệu lực"). Hiếm, chấp nhận.
- API-12: như trên với vai trò của lời mời cũ. API-13: khoá tổ chức → tìm lời mời (404) → `CanTakeAway` (403) → `IAuditTrail` (`Revoked` nếu còn hạn, `Deleted` nếu hết hạn) → xoá dòng → commit.

### 6.3 Mở lời mời (API-20) và chấp nhận (API-21, API-22)

Thứ tự kiểm BR-81 điểm 3, dừng ở điều kiện đầu tiên không đạt:

| Bước | Điều kiện | `InvitationLookupDto` (API-20) | API-21 | API-22 |
|---|---|---|---|---|
| 1 | Không có dòng với `TokenHash` (không tồn tại, đã dùng, đã thu hồi, đã bị thay, vai trò bị xoá) | `NoLongerValid`, không trường nào khác (D10) | 409 `InvitationNoLongerValid` | 409, không tạo tài khoản |
| 1 | `ExpiresAt ≤ now` | `Expired`, kèm `expiresAt` (không tên tổ chức, vai trò, email) | 409 `InvitationExpired` | 409, không tạo tài khoản |
| 2 | Có phiên và email tài khoản ≠ email lời mời | `WrongAccount`, không trường nào khác (BR-81 điểm 4, AC-306, AC-308) | 409 `InvitationWrongAccount` | Không thể xảy ra (email lấy từ lời mời) |
| 3 | Có phiên đúng tài khoản, tổ chức bị vô hiệu | `OrganizationSuspended`, kèm `organizationName`, `expiresAt` | 409 `InvitationOrganizationSuspended`, lời mời giữ nguyên (AC-151) | Tạo tài khoản đã xác minh, không vào tổ chức, `invitationOutcome = OrganizationSuspended`, lời mời giữ nguyên (AC-333) |
| — | Không có phiên, lời mời còn hiệu lực (kể cả tổ chức bị vô hiệu) | `Valid` với đúng bốn điều: `organizationName`, `role`, `expiresAt`, `email` (BR-81 điểm 5, 6; AC-309, AC-332) | — | — |
| — | Có phiên đúng tài khoản, tổ chức hoạt động | `Valid` với bốn điều | Vào tổ chức: kích hoạt lại dòng thành viên cũ hoặc tạo mới, gán vai trò của lời mời, xoá lời mời, xoá cache quyền; 200 | Tạo tài khoản đã xác minh (`ConfirmEmail`), vào tổ chức như API-21, đăng nhập; `Joined` (AC-57, AC-182) |

- API-20 không tiêu thụ lời mời, nên trình quét link trong email không làm hỏng nó. Tra cứu luôn qua `POST` thân JSON: mã không nằm trong URL nên không vào log truy cập của nginx hay log request của API.
- Đăng nhập Google/Microsoft từ trang lời mời dùng `POST api/auth/external/{provider}` có sẵn (không truyền mã), rồi gọi API-21. Tài khoản Google/Microsoft khác email lời mời bị API-21 từ chối `InvitationWrongAccount`; trang không nhắc lại tên tổ chức (AC-305). Tài khoản vừa tạo (`accountCreated = true`) được trang tự chấp nhận (AC-178); tài khoản có sẵn thấy nút *Chấp nhận lời mời* (AC-177). Độ tin cậy email của Microsoft: §7.2 mối đe doạ T5, §14 T2.
- Đã là thành viên đang hoạt động khi chấp nhận (chỉ xảy ra khi hai tab cùng chấp nhận): xoá lời mời và trả 200, không đổi vai trò.
- Người đã có tài khoản chưa xác minh không đăng ký lại được (`AccountEmailAlreadyExists`); họ xác minh email theo luồng có sẵn rồi chấp nhận.

### 6.4 Đổi vai trò, gỡ, rời

- API-07, API-08: cổng → khoá tổ chức → tìm thành viên đang hoạt động trong `org_id` (404) → BR-58 (403) → Chủ sở hữu cuối cùng (409) → ghi → xoá cache `ITenantAccessService`, `IPermissionResolver`, `IProductAccessScope` của người bị đổi.
- Gỡ và rời: `IsActive = false`, xoá `organization_member_roles` của họ (BR-30, AC-69, AC-259, AC-276); phần `product_members` do TECH01 thêm (§5.5).
- API-08 với `accountId` là chính người gọi: 409 `CannotRemoveSelf`, kiểm sau bước 5 và trước BR-58; tự rời chỉ qua API-09 (§14 P2). Feedback họ đã gửi không đổi (TECH03).
- API-09 thêm mục nhật ký `Left` (AC-279). Frontend sau 204 gọi `switch-organization` với `null` rồi tải lại về trang chào (BR-80, AC-276). Nếu bước đó lỗi, request kế tiếp mang `org_id` cũ nhận `OrganizationAccessDenied` và frontend xử lý như §6.5.

### 6.5 Tổ chức bị vô hiệu trong lúc làm việc

1. API-05 hoặc API-25 đặt `Status = false` và trường vô hiệu, xoá cache `tenant-access:{org}` và `permissions:{org}` (Redis, mọi máy thấy ngay).
2. Request kế tiếp của thành viên mang `org_id` đó nhận 403 `OrganizationSuspended` + `organizationName`; thao tác không có tác dụng (AC-183, AC-320).
3. Frontend (interceptor trong `api/client.ts` báo sự kiện; store `organization-context` xử lý) ghi thông báo vào `sessionStorage`, gọi `switch-organization(null)`, tải lại về trang chào; trang chào hiện "<tên> vừa tạm ngừng hoạt động. Việc bạn vừa làm chưa được thực hiện" và bộ chọn tổ chức (AC-199, AC-200, AC-301). `OrganizationAccessDenied` từ cổng (thành viên đã bị gỡ) cũng đưa về ngữ cảnh cá nhân, không kèm thông báo tạm ngừng.
4. Bộ chọn tổ chức hiện tổ chức bị vô hiệu làm mờ, không chọn được (API-01 trả `status`, AC-73); gọi thẳng API-28 vào tổ chức đó bị 403 (FR-30).
5. API-26 kích hoạt lại: `Status = true`, xoá trường vô hiệu, xoá cache; thành viên, vai trò, vai trò theo sản phẩm, lời mời còn hạn còn nguyên (BR-49, BR-55, AC-106, AC-128, AC-152, AC-334). Hạn lời mời không được cộng thêm (AC-144).

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
| T5 | Kẻ tấn công dựng tenant Microsoft Entra đặt email trùng email nạn nhân ("nOAuth") để chấp nhận lời mời hoặc khớp tài khoản sẵn có theo email | Với Microsoft chỉ tin email khi tài khoản thuộc tenant cá nhân (MSA) hoặc token có claim `xms_edov = true` (miền email đã được tenant xác minh); không đạt thì coi email chưa xác minh: không khớp tài khoản có sẵn, không khớp lời mời. Ảnh hưởng tới luồng đăng nhập Microsoft đang có: §14 T2, §12 QĐ-12 |
| T6 | Thành viên đã bị gỡ hoặc đã rời dùng phiên cũ (JWT còn `org_id`) | Cổng §5.1 kiểm thành viên đang hoạt động mỗi request (cache bị xoá ngay khi gỡ); dữ liệu bên trong trả 404 |
| T7 | Admin hệ thống tìm đường vào dữ liệu của tổ chức | Policy `SystemAdministrator` chỉ mở API-24–API-26, chỉ trả tên, số thành viên, trạng thái; không route nào nhận `organizationId` khác `org_id` để đọc dữ liệu bên trong; vào tổ chức chỉ qua lời mời (BR-53, AC-135) |
| T8 | Cấp quyền admin hệ thống từ bên trong Hub | Không endpoint HTTP nào ghi `system_administrators`; chỉ lệnh CLI chạy trong máy chủ; test API khẳng định không có route nào như vậy; policy không nhận khoá API |
| T9 | Người thường đọc email đồng nghiệp hoặc lời mời bằng gọi thẳng | `email = null` trong API-06 cho người không có quyền quản lý thành viên; API-10 cần quyền đó; `pendingInvitationCount = null` cho người chỉ có quyền vai trò (BR-74, BR-75) |
| T10 | Người xem nhật ký kiểm toán đọc nội dung feedback qua snapshot | Quy tắc cho TECH02/TECH03: entity chứa nội dung feedback, bình luận, ảnh, ngữ cảnh kỹ thuật phải nằm trong `AuditExcludedEntityTypes` hoặc chỉ ghi mục ngữ nghĩa không có nội dung (BR-59, AC-161). BR-59 đã đủ; TECH03 áp T10 (§14 P4) |
| T11 | Ảnh feedback đọc được qua quyền quản lý file hoặc qua URL công khai | Quy tắc §7.5 |
| T12 | CSRF trên chấp nhận lời mời, rời tổ chức, vô hiệu | Middleware CSRF có sẵn (header bắt buộc với cookie); API-21 còn cần mã |
| T13 | Liệt kê định danh ngắn hay email qua thông báo lỗi | Thông báo trùng định danh không nêu tổ chức (BR-78); `InvitationEmailAlreadyMember` chỉ người có quyền quản lý thành viên của chính tổ chức đó thấy |
| T14 | Cửa sổ cập nhật cuốn chiếu: máy cũ còn endpoint thêm thẳng và gán vai trò không giới hạn | Triển khai API trước, cuốn chiếu nhanh, ghi rõ cửa sổ vài phút được chấp nhận (§10.2) |

### 7.3 Không được ghi log

Mã lời mời (thô hoặc hash), mật khẩu, JWT, refresh token, thân request của `api/invitations/*` và `api/auth/*`. Email chỉ được ghi trong mục nhật ký kiểm toán của §3.7, không trong log ứng dụng (log dùng `accountId`, `invitationId`).

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
- Khung cũ `AppSidebar`/`AppTopMenu`/chế độ menu: §14 T6.

### 8.2 Màn hình và API

| Màn hình (bảng) | Route (hash) | API | Trạng thái phía khách |
|---|---|---|---|
| Khung ứng dụng (*AppShell*) | mọi route có tổ chức | API-01 (bộ chọn), API-02 (tạo), API-27 (nhóm "Hệ thống") | Store `organizations` (của tôi), `auth` (`organizationId`, `permissions`, `isSystemAdministrator`). Nhóm "Tổ chức": *Thành viên* (mọi thành viên), *Quản lý tổ chức* (`organizations.manage`); nhóm "Hệ thống": *Các tổ chức* khi `isSystemAdministrator` |
| Trang chào (*OrgContext*) | `/welcome` (trang chủ khi không có tổ chức) | API-01, API-02, API-28 | Thông báo tạm ngừng từ `sessionStorage` (§6.5) |
| Không tìm thấy (*OrgContext*) | `not-found` trong khung | — | Giữ `NotFoundView`, đổi theo thiết kế |
| Thành viên (*Members*) | `/organization/members` | API-06, API-07, API-08, API-09, API-14 | Tab *Thành viên* (mọi thành viên), *Lời mời* (`organizations.members.manage`), *Vai trò* (`organizations.roles.manage`) — tab ẩn hẳn khi thiếu quyền (BR-77, AC-298, AC-299, AC-314). Hàng khoá theo `canChangeRoles`, `canRemove`, `lockReason`; email chỉ hiện khi khác `null`; dòng "Bạn" có *Rời tổ chức* với khối xác nhận dưới dòng (D6) |
| Lời mời (*Members*) | `/organization/members/invitations` | API-10–API-14 | "Đang gửi…" trong lúc chờ API-11/API-12, nút bị khoá, không tự thử lại khi 503 hay hết thời gian chờ (§6.2); nhãn *Hết hạn* với *Mời lại*, *Xoá*; trạng thái trống (AC-274) |
| Vai trò (*Roles*) | `/organization/members/roles` | API-15–API-19 | Quyền không có thì ô tick khoá ("Chỉ chọn được quyền mà bạn có"); câu mô tả quyền lấy từ `locales/{vi,en}.ts` `permissions.<code>.description` (FR-65); vai trò hệ thống, vai trò thiếu quyền: chỉ xem; xác nhận xoá nêu số lời mời đang chờ khi có |
| Quản lý tổ chức (*OrgSettings*) | `/organization/settings` (`meta.permission = organizations.manage`) | API-03, API-04, API-05 | Lý do bắt buộc khi `isSystemAdministrator` (BR-54), đếm 0/500 |
| Nhận lời mời (*InviteAccept*) | `/invite` (không `requiresAuth`, không `guestOnly`) | API-20, API-21, API-22, API-23 | Các trạng thái của §6.3; form đăng ký với email chỉ đọc (AC-304); "Đăng xuất để dùng tài khoản khác" khi `WrongAccount` |
| Các tổ chức (*SysOrgs*) | `/system/organizations` (`meta.systemAdministrator`) | API-24, API-25, API-26 | "Xem thêm"; xác nhận kích hoạt lại trong dòng; hộp thoại vô hiệu với lý do bắt buộc |

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

### 10.2 Thứ tự triển khai

1. Chạy lệnh `migrate` một lần (service hoặc job riêng) trước khi cập nhật máy API; `DatabaseSettings:MigrateOnStartup = false` trên production nhiều máy (§12 QĐ-03).
2. Cập nhật lần lượt từng máy API (route mới có, route `{id}` cũ và thêm thẳng không còn).
3. **Quy tắc cứng: frontend mới chỉ được phát hành sau khi máy API cuối cùng đã cập nhật.** Trong vài phút giữa bước 2 và 3, tab đang mở của bản frontend cũ gặp 404 ở màn hình tổ chức cũ cho đến khi tải lại (chấp nhận theo system-context: "mất vài phút khi cập nhật là chấp nhận được").
   Việc bỏ route `{id}` cũ ngay trong bản phát hành này đã chốt (§14 T4); PR triển khai ghi một dòng `- Removed:` trong `## Changelog`. Tab frontend cũ gặp 404 cho đến khi tải lại; điều này, cùng cửa sổ T14, nằm trong mức "mất vài phút khi cập nhật" của system-context.
4. Người vận hành chạy `grant-system-admin --email <email>` cho admin hệ thống đầu tiên.
5. Quay lại: image cũ cộng bản sao lưu (system-context). M1–M5 không phá code cũ nên image cũ chạy được trên schema mới, trừ chỉ mục M3 chặt hơn.

Không dùng cờ tính năng: toàn bộ module 00 bật cùng một bản phát hành, vì nửa vời (ví dụ còn thêm thẳng mà đã có giới hạn BR-58) để lại lỗ hổng.

### 10.3 Sẵn sàng nhiều máy API (bắt buộc)

`ProductionTopologyValidator` chạy lúc khởi động khi `ASPNETCORE_ENVIRONMENT = Production` (không chạy khi xuất OpenAPI hay test) và dừng máy nếu:

- `CacheSettings:Provider ≠ Redis`, `RealtimeSettings:Backplane ≠ Redis`, hoặc thiếu `ConnectionStrings:Redis`;
- `StorageSettings:Provider = Local`;
- `RateLimiterSettings:Store ≠ Redis`;
- `DatabaseSettings:MigrateOnStartup = true`.

Ngoài ra: Data Protection đặt `SetApplicationName("FeedbackHub")` cố định để mọi máy giải được cookie và dữ liệu bảo vệ của nhau (khoá đã lưu trong PostgreSQL); client SignalR dùng `transport: WebSockets`, `skipNegotiation: true` nên bộ cân bằng tải không cần sticky session; API không giữ trạng thái nào trong bộ nhớ ngoài cache có TTL; Hangfire dùng PostgreSQL (đã có), job và seeder idempotent (quy tắc hiện có). Môi trường dev và test vẫn được dùng `Memory`, `None`, `Local`.

## 11. Chiến lược kiểm thử

| Nhóm | Tầng | AC |
|---|---|---|
| Định danh ngắn, tên tổ chức | Domain (`Organization.Create`, `Update`); frontend unit `slug-suggestion` | AC-281, AC-283, AC-284, AC-285, AC-187, AC-328, AC-329, AC-331 |
| Tạo tổ chức, trùng định danh, chuyển vào | API; frontend view | AC-56, AC-282, AC-286, AC-330 |
| Tên vai trò | Domain (`Role`, NFC); Application; Infrastructure (chỉ mục lower, migration M3 trên dữ liệu trùng) | AC-287–AC-293, AC-326 |
| `RoleGrantPolicy` | Application: mọi nhánh cho phép và từ chối, Owner, tự đổi vai trò mình | AC-153–AC-158, AC-193, AC-194, AC-197, AC-198, AC-249, AC-253–AC-255, AC-257–AC-259, AC-269–AC-271, AC-295–AC-297, AC-311 |
| Đổi vai trò, gỡ, Owner cuối cùng, gỡ chính mình, đồng thời | Application; Infrastructure (khoá `FOR UPDATE` với hai giao dịch song song); API | AC-69 (phần thành viên), AC-125, AC-157, AC-264, AC-277, AC-278; AC mới của PRD-00 v1.3 cho P1, P2 |
| Xoá vai trò, người giữ, lời mời theo vai trò | Application; Infrastructure (cascade lời mời); API | AC-55, AC-159, AC-247–AC-252, AC-256, AC-257 |
| Danh sách thành viên, email | API (gọi thẳng với ba loại người gọi) | AC-261–AC-265, AC-335 |
| Lời mời: gửi, thay, mời lại, thu hồi, xoá, danh sách, nhật ký | Application (email lỗi hoặc hết 15 giây → rollback, `DidNotReceive`; log không có email, mã); frontend (nút khoá khi đang gửi, không tự thử lại); API; Infrastructure (unique, mục audit không có `TokenHash`) | AC-60, AC-115–AC-117, AC-179, AC-180, AC-196, AC-266–AC-275, AC-310, AC-312, AC-316, AC-324, AC-325 |
| Mở và chấp nhận lời mời, thứ tự BR-81 | Application (bảng §6.3 thành `[Theory]`); API (ẩn danh, đúng tài khoản, sai tài khoản; header `no-store`, `no-referrer`, `noindex`; 429); frontend view `/invite`; e2e cục bộ cho đăng ký qua lời mời | AC-57–AC-59, AC-118, AC-119, AC-144, AC-151, AC-152, AC-177, AC-178, AC-181, AC-182, AC-303–AC-309, AC-332–AC-334 |
| Thêm thẳng đã bỏ | API: route cũ trả 404; không route nào tạo `OrganizationMember` ngoài tạo tổ chức và chấp nhận | AC-135, AC-260 |
| Cổng tổ chức, thứ tự BR-77 | Infrastructure (handler: mọi lý do từ chối); API (ngữ cảnh cá nhân, tạm ngừng trước thiếu quyền, đối tượng tổ chức khác 404) | AC-107, AC-186, AC-275, AC-298–AC-300, AC-313, AC-316–AC-321 |
| Phần ẩn theo quyền | Frontend (tab, menu, route guard) | AC-298, AC-299, AC-314, AC-315 |
| Vô hiệu, kích hoạt lại, tạm ngừng giữa chừng | Application; API; frontend (interceptor, trang chào với thông báo) | AC-73, AC-105, AC-106, AC-128, AC-131, AC-138, AC-139, AC-141, AC-146–AC-148, AC-183, AC-184, AC-199–AC-201, AC-301 |
| Đổi tên tổ chức | API; frontend | AC-185–AC-187, AC-225, AC-226 |
| Admin hệ thống | API (policy, không nhận khoá API, không route cấp quyền); CLI command test; Infrastructure (sắp xếp ICU) | AC-51, AC-52, AC-122, AC-124, AC-126, AC-127, AC-302 |
| Rời tổ chức | Application; API; frontend (khối xác nhận) | AC-276–AC-280, AC-321–AC-323 |
| Bộ chọn tổ chức, trang chào | Frontend | AC-04, AC-05, AC-74, AC-201 |
| Hiệu lực ngay | Infrastructure (xoá cache Redis thấy ở instance khác: hai `RedisCacheService` cùng một Redis Testcontainers) | AC-12, AC-190 (phần cache) |
| Câu mô tả quyền | Frontend unit | AC-294, AC-327 |
| Giới hạn tần suất chung | Infrastructure (Redis Testcontainers: hai limiter cùng khoá dùng chung đếm; Redis lỗi thì về limiter cục bộ) | — (QĐ-02) |
| Kiểm cấu hình production | API host test với môi trường Production | — (§10.3) |
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
| QĐ-07 | Một requirement cổng tổ chức kiểm bước 1–4 của BR-77 theo thứ tự, hai chế độ từ chối (`Forbidden` cho quản trị, `NotFound` cho dữ liệu); lý do từ chối mang mã riêng | "Tạm ngừng" phải báo trước "thiếu quyền"; middleware chạy sau policy nên không làm được thứ tự đó | Giữ `TenantAccessMiddleware` làm cổng chính: chạy sau policy quyền, sai thứ tự | Nền tảng |
| QĐ-08 | BR-58 tính ở một `RoleGrantPolicy` trên quyền hiệu lực; mọi thao tác đổi thành viên, vai trò, lời mời khoá dòng tổ chức (`FOR UPDATE`) trong giao dịch | Một chỗ cho máy chủ và cờ giao diện; chặn hai Owner gỡ nhau cùng lúc | Kiểm rải rác từng service: dễ lệch; khoá tư vấn Postgres theo hash: khó đọc hơn, cùng tác dụng | Module |
| QĐ-09 | Mã lời mời 256 bit, chỉ lưu SHA-256, một dòng mỗi (tổ chức, email), xoá hẳn khi dùng, thu hồi, thay, xoá; mã trong fragment URL, tra bằng POST | Mã sai và mã đã thu hồi không phân biệt được; không lưu dấu vết mã đã chết; mã không vào log | Lưu trạng thái `Revoked`/`Accepted`: giữ hash vô ích và thêm trạng thái phải lọc ở mọi truy vấn; mã trong query string: vào log nginx | Module |
| QĐ-10 | Gửi email mời đồng bộ trong giao dịch, lỗi SMTP thì rollback và trả 503; timeout SMTP ≤ 15 giây; frontend khoá nút, không tự thử lại; log lỗi chỉ kèm `invitationId`; chấp nhận rủi ro SMTP nhận thư mà commit lỗi (§6.2). SA (thay người dùng), 2026-10-23 | "Không email nào được gửi" và "lời mời cũ vẫn còn" giữ đúng khi lỗi; mã không bao giờ nằm trong job; khoá tổ chức không bị giữ quá timeout của nginx | Hangfire: mã thô nằm trong tham số job trong PostgreSQL và dashboard, và người gửi không được báo khi gửi lỗi; Hangfire với mã được bảo vệ bằng `ISecretProtector`: thêm phần chuyển động, vẫn không báo lỗi được; khoá idempotency: thử lại bằng tay đã thay lời mời (BR-63 điểm 3) | Module |
| QĐ-11 | Đăng ký bằng email qua lời mời là endpoint riêng `register-with-invitation`; Google/Microsoft dùng đăng nhập có sẵn rồi API-21 | Email được xác minh nhờ sở hữu link (BR-63 điểm 6) chỉ ở một chỗ; không thêm nhánh vào đăng nhập ngoài | Thêm `invitationToken` tuỳ chọn vào `register` và `external`: nhánh bảo mật trộn vào luồng chung | Module |
| QĐ-12 | Microsoft: chỉ tin email khi tenant cá nhân hoặc `xms_edov = true`, áp cho cả khớp tài khoản có sẵn theo email lẫn khớp lời mời | Chặn chiếm lời mời và tài khoản bằng tenant giả ("nOAuth") | Tin claim `email` như hiện tại (quyết định 2025-07-25): bị khai thác được; gọi Graph: không chứng minh được miền | Nền tảng (chờ §14 T2) |
| QĐ-13 | `OrganizationInvitation` loại khỏi audit tự động; mục ngữ nghĩa qua cổng `IAuditTrail`; `Role`, `OrganizationMember` cài `IHasOrganization` | Snapshot sẽ chứa `TokenHash`; PRD cần email và vai trò trong mục, không cần snapshot | Snapshot tự động rồi lọc trường: dễ quên khi thêm trường | Nền tảng (cổng audit) |
| QĐ-14 | Hai đường đọc vai trò: API-15 (có quyền, cần quản lý vai trò) và API-14 (tên + `canGrant`, cần quản lý thành viên) | BR-77, AC-300, AC-311 | Một endpoint lọc trường theo quyền: hợp đồng mơ hồ | Module |
| QĐ-15 | Admin hệ thống trong bảng `system_administrators`, chỉ lệnh CLI ghi; policy `SystemAdministrator` chỉ nhận JWT người dùng, tra có cache, không nằm trong JWT | BR-20; thu hồi có hiệu lực trong vài giây; có dấu thời gian cấp | Danh sách email trong cấu hình: phải khởi động lại, lệch giữa các máy; cột trên `accounts`: lệnh cập nhật hồ sơ ghi đè mọi cột, có thể xoá mất cờ | Nền tảng |
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
| QĐ-27 | Sửa, xoá vai trò hệ thống báo `OrganizationSystemRoleImmutable` trước BR-58 | AC-55 yêu cầu đúng lý do này; vai trò hệ thống không phải đối tượng sửa được nên xếp cùng bước 5 | Theo D2 báo "không đủ quyền": trái AC-55 | Module |

**Rủi ro còn lại**: cửa sổ vài phút khi cuốn chiếu (T14); email mời phụ thuộc SMTP đồng bộ, và hiếm khi người nhận có link chết nếu commit lỗi sau khi SMTP đã nhận thư (QĐ-10); mẫu email tạm chỉ tiếng Việt trước PRD05; người dùng Microsoft tenant chưa xác minh miền không chấp nhận được lời mời nếu QĐ-12 được duyệt.

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
| FR-25 | §3.4, §4.2, §4.4, §6.2, §6.3 | AC-57–AC-60, AC-115–AC-119, AC-144, AC-151, AC-152, AC-177–AC-182, AC-196, AC-303–AC-309, AC-312, AC-332–AC-334 |
| FR-29 | API-08, §6.4, §5.5 (nghĩa vụ TECH01) | AC-69, AC-259 (phần thành viên; phần vai trò theo sản phẩm ở truy vết TECH01), AC-70 (404 phần dữ liệu ở TECH03), AC-318, AC-320 |
| FR-30 | API-05, API-25, API-28, §6.5 | AC-73, AC-107; AC-71, AC-72 (TECH02, TECH04) |
| FR-31 | §8.2 trang chào | AC-74, AC-201, AC-303 |
| FR-37 | §5.1 bước 5, §5.5 | AC-85, AC-121–AC-123, AC-134, AC-136, AC-176, AC-195, AC-280, AC-310, AC-317, AC-318 (phần feedback ở TECH03) |
| FR-38 | §3.5, §5.2, T8 | AC-51, AC-52 |
| FR-42 | API-26, §6.5 | AC-105, AC-106, AC-128, AC-131, AC-144, AC-152, AC-334; AC-129, AC-130, AC-142, AC-143, AC-145 (TECH02–TECH04) |
| FR-44 | API-24, §4.8 | AC-124–AC-127, AC-302 |
| FR-45 | §6.3 (admin hệ thống vào qua lời mời như mọi tài khoản), §5.2 | AC-132–AC-134 (TECH03), AC-135 |
| FR-46 | API-05, API-25, §3.1 | AC-138, AC-139, AC-141, AC-146–AC-148 |
| FR-52 | §5.3, §5.4 | AC-55, AC-153–AC-158, AC-193, AC-194, AC-197, AC-198, AC-247–AC-259, AC-269–AC-271, AC-295–AC-297, AC-311, AC-317, AC-319 |
| FR-53 | API-04, §3.1 | AC-185–AC-187, AC-225, AC-226 |
| FR-54 | §5.1 bước 3, §6.5, §8.2 | AC-73, AC-151, AC-183, AC-184, AC-199–AC-201, AC-301, AC-308, AC-320, AC-321, AC-332, AC-333 |
| FR-56 | §5.5, §7.2 T10, T11, §7.5 | AC-160, AC-161, AC-195 (TECH02, TECH03) |
| FR-59 | §4.5 (bỏ), QĐ-17 | AC-135, AC-260 |
| FR-60 | API-06, §4.6, §7.4 | AC-261–AC-265, AC-335 |
| FR-61 | API-10–API-13, §3.4, §3.7 | AC-266–AC-275, AC-310, AC-316, AC-324, AC-325, AC-335 |
| FR-62 | API-09, §6.4, §5.5 (nghĩa vụ TECH01) | AC-276 (phần thành viên; phần vai trò theo sản phẩm ở truy vết TECH01), AC-277–AC-280, AC-321–AC-323 |
| FR-63 | API-02, §3.1, §6.1 | AC-281–AC-286, AC-328–AC-331 |
| FR-64 | §3.3, §5.3 | AC-287–AC-293, AC-295, AC-297, AC-326 |
| FR-65 | §8.2 (locale, test) | AC-294, AC-327 |
| FR-66 | §4.2 API-14, §4.3, §5.1, §8.2 | AC-275, AC-298–AC-300, AC-311, AC-313–AC-315 |

Ngoài phạm vi, thuộc module khác: FR-10 (TECH04: AC-20, AC-21, AC-92, AC-191, AC-192); FR-13–FR-17, FR-20, FR-26–FR-28, FR-35 (TECH03: AC-24–AC-36, AC-40, AC-41, AC-61–AC-68, AC-82, AC-83, AC-90, AC-120, AC-173, AC-188, AC-189, AC-210, AC-211, AC-243, AC-244); FR-49 (TECH07: AC-43).

Phần thiết kế không có yêu cầu PRD trực tiếp, giữ vì quy tắc repo hoặc system-context: §10.3 và QĐ-01–QĐ-04 (system-context: nhiều máy), `IHasOrganization` cho `Role`, `OrganizationMember` (quy tắc kiến trúc), QĐ-24 (catalog TECH00: chuyển design system).

## 14. Câu hỏi mở

### Câu hỏi kỹ thuật (gửi `solution-architect`)

| # | Câu hỏi | Đề xuất trả lời | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|
| T2 | Có áp QĐ-12 (Microsoft chỉ tin email khi tenant cá nhân hoặc `xms_edov = true`) cho cả đăng nhập Microsoft đang có, không chỉ lời mời? | Có. Cần bật optional claim `xms_edov` trong app registration; tài khoản tenant chưa xác minh miền không khớp được tài khoản có sẵn theo email và không chấp nhận được lời mời | Đổi quyết định 2025-07-25; vài người dùng Microsoft công ty nhỏ có thể phải đăng ký bằng email | Mở — SA chuyển cho người dùng |
| T6 | Khung ứng dụng mới chỉ có rail như bảng *AppShell*; có bỏ chế độ menu trên (`AppTopMenu`) và tuỳ chọn bố cục hiện có? | Bỏ; giữ cài đặt giao diện sáng/tối, ngôn ngữ, cỡ chữ mà bảng *AppShell* có | Giảm mã và test bố cục; người đang dùng menu trên mất tuỳ chọn | Mở — SA chuyển cho người dùng |
| T1 | Bảng sản phẩm và `IProductAccessScope` dựng trong kế hoạch TECH00 hay TECH01? | — | — | Đã trả lời: không dựng trong kế hoạch TECH00. Giữ §5.5 làm thiết kế; bảng, hiện thực và việc xoá `product_members` khi gỡ, rời thuộc kế hoạch TECH01 kèm nghĩa vụ ở §5.5; bỏ hai bảng khỏi M2; phần sản phẩm của AC-69, AC-259 sang truy vết TECH01 (§3.6, §5.5, §10.1, §13, QĐ-21). Nguồn: SA (thay người dùng), 2026-10-23 |
| T3 | Gửi email mời đồng bộ trong giao dịch? | — | — | Đã trả lời: đồng bộ, với bốn điều kiện (timeout ≤ 15 giây; frontend khoá nút, không tự thử lại; log chỉ `invitationId`; chấp nhận rủi ro link chết). Hangfire bị loại (§6.2, QĐ-10). Nguồn: SA (thay người dùng), 2026-10-23 |
| T4 | Bỏ route `{id}` cũ cùng bản phát hành? | — | — | Đã trả lời: bỏ, theo thứ tự §10.2; frontend mới chỉ phát hành sau máy API cuối cùng; PR có dòng `- Removed:` (§10.2, QĐ-25). Nguồn: SA (thay người dùng), 2026-10-23 |

### Câu hỏi cho PRD (gửi `product-manager`)

| # | Câu hỏi | Đề xuất trả lời | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|
| P1 | Người có quyền quản lý thành viên có được **thêm** vai trò cho một thành viên đang giữ vai trò mà mình không đủ quyền không? | — | — | Đã trả lời: đổi vai trò của một người cần `CanTakeAway` với mọi vai trò người đó đang giữ (cùng điều kiện như gỡ), kể cả khi chỉ thêm; mỗi vai trò thêm vẫn qua `CanGrant`; `canChangeRoles` theo đó, hàng khoá với `InsufficientRolePermissions` (§4.6, §5.3). **Chờ PRD-00 v1.3** (phiên chính làm với `product-manager`). Nguồn: SA (thay người dùng), 2026-10-23 |
| P2 | Gỡ chính mình bằng *Gỡ*? | — | — | Đã trả lời: từ chối; API-08 với `accountId` là người gọi trả 409 `CannotRemoveSelf`; rời chỉ qua API-09 (§4.1, §5.3, §6.4). PRD-00 v1.3 thêm một AC. Nguồn: SA (thay người dùng), 2026-10-23 |
| P3 | Mẫu email mời tạm trước PRD05? | — | — | Đã trả lời: mẫu tạm chỉ tiếng Việt, viết cứng như email thông báo hiện có; tên tổ chức, tên vai trò (vai trò hệ thống theo `vi`), hạn, link; không tên người mời; mã hoá HTML mọi giá trị; không đổi PRD; PRD05 quyết ngôn ngữ cho người nhận chưa có tài khoản (§6.2). Nguồn: SA (thay người dùng), 2026-10-23 |
| P4 | Ghi cho PRD03 về BR-59 và snapshot nhật ký kiểm toán | — | — | Đã đóng: BR-59 đã đủ; TECH03 áp T10 (§7.2). Không cần dòng chuyển giao PRD. Nguồn: SA (thay người dùng), 2026-10-23 |

## 15. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| 2026-10-23 | 0.2 | Áp câu trả lời T1, T3, T4, P1–P4: bảng sản phẩm và hiện thực `IProductAccessScope` chuyển sang kế hoạch TECH01 kèm nghĩa vụ ở §5.5, bỏ khỏi M2; bốn điều kiện gửi email mời đồng bộ (§6.2, QĐ-10); quy tắc cứng frontend sau máy API cuối cùng và dòng `- Removed:` (§10.2, QĐ-25); đổi vai trò cần `CanTakeAway` mọi vai trò hiện có (§4.6, §5.3, chờ PRD-00 v1.3); `CannotRemoveSelf` (§4.1, §5.3, §6.4); mẫu email mời tạm tiếng Việt (§6.2); P4 đóng (§7.2). T2, T6 còn mở, chờ người dùng. Vẫn Draft | SA (thay người dùng) |
| 2026-10-23 | 0.1 | Bản nháp đầu từ PRD-00 v1.2, thiết kế module 00, system-context và code hiện có; giải quyết các ràng buộc *Quyền gán vai trò*, *Quyền sửa vai trò tự tạo*, *Xoá vai trò tự tạo* và phần TECH00 của *Quyền đọc và tải file* | — |

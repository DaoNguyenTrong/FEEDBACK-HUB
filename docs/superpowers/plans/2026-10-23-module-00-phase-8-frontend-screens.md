# Module 00 — Phần 8: Các màn hình frontend của module 00 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23). **Push/PR:** người dùng cho phép (2026-10-23): sau khi phần 8 xong và `scripts/test-gate.sh` qua, push `feature/module-00` và mở một PR vào `dev` cho cả module 00; không bao giờ vào `main`.

**Goal:** Dựng mọi màn hình của module 00 trong bảng TECH00 §8.2 trên nền của phần 7, gọi thẳng client orval sinh từ hợp đồng phần 3–6:
- tổ chức: tạo (định danh ngắn gợi ý từ tên), chuyển, trang chào, xử lý tổ chức tạm ngừng giữa chừng, quản lý tổ chức (đổi tên, vô hiệu kèm lý do);
- thành viên: danh sách, đổi vai trò ngay trong dòng, gỡ, tự rời;
- lời mời: quản lý (gửi, mời lại, thu hồi, xoá) và trang công khai mở, đăng ký, chấp nhận lời mời;
- vai trò tự tạo;
- danh sách tổ chức của admin hệ thống;
- nhật ký kiểm toán.

**Architecture:**
- Mỗi màn hình là một view trong `src/views/organization/`, `src/views/system/` hoặc `src/views/`, đăng ký route với `meta.requiresOrganization`, `meta.permission`, `meta.systemAdministrator` của phần 7. Mục rail sinh từ `meta.sidebar` (nhóm `organization`, `system`).
- Trạng thái dùng chung giữa các tab *Thành viên*, *Lời mời*, *Vai trò* nằm trong store `organization-people`. Xử lý "tổ chức vừa tạm ngừng / vừa mất quyền" (§6.5) nằm trong store `organization-context`; `api/client.ts` chỉ báo sự kiện.
- Lỗi khi tải trang: `accessProblemOf` → `AccessProblemState`. Lỗi của thao tác: `errorMessage` theo `code`, hiện tại chỗ. Xác nhận nằm dưới dòng (`InlineConfirm`); hộp thoại duy nhất là *Vô hiệu tổ chức*.

**Tech Stack:** Vue 3 + TypeScript, Pinia, naive-ui, Tailwind v4, vue-i18n, client orval (axios), vitest + `@vue/test-utils` + MSW, Playwright + Mailpit.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0: §4.1–§4.6, §4.7 (mã lỗi), §4.8 (phân trang, *Xem thêm*), §4.10, §5.1 (cổng, `DenyMode`), §5.3 (cờ `can*`, `lockReason`), §6.1–§6.5, §6.8 bước 2–3, §7.2 T2, T3, §8.2 (toàn bộ), §11 (các hàng có tầng *Frontend*), §12 QĐ-23, QĐ-31.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: FR-03, FR-21, FR-23–FR-25, FR-29–FR-31, FR-42, FR-44–FR-46, FR-52–FR-54, FR-59–FR-66, FR-68; BR-24, BR-45, BR-51, BR-54, BR-58, BR-63, BR-64, BR-74, BR-75, BR-77–BR-81, BR-83, BR-84; D4–D7, D12, D14–D16, D19; AC ghi trong từng task.
- Thiết kế đã duyệt (https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn), sáng và tối, 1280/834/390 px: *OrgContext*, *AppShell* (tấm trượt *Tạo tổ chức*), *Members*, *Roles*, *OrgSettings*, *InviteAccept*, *SysOrgs*, *AuditLog*, *Components*.

Nền (token, khung, `AppSheet`, `InlineConfirm`, `AccessProblemState`, `routeAccess`, `OrgSwitcher`, `roleDisplayName`, `errorMessage`) ở `2026-10-23-module-00-phase-7-frontend-foundation.md`.

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
| 6 | Màn hình nhật ký: API-33, bỏ `api/admin/audit-logs` | §4.10, §6.8 |
| 7 | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| **8 (tệp này)** | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Điều kiện bắt đầu:** phần 7 xong; phần 3–6 đã export `shared/openapi/openapi.json` và chạy `bun run --cwd frontend codegen`. Gọi đúng tên hàm orval sinh ra (operationId `<Controller>_<Action>` do phần 3–6 đặt); test MSW khớp theo route của TECH00, không theo tên hàm.

  | Màn hình | Endpoint (TECH00) | Module orval dự kiến | DTO |
  |---|---|---|---|
  | Tạo, chuyển tổ chức, trang chào | API-01 `GET api/organizations`, API-02 `POST api/organizations`, API-28 `POST api/auth/switch-organization` | `organizations/organizations`, `auth/auth` | `OrganizationDto`, `CreateOrganizationRequest { name, slug }` |
  | Quản lý tổ chức | API-03 `GET api/organization`, API-04 `PATCH api/organization`, API-05 `POST api/organization/deactivate` | `current-organization/current-organization` | `CurrentOrganizationDto { id, name, slug, status }`, `RenameOrganizationRequest { name }`, `DeactivateOrganizationRequest { reason? }` |
  | Thành viên | API-06 `GET api/organization/members`, API-07 `PUT …/members/{accountId}/roles`, API-08 `DELETE …/members/{accountId}`, API-09 `POST api/organization/leave`, API-14 `GET api/organization/role-options` | `current-organization/current-organization` | `PagedResult<OrganizationMemberDto>`, `UpdateMemberRolesRequest { roleIds }`, `RoleOptionDto[]` |
  | Lời mời | API-10 `GET api/organization/invitations`, API-11 `POST …/invitations`, API-12 `POST …/invitations/{id}/resend`, API-13 `DELETE …/invitations/{id}` | `current-organization/current-organization` | `PagedResult<InvitationDto>`, `SendInvitationRequest { email, roleId }` |
  | Vai trò | API-15 `GET api/organization/roles`, API-16 `POST …/roles`, API-17 `PUT …/roles/{roleId}`, API-18 `DELETE …/roles/{roleId}`, API-19 `GET api/permissions` | `current-organization/current-organization`, `permissions/permissions` | `RoleDto` (`pendingInvitationCount: number \| null` do phần 5 Task 4 thêm), `CreateRoleRequest`/`UpdateRoleRequest { name, permissionCodes[] }`, `PermissionDto[]` |
  | Nhận lời mời | API-20 `POST api/invitations/lookup`, API-21 `POST api/invitations/accept`, API-22 `POST api/auth/register-with-invitation`, API-23 `POST api/auth/external/{provider}` | `invitations/invitations`, `auth/auth` | `InvitationTokenRequest { token }`, `InvitationLookupDto`, `AcceptInvitationResult { organizationId, organizationName }`, `RegisterWithInvitationResponse` (= `LoginResponse` + `invitationOutcome`), `ExternalLoginResponse` (= `LoginResponse` + `accountCreated`) |
  | Các tổ chức (admin hệ thống) | API-24 `GET api/system/organizations`, API-25 `POST …/{id}/deactivate`, API-26 `POST …/{id}/reactivate` | `system-organizations/system-organizations` | `PagedResult<SystemOrganizationDto { id, name, memberCount, isDeactivated }>`, `{ reason }` |
  | Nhật ký kiểm toán | API-33 `GET api/organization/audit-log?cursor&limit` | `current-organization/current-organization` | `AuditEntryPage { items: AuditEntryDto[], nextCursor }`; `AuditEntryDto.parameters` là từ điển `string → object` ở backend, nên kiểu orval sinh cho giá trị là `unknown` |

  Hình dạng DTO đúng như TECH00 §4.6 và §4.10; tên DTO là tên phần 3–6 đặt. Mọi enum (`systemKind`, `lockReason`, `status`, `invitationOutcome`, `actorKind`, `type`…) ra chuỗi. 403 `OrganizationSuspended` có `organizationName` ở cấp cao nhất của thân, cạnh `code` (phần 3 Task 4). Mỗi DTO mới được re-export trong `src/api/types.ts` dưới đúng tên của nó; `PagedResult` chuẩn hoá bằng `toPagedResult()`.
- **Nhánh và commit:** `feature/module-00`, sau phần 7. Conventional Commits, subject ≤ 100 ký tự, stage theo đường dẫn. Mỗi message kết thúc bằng đúng một dòng:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```
- **Không đụng `backend/`**, không export OpenAPI.
- **Quy ước frontend** như phần 7: `<script setup lang="ts">`, auto-import, store kiểu setup, gọi thẳng factory `get*()` một lần mỗi tệp, không `import.meta.env`, Prettier, `lint:boundaries`. Màu chỉ qua lớp theo vai trò của phần 7; không thang `primary-*`, `surface-*`.
- **Hiện lỗi:**
  - lỗi khi tải trang → `accessProblemOf(error)`; khác `null` thì render `AccessProblemState` (không tên, mã hay đường dẫn đối tượng; BR-45);
  - lỗi của thao tác → `errorMessage(error, t, te)` hiện ngay dưới ô hoặc trong dòng, hoặc toast lỗi khi không có chỗ gắn;
  - không bao giờ hiện `detail`, `title` hay extension của backend; ngoại lệ duy nhất là `organizationName` của `OrganizationSuspended` (§6.5);
  - 404 từ API-07, API-08, API-13, API-17, API-18 → toast `common.notFoundReloaded` rồi tải lại danh sách (§8.2).
- **Ít hộp thoại, chữ thường:** hộp thoại duy nhất là *Vô hiệu tổ chức* (Task 3, dùng lại ở Task 8). Gỡ, thu hồi, xoá vai trò, rời tổ chức, kích hoạt lại dùng `InlineConfirm`. Tạo tổ chức, mời, tạo/sửa vai trò dùng `AppSheet`. Xoá lời mời hết hạn không có bước xác nhận (AC-268).
- **Danh sách:** `useLazyList` cỡ trang 50 với nút *Xem thêm* (§4.8); API-33 dùng con trỏ (`useCursorList`, Task 9). Không có ô tìm kiếm hay lọc nào mà bảng thiết kế không vẽ.
- **Thời gian:** `formatDateTime(iso, locale)` (Task 5) cho mọi hạn lời mời, giờ trong nhật ký. Fixture test dựng từ thời điểm UTC (`new Date('2026-10-30T07:30:00Z')`), chạy được với `TZ=UTC`.
- **Chuỗi:** vi và en cho mọi chuỗi; nhóm `errorCodes.<Code>` cho mọi mã ở TECH00 §4.7 mà màn hình hiện. Test so chữ qua `i18n.global.t(...)`.
- **Test** theo `testing.md` § Frontend như phần 7. Thêm vào `tests/helpers/render.ts` mọi route name mới của phần này (Task 2 thêm một lượt). Factory mới ở `tests/factories/organization.ts`: `makeCurrentOrganization`, `makeMember`, `makeRoleOption`, `makeRoleRef`, `makeInvitation`, `makeInvitationLookup`, `makeSystemOrganization`, `makeAuditEntry`.
- **Lệnh kiểm** sau mỗi task: `bun run --cwd frontend test:run`, `bun run --cwd frontend build-only`, `bun run --cwd frontend type-check`, `bun run --cwd frontend lint:boundaries`.

## Review Focus

1. **Bấm *Gửi lời mời* hai lần, hoặc SMTP chậm tới 15 giây rồi 503:** chỉ một request; nút khoá và hiện "Đang gửi…"; 503 không tự gửi lại; lỗi nằm trong form và form giữ nguyên dữ liệu đã nhập (§6.2 điều kiện 2). Test ở Task 5.
2. **Tên người, tên vai trò hay email chứa thẻ HTML** (`<img src=x onerror=alert(1)>`) ở danh sách thành viên, lời mời, nhật ký, trang lời mời: hiện nguyên văn, DOM không có phần tử `img` (B6). Test ở Task 4 và Task 9.
3. **Thành viên bị gỡ hoặc tổ chức bị vô hiệu trong lúc đang mở trang** (request kế tiếp nhận 403, hoặc refresh trả `organizationId = null`): không phát lại request trong ngữ cảnh mới; tạm ngừng → trang chào có thông báo kèm tên; bị gỡ → trang chào không thông báo; thiếu quyền mà vẫn là thành viên → ở lại, hiện *Không đủ quyền* (AC-183, AC-199, AC-200, AC-301). Test ở Task 2.
4. **Mã lời mời trong URL:** sau khi trang `/invite` đọc mã, thanh địa chỉ không còn `token=`; tải lại tab (hoặc quay về từ trang đăng nhập) vẫn dùng được mã từ `sessionStorage`; trang có `<meta name="robots" content="noindex">` và `<meta name="referrer" content="no-referrer">` trong lúc mở (T2, QĐ-23). Test ở Task 7.
5. **Chủ sở hữu cuối cùng bấm *Rời tổ chức*:** máy chủ trả 409 `LastOwnerCannotLeave`; khối dưới dòng đổi sang lời nhắc giao Chủ sở hữu, chỉ còn *Huỷ*, không chuyển ngữ cảnh, không tải lại trang (D6, AC-277). Test ở Task 4.

---

### Task 1: Gợi ý định danh ngắn và tấm trượt *Tạo tổ chức*

**Files:**
- Create: `frontend/src/lib/slug-suggestion.ts`, `frontend/src/components/CreateOrganizationSheet.vue`
- Modify:
  - `frontend/src/stores/organizations.ts` (`create(name, slug)`; `deactivate` đã bỏ ở phần 3 Task 11)
  - `frontend/src/layouts/AppLayout.vue` (gắn tấm trượt vào `layout.createOrganizationOpen`)
  - `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Delete: `views/OrganizationsView.vue`, `components/OrganizationList.vue`, `components/OrganizationForm.vue`, route `organizations`, cùng test của chúng (TECH00 §8.2). `OrganizationMembersDialog.vue`, `OrganizationRolesDialog.vue`, `RoleForm.vue`, `stores/roles.ts` đã bị xoá ở phần 4 Task 10.
- Test: `frontend/tests/unit/lib/slug-suggestion.test.ts`, `frontend/tests/components/CreateOrganizationSheet.test.ts`, `frontend/tests/unit/stores/organizations.test.ts`

**Interfaces:**
- Consumes: API-02, API-28 (`useOrganizationSwitch`), `auth.resendVerification`, phần 7 (`AppSheet`, `InlineNotice`, `errorMessage`, `layout.createOrganizationOpen`).
- Produces:
  - `suggestSlug(name: string): string`: BR-78 (a)–(e) — bỏ dấu tiếng Việt (`normalize('NFD')`, bỏ dấu kết hợp), `đ`/`Đ` → `d`, chữ thường; mỗi chuỗi ký tự ngoài `a–z0–9` → một `-`; bỏ `-` đầu, cuối; cắt còn 50; bỏ `-` cuối nếu có.
  - `normalizeSlugInput(value: string): string`: chỉ đổi chữ hoa thành chữ thường (AC-329), không sửa gì khác.
  - Store `organizations`: `create(data: { name: string; slug: string }): Promise<OrganizationDto>`.
  - `CreateOrganizationSheet` (`v-model:show`): ô *Tên tổ chức*, ô *Định danh ngắn* (gợi ý theo tên cho tới khi người dùng tự sửa ô định danh; gợi ý dưới 3 ký tự vẫn hiện, AC-328), gợi ý dưới ô "Không đổi được sau khi tạo."; 201 → `switchOrganization(organization.id)` (tải lại vào tổ chức mới, AC-56).
  - Lỗi:
    - `OrganizationSlugInvalid` và `OrganizationSlugAlreadyExists` dưới ô định danh, không nêu tổ chức nào (AC-283, AC-284, AC-286, AC-330);
    - tên trống → kiểm ở client;
    - `EmailNotConfirmed` (403) → `InlineNotice tone="warning"` trong tấm trượt, kèm nút chữ *Gửi lại email xác thực* gọi `auth.resendVerification({ email: auth.account.email })`.
  - Locale:
    - `organizations.createTitle` (vi "Tạo tổ chức", en "Create organization"), `organizations.name` (vi "Tên tổ chức", en "Organization name"), `organizations.slug` (vi "Định danh ngắn", en "Short identifier"), `organizations.slugHint` (vi "Không đổi được sau khi tạo.", en "Can't be changed after creation."), `organizations.nameRequired` (vi "Nhập tên tổ chức.", en "Enter the organization name."), `organizations.resendVerification` (vi "Gửi lại email xác thực", en "Resend verification email");
    - `errorCodes.OrganizationSlugInvalid`: vi "Định danh chỉ gồm chữ a–z, số 0–9 và dấu \"-\", bắt đầu và kết thúc bằng chữ hoặc số, không có hai dấu \"-\" liền nhau, dài từ 3 đến 50 ký tự.", en "Use only a–z, 0–9 and \"-\", start and end with a letter or digit, no two \"-\" in a row, 3 to 50 characters.";
    - `errorCodes.OrganizationSlugAlreadyExists`: vi "Định danh này đã được dùng.", en "This identifier is already taken.";
    - `errorCodes.EmailNotConfirmed`: vi "Bạn cần xác minh email trước khi làm việc này.", en "Verify your email before doing this.".

- [ ] **Step 1: Viết test thất bại**

```ts
describe('suggestSlug')
it.each([
  ['Công ty Đông Á', 'cong-ty-dong-a'],                                                        // AC-281
  ['AB', 'ab'],                                                                                // AC-328
  ['Công ty TNHH Thương mại Dịch vụ Kỹ thuật Hải Đăng Hà Nội', 'cong-ty-tnhh-thuong-mai-dich-vu-ky-thuat-hai-dang'], // AC-331, 49 ký tự
  ['  --Sao   Mai!! Tech--  ', 'sao-mai-tech'],
  ['Đường Ống', 'duong-ong'],
  ['', ''],
])('suggests %s → %s')
describe('normalizeSlugInput')
it('lowercases typed capitals and changes nothing else')        // 'Sao-Mai' → 'sao-mai' (AC-329); 'sao_mai' giữ nguyên để máy chủ báo
describe('CreateOrganizationSheet')
it('suggests the identifier from the name until the identifier is edited')
it('sends name and identifier, then switches into the new organization')
it('shows the identifier rule under the identifier on OrganizationSlugInvalid')
it('shows that the identifier is taken without naming any organization')
it('offers to resend the verification email on EmailNotConfirmed')
it('requires a name before calling the server')
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- slug-suggestion CreateOrganizationSheet stores/organizations`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *AppShell* (panel bên phải ở 1280, tấm trượt toàn chiều ngang ở 834, từ dưới ở 390) và *OrgContext* § "Tạo tổ chức, định danh bị trùng".

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(organizations): create an organization with a suggested short identifier`

### Task 2: Trang chào và tổ chức tạm ngừng hay mất quyền giữa chừng (§6.5)

**Files:**
- Create: `frontend/src/views/WelcomeView.vue`, `frontend/src/stores/organization-context.ts`
- Modify:
  - `frontend/src/api/client.ts`, `frontend/src/api/types.ts` (`ProblemDetails.organizationName?: string`)
  - `frontend/src/stores/auth.ts` (`refreshToken` báo khi `organizationId` từ khác `null` thành `null`)
  - `frontend/src/main.ts` (nối handler)
  - `frontend/src/composables/use-home-route.ts`, `frontend/src/router/index.ts`
  - `frontend/tests/helpers/render.ts` (route `welcome`, `invite`, `organization-members`, `organization-invitations`, `organization-roles`, `organization-settings`, `organization-audit-log`, `system-organizations`)
  - `frontend/src/locales/{vi,en}.ts`
- Test: `frontend/tests/views/WelcomeView.test.ts`, `frontend/tests/layouts/OrgSwitcher.test.ts`, `frontend/tests/unit/stores/organization-context.test.ts`, `frontend/tests/unit/api/client.test.ts`, `frontend/tests/unit/router/guards.test.ts`, `frontend/tests/unit/stores/auth.test.ts`

**Interfaces:**
- Consumes: API-01, API-28, Task 1 (`CreateOrganizationSheet`), phần 7 (`OrgSwitcher` dùng lại phần danh sách, `AmbientBackdrop`, `InlineNotice`, `StatusLabel`).
- Produces:
  - `api/client.ts`: `setOrganizationAccessHandler(handler: (problem: ProblemDetails, url: string) => void): void`. Gọi handler với mọi lỗi 403 có `code` `OrganizationSuspended` hoặc `OrganizationAccessDenied`, trừ URL bắt đầu bằng `/api/auth`, `/api/organizations`, `/api/invitations`, `/api/system`, `/api/permissions` (danh sách miễn trừ của §5.1). Lỗi vẫn bị reject như cũ. `toProblemDetails` đọc thêm `organizationName` khi là chuỗi.
  - Trong luồng 401 → refresh: nếu `organizationId` trước refresh khác `null` và sau refresh là `null` thì **không** phát lại request gốc; reject bằng lỗi gốc và gọi `useOrganizationContextStore().handleOrganizationLost()` (QĐ-31).
  - Store `organization-context`:
    - `handleAccessProblem(problem: ProblemDetails): Promise<void>`:
      - `OrganizationSuspended` → ghi `sessionStorage['organization-suspended-notice'] = problem.organizationName`, `switchOrganization(null)`;
      - `OrganizationAccessDenied` → gọi API-01; tổ chức đang làm việc không còn trong danh sách → `switchOrganization(null)` không thông báo; còn trong danh sách với `status === false` → như tạm ngừng; còn và đang hoạt động → không làm gì (đó là thiếu quyền, trang tự hiện *Không đủ quyền*).
    - `handleOrganizationLost(): Promise<void>`: gọi API-01 rồi theo đúng nhánh trên.
    - `consumeSuspendedNotice(): string | null`: đọc rồi xoá khoá `sessionStorage`.
    - "Về ngữ cảnh cá nhân" trong store = gọi API-28 với `organizationId: null` qua `getAuth()` rồi `reloadTo('/welcome')` (`lib/session-exit.ts`). Không dùng `useOrganizationSwitch` ở đây: composable đó cần `useRouter()` của một component, còn store được gọi từ interceptor.
    - Chỉ lần gọi đầu mỗi trang có hiệu lực (trang tải lại).
  - `useHomeRoute()`: `auth.isAuthenticated && auth.organizationId === null` → `{ name: 'welcome' }`, ngược lại `{ name: 'home' }`. Guard: `home` khi `organizationId === null` → `welcome`.
  - Route `welcome` (`/welcome`), cấp cao nhất (ngoài `AppLayout`), `meta.requiresAuth: true`.
  - `WelcomeView` (bảng *OrgContext*, `AmbientBackdrop strength="full"`):
    - thanh trên mỏng có logo và `UserMenu`;
    - "Chào {name}";
    - có tổ chức đang hoạt động → "Chọn tổ chức để tiếp tục" và danh sách: chọn → `switchOrganization(id)`; tổ chức tạm ngừng mờ, có nhãn, không chọn được;
    - `auth.isSystemAdministrator` → link chữ *Các tổ chức* tới `/system/organizations` (trong khung có rail), hiện cho mọi admin hệ thống, kể cả khi họ có tổ chức, không chỉ khi họ chưa có tổ chức nào. Máy chủ vẫn kiểm policy `SystemAdministrator` ở API-24–26. Ui-designer bổ sung link vào bảng *OrgContext*;
    - thẻ *Tạo tổ chức mới* mở `CreateOrganizationSheet`, kèm dòng "Hoặc nhờ người quản lý gửi lời mời qua email.";
    - không có tổ chức nào → "Chưa có tổ chức? Tạo tổ chức mới, hoặc nhờ người quản lý gửi lời mời qua email." (AC-74, AC-303);
    - mọi tổ chức đều tạm ngừng → `InlineNotice tone="warning"`: "{names} đang tạm ngừng hoạt động. Liên hệ admin hệ thống FeedbackHub để mở lại." (`names` nối ", ", AC-184);
    - có `consumeSuspendedNotice()` → `InlineNotice tone="info"` (theo bảng *OrgContext*, người dùng duyệt 2026-10-23): "{name} vừa tạm ngừng hoạt động. Việc bạn vừa làm chưa được thực hiện. Liên hệ admin hệ thống FeedbackHub để mở lại." (AC-199, AC-301). Bảng *OrgContext* đã có câu cuối.
  - Locale `welcome.*` cho các câu trên và `welcome.systemOrganizations` (vi "Các tổ chức", en "Organizations") (en: "Hi {name}", "Choose an organization to continue", "No organization yet? Create one, or ask a manager to send you an invitation by email.", "Create a new organization", "Or ask a manager to send you an invitation by email.", "{names} is suspended. Contact the FeedbackHub system administrator to reopen it.", "{name} has just been suspended. What you just did wasn't carried out. Contact the FeedbackHub system administrator to reopen it.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('organization-context store')
it('stores the suspended name and switches to the personal context on OrganizationSuspended')
it('switches without a notice when the organization is gone from my list')        // AC-200 (bị gỡ)
it('treats a listed but suspended organization like OrganizationSuspended')
it('does nothing when the organization is still listed and active')               // thiếu quyền
it('reads the suspended notice once')
describe('apiClient')
it('reports 403 OrganizationSuspended with its organizationName to the handler')
it('does not report a 403 from an exempt path')                                   // /api/organizations, /api/auth/…
it('does not replay the request when a refresh drops the organization')           // Review Focus 3, QĐ-31
// GET /api/organization/members → 401; refresh → { organizationId: null }; members chỉ được gọi 1 lần; handleOrganizationLost được gọi
describe('guards')
it('sends a signed-in user without an organization from home to welcome')
describe('OrgSwitcher')
it('lands on /welcome from the Back to welcome page item')              // useHomeRoute() đã chuyển sang welcome ở Task này; AC-201
describe('WelcomeView')
it('greets the user and lists organizations with suspended ones dimmed and not selectable')   // AC-73, AC-201
it('switches into a picked active organization')
it('shows the create invitation line when the user has no organization')          // AC-74
it('shows the contact line when every organization is suspended')                  // AC-184
it('shows the just-suspended notice once with the organization name')              // AC-199, AC-301
it('opens the create-organization sheet')
it('shows the Organizations link to a system administrator without any organization')   // P8-2
it('shows the Organizations link to a system administrator who has organizations')
it('hides the Organizations link from a user who is not a system administrator')
it('opens /system/organizations from the link')
it('shows the dimmed organization with its label after leaving the one just deactivated')   // P8-3, trong switcher
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- organization-context client guards WelcomeView stores/auth`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces. `main.ts` gọi `setOrganizationAccessHandler((p) => useOrganizationContextStore().handleAccessProblem(p))` sau khi cài Pinia. Không thêm reset theo từng store: việc tải lại trang lo phần đó (`frontend-conventions.md` § Session and Organization Exits).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(organizations): add the welcome page and leave a suspended or lost organization`

### Task 3: *Quản lý tổ chức* và hộp thoại *Vô hiệu tổ chức* (*OrgSettings*)

**Files:**
- Create: `frontend/src/views/organization/OrganizationSettingsView.vue`, `frontend/src/components/DeactivateOrganizationDialog.vue`
- Modify: `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Test: `frontend/tests/views/organization/OrganizationSettingsView.test.ts`, `frontend/tests/components/DeactivateOrganizationDialog.test.ts`

**Interfaces:**
- Consumes: API-03, API-04, API-05, `auth.isSystemAdministrator`, `useOrganizationSwitch`.
- Produces:
  - Route `organization-settings` (`/organization/settings`), con của layout: `meta { requiresAuth, requiresOrganization, permission: Permissions.OrganizationManage, sidebar: { labelKey: 'nav.organizationSettings', icon: Settings, group: 'organization', order: 20 } }`.
  - `DeactivateOrganizationDialog` (`n-modal` preset dialog; hộp thoại duy nhất của module):
    - props `organizationName: string`, `reasonRequired: boolean`, `variant: 'member' | 'system-member' | 'system'`, `busy: boolean`; `v-model:show`; emit `submit(reason: string | null)`;
    - tiêu đề "Vô hiệu {name}?", dòng "Toàn bộ tổ chức tạm ngừng ngay:" và ba gạch đầu dòng theo `variant` (chép từ bảng *OrgSettings* và *SysOrgs*);
    - ô *Lý do* (thêm "(không bắt buộc)" khi `!reasonRequired`), đếm `{n}/500`, tối đa 500;
    - trống mà bắt buộc → "Nhập lý do." dưới ô, không emit (AC-147);
    - nút *Huỷ* và *Vô hiệu tổ chức* (`danger-solid`); ở 390 px hai nút xếp dọc, nút nguy hiểm trên.
  - `OrganizationSettingsView`:
    - tải API-03 (`accessProblemOf` khi lỗi);
    - phần *Thông tin tổ chức*: ô tên + nút *Lưu* (API-04), định danh mono kèm "Không đổi được.";
    - lưu xong → toast "Đã đổi tên thành {name}", cập nhật `auth.organizationName` và mục trong store `organizations`;
    - `OrganizationNameRequired` hoặc tên trống → "Nhập tên tổ chức." dưới ô (AC-187);
    - phần *Vô hiệu tổ chức*: một dòng mô tả, nút `danger-soft` mở hộp thoại với `reasonRequired = auth.isSystemAdministrator` (BR-54, AC-147) và `variant = isSystemAdministrator ? 'system-member' : 'member'`;
    - API-05 204 → `switchOrganization(null)`: về trang chào, không ghi `sessionStorage['organization-suspended-notice']` (người làm vừa tự thao tác). Bộ chọn tổ chức hiện tổ chức vừa vô hiệu mờ và có nhãn *Tạm ngừng*; nếu mọi tổ chức đều tạm ngừng thì trang chào hiện câu giải thích BR-64 (AC-184); `DeactivationReasonRequired` → lỗi dưới ô lý do.
  - Locale (vi theo bảng; en tương ứng): `organizationSettings.*`, `deactivateOrganization.*` gồm ba bộ gạch đầu dòng:
    - `member`: "Widget của mọi sản phẩm ngừng nhận feedback, link riêng của khách tạm ngừng." / "Không ai vào được tổ chức, kể cả bạn." / "Dữ liệu giữ nguyên. Chỉ admin hệ thống FeedbackHub kích hoạt lại được.";
    - `system-member`: như trên, câu cuối "Dữ liệu giữ nguyên. Bạn kích hoạt lại được trong danh sách Các tổ chức.";
    - `system`: "Widget của mọi sản phẩm ngừng nhận feedback, link riêng của khách tạm ngừng." / "Thành viên không vào được tổ chức." / "Dữ liệu giữ nguyên. Bạn kích hoạt lại được bất cứ lúc nào.";
    - `errorCodes.OrganizationNameRequired` (vi "Nhập tên tổ chức.", en "Enter the organization name."), `errorCodes.DeactivationReasonRequired` (vi "Nhập lý do.", en "Enter a reason."), `nav.organizationSettings` (vi "Quản lý tổ chức", en "Organization settings").
  - Dòng gợi ý "Gửi kèm email báo các Chủ sở hữu." dưới ô lý do **không** hiện cho tới khi TECH05 gửi email thật (đã chốt 1). Ô *Lý do* vẫn có: bắt buộc với admin hệ thống (BR-54), không bắt buộc với người khác (BR-52). TECH05 đưa dòng này trở lại.

- [ ] **Step 1: Viết test thất bại**

```ts
describe('DeactivateOrganizationDialog')
it('marks the reason optional and submits null when left empty for a member')
it('requires a reason for a system administrator and does not submit without one')   // AC-147
it('counts characters up to 500 and stops at 500')
it('lists the bullets of its variant')
describe('OrganizationSettingsView')
it('shows the name, the read-only identifier and the deactivate section')             // AC-315
it('renames the organization and shows the new name in the toast')                   // AC-185
it('rejects an empty name under the field without calling the server')               // AC-187
it('opens the deactivate dialog and moves to the personal context after 204')
it('goes to the welcome page after 204 without storing a suspended notice')         // P8-3: sessionStorage không có organization-suspended-notice
it('shows the welcome page without the just-suspended notice after deactivating the current organization')
it('keeps the reason field and hides the owner email hint for both a member and a system administrator')   // P8-1
it('shows the not-found or no-permission state when loading fails')                  // 404, 403 OrganizationAccessDenied
it('never shows the reason hint about an email to owners')       // ẩn tới khi TECH05 gửi thư thật; cũng ở SystemOrganizationsView
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- OrganizationSettingsView DeactivateOrganizationDialog`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *OrgSettings* (1280: cột nội dung tối đa 720 px; 834; 390: nút *Lưu* rộng hết dòng dưới ô).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(organizations): rename or deactivate the current organization`

### Task 4: Tab *Thành viên*: danh sách, đổi vai trò, gỡ, rời (*Members*)

**Files:**
- Create:
  - `frontend/src/views/organization/MembersView.vue`
  - `frontend/src/components/OrganizationPeopleTabs.vue` (tiêu đề "Thành viên", tab, nút thao tác chính; dùng cho cả ba tab)
  - `frontend/src/components/MemberRow.vue`
  - `frontend/src/stores/organization-people.ts`
- Modify: `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`, `frontend/tests/factories/organization.ts`
- Test: `frontend/tests/views/organization/MembersView.test.ts`, `frontend/tests/components/MemberRow.test.ts`, `frontend/tests/components/OrganizationPeopleTabs.test.ts`, `frontend/tests/unit/stores/organization-people.test.ts`

**Interfaces:**
- Consumes: API-06, API-07, API-08, API-09, API-14; phần 7 (`InlineConfirm`, `roleDisplayName`, `errorMessage`, `accessProblemOf`); `useOrganizationSwitch`.
- Produces:
  - Route `organization-members` (`/organization/members`), con của layout: `meta { requiresAuth, requiresOrganization, sidebar: { labelKey: 'nav.members', icon: Users, group: 'organization', order: 10 } }`. Không `permission`: mọi thành viên xem được (BR-74).
  - Store `organization-people`:
    - `members` (lazy list API-06 cỡ 50: `items`, `totalCount`, `loading`, `hasMore`, `loadMore()`, `reload()`);
    - `roleOptions: RoleOptionDto[]`, `loadRoleOptions()` (API-14, chỉ gọi khi có `organizations.members.manage`);
    - `changeRoles(accountId: string, roleIds: string[])`, `removeMember(accountId: string)`, `leave()` — thành công thì cập nhật hoặc gỡ dòng trong `members.items`.
    - Task 5 và 6 thêm `invitations`, `roles` vào cùng store.
  - `OrganizationPeopleTabs`: props `active: 'members' | 'invitations' | 'roles'`; tab là `RouterLink`, kèm số đếm khi đã tải (`totalCount` của lazy list tương ứng); tab *Lời mời* chỉ khi có `organizations.members.manage`, tab *Vai trò* chỉ khi có `organizations.roles.manage` (ẩn hẳn, AC-298, AC-299, AC-314); slot `action` cho nút chính (*Mời thành viên*, *Tạo vai trò*).
  - `MemberRow`: props `member: OrganizationMemberDto`, `organizationName: string`. Hiện:
    - chữ viết tắt, `accountName` + " (bạn)" khi `isSelf`, `email` chỉ khi khác `null`, vai trò (`roleDisplayName`, nối ", ");
    - dòng của mình: nút *Rời tổ chức* và, khi `canChangeRoles`, *Đổi vai trò*; không có *Gỡ* (D16, AC-339);
    - dòng khác có `canChangeRoles` hoặc `canRemove`: menu *Thao tác* (`n-dropdown`, nền `raised`) với *Đổi vai trò* rồi đường kẻ rồi *Gỡ khỏi tổ chức* (mục nguy hiểm cuối);
    - `lockReason = 'OwnerOnly'` → một dòng "Chỉ Chủ sở hữu đổi được dòng này" (AC-341); `'InsufficientRolePermissions'` → "Bạn không có đủ quyền của vai trò {role}" với `roleDisplayName(lockRole)` (AC-338); không có menu.
  - *Đổi vai trò* mở ngay trong dòng:
    - danh sách ô tick từ `roleOptions`; vai trò đang giữ được tick;
    - một tuỳ chọn bị khoá khi `!canGrant` và chưa giữ, kèm lý do: Chủ sở hữu → "Chỉ Chủ sở hữu chọn được vai trò này.", vai trò khác → "Bạn không có đủ quyền của vai trò này.";
    - dòng "Mỗi thành viên cần ít nhất một vai trò." khi bỏ hết; *Lưu* bị khoá lúc đó;
    - *Lưu* → API-07 → toast "Đã đổi vai trò của {name} thành {roles}."; *Huỷ* đóng.
  - *Gỡ khỏi tổ chức* → `InlineConfirm`: message "Gỡ {name} khỏi {org}?", detail "{name} sẽ không xem được dữ liệu của tổ chức này nữa.", confirmLabel "Gỡ khỏi tổ chức" → API-08 → toast "Đã gỡ {name} khỏi {org}".
  - *Rời tổ chức* → `InlineConfirm tone="neutral"`: message "Rời {org}?", detail "Bạn sẽ không xem được dữ liệu của {org} nữa. Muốn quay lại, bạn cần một lời mời mới.", confirmLabel "Xác nhận" (AC-322, AC-323) → API-09 → `switchOrganization(null)` (AC-276). 409 `LastOwnerCannotLeave` → cùng khối đổi sang `showConfirm = false` với message "Bạn là Chủ sở hữu duy nhất. Hãy giao vai trò Chủ sở hữu cho người khác trước khi rời tổ chức." (D6, AC-277).
  - `MembersView`: `OrganizationPeopleTabs active="members"` + danh sách `MemberRow` + *Xem thêm* ("Đang hiện {shown} trên {total} thành viên"). Lỗi tải trang → `AccessProblemState` (404 cho người ngoài, AC-122). Lỗi thao tác: `OwnerOnlyOperation`, `InsufficientRolePermissions`, `OrganizationCannotRemoveLastOwner`, `OrganizationMemberRequiresAtLeastOneRole`, `CannotRemoveSelf` → chữ theo locale trong dòng; 404 → toast `common.notFoundReloaded` và `members.reload()`.
  - Locale `members.*`, `nav.members` (vi "Thành viên", en "Members"), `common.notFoundReloaded` (vi "Không tìm thấy mục này nữa. Danh sách đã được tải lại.", en "This item no longer exists. The list has been reloaded."), `common.showMore` (vi "Xem thêm", en "Show more"), và `errorCodes`:
    - `OwnerOnlyOperation`: vi "Chỉ Chủ sở hữu làm được việc này.", en "Only an owner can do this.";
    - `InsufficientRolePermissions`: vi "Bạn không có đủ quyền của vai trò này.", en "You don't have every permission of this role.";
    - `OrganizationCannotRemoveLastOwner`: vi "Tổ chức cần còn ít nhất một Chủ sở hữu.", en "The organization needs at least one owner.";
    - `OrganizationMemberRequiresAtLeastOneRole`: vi "Mỗi thành viên cần ít nhất một vai trò.", en "Every member needs at least one role.";
    - `CannotRemoveSelf`: vi "Bạn không thể tự gỡ mình khỏi tổ chức. Hãy dùng Rời tổ chức.", en "You can't remove yourself. Use Leave organization.";
    - `LastOwnerCannotLeave`: câu Chủ sở hữu duy nhất ở trên (en "You are the only owner. Give the owner role to someone else before you leave.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('OrganizationPeopleTabs')
it('shows only the members tab to a member without management permissions')            // AC-298
it('shows the invitations tab with members.manage and the roles tab with roles.manage')   // AC-299, AC-314
describe('MemberRow')
it('shows the email only when the API returns one')                                      // BR-74, AC-261–263
it('offers Leave and Change roles but never Remove on my own row')                       // AC-339
it('locks an owner row with the owner-only line and no menu')                            // AC-341
it('locks a row with the missing-role line naming the role in the interface language')   // AC-338
it('renders names and roles containing HTML as text')                                    // Review Focus 2
it('disables role options I cannot grant with their reason and keeps held roles checked')
it('blocks saving with no role selected')
it('saves the role change and reports it in a toast')
it('confirms removal below the row and removes the row after 204')
it('confirms leaving below the row and moves to the personal context after 204')         // AC-276, AC-322
it('turns the leave block into the only-owner reminder on LastOwnerCannotLeave')         // Review Focus 5, AC-277
it('closes the leave block on cancel and changes nothing')                               // AC-323
describe('MembersView')
it('loads 50 members and appends the next page from Show more')
it('reloads the list after a 404 on change roles or remove')
it('shows the not-found state when the member list returns 404')                        // AC-122
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- MembersView MemberRow OrganizationPeopleTabs organization-people`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *Members* (1280: menu dòng, đổi vai trò trong dòng, xác nhận gỡ; 834; 390: thành viên thường chỉ thấy tên và vai trò, khối rời tổ chức, khối Chủ sở hữu cuối cùng).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(members): list members, change their roles, remove them and leave the organization`

### Task 5: Tab *Lời mời* và tấm trượt *Mời thành viên*

**Files:**
- Create: `frontend/src/views/organization/InvitationsView.vue`, `frontend/src/components/InvitationRow.vue`, `frontend/src/components/InviteMemberSheet.vue`, `frontend/src/lib/date-format.ts`
- Modify: `frontend/src/stores/organization-people.ts`, `frontend/src/views/organization/MembersView.vue` (nút *Mời thành viên*), `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Test: `frontend/tests/views/organization/InvitationsView.test.ts`, `frontend/tests/components/InvitationRow.test.ts`, `frontend/tests/components/InviteMemberSheet.test.ts`, `frontend/tests/unit/lib/date-format.test.ts`, `frontend/tests/unit/stores/organization-people.test.ts`

**Interfaces:**
- Consumes: API-10–API-14, Task 4 (`organization-people`, `OrganizationPeopleTabs`).
- Produces:
  - `formatDateTime(iso: string, locale: string): string` → giờ `HH:mm` rồi ", " rồi ngày `dd/MM/yyyy` (vi: "14:30, 30/10/2026"), theo múi giờ của trình duyệt bằng `Intl.DateTimeFormat`, giống cách `ProfilePanel` đang định dạng phiên.
  - Route `organization-invitations` (`/organization/members/invitations`): `meta { requiresAuth, requiresOrganization, permission: Permissions.OrganizationMembersManage, activeMenu: 'organization-members' }`.
  - Store `organization-people` thêm:
    - `invitations` (lazy list API-10);
    - `invite(email: string, roleId: string)`: API-11, thêm lời mời mới lên đầu và bỏ dòng cũ cùng email;
    - `resend(id: string)`: API-12, thay dòng bằng `InvitationDto` id mới;
    - `revokeOrDelete(id: string)`: API-13, bỏ dòng.
    - Mỗi hàm từ chối gọi lần hai khi lần trước chưa xong (một request mỗi lời mời hoặc mỗi form).
  - `InvitationRow` (props `invitation: InvitationDto`):
    - email; dòng phụ "{role} · Còn hạn đến {time}" (`status = 'Pending'`) hoặc "{role} · Hết hạn lúc {time}" cùng `StatusLabel tone="warning"` *Hết hạn*;
    - đang chờ: *Mời lại* (khi `canResend`), *Thu hồi* (khi `canRevokeOrDelete`) → `InlineConfirm` "Thu hồi lời mời gửi {email}?" / "Đường dẫn trong email sẽ không dùng được nữa." / "Thu hồi";
    - hết hạn: *Mời lại*, *Xoá* (gọi API-13 ngay, không xác nhận, AC-268);
    - đang gửi lại: nút thành "Đang gửi…" và khoá, không tự thử lại (§6.2);
    - `lockReason` → một dòng như `MemberRow` ("Chỉ Chủ sở hữu thu hồi được lời mời này" / "Bạn không có đủ quyền của vai trò {role}").
  - `InviteMemberSheet` (`v-model:show`; `AppSheet`):
    - ô *Email* với gợi ý "Gửi được cho cả người chưa có tài khoản Hub.";
    - danh sách chọn một vai trò từ `roleOptions` (`canGrant = false` → khoá kèm lý do như Task 4; không hiện quyền nào, AC-311);
    - dòng "Lời mời hết hạn sau 7 ngày và thu hồi được trước khi dùng.";
    - *Gửi lời mời* → "Đang gửi…", khoá nút và ô;
    - 201 → đóng, toast "Đã gửi lời mời đến {email}";
    - lỗi: `InvitationEmailAlreadyMember` dưới ô email ("Người này đã là thành viên {org}."), `FieldEmailAddress` dưới ô email, `InvitationEmailDeliveryFailed` (503) và hết thời gian chờ → `InlineNotice tone="danger"` trong form, giữ dữ liệu, không tự gửi lại.
  - `InvitationsView`: `OrganizationPeopleTabs active="invitations"` (nút *Mời thành viên* mở tấm trượt) + danh sách + *Xem thêm*. Trống → `EmptyState` "Chưa có lời mời nào đang chờ" / "Lời mời bạn gửi sẽ nằm ở đây cho tới khi được dùng, bị thu hồi hoặc hết hạn." + nút *Mời thành viên* (AC-274). Toast *Mời lại*: "Đã gửi lại lời mời. Lời mời có hạn đến {time}".
  - Locale `invitations.*`, `errorCodes.InvitationEmailAlreadyMember`, `errorCodes.FieldEmailAddress` (vi "Email cần có dạng ten@congty.vn.", en "Enter an email like name@company.com."), `errorCodes.InvitationEmailDeliveryFailed` (vi "Chưa gửi được lời mời. Hãy thử lại.", en "The invitation wasn't sent. Please try again.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('formatDateTime')
it('formats an instant as time, then date in the browser time zone')     // TZ=UTC: '2026-10-30T07:30:00Z' → '07:30, 30/10/2026'
describe('InviteMemberSheet')
it('lists every role and disables the ones I cannot grant without showing permissions')  // AC-311
it('sends one request however many times Send is pressed while sending')                 // Review Focus 1
it('keeps the form and shows the error without retrying on 503')                         // Review Focus 1, §6.2
it('shows the already-member message under the email')                                   // AC-180
it('closes and reports the sent invitation on 201')                                      // AC-312
describe('InvitationRow')
it('shows a pending invitation with its role and expiry')
it('labels an expired invitation and offers Resend and Delete')                          // AC-266, AC-267
it('deletes an expired invitation without a confirmation step')                          // AC-268
it('confirms revoking below the row')                                                    // AC-117, AC-272
it('shows Sending… and locks the row while resending')
it('shows the lock line instead of the actions')                                         // AC-253–AC-255
describe('InvitationsView')
it('shows the empty state when there is no invitation')                                  // AC-274
it('replaces the row with the new invitation after Resend')
it('reloads after a 404 on revoke')
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- InvitationsView InvitationRow InviteMemberSheet date-format organization-people`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *Members* § Tab Lời mời (đang chờ, xác nhận thu hồi, bị từ chối, đang gửi lại, hết hạn, bị khoá) ở ba khoảng; panel *Mời thành viên* bên phải ở 1280, tấm trượt toàn chiều ngang ở 834, từ dưới ở 390.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(invitations): send, resend, revoke and delete invitations from the members page`

### Task 6: Tab *Vai trò* và câu mô tả quyền (*Roles*)

**Files:**
- Create: `frontend/src/views/organization/RolesView.vue`, `frontend/src/components/RoleRow.vue`, `frontend/src/components/RoleSheet.vue`
- Modify:
  - `frontend/src/stores/organization-people.ts` (thêm `roles`, `permissionCatalog`)
  - `frontend/src/lib/permission-label.ts` (thay bằng `permissionName`, `permissionDescription`). `lib/permissions.ts` đã có `FeedbackHubAdmin: 'feedbackhub.admin'` từ phần 4 Task 1.
  - `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`
- Delete: các khoá `organizations.permission*` cũ (gồm `organizations.permissionFeedbackhubAdmin` của phần 4). `stores/roles.ts` đã bị xoá ở phần 4 Task 10.
- Test: `frontend/tests/views/organization/RolesView.test.ts`, `frontend/tests/components/RoleRow.test.ts`, `frontend/tests/components/RoleSheet.test.ts`, `frontend/tests/unit/lib/permission-label.test.ts`, `frontend/tests/unit/lib/permissions.test.ts`

**Interfaces:**
- Consumes: API-15–API-19, Task 4.
- Produces:
  - Route `organization-roles` (`/organization/members/roles`): `meta { requiresAuth, requiresOrganization, permission: Permissions.OrganizationRolesManage, activeMenu: 'organization-members' }`.
  - `permissionName(code: string, t): string` và `permissionDescription(code: string, t): string` đọc `permissions.<code>.name` / `.description` (khoá locale dùng mã với dấu chấm thay bằng `_`, ví dụ `permissions.organizations_members_manage`); mã lạ → chính mã đó cho tên, chuỗi rỗng cho mô tả. Thay `permissionLabel`.
  - Tên và câu mô tả (vi theo bảng *Roles*; en tương ứng, ≤ 120 ký tự):

    | Mã | vi tên | vi mô tả | en tên | en mô tả |
    |---|---|---|---|---|
    | `organizations.manage` | Quản lý tổ chức | Đổi tên tổ chức và vô hiệu tổ chức. | Manage organization | Rename and deactivate the organization. |
    | `organizations.members.manage` | Quản lý thành viên | Mời, gỡ thành viên và đổi vai trò của họ. | Manage members | Invite and remove members and change their roles. |
    | `organizations.roles.manage` | Quản lý vai trò | Tạo, sửa và xoá các vai trò tự tạo. | Manage roles | Create, edit and delete custom roles. |
    | `apikeys.manage` | Quản lý khoá API | Tạo và thu hồi khoá API của tổ chức. | Manage API keys | Create and revoke the organization's API keys. |
    | `auditlogs.view` | Xem nhật ký kiểm toán | Xem lại ai đã làm gì trong tổ chức. | View audit log | See who did what in the organization. |
    | `files.manage` | Quản lý file | Quản lý các file đã tải lên của tổ chức. | Manage files | Manage the files uploaded to the organization. |
    | `settings.manage` | Quản lý cài đặt | Đổi các cài đặt chung của tổ chức. | Manage settings | Change the organization's general settings. |
    | `feedbackhub.admin` | Quản trị FeedbackHub | Quản lý sản phẩm và mọi feedback của tổ chức. | FeedbackHub administration | Manage products and all feedback of the organization. |

  - Store `organization-people` thêm `roles: RoleDto[]`, `permissionCatalog: PermissionDto[]`, `loadRoles()`, `loadPermissionCatalog()` (một lần mỗi trang), `createRole(name, permissionCodes)`, `updateRole(roleId, name, permissionCodes)`, `deleteRole(roleId)`.
  - `RoleRow` (props `role: RoleDto`):
    - tên (`roleDisplayName`), danh sách quyền (`permissionName` nối ", "; Chủ sở hữu → "Mọi quyền"; rỗng → "Không có quyền nào"), "{n} người";
    - `!canEdit` → nhãn *Chỉ xem*; vai trò tự tạo thiếu quyền thêm dòng "Bạn không có đủ mọi quyền trong vai trò này nên chỉ xem được.";
    - menu *Sửa vai trò*, *Xoá vai trò*; `deleteBlockedReason = 'HasHolders'` → *Xoá vai trò* khoá và dòng "Vai trò còn {n} người giữ. Đổi vai trò của họ hoặc gỡ họ khỏi tổ chức trước." (lý do cạnh nút, bảng *System* § Nút tắt);
    - xoá → `InlineConfirm` "Xoá vai trò {name}?", detail "{n} lời mời đang chờ với vai trò này sẽ không dùng được nữa." chỉ khi `pendingInvitationCount > 0`, confirmLabel "Xoá vai trò".
  - `RoleSheet` (props `role?: RoleDto`; tạo khi không có):
    - ô *Tên vai trò*; danh sách ô tick theo `permissionCatalog`, mỗi ô có tên và câu mô tả (FR-65, AC-294, AC-327); ô của quyền mà `auth.hasPermission(code)` sai bị khoá; dòng "Chỉ chọn được quyền mà bạn có.";
    - nút *Tạo vai trò* / *Lưu*;
    - ô tên `maxlength="100"`; lỗi dưới ô tên: `OrganizationRoleNameAlreadyExists` ("Tên này đã có trong tổ chức."), `RoleNameReservedForSystemRole` ("Tên này trùng tên một vai trò có sẵn."), tên trống hoặc chỉ khoảng trắng ("Nhập tên vai trò."), `FieldMaxLength` ("Tên vai trò tối đa 100 ký tự."); lỗi khác (`InsufficientRolePermissions`, `OrganizationSystemRoleImmutable`, `UnknownPermissionCode`) → `InlineNotice tone="danger"` trong tấm trượt.
  - `RolesView`: `OrganizationPeopleTabs active="roles"` (nút *Tạo vai trò*) + danh sách. Toast "Đã tạo vai trò {name}", "Đã lưu vai trò {name}", "Đã xoá vai trò {name}". 409 `RoleHasHolders` khi xoá → dòng lý do trong `RoleRow` rồi tải lại danh sách.
  - Locale `roles.*` (giữ nguyên khối `roles.systemKind.*` của phần 4 Task 1: test backend `Names_MatchFrontendLocales` đọc nó), `permissions.*` theo bảng trên, `errorCodes` cho sáu mã nói trên (en: "That name is already used in this organization.", "That name belongs to a built-in role.", "Built-in roles can't be edited or deleted.", "Unknown permission.", "This role is still held by members.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('permission names')
it.each(['vi', 'en'])('gives every permission code a %s name and a description of 1 to 120 characters') // AC-294, AC-327
it('includes feedbackhub.admin in Permissions')
describe('RoleRow')
it('names system roles in the interface language and marks them view-only')
it('explains a view-only custom role the caller lacks permissions for')
it('disables Delete with the holders line when the role has holders')                     // AC-248, AC-251
it('mentions pending invitations in the delete confirmation only when there are some')    // AC-250
describe('RoleSheet')
it('shows each permission with its description and locks the ones I do not have')       // AC-156
it('shows the duplicate-name message under the name')                                     // AC-290, AC-326
it('shows the reserved-name message under the name')                                      // AC-291
it('requires a name before calling the server')                                           // AC-287
it('limits the name to 100 characters and accepts exactly 100')                           // AC-288, AC-289
describe('RolesView')
it('creates a role and adds it to the list')                                              // AC-292
it('deletes a role after the inline confirmation')
it('shows the not-found or no-permission state when the role list fails to load')         // AC-300, AC-313
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- RolesView RoleRow RoleSheet permission-label permissions organization-people`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *Roles* (1280: panel *Tạo vai trò*; 834: *Sửa vai trò* thành tấm trượt toàn chiều ngang; 390: xác nhận xoá, tạo vai trò chưa nhập tên, tên dài được rút gọn bằng `truncate` kèm `title`).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(roles): manage custom roles with a description for every permission`

### Task 7: Trang công khai *Nhận lời mời* `/invite` (*InviteAccept*)

**Files:**
- Create: `frontend/src/views/InviteView.vue`
- Modify:
  - `frontend/src/stores/auth.ts` (`registerWithInvitation`, `externalLogin` trả cả `accountCreated`)
  - `stores/auth.ts` `logout(data?, options?: { redirect?: string })` (truyền `{ redirect }` cho `reloadToLogin(query)` có sẵn)
  - `frontend/src/views/LoginView.vue` (không đổi hành vi; chỉ khi cần để `redirect=/invite` giữ nguyên)
  - `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Test: `frontend/tests/views/InviteView.test.ts`, `frontend/tests/unit/stores/auth.test.ts`

**Interfaces:**
- Consumes: API-20, API-21, API-22, API-23, `useGoogleAuth`, `useMicrosoftAuth`, `GoogleLoginButton`, `MicrosoftLoginButton`, `useOrganizationSwitch`, Task 2 (`organization-suspended-notice`).
- Produces:
  - Route `invite` (`/invite`), cấp cao nhất, không `meta` (không `requiresAuth`, không `guestOnly`).
  - Store `auth`: `registerWithInvitation(data: { token, name, username, password, phone?, position?, address? }): Promise<AuthResponse>` gọi API-22 rồi `setAuth`; `externalLogin` giữ chữ ký, phản hồi có `accountCreated`.
  - `InviteView`, thứ tự khi mở:
    1. Thêm `<meta name="robots" content="noindex">` và `<meta name="referrer" content="no-referrer">` vào `document.head`; gỡ khi rời trang.
    2. Có `route.query.token` → lưu `sessionStorage['invitation-token']`, rồi `history.replaceState` bỏ `token` khỏi URL **trước** mọi request. Không có trong query → đọc `sessionStorage`. Không có ở đâu → trạng thái *không còn dùng được*.
    3. API-20 với mã → dựng trạng thái theo `InvitationLookupDto.status`.
  - Trạng thái (chữ theo bảng *InviteAccept*):
    - `Valid` + chưa đăng nhập: "Lời mời vào {org}", "Bạn được mời với vai trò {role}. Lời mời có hạn đến {time}."; form *Họ tên*, *Tên đăng nhập*, *Email* (chỉ đọc, điền `email`, gợi ý "Email theo lời mời, không đổi được.", AC-304), *Mật khẩu* (gợi ý "Từ {min} đến {max} ký tự."); *Tạo tài khoản và tham gia*; "Hoặc tiếp tục với" + Google, Microsoft; "Đã có tài khoản? Đăng nhập" → `login?redirect=/invite`. Chỉ đúng bốn điều về tổ chức (AC-309, AC-332).
    - `Valid` + đã đăng nhập: thẻ tài khoản (chữ viết tắt, tên, email) + *Chấp nhận lời mời* → API-21 → `switchOrganization(organizationId)` (AC-177).
    - `WrongAccount`: "Lời mời này không dành cho tài khoản bạn đang dùng" + *Đăng xuất để dùng tài khoản khác* → `auth.logout(undefined, { redirect: '/invite' })`. Không tên tổ chức, vai trò hay email (AC-305, AC-306, AC-308, AC-348).
    - `EmailNotConfirmed`: "Cần xác minh email trước khi dùng lời mời", "Mở thư xác minh trong hộp thư của bạn rồi mở lại lời mời này." + *Gửi lại email xác thực* (AC-351).
    - `Expired`: "Lời mời đã hết hạn", "Lời mời hết hạn lúc {time}. Nhờ người quản lý của tổ chức gửi lại lời mời." (AC-307).
    - `NoLongerValid`: "Lời mời này không còn dùng được", "Lời mời có thể đã bị thu hồi, hoặc bạn đã nhận được lời mời mới hơn. Hãy mở email mời mới nhất." (AC-59, AC-117).
    - `OrganizationSuspended`: "{org} đang tạm ngừng hoạt động", "Lời mời vẫn còn hạn đến {time}. Bạn có thể nhận lời khi tổ chức hoạt động lại." (AC-151).
  - Sau đăng ký (API-22): `invitationOutcome = 'Joined'` → xoá `invitation-token`, `reloadTo` trang chủ (đã ở trong tổ chức, AC-57, AC-182); `'OrganizationSuspended'` → ghi `organization-suspended-notice` với tên tổ chức, xoá mã, `reloadTo` trang chào (AC-333).
  - Google/Microsoft: `externalLogin` xong → `accountCreated` → gọi API-21 ngay (AC-178); tài khoản có sẵn → tra lại API-20 và hiện thẻ *Chấp nhận lời mời* (AC-177). API-21 trả 409 hay 403 → chuyển sang đúng trạng thái trên theo mã (`InvitationWrongAccount` → `WrongAccount`, `InvitationExpired` → `Expired`, `InvitationNoLongerValid` → `NoLongerValid`, `InvitationOrganizationSuspended` → `OrganizationSuspended`, `EmailNotConfirmed` → `EmailNotConfirmed`).
  - Lỗi đăng ký: ánh xạ như `RegisterView.vue` (`AccountUsernameAlreadyExists` dưới ô tên đăng nhập, lỗi kiểm hợp lệ theo ô); `AccountEmailAlreadyExists` → `InlineNotice tone="warning"` "Email này đã có tài khoản. Hãy đăng nhập để nhận lời mời." kèm link đăng nhập; 429 → `InlineNotice tone="danger"` với khoá mới `errors.tooManyRequests` (vi "Bạn thao tác quá nhanh. Đợi một chút rồi thử lại.", en "Too many attempts. Wait a moment and try again.").
  - Xong luồng (vào tổ chức, hoặc trạng thái cuối không dùng được nữa) thì xoá `invitation-token`.
  - Locale `invite.*`, `errorCodes.InvitationExpired`, `InvitationNoLongerValid`, `InvitationWrongAccount`, `InvitationOrganizationSuspended` (chữ trùng tiêu đề trạng thái).

- [ ] **Step 1: Viết test thất bại**

```ts
describe('InviteView')
it('removes the token from the address bar before calling the lookup')            // Review Focus 4
it('keeps using the token from sessionStorage after a reload or a login round trip')
it('adds noindex and no-referrer meta tags while open and removes them after')
it('shows only organization, role, expiry and the read-only email to a visitor')   // AC-304, AC-309
it('registers, then reloads into the organization when the outcome is Joined')     // AC-57, AC-182
it('registers, then goes to welcome with the suspended notice when the organization is suspended') // AC-333
it('accepts for a signed-in invitee and switches into the organization')          // AC-177
it('accepts right after an external sign-in that created the account')           // AC-178
it('shows only the wrong-account message and a sign-out button')                  // AC-305, AC-306
it('shows the expired message without saying wrong account')                      // AC-307
it('shows the verify-email message with a resend button')                         // AC-351
it('shows the no-longer-valid message when the token is missing or unknown')      // AC-59
it('shows the suspended message with the expiry')                                 // AC-151
it('renders an organization name containing HTML as text')                        // Review Focus 2
describe('auth store')
it('sets the session from register-with-invitation')
it('logs out to the login page with the given redirect')
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- InviteView stores/auth`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *InviteAccept* (1280: form tạo tài khoản và bốn thẻ trạng thái; 834: đã đăng nhập đúng tài khoản; 390: tên đăng nhập trùng, đang dùng tài khoản khác), `AmbientBackdrop strength="full"`. `api/client.ts` không refresh-và-gửi-lại cho `/api/invitations/lookup` (endpoint ẩn danh; một 401 ở đó không có nghĩa phiên hết hạn). `register-with-invitation` đã được miễn vì nằm dưới `/api/auth/`; `/api/invitations/accept` giữ hành vi refresh bình thường.

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(invitations): open, register with and accept an invitation from its link`

### Task 8: *Các tổ chức* của admin hệ thống (*SysOrgs*)

**Files:**
- Create: `frontend/src/views/system/SystemOrganizationsView.vue`
- Modify: `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Test: `frontend/tests/views/system/SystemOrganizationsView.test.ts`

**Interfaces:**
- Consumes: API-24, API-25, API-26, Task 3 (`DeactivateOrganizationDialog`), phần 7 (`InlineConfirm`, `AppSheet`, `StatusLabel`).
- Produces:
  - Route `system-organizations` (`/system/organizations`), con của layout: `meta { requiresAuth, systemAdministrator: true, sidebar: { labelKey: 'nav.systemOrganizations', icon: BuildingCommunity, group: 'system', order: 10 } }`. Không `requiresOrganization`: admin hệ thống mở được từ mọi ngữ cảnh, kể cả khi `org_id` rỗng; lúc đó rail chỉ hiện mục nhóm `system` (kiểm trong Step 1). Trang chào dẫn tới đây bằng link *Các tổ chức* (Task 2).
  - Trang:
    - tiêu đề "Các tổ chức", dòng "{n} tổ chức" (`totalCount`); lazy list cỡ 50, *Xem thêm*; giữ thứ tự máy chủ trả (AC-302);
    - mỗi dòng: chữ viết tắt, tên, "{n} thành viên", `StatusLabel tone="neutral"` *Đã vô hiệu* khi `isDeactivated`;
    - đang hoạt động: *Vô hiệu* → `DeactivateOrganizationDialog` với `reasonRequired = true`, `variant = 'system'` → API-25 → toast "Đã vô hiệu {name}", dòng cập nhật `isDeactivated = true`;
    - đã vô hiệu: *Kích hoạt lại* → `InlineConfirm tone="neutral"` "Kích hoạt lại {name}?" / "Thành viên vào lại được ngay. Mọi thứ trở lại như trước, trừ những gì đã hết hạn." / "Kích hoạt lại" → API-26 → toast "Đã kích hoạt lại {name}" (AC-106, AC-128);
    - 390 px: nút thao tác của dòng mở `AppSheet` từ dưới, tiêu đề là tên và "{n} thành viên" (bảng *SysOrgs* § Thao tác mở từ dưới lên);
    - 404 khi thao tác → toast `common.notFoundReloaded` và tải lại; 403 `SystemAdministratorRequired` khi tải → `AccessProblemState kind="forbidden"`.
  - Sau API-25 trên chính tổ chức đang làm việc, request kế tiếp nhận `OrganizationSuspended` và Task 2 lo phần còn lại; trang này không xử lý riêng.
  - Locale `systemOrganizations.*`, `nav.systemOrganizations` (vi "Các tổ chức", en "Organizations"), `errorCodes.SystemAdministratorRequired` (vi "Chỉ admin hệ thống FeedbackHub làm được việc này.", en "Only a FeedbackHub system administrator can do this.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('SystemOrganizationsView')
it('lists organizations with member counts and the deactivated label in server order')  // AC-302
it('appends the next page from Show more')
it('requires a reason before deactivating and marks the row after 204')                 // AC-146, AC-147
it('does not show the owner email hint under the reason field')                         // P8-1
it('confirms reactivation below the row and clears the label after 204')                // AC-106, AC-128
it('opens the row actions as a bottom sheet at 390 px')
it('shows the no-permission state on 403 SystemAdministratorRequired')                  // AC-126, AC-131
it('is hidden from the rail and shows the no-permission state for a non-administrator') // routeAccess
it('opens for a system administrator whose token has an empty org_id and the rail shows only system items')   // P8-2
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- SystemOrganizationsView`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *SysOrgs* (1280: đang xác nhận kích hoạt lại, hộp thoại vô hiệu; 834; 390).

- [ ] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(system): list, deactivate and reactivate organizations as a system administrator`

### Task 9: *Nhật ký kiểm toán* (*AuditLog*)

**Files:**
- Create: `frontend/src/views/organization/AuditLogView.vue`, `frontend/src/composables/use-cursor-list.ts`, `frontend/src/lib/audit-sentence.ts`
- Modify: `frontend/src/router/index.ts`, `frontend/src/locales/{vi,en}.ts`, `frontend/src/api/types.ts`
- Test: `frontend/tests/views/organization/AuditLogView.test.ts`, `frontend/tests/unit/composables/use-cursor-list.test.ts`, `frontend/tests/unit/lib/audit-sentence.test.ts`

**Interfaces:**
- Consumes: API-33 (`AuditEntryDto { id, occurredAt, actorKind, actorName, type, parameters }`), Task 5 (`formatDateTime`), Task 6 (`permissionName`), phần 7 (`roleDisplayName`, `EmptyState`, `InlineNotice`).
- Produces:
  - Route `organization-audit-log` (`/organization/audit-log`), con của layout: `meta { requiresAuth, requiresOrganization, permission: Permissions.AuditLogsView, sidebar: { labelKey: 'nav.auditLog', icon: ListDetails, group: 'organization', order: 30 } }` (AC-362).
  - `useCursorList<T extends { id: string | number }>(options: { fetchPage: (cursor: string | undefined) => Promise<{ items: T[]; nextCursor: string | null }> })` → `{ items, loading, loadingMore, hasMore, error, loadMore, reset }`. `hasMore = nextCursor !== null`; bỏ mục trùng `id` khi nối trang (AC-360).
  - `auditSentence(entry: AuditEntryDto, ctx: { t: Function; te: (key: string) => boolean; organizationName: string }): { actor: string; text: string }`:
    - `actor`: `actorKind = 'SystemAdministrator'` → `t('auditLog.systemAdministrator')` ("admin hệ thống FeedbackHub"); `actorName = null` → `t('auditLog.deletedAccount')` ("một tài khoản đã xoá"); ngược lại `actorName`.
    - `text`: `te('auditLog.events.' + type)` sai → `t('auditLog.unknownEvent')` ("đã thực hiện một thao tác quản trị"); đúng → `t('auditLog.events.' + type, params)`.
    - `params` dựng **chỉ** từ các khoá khai báo của loại đó (§3.7) cộng `organization = ctx.organizationName`; giá trị trong `parameters` có kiểu `unknown` nên thu hẹp theo kiểu khai báo của khoá (chuỗi, mảng chuỗi, `RoleRefDto`, mảng `RoleRefDto`), sai hình dạng thì coi như loại lạ: chuỗi giữ nguyên; `RoleRefDto` → `roleDisplayName`; mảng `RoleRefDto` → nối ", "; `permissionCodes`, `permissionsAdded`, `permissionsRemoved` → `permissionName` nối ", ", mảng rỗng → `t('auditLog.noPermissions')`. Không bao giờ in `parameters` thô.
    - `RoleUpdated` ghép từ ba phần không rỗng: "đã sửa vai trò \"{nameBefore}\"" + (", đổi tên thành \"{nameAfter}\"" khi khác) + (", thêm quyền {permissionsAdded}" khi có) + (", bỏ quyền {permissionsRemoved}" khi có).
  - Câu (vi; en tương ứng ở `en.ts`), người làm đứng trước:

    | `type` | vi |
    |---|---|
    | `InvitationSent` | đã gửi lời mời tới {email} (vai trò {role}) |
    | `InvitationRevoked` | đã thu hồi lời mời tới {email} (vai trò {role}) |
    | `InvitationDeleted` | đã xoá lời mời đã hết hạn tới {email} (vai trò {role}) |
    | `MemberJoined` | ({email}) đã vào {organization} với vai trò {role} |
    | `MemberRolesChanged` | đã đổi vai trò của {accountName} ({email}): {rolesBefore} → {rolesAfter} |
    | `MemberRemoved` | đã gỡ {accountName} ({email}) khỏi tổ chức, vai trò trước đó: {roles} |
    | `MemberLeft` | ({email}) đã rời tổ chức |
    | `RoleCreated` | đã tạo vai trò "{role}" với quyền {permissionCodes} |
    | `RoleDeleted` | đã xoá vai trò "{role}" |
    | `OrganizationRenamed` | đã đổi tên tổ chức: "{nameBefore}" → "{nameAfter}" |
    | `OrganizationDeactivated` | đã vô hiệu hoá tổ chức |
    | `OrganizationReactivated` | đã kích hoạt lại tổ chức |

  - `AuditLogView`:
    - tiêu đề "Nhật ký kiểm toán"; mục mới nhất trước, chia theo ngày địa phương: "Hôm nay · {date}", "Hôm qua · {date}", còn lại "{date}";
    - mỗi dòng: chữ viết tắt người làm (`FH` cho admin hệ thống), câu (tên in đậm + phần còn lại), giờ `HH:mm` trong thẻ `<time datetime="{occurredAt}">`; ở 390 px giờ xuống dòng dưới (AC-359);
    - *Xem thêm* khi `hasMore` (AC-360); đang tải đầu → khung xương (bảng *AuditLog* § Đang tải);
    - không có mục → `EmptyState` "Chưa có mục nào" (AC-364);
    - lỗi tải đầu: `accessProblemOf` khác `null` → `AccessProblemState` (AC-366); còn lại `InlineNotice tone="danger"` "Không tải được nhật ký kiểm toán." + *Thử lại*;
    - không có ô lọc, ô tìm kiếm, IP hay thiết bị (AC-365, AC-371);
    - chỉ nội suy văn bản của vue-i18n: không `v-html`, không `<i18n-t>` với HTML (B6).
  - Đổi tổ chức tải lại trang (`useOrganizationSwitch`), nên danh sách và con trỏ cũ không còn; không thêm xử lý riêng (TECH00 §6.8 bước 2).
  - Locale `auditLog.*`, `nav.auditLog` (vi "Nhật ký kiểm toán", en "Audit log"), `errorCodes.InvalidCursor` (vi "Không tải được phần tiếp theo. Hãy tải lại trang.", en "Couldn't load more. Reload the page.").

- [ ] **Step 1: Viết test thất bại**

```ts
describe('useCursorList')
it('loads the first page, then the next one with nextCursor')
it('stops when nextCursor is null and skips duplicated ids')
describe('auditSentence')
it.each([...12 loại])('builds the %s sentence in vi and en')                   // AC-367
it('names a system administrator actor without a name')                       // AC-368
it('names a deleted account actor')
it('uses the generic sentence for an unknown type and never prints raw parameters') // §6.8
it('ignores parameters the catalogue does not declare for the type')
it('lists permission names in the interface language')
it('composes RoleUpdated from rename, added and removed parts')
describe('AuditLogView')
it('shows newest first, grouped by local day with the time of each entry')     // AC-359 (TZ=UTC fixtures)
it('appends older entries from Show more')                                     // AC-360
it('shows the empty state when there are no entries')                          // AC-364
it('has no filter or search control and no IP or device text')                // AC-365, AC-371
it('shows the email in entries to a viewer without members.manage')            // AC-335
it('renders an actor name containing an img tag as text with no img element')  // Review Focus 2, B6
it('shows the no-permission state on 403 OrganizationAccessDenied')            // AC-362, AC-366
it('offers Retry after a load failure')
```

- [ ] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- AuditLogView use-cursor-list audit-sentence`
Expected: FAIL.

- [ ] **Step 3: Cài đặt** theo Interfaces và bảng *AuditLog* (1280, 834, 390; trạng thái đang tải, không tải được, trống).

- [ ] **Step 4: Chạy lại, phải qua**

Run: `TZ=UTC bun run --cwd frontend test:run -- AuditLogView use-cursor-list audit-sentence`, rồi bốn lệnh kiểm.
Expected: PASS.

- [ ] **Step 5: Commit** `feat(audit-log): show the organization's audit log as plain sentences`

### Task 10: E2E cho luồng nhiều trang, feature map, gate

**Files:**
- Create: `frontend/e2e/organizations.spec.ts`, `frontend/e2e/invitation.spec.ts`, `frontend/e2e/mailpit.ts`
- Modify: `frontend/e2e/fixtures.ts`, `frontend/.env.e2e.example` (`E2E_MAILPIT_URL=http://localhost:8025/`), `docs/feature-map.md`

**Interfaces:**
- Consumes: tài khoản `seed-dev` (`e2e-admin` là Chủ sở hữu tổ chức "Dev", `e2e-user` là thành viên), Mailpit của `scripts/dev-up.sh` (API `GET {E2E_MAILPIT_URL}api/v1/search?query=to:"<email>"`, `GET …/api/v1/message/{ID}`).
- Produces: `latestInvitationLink(email: string): Promise<string>` trong `e2e/mailpit.ts` (đọc thư mới nhất gửi tới `email`, lấy link `#/invite?token=`; thử lại tối đa 10 lần cách 500 ms).

- [ ] **Step 1: Viết spec**

```ts
// e2e/organizations.spec.ts (locale en)
test('creates an organization with the suggested identifier and lands inside it')   // AC-56, AC-281
// tên 'E2E Org <timestamp>' → ô định danh 'e2e-org-<timestamp>'; sau tạo, bộ chọn hiện tên mới
test('switches organizations from the top-bar switcher')                            // AC-04
test('the only owner of a new organization cannot leave it')                         // AC-277
test('a member sees no Invitations, Roles or Settings and opening them shows No permission') // AC-298

// e2e/invitation.spec.ts
test('an invitee registers from the emailed link and joins Dev as Member')          // AC-57, AC-182, AC-304
// e2e-admin mời 'e2e+<timestamp>@test.local' vai trò Member → Mailpit → context ẩn danh mở link
// → URL không còn token= → đăng ký → vào Dev → e2e-admin thấy người đó ở tab Thành viên
// → nhật ký kiểm toán có mục gửi lời mời và mục vào tổ chức → e2e-admin gỡ người đó (dọn dữ liệu)
test('a signed-in different account sees only the wrong-account message')           // AC-306
```

- [ ] **Step 2: Chạy e2e cục bộ**

Run: `scripts/dev-up.sh`, rồi `bun run --cwd frontend test:e2e`
Expected: PASS (spec cũ và hai spec mới).

- [ ] **Step 3: Cập nhật `docs/feature-map.md`**

Thêm các mục theo cấu trúc hiện có (PRD, Vào bằng, Frontend, Backend, Test đã có, Dữ liệu test, Cách kiểm tra):
- *Tổ chức: tạo, chuyển, trang chào, tạm ngừng*;
- *Quản lý tổ chức*;
- *Thành viên và lời mời*;
- *Nhận lời mời*;
- *Vai trò tự tạo*;
- *Admin hệ thống: các tổ chức*;
- *Nhật ký kiểm toán*.

Gỡ mọi nhắc tới `OrganizationsView`, `OrganizationMembersDialog`, `OrganizationRolesDialog`.

- [ ] **Step 4: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [ ] **Step 5: Commit** `test(e2e): cover creating organizations and joining by invitation; map module 00 screens`

---

## Lệch thiết kế khai trong PR

- Không hiện dòng "Gửi kèm email báo các Chủ sở hữu." dưới ô lý do cho tới khi TECH05 gửi thư thật (đã chốt 1).
- Sau khi tự vô hiệu tổ chức đang làm việc, người làm về trang chào không có thông báo (đã chốt 3). Link *Các tổ chức* và câu "Liên hệ admin hệ thống…" đã có trên bảng *OrgContext* (người dùng duyệt 2026-10-23); khổ máy tính bảng dùng chung bố cục khổ máy tính.
- Câu nhật ký `MemberRemoved` thêm vai trò trước đó vì danh mục §3.7 khai `roles[]` cho loại này.

## Câu hỏi mở

- **Đã chốt (SA, 2026-10-23):** Ẩn dòng "Gửi kèm email báo các Chủ sở hữu." dưới ô lý do cho tới khi TECH05 gửi email thật. Ô lý do vẫn có: bắt buộc với admin hệ thống (BR-54), không bắt buộc với người khác (BR-52). TECH05 đưa dòng này trở lại (Task 3, Task 8).
- **Đã chốt (SA, 2026-10-23):** Trang chào hiện link chữ *Các tổ chức* tới `/system/organizations` mỗi khi `isSystemAdministrator`, không chỉ khi admin chưa có tổ chức nào. Máy chủ vẫn kiểm policy `SystemAdministrator` ở API-24–26; rail mở được với `org_id` rỗng và chỉ hiện mục hệ thống (Task 2, Task 8).
- **Đã chốt (SA, 2026-10-23):** Sau khi tự vô hiệu tổ chức đang làm việc (API-05 trả 204), chuyển về trang chào, không ghi thông báo vào `sessionStorage`. Bộ chọn hiện tổ chức đó mờ có nhãn, hoặc câu giải thích BR-64 nếu mọi tổ chức đều tạm ngừng (Task 3).

---

### Task 11: Cập nhật hướng dẫn sử dụng theo giao diện mới (SA, 2026-10-26)

Thêm sau pre-flight (P11). Phần 7 (F16) và `docs/feature-map.md` đã giao việc này cho phần 8.

- **Sửa** (vi và en):
  - `account/appearance.md`: bỏ chế độ menu;
  - `getting-started/interface.md`: rail, thanh trên, bộ chọn tổ chức;
  - `organizations/create.md`, `organizations/switch.md`;
  - các trang thành viên, vai trò và vô hiệu tổ chức, theo màn hình của Task 3–6.
- **Selector chụp ảnh:** `frontend/user-guide/tools/sections/**/getting-started.capture.ts` và các tệp capture khác dùng testid đã bị bỏ (`app-sidebar`, `app-header-title/-toggle/-settings`).
- **Kiểm:** `bun run --cwd frontend/user-guide/site test` và `build`.
- **Chụp lại ảnh** (`bun run --cwd frontend guide:capture`): bước cuối, chỉ khi người dùng cho chạy `scripts/dev-up.sh`. Nếu chưa được phép, PR ghi rõ ảnh còn cũ; việc này không chặn merge.
- **Commit:** `docs(guide): describe the new app shell and organization screens`.

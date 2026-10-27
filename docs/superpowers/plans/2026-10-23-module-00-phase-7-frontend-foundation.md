# Module 00 — Phần 7: Nền frontend "Lớp kính", khung ứng dụng, bảng hồ sơ — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Duyệt:** solution-architect thay người dùng (người dùng uỷ quyền 2026-10-23). **Push/PR:** người dùng cho phép (2026-10-23): sau khi phần 8 xong và `scripts/test-gate.sh` qua, push `feature/module-00` và mở một PR vào `dev` cho cả module 00; không bao giờ vào `main`.

**Goal:** Đưa design system "Lớp kính · WeatherPlus" đã duyệt vào `frontend/` và dựng phần nền mà mọi màn hình module 00 (phần 8) dùng chung:
- token sáng/tối một nguồn, font tự host, kính và ánh nền;
- các thành phần dùng chung của bảng *Components* (thông báo trong trang, khối xác nhận dưới dòng, tấm trượt, trạng thái trống, nhãn);
- khung ứng dụng chỉ còn rail (rộng, biểu tượng, menu trượt trên điện thoại), thanh trên, bộ chọn tổ chức, nhóm "Tổ chức" và "Hệ thống";
- `ProfilePanel.vue` thay `ProfileDialog.vue`, giữ nguyên mọi hành vi của phần 1;
- `AccessProblemState`: mẫu chung "Không tìm thấy" / "Không đủ quyền" trong khung có rail.

**Architecture:**
- **Token:** `src/theme/tokens.ts` là nguồn duy nhất. `tailwind.css` (`@theme`, `.dark`) và `naive-theme.ts` lấy giá trị từ đây; một test unit so `tailwind.css` với `tokens.ts` (thay ghi chú "keep in sync").
- **Quyền vào route:** một hàm thuần `routeAccess(meta, ctx)` (`lib/route-access.ts`) dùng chung cho router guard, menu rail và `AppLayout`. Thiếu quyền thì giữ URL và hiện `AccessProblemState` thay vì đẩy về trang chủ.
- **Khung:** `AppLayout` = `AppRail` (rộng ≥ 1200 px, biểu tượng 768–1199 px, menu trượt < 768 px) + `AppTopBar` (bộ chọn tổ chức, *Đi tới…*, chuông, menu tài khoản) + nội dung. Bỏ năm chế độ menu và hai màu menu (QĐ-28).
- **Hồ sơ:** `ProfilePanel.vue` dùng `AppSheet` (bảng bên phải ở ≥ 1200 px, tấm trượt từ dưới ở khoảng nhỏ hơn); logic, testid và test của `ProfileDialog.vue` chuyển nguyên sang.

**Tech Stack:** Vue 3 + Vite + TypeScript, Pinia, naive-ui 2.44, Tailwind v4, vue-i18n, vitest + `@vue/test-utils` + MSW, Playwright, `@fontsource-variable/plus-jakarta-sans`, `@fontsource-variable/jetbrains-mono`.

**Spec:**
- `docs/tech/00-nen-tang.md` v1.0: §8.1 (toàn bộ), §8.2 (hàng *Khung ứng dụng*, *Mẫu lỗi chung*, *Hồ sơ*; ba gạch đầu dòng cuối về tên vai trò hệ thống và lỗi), §6.5 bước 4 (tổ chức tạm ngừng trong bộ chọn), §6.7 bước 4 (bảng hồ sơ), §11 (hàng *Mẫu lỗi chung*, *Bảng hồ sơ*, *Bố cục chỉ còn rail*, *Token design system*, *Bộ chọn tổ chức, trang chào*), §12 QĐ-24, QĐ-28.
- `docs/prd/00-tong-quan-va-nen-tang.md` v1.4: FR-03, FR-30, FR-66, FR-67; AC-04, AC-05, AC-73, AC-298, AC-299, AC-310, AC-317, AC-318 (phần giao diện), AC-342, AC-352–AC-358 (phần giao diện), AC-362 (phần mục rail).
- Thiết kế đã duyệt (https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn), sáng và tối, 1280/834/390 px: *SystemLight/Dark*, *ComponentsLight/Dark*, *AppShell*, *OrgContext* (mẫu chung *Không tìm thấy*, *Không đủ quyền*), *ProfileEmail*.

Màn hình cụ thể của module 00 thuộc phần 8 (`2026-10-23-module-00-phase-8-frontend-screens.md`).

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
| **7 (tệp này)** | Frontend: design system "Lớp kính", khung ứng dụng, `ProfilePanel`, `AccessProblemState` | §8.1, §8.2 |
| 8 | Frontend: các màn hình module 00 (tổ chức, thành viên, lời mời, vai trò, admin hệ thống, nhật ký) | §8.2 |

## Global Constraints

- **Điều kiện bắt đầu:** phần 3–6 đã export `shared/openapi/openapi.json` và chạy `bun run --cwd frontend codegen`. Phần này đọc từ hợp đồng mới:
  - `LoginResponse.isSystemAdministrator: bool` (API-27, phần 3), type `AuthResponse` trong `api/types.ts`;
  - `OrganizationDto { id, name, slug, status, myRoleNames[], myRoles: RoleRefDto[], myPermissionCodes[] }` (API-01, phần 3; TECH00 §4.6);
  - `RoleRefDto { id, name, systemKind: 'Owner' | 'Admin' | 'Member' | null }` (phần 3 Task 10; enum ra chuỗi);
  - khoá locale `roles.systemKind.Owner|Admin|Member` (phần 4 Task 1 thêm; test backend `SystemRoleDisplayNamesTests.Names_MatchFrontendLocales` đọc chúng, nên giữ nguyên tên và chữ);
  - frontend sau phần 3–6 đã build và qua test: phần 3 Task 11 bỏ `organizationsStore.deactivate` và nút vô hiệu trong `OrganizationMembersDialog.vue`; phần 4 Task 10 xoá `OrganizationMembersDialog.vue`, `OrganizationRolesDialog.vue`, `RoleForm.vue`, `stores/roles.ts` và hai nút *Thành viên*, *Vai trò* trong `OrganizationsView.vue`; phần 6 Task 6 để orval xoá `api/generated/audit-logs/`. `OrganizationsView.vue`, `OrganizationList.vue`, `OrganizationForm.vue` và route `organizations` vẫn còn (gọi API-01, API-02 còn sống); phần 8 Task 1 thay chúng;
  - `ProfileDto.pendingEmail`, `ProfileDto.hasPassword`, API-29–API-31 (phần 1, đã có);
  - `POST api/auth/switch-organization` (API-28, có sẵn, phần 3 đổi mã lỗi).
- **Nhánh và commit:**
  - Làm trên `feature/module-00`, sau phần 6.
  - Conventional Commits, subject ≤ 100 ký tự, stage từng tệp theo đường dẫn (không `git add -A`).
  - Mỗi message kết thúc bằng đúng một dòng:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    ```
- **Không đụng `backend/`**. Phần này không đổi hợp đồng, không export OpenAPI.
- **Quy ước frontend** (`frontend-conventions.md`): `<script setup lang="ts">`; dùng auto-import, không import tay `ref`, `computed`, `useI18n`, store; store kiểu setup; gọi thẳng factory `get*()` của client orval; không đọc `import.meta.env.VITE_*`; Prettier (`bun run --cwd frontend format`); `bun run --cwd frontend lint:boundaries` phải qua (thành phần trong `components/` không import `views/` hay `layouts/`).
- **Màu:** mọi màu trong `src/**/*.vue` và `src/**/*.ts` đi qua lớp Tailwind theo vai trò (`bg-page`, `text-ink`…) hoặc biến CSS `var(--color-…)`. Mã hex và `rgba(` chỉ được có trong `src/theme/tokens.ts` và `src/assets/styles/tailwind.css`; Task 1 thêm test chặn. Màn hình mới của phần 7–8 không dùng thang cũ `primary-*`, `surface-*`.
- **Khoảng màn hình:**
  - điện thoại `< 768 px`: thanh trên, menu trượt, tấm trượt từ dưới; vùng bấm ≥ 44 px;
  - máy tính bảng `768–1199 px`: rail biểu tượng;
  - máy tính `≥ 1200 px`: rail rộng.
  - Truy vấn: `PHONE_QUERY = '(max-width: 47.9999rem)'`, `DESKTOP_QUERY = '(min-width: 75rem)'` trong `lib/responsive.ts`; breakpoint Tailwind `--breakpoint-desktop: 75rem` (lớp `desktop:`), `md:` (48rem) có sẵn.
- **Kính:** `backdrop-filter` chỉ cho rail, thanh trên và panel. Popover, menu, toast dùng nền đặc `raised`. `@media (prefers-reduced-transparency: reduce)` và `@supports not (backdrop-filter: blur(1px))` đổi mọi lớp kính sang màu đặc và ẩn ánh nền. `prefers-reduced-motion: reduce` tắt chuyển động.
- **Ít hộp thoại, chữ thường:** phần này không thêm `n-modal` nào. Xác nhận nằm ngay dưới dòng (`InlineConfirm`), sửa trong tấm trượt (`AppSheet`). Ghi chú trên màn hình ngắn, lời thường. Sáng và tối đều phải đúng.
- **Chuỗi:** mọi chuỗi người dùng thấy có cả `vi` và `en` trong `frontend/src/locales/{vi,en}.ts` (`en.ts` là schema, `vi.ts` gõ kiểu `LocaleSchema`). Lỗi API hiện theo `code` qua locale, không hiện `detail` của backend (`localization.md` § Cross-Cutting Rule).
- **Test** (`testing.md` § Frontend):
  - tệp test soi gương tệp nguồn (`src/x/y.ts` → `tests/unit/x/y.test.ts`; component → `tests/components/`; layout → `tests/layouts/`);
  - `describe('<tên>')`, `it('<hành vi, tiếng Anh, thì hiện tại>')`;
  - `renderComponent`, `storeIn`, factory trong `tests/factories/`; HTTP chỉ mock bằng MSW `server.use(...)`;
  - chữ của app so qua `i18n.global.t(...)`, không chép chuỗi từ `en.ts`.
- **Lệnh kiểm** sau mỗi task:
  - `bun run --cwd frontend test:run`
  - `bun run --cwd frontend build-only` (sinh `src/typings/`)
  - `bun run --cwd frontend type-check`
  - `bun run --cwd frontend lint:boundaries`

## Review Focus

1. **`localStorage` còn lựa chọn cũ** (`layout-menu-mode = 'horizontal'`, `layout-menu-theme = 'transparent'`, `sidebar-mode = 'full'`): app vẫn hiện bố cục rail, không đọc, không ghi, không lỗi (QĐ-28). Test ở Task 4.
2. **Máy bật giảm trong suốt, hoặc trình duyệt không có `backdrop-filter`:** mọi lớp kính thành màu đặc ghi trên bảng *System*, ánh nền ẩn. Test ở Task 1 (khẳng định quy tắc CSS có trong `tailwind.css`).
3. **Mọi tổ chức của người dùng đều tạm ngừng, hoặc danh sách rỗng:** bộ chọn vẫn mở được; tổ chức tạm ngừng làm mờ, có nhãn *Tạm ngừng*, bấm không gọi `switch-organization` (AC-73). Test ở Task 5.
4. **API trả 404 hoặc 403 kèm `detail`, `title` hay extension chứa tên đối tượng:** `AccessProblemState` chỉ hiện câu chung theo locale; DOM và `document.title` không chứa chuỗi đó (BR-45). Test ở Task 7.
5. **Mật khẩu hiện tại sai khi đổi email trong `ProfilePanel`:** phiên giữ nguyên (không refresh, không đăng xuất), lỗi nằm dưới ô mật khẩu; đóng rồi mở lại bảng thì ô mật khẩu trống và hồ sơ tải lại từ máy chủ. Test ở Task 6.

---

### Task 1: Token "Lớp kính" một nguồn, font tự host, kính và ánh nền

**Files:**
- Create: `frontend/src/theme/tokens.ts`
- Modify: `frontend/src/assets/styles/tailwind.css`, `frontend/src/theme/naive-theme.ts`, `frontend/src/assets/styles/main.scss`, `frontend/src/main.ts` (import font), `frontend/package.json` (+ `bun.lock`)
- Test: `frontend/tests/unit/theme/tokens.test.ts`, `frontend/tests/unit/theme/naive-theme.test.ts`, `frontend/tests/unit/theme/no-raw-colors.test.ts`

**Interfaces:**
- Produces:
  ```ts
  export type ThemeMode = 'light' | 'dark'
  export interface ThemeColors { /* mỗi khoá dưới đây: string */ }
  export const tokens: {
    light: { color: ThemeColors; glass: GlassTokens; ambient: AmbientTokens }
    dark: { color: ThemeColors; glass: GlassTokens; ambient: AmbientTokens }
    legacy: { light: Record<string, string>; dark: Record<string, string> } // thang primary-*, surface-* cũ
    radius: { sm: '8px'; md: '12px'; lg: '16px'; xl: '22px'; full: '999px' }
    motion: { quick: '160ms ease'; panel: '240ms var(--ease-spring)'; pop: '320ms var(--ease-spring)'; spring: 'cubic-bezier(0.34, 1.36, 0.64, 1)' }
    font: { sans: "'Plus Jakarta Sans Variable', system-ui, sans-serif"; mono: "'JetBrains Mono Variable', ui-monospace, monospace" }
  }
  export function cssVariables(mode: ThemeMode): Record<string, string> // '--color-page' → '#E9F1FA'
  ```
  - `GlassTokens = { thin: { tint: string; solid: string; blur: '24px' }; thick: { tint: string; solid: string; blur: '30px' } }`, `AmbientTokens = { blobs: [string, string, string]; full: number; soft: number }`.
  - Tên lớp Tailwind theo vai trò (biến `--color-<tên>`): `page`, `panel`, `panel-solid`, `panel-thin`, `panel-thin-solid`, `raised`, `sunken`, `line`, `control`, `focus`, `ink`, `ink-muted`, `ink-disabled`, `accent`, `accent-hover`, `accent-pressed`, `on-accent`, `accent-soft`, `on-accent-soft`, `secondary-hover`, `secondary-pressed`, `ghost-hover`, `ghost-pressed`, `success`, `success-soft`, `warning`, `warning-soft`, `danger`, `danger-soft`, `danger-soft-hover`, `danger-solid`, `danger-solid-hover`, `danger-solid-pressed`, `on-danger-solid`, `info`, `info-soft`, `priority-high`, `brand`, `leaf`, và 9 cặp trạng thái `status-<key>` / `status-<key>-soft` (`new`, `accepted`, `need-info`, `in-progress`, `in-review`, `resolved`, `closed`, `duplicate`, `rejected`).
  - Utility CSS: `glass-thin`, `glass-thick`; biến `--ambient-1..3`, `--ambient-full`, `--ambient-soft`, `--shadow-panel` (sáng `rgba(0,39,67,0.08)`, tối `rgba(0,0,0,.40)`).

Giá trị (chép từ *SystemLight* và *SystemDark*, không suy ra):

| Khoá | Sáng | Tối |
|---|---|---|
| `page` | `#E9F1FA` | `#0E1124` |
| `panel` / `panel-solid` | `rgba(255,255,255,.78)` / `#FAFCFE` | `rgba(36,33,68,.78)` / `#1F1D3D` |
| `panel-thin` / `panel-thin-solid` | `rgba(255,255,255,.55)` / `#F5F9FD` | `rgba(30,28,58,.55)` / `#171730` |
| `raised` · `sunken` | `#FFFFFF` · `#EEF1F7` | `#2C2950` · `#15132B` |
| `line` · `control` · `focus` | `rgba(28,26,51,.10)` · `#7C7896` · `#5B3FD9` | `rgba(255,255,255,.10)` · `#807CA6` · `#E3DBFF` |
| `ink` · `ink-muted` · `ink-disabled` | `#1C1A33` · `#56536F` · `#8E8BA6` | `#ECEAF7` · `#A9A5C6` · `#6E6A90` |
| `accent` · `-hover` · `-pressed` · `on-accent` | `#4F3D91` · `#43337F` · `#37296B` · `#FFFFFF` | `#B4A6F2` · `#C4B9F6` · `#A394E6` · `#1B1242` |
| `accent-soft` · `on-accent-soft` | `#ECE8F7` · `#3E2F7A` | `#342D60` · `#D4CBFA` |
| `secondary-hover` · `-pressed` | `#F1F3F8` · `#E6E9F1` | `#36325E` · `#2A2649` |
| `ghost-hover` · `-pressed` | `#ECE8F7` · `#DFD8F2` | `#302A57` · `#3A3368` |
| `success` / `-soft` | `#256126` / `#DCF1D8` | `#9EDE99` / `#1B3B22` |
| `warning` / `-soft` | `#7A4A00` / `#FFE8C2` | `#F7C873` / `#3D2F17` |
| `danger` / `-soft` / `-soft-hover` | `#A12A26` / `#FBDCDC` / `#F7C9C9` | `#FF9C97` / `#47202A` / `#57252F` |
| `danger-solid` · `-hover` · `-pressed` · `on-danger-solid` | `#B4232A` · `#9C1C23` · `#84161C` · `#FFFFFF` | `#FF8A84` · `#FFA19C` · `#F2716B` · `#2A0A0D` |
| `info` / `-soft` | `#14598F` / `#DCEDFB` | `#9CCFF7` / `#173452` |
| `priority-high` · `brand` · `leaf` | `#C2410C` · `#4F3D91` · `#8CC640` | `#FF8A4C` · `#4F3D91` · `#8CC640` |
| `status-new` / `-soft` | `#3E2F7A` / `#ECE8F7` | `#D4CBFA` / `#342D60` |
| `status-accepted` | `#36334F` / `#E7E9F2` | `#D6D3E8` / `#2F2C4B` |
| `status-need-info` | `#7A4A00` / `#FFE8C2` | `#F7C873` / `#3D2F17` |
| `status-in-progress` | `#14598F` / `#DCEDFB` | `#9CCFF7` / `#173452` |
| `status-in-review` | `#0B5E63` / `#D2F1EF` | `#7FDDD8` / `#12393D` |
| `status-resolved` | `#256126` / `#DCF1D8` | `#9EDE99` / `#1B3B22` |
| `status-closed`, `status-duplicate` | `#56536F` / `#F0F1F6` | `#A9A5C6` / `#27243F` |
| `status-rejected` | `#A12A26` / `#FBDCDC` | `#FF9C97` / `#47202A` |
| Ánh nền (3 đốm), độ đậm đầy đủ / dịu | `#A9D3F5` `#CBBEF0` `#CFE8A8`, `.75` / `.35` | `#3A2C7A` `#14406B` `#2E5418`, `.55` / `.30` |

Chữ (bảng *System* § Chữ): tiêu đề trang 18/700/1.3, tiêu đề mục 15/600/1.3, tiêu đề dòng 14/600/1.4, nội dung 14/400/1.5, thông tin phụ 13/500/1.4, chú thích 12/500/1.4, mã 12/400/1.5 mono. Khai bằng `--text-page-title`, `--text-section`, `--text-row`, `--text-body`, `--text-meta`, `--text-caption`, `--text-code` trong `@theme` (kèm `--line-height` và `--font-weight` tương ứng của Tailwind v4).

- [x] **Step 0: Kiểm điều kiện bắt đầu**

Run: `bun run --cwd frontend build-only && bun run --cwd frontend type-check`
Expected: PASS, cùng `bun run --cwd frontend test:run`. Mỗi phần 3–6 kết thúc với frontend build và qua test (xem *Điều kiện bắt đầu*), nên ở đây không còn caller nào hỏng và phần này không xoá màn hình tổ chức cũ. Lệnh nào lỗi: dừng và báo controller, không tự xoá tệp.

- [x] **Step 1: Viết test thất bại**

```ts
// tests/unit/theme/tokens.test.ts — đọc tailwind.css bằng import '?raw'
describe('tokens')
it('declares every light variable in the @theme block with the token value')
// for [name, value] of Object.entries(cssVariables('light')): @theme block chứa `${name}: ${value};`
it('declares every dark variable in the .dark block with the token value')
it('keeps the legacy primary and surface ramps equal to tokens.legacy')
it('gives glass-thin a 24px blur and glass-thick a 30px blur')
it('switches every glass utility to its solid colour under prefers-reduced-transparency')   // Review Focus 2
it('switches every glass utility to its solid colour when backdrop-filter is unsupported')
it('hides the ambient backdrop under prefers-reduced-transparency')
it('matches the System board for the accent, focus and danger colours')
// light.color.accent === '#4F3D91', focus '#5B3FD9', dangerSolid '#B4232A'; dark.color.accent '#B4A6F2', focus '#E3DBFF'

// tests/unit/theme/naive-theme.test.ts
describe('naive theme')
it('takes primary, error, success, warning and info colours from the tokens in each mode')
it('uses solid raised colours for popover, modal and card')
it('sizes buttons and inputs at 32, 40 and 44 px')     // heightSmall '2rem', heightMedium '2.5rem', heightLarge '2.75rem'
it('uses the self-hosted font families')

// tests/unit/theme/no-raw-colors.test.ts — import.meta.glob('/src/{components,layouts,views,composables,lib,stores}/**/*.{vue,ts}', { query: '?raw', eager: true })
it('keeps hex and rgba colours out of every source file except the brand logo buttons')
// ALLOWED = ['components/GoogleLoginButton.vue', 'components/MicrosoftLoginButton.vue'] (màu thương hiệu do Google, Microsoft quy định)
```

Bốn chỗ hiện có vi phạm được sửa trong Step 3: bóng `rgba(...)` của `ServerErrorScreen.vue`, `GuideAuthView.vue`, `NotFoundView.vue` đổi sang `shadow-[0_20px_55px_var(--shadow-panel)]` với `--shadow-panel` khai trong `tokens.ts`; `NotificationBell.vue` bỏ nhánh `tone === 'on-brand'` (thanh tím ngang bị bỏ ở Task 4) hoặc dùng `var(--color-on-accent)`. `AppSidebar.vue`, `AppHeader.vue` bị xoá ở Task 4; trong lúc chờ, thêm chúng tạm vào `ALLOWED` và Task 4 gỡ ra.

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- tests/unit/theme`
Expected: FAIL (`tokens.ts` chưa có).

- [x] **Step 3: Cài đặt**
  - `bun add --cwd frontend @fontsource-variable/plus-jakarta-sans @fontsource-variable/jetbrains-mono`; import cả hai ở đầu `main.ts`. Không còn tham chiếu Google Fonts hay `Inter` (QĐ-24).
  - `tailwind.css`: bỏ thang `accent-*`, `leaf-*`; `@theme` khai biến theo vai trò của bảng trên (sáng), `.dark` khai lại giá trị tối; thang `primary-*`, `surface-*` giữ nguyên giá trị hiện tại nhưng chép vào `tokens.legacy` để test bao phủ; thêm `--radius-*`, `--ease-spring`, `--breakpoint-desktop: 75rem`, `--font-sans`, `--font-mono`, cỡ chữ ở trên.
  - `@utility glass-thin` / `glass-thick`: `background-color: var(--color-panel-thin|panel)`, `backdrop-filter: blur(24px|30px) saturate(150%|160%)`, viền `1px solid var(--color-line)`. Hai khối `@media (prefers-reduced-transparency: reduce)` và `@supports not (backdrop-filter: blur(1px))` đặt nền `*-solid`, `backdrop-filter: none`, và `[data-ambient] { display: none }`.
  - `naive-theme.ts`: dựng hai object overrides từ `tokens.light`/`tokens.dark` (không chuỗi màu nào viết tay); `common.fontFamily`, `fontFamilyMono`, `borderRadius: '0.75rem'`, `borderRadiusSmall: '0.5rem'`, `heightSmall/Medium/Large` như test; `popoverColor`, `modalColor`, `cardColor` = `raised`; `bodyColor` = `page`; `borderColor` = `control`.
  - `main.scss`: `body` dùng `var(--font-sans)`, `background-color: var(--color-page)`, `color: var(--color-ink)`; bỏ `.dark body` cố định màu cũ. Thanh cuộn dùng `--color-line`.

- [x] **Step 4: Chạy lại, phải qua**

Run: `bun run --cwd frontend test:run -- tests/unit/theme` rồi bốn lệnh kiểm ở Global Constraints.
Expected: PASS.

- [x] **Step 5: Commit** `feat(frontend): add the Lớp kính design tokens as the single source for Tailwind and Naive`

### Task 2: Thành phần dùng chung của bảng *Components*

**Files:**
- Create:
  - `frontend/src/components/InlineNotice.vue`
  - `frontend/src/components/InlineConfirm.vue`
  - `frontend/src/components/AppSheet.vue`
  - `frontend/src/components/EmptyState.vue`
  - `frontend/src/components/StatusLabel.vue`
  - `frontend/src/components/AmbientBackdrop.vue`
  - `frontend/src/composables/use-control-size.ts`
  - `frontend/src/lib/error-message.ts`
  - `frontend/tests/helpers/viewport.ts` (tách bộ giả `matchMedia` đang nằm trong `tests/layouts/AppLayout.test.ts`)
- Modify: `frontend/src/lib/responsive.ts`, `frontend/src/lib/feedback-naive.ts`, `frontend/src/locales/{vi,en}.ts`
- Test: `frontend/tests/components/{InlineNotice,InlineConfirm,AppSheet,EmptyState,StatusLabel}.test.ts`, `frontend/tests/unit/composables/use-control-size.test.ts`, `frontend/tests/unit/lib/error-message.test.ts`, `frontend/tests/unit/lib/feedback-naive.test.ts`

**Interfaces:**
- Consumes: Task 1 (lớp `glass-*`, màu theo vai trò).
- Produces:
  - `lib/responsive.ts`: `PHONE_QUERY = '(max-width: 47.9999rem)'`, `DESKTOP_QUERY = '(min-width: 75rem)'` (giữ `BELOW_LG_QUERY` đến Task 4 rồi xoá nếu không còn ai dùng).
  - `InlineNotice`: props `tone: 'info' | 'success' | 'warning' | 'danger'`, `dismissible?: boolean`; slot mặc định, slot `action`; emit `dismiss`. `role="alert"` khi `danger`, còn lại `role="status"`.
  - `InlineConfirm`: props `message: string`, `detail?: string`, `confirmLabel?: string`, `tone?: 'danger' | 'neutral'` (mặc định `'danger'`), `busy?: boolean`, `showConfirm?: boolean` (mặc định `true`); emit `confirm`, `cancel`. Khi gắn vào, tiêu điểm chuyển vào khối; `Esc` = `cancel`; nút *Huỷ* dùng `common.cancel`; `busy` khoá cả hai nút và hiện vòng tải trên nút xác nhận.
  - `AppSheet`: `v-model:show`, props `title: string`; slot mặc định, slot `footer`. `DESKTOP_QUERY` khớp → `n-drawer` `placement="right"`, rộng `440px`, `show-mask="transparent"` (danh sách phía sau vẫn đọc được); không khớp → `placement="bottom"`, toàn chiều ngang, cao tối đa `90dvh`, bo góc trên `22px`. Nút đóng có `aria-label` `common.close`. Nền `panel-solid` (tấm trượt là lớp nổi, không chồng kính lên kính).
  - `EmptyState`: props `title: string`, `description?: string`; slot `icon`, slot `action`.
  - `StatusLabel`: props `tone: 'neutral' | 'info' | 'success' | 'warning' | 'danger'`; slot chữ; dạng chip tròn (`radius full`).
  - `AmbientBackdrop`: props `strength: 'full' | 'soft'` (full: 3 đốm, soft: 2 đốm, bỏ đốm xanh lá); `aria-hidden="true"`, `data-ambient`.
  - `useControlSize(): ComputedRef<'medium' | 'large'>`: `'large'` khi `PHONE_QUERY` khớp.
  - `errorMessage(error: unknown, t: (key: string, values?: Record<string, unknown>) => string, te: (key: string) => boolean): string`: `ApiError` có `problem.code` và `te('errorCodes.' + code)` → `t('errorCodes.' + code)`; mọi trường hợp khác → `t('errors.requestFailed')`. Không bao giờ trả `detail` hay `title` của backend.
  - `showSuccessMessageNaive`: mặc định `duration: 5000`, `keepAliveOnHover: true`. `showErrorMessageNaive`: mặc định `duration: 0`, `closable: true` (lỗi ở lại tới khi đóng, bảng *Components* § Toast).
  - `tests/helpers/viewport.ts`: `stubViewport(width: number): { setWidth(width: number): void }` — giả `window.matchMedia` cho mọi truy vấn `min-width`/`max-width` (rem × 16) và báo listener khi đổi.
  - Locale: nhóm `errorCodes` (rỗng, phần 8 điền), `common.close` nếu chưa có.

- [x] **Step 1: Viết test thất bại**

```ts
describe('InlineConfirm')
it('moves focus into the block when it appears')
it('emits cancel on Escape and on the cancel button')
it('emits confirm once and disables both buttons while busy')
it('renders only the message and the cancel button when showConfirm is false')
describe('InlineNotice')
it('uses role alert for danger and role status otherwise')
it('shows a close button only when dismissible and emits dismiss')
describe('AppSheet')
it('opens on the right without a dimming mask at 1280 px')         // stubViewport(1280)
it('opens from the bottom at full width at 834 and 390 px')
it('closes from the close button and from Escape')
describe('useControlSize')
it('returns large below 768 px and medium from 768 px')
describe('errorMessage')
it('maps a known error code to its locale text')
it('falls back to the generic text for an unknown code, a non-ApiError and a missing code')
it('never returns the backend detail')        // ApiError { code: 'X', detail: 'Sao Mai Tech' } → không chứa 'Sao Mai Tech'
describe('feedback-naive')
it('keeps success toasts for 5 seconds and pauses them on hover')
it('keeps error toasts until closed')
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- InlineNotice InlineConfirm AppSheet EmptyState StatusLabel use-control-size error-message feedback-naive`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces và bảng *ComponentsLight/Dark* (§ Thay cho hộp thoại, § Trống, đang tải, § Điều khiển và trạng thái). Nút trong `InlineConfirm` cỡ theo `useControlSize()`; nút xác nhận `tone='danger'` dùng `danger-solid` (thao tác không hoàn tác), `'neutral'` dùng `accent`. Chuyển `tests/layouts/AppLayout.test.ts` sang dùng `stubViewport`.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [x] **Step 5: Commit** `feat(frontend): add inline notice, inline confirm, sheet and empty state from the Components board`

### Task 3: Quyền vào route một chỗ, nhóm menu "Tổ chức" và "Hệ thống", `isSystemAdministrator`

**Files:**
- Create: `frontend/src/lib/route-access.ts`
- Modify:
  - `frontend/src/router/index.ts` (`RouteMeta`, `resolveGuardRedirect`)
  - `frontend/src/composables/use-sidebar-menu.ts`
  - `frontend/src/layouts/menu-groups.ts`
  - `frontend/src/stores/auth.ts`
  - `frontend/src/locales/{vi,en}.ts`
  - `.claude/rules/frontend-conventions.md` (đoạn `meta.permission`)
- Test: `frontend/tests/unit/lib/route-access.test.ts`, `frontend/tests/unit/router/guards.test.ts`, `frontend/tests/unit/composables/use-sidebar-menu.test.ts`, `frontend/tests/unit/stores/auth.test.ts`

**Interfaces:**
- Consumes: `AuthResponse.isSystemAdministrator` (phần 3).
- Produces:
  - `RouteMeta` thêm `requiresOrganization?: boolean` (trang bên trong tổ chức đang làm việc), `systemAdministrator?: boolean` (trang của admin hệ thống) và `activeMenu?: string` (tên route của mục rail được đánh dấu đang mở khi trang không có mục riêng, ví dụ tab *Lời mời* đánh dấu *Thành viên*). `permission` giữ nguyên nghĩa.
  - ```ts
    export type RouteAccess = 'allowed' | 'login' | 'no-organization' | 'forbidden'
    export interface RouteAccessContext {
      isAuthenticated: boolean
      organizationId: string | null
      isSystemAdministrator: boolean
      hasPermission: (code: string) => boolean
    }
    export function routeAccess(meta: RouteMeta, ctx: RouteAccessContext): RouteAccess
    ```
    Thứ tự: `requiresAuth` và chưa đăng nhập → `'login'`; `requiresOrganization` và `organizationId === null` → `'no-organization'`; `systemAdministrator` và không phải admin → `'forbidden'`; `permission` mà thiếu → `'forbidden'`; còn lại `'allowed'`.
  - `resolveGuardRedirect`: `'login'` → `{ name: 'login', query: { redirect } }` như cũ; `'no-organization'` → `useHomeRoute()`; `'forbidden'` → **không** chuyển hướng (giữ URL; `AppLayout` hiện mẫu *Không đủ quyền* ở Task 7); `guestOnly` như cũ.
  - `buildMenuEntries(routes, ctx: RouteAccessContext, groups)` chỉ giữ route có `routeAccess === 'allowed'`; `useSidebarMenu()` truyền ngữ cảnh từ store `auth`.
  - `MENU_GROUPS = [{ id: 'organization', labelKey: 'nav.groups.organization', icon: Building, order: 100 }, { id: 'system', labelKey: 'nav.groups.system', icon: Server, order: 200 }]`. Nhóm hiện như tiêu đề chữ, không thu gọn (bảng *AppShell*).
  - Store `auth`: `isSystemAdministrator: Ref<boolean>`, đặt trong `setAuth` từ `response.isSystemAdministrator`, về `false` trong `clearAuth`. Chỉ để hiện nhóm "Hệ thống"; máy chủ vẫn kiểm từng request (API-27).
  - Locale: `nav.groups.organization` (vi "Tổ chức", en "Organization"), `nav.groups.system` (vi "Hệ thống", en "System").

- [x] **Step 1: Viết test thất bại**

```ts
describe('routeAccess')
it.each([...])('returns %s for %s')   // bảng: chưa đăng nhập; không có tổ chức; không phải admin; thiếu quyền; đủ hết
it('checks the organization before the permission')          // org null + thiếu quyền → 'no-organization'
describe('resolveGuardRedirect')
it('keeps the URL for a signed-in user without the page permission')       // trả undefined
it('keeps the URL for a non-administrator on a system page')
it('sends a user without an active organization to the home route on an organization page')
describe('useSidebarMenu')
it('shows the system group only to a system administrator')
it('hides organization pages in the personal context')
it('hides a page whose permission the user lacks')                          // AC-298, AC-299, AC-362
it('renders groups in the order main, organization, system')
describe('auth store')
it('sets isSystemAdministrator from the login response and clears it on clearAuth')
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- route-access guards use-sidebar-menu stores/auth`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces. Sửa đoạn `meta.permission` trong `frontend-conventions.md`: "`useSidebarMenu` ẩn mục; mở thẳng URL khi thiếu quyền thì giữ URL và hiện mẫu *Không đủ quyền* (`AccessProblemState`); `meta.requiresOrganization` đưa người ở ngữ cảnh cá nhân về `useHomeRoute()`; `meta.systemAdministrator` cho trang admin hệ thống. Tất cả chỉ là cổng giao diện, endpoint vẫn phải có policy." Cập nhật test guard cũ đang khẳng định "chuyển về home khi thiếu quyền".

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [x] **Step 5: Commit** `feat(frontend): decide route access in one place and add the organization and system menu groups`

### Task 4: Khung chỉ còn rail (*AppShell*), bỏ năm chế độ menu

**Files:**
- Create: `frontend/src/layouts/AppRail.vue`, `frontend/src/layouts/AppTopBar.vue`
- Modify:
  - `frontend/src/layouts/AppLayout.vue`, `UserMenu.vue`, `AppearanceSettings.vue`, `AppFooter.vue`, `CommandPalette.vue` (nếu đọc chế độ menu)
  - `frontend/src/stores/layout-store.ts`
  - `frontend/src/lib/responsive.ts` (bỏ `BELOW_LG_QUERY` khi không còn ai dùng)
  - `frontend/src/locales/{vi,en}.ts` (bỏ `layout.menuModes`, `layout.menuThemes`, `layout.settings.menuMode*`, `layout.pinSidebar`)
- Delete:
  - `frontend/src/layouts/AppSidebar.vue`, `AppSidebarNav.vue`, `AppSidebarItem.vue`, `AppHeader.vue`, `AppTopMenu.vue`, `MenuModePreview.vue`, `PageTitle.vue`
  - `frontend/src/layouts/sidebar-theme.ts`, `overflow-split.ts`, `menu-dropdown.ts`
  - `frontend/src/lib/layout-preferences.ts`
  - test tương ứng: `tests/layouts/{AppSidebar,AppSidebarItem,AppHeader,AppTopMenu,PageTitle}.test.ts`, `tests/unit/layouts/{sidebar-theme,overflow-split}.test.ts`, `tests/unit/lib/layout-preferences.test.ts`
- Test: `frontend/tests/layouts/AppRail.test.ts`, `AppTopBar.test.ts`, `AppLayout.test.ts`, `AppearanceSettings.test.ts`, `UserMenu.test.ts`, `frontend/tests/unit/stores/layout-store.test.ts`

**Interfaces:**
- Consumes: Task 2 (`PHONE_QUERY`, `DESKTOP_QUERY`, `AmbientBackdrop`, `stubViewport`), Task 3 (`useSidebarMenu`, `MENU_GROUPS`).
- Produces:
  - `layout-store` chỉ còn trạng thái phiên: `mobileOpen`, `settingsOpen`, `paletteOpen`, `profileOpen`, `createOrganizationOpen` (mới; phần 8 gắn tấm trượt *Tạo tổ chức*) cùng các hàm `open*`/`close*`. Không đọc, không ghi `localStorage`.
  - `AppRail`: `DESKTOP_QUERY` → rail rộng (`glass-thin`, logo + "FeedbackHub", tiêu đề nhóm, mục có biểu tượng và chữ, `aria-current="page"`); 768–1199 px → rail biểu tượng (mục có `aria-label` và `title`, nhóm ngăn bằng đường kẻ); `PHONE_QUERY` → không có rail, menu trượt trái (`n-drawer`) mở bằng `layout.mobileOpen`, có ô *Đi tới…* ở đầu như bảng *AppShell* § Điện thoại.
  - `AppTopBar` (`glass-thin`): trái là chỗ đặt bộ chọn tổ chức (slot `organization`, Task 5 lấp); máy tính có nút *Đi tới…* kèm `Ctrl K`/`⌘K`, máy tính bảng và điện thoại chỉ biểu tượng tìm; `NotificationBell`; `UserMenu`. Điện thoại: thêm nút mở menu (`aria-label` `nav.openMenu`) ở đầu. Vùng bấm 44 px dưới 1200 px.
  - `UserMenu` (nền đặc `raised`): tên, email; *Hồ sơ* (`layout.openProfile()`), *Giao diện* (`layout.openSettings()`, kèm giá trị đang chọn), *Ngôn ngữ* (như hiện có), *Hướng dẫn sử dụng* (link hiện ở `AppHeader`, chuyển vào đây, đứng ngay trên *Đăng xuất*), *Đăng xuất*. Giữ testid `app-header-user-menu`, `user-menu-profile`, `user-menu-logout` (e2e đang dùng).
  - `AppearanceSettings`: chỉ còn *Chế độ màu* (Sáng, Tối, Theo hệ thống), *Ngôn ngữ*, *Cỡ chữ* (TECH00 §8.1).
  - `AppLayout`: nền `page` + `AmbientBackdrop strength="soft"`; `AppRail` | cột (`AppTopBar`, `main`, `AppFooter`); `main` vẫn cuộn riêng và về đầu khi đổi `route.path`; giữ `AppearanceSettings`, `CommandPalette`, chỗ gắn bảng hồ sơ (Task 6).
  - `AppFooter` giữ nguyên chân trang một dòng hiện có (tên ứng dụng, phiên bản, chấm kết nối) dưới nội dung, đổi sang màu theo vai trò (`ink-muted`, `line`). Link hướng dẫn không còn ở chân trang. Chữ cạnh chấm kết nối khi API chạy theo bảng *AppShell*: đổi `app.apiStatus.online` thành vi "Đã kết nối", en "Connected" (người dùng duyệt 2026-10-23); các trạng thái khác giữ nguyên.
  - Locale mới: `nav.openMenu` (vi "Mở menu", en "Open menu"), `nav.goTo` (vi "Đi tới…", en "Go to…"), `layout.userMenu.appearance` (vi "Giao diện", en "Appearance"). Link hướng dẫn dùng lại khoá `userGuide` mà `AppHeader.vue` đang dùng.
  - Gỡ `AppSidebar.vue`, `AppHeader.vue` khỏi `ALLOWED` của `no-raw-colors.test.ts` (Task 1).

- [x] **Step 1: Viết test thất bại**

```ts
describe('AppRail')
it('shows the wide rail with group headings at 1280 px')
it('shows the icon rail with labelled icon links at 834 px')
it('shows no rail at 390 px and opens the slide-in menu from the top bar')
it('marks the current page with aria-current')
it('marks the item named by meta.activeMenu for a page without its own item')
it('closes the slide-in menu after a navigation')
describe('AppTopBar')
it('shows the Go to button with its shortcut at 1280 px and an icon button below')
it('gives every icon button an accessible name and a 44 px target below 1200 px')
describe('AppLayout')
it('renders the rail layout whatever menu mode is left in localStorage')        // Review Focus 1
// localStorage: layout-menu-mode='horizontal', layout-menu-theme='transparent', sidebar-mode='full' → có AppRail, không AppTopMenu
it('neither reads nor writes the old layout keys')                            // spy Storage.prototype.getItem/setItem
it('scrolls main back to the top on a path change and keeps it on a query change')
describe('AppearanceSettings')
it('offers only colour mode, language and font size')
describe('UserMenu')
it('opens the profile, the appearance settings and the user guide from the menu')
it('puts the user guide item directly above Log out')                         // P7-1
it('keeps the user guide link out of the footer')
describe('AppFooter')
it('keeps the one-line footer with app name, version and connection dot below the content')   // P7-1
it('labels the connection dot with app.apiStatus.online when the API is up')
describe('layout store')
it('starts every panel closed and touches no storage')
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- tests/layouts tests/unit/stores/layout-store`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces và bảng *AppShellLight/Dark* (ba khoảng). Dùng CodeGraph blast radius (`codegraph explore "AppSidebar AppHeader AppTopMenu PageTitle layout-preferences sidebar-theme"`) để tìm mọi caller trong `.vue` trước khi xoá (Serena không thấy caller `.vue`, `.claude/rules/serena.md`). Trang tự đặt `h1` của mình; thanh trên không còn tiêu đề hay breadcrumb.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [x] **Step 5: Commit** `feat(frontend): replace the five menu modes with the AppShell rail, top bar and slide-in menu`

### Task 5: Bộ chọn tổ chức trên thanh trên

**Files:**
- Create: `frontend/src/layouts/OrgSwitcher.vue`, `frontend/src/lib/role-name.ts`
- Modify: `frontend/src/layouts/AppTopBar.vue`, `frontend/src/stores/organizations.ts` (chỉ đọc; không đổi API), `frontend/src/locales/{vi,en}.ts`, `frontend/tests/factories/organization.ts`
- Test: `frontend/tests/layouts/OrgSwitcher.test.ts`, `frontend/tests/unit/lib/role-name.test.ts`

**Interfaces:**
- Consumes: Task 2 (`AppSheet`, `StatusLabel`, `errorMessage`), Task 4 (`layout.createOrganizationOpen`), `useOrganizationsStore().fetchMine` (API-01), `useOrganizationSwitch().switchOrganization` (API-28).
- Produces:
  - `roleDisplayName(role: { name: string; systemKind?: 'Owner' | 'Admin' | 'Member' | null }, t: (key: string) => string): string`: có `systemKind` → `t('roles.systemKind.' + systemKind)`, không có → `role.name`. Dùng đúng khoá `roles.systemKind.Owner|Admin|Member` phần 4 Task 1 đã thêm (vi "Chủ sở hữu"/"Admin"/"Member", en "Owner"/"Admin"/"Member"); không thêm khoá thứ hai. Mọi nơi hiện tên vai trò hệ thống dùng hàm này (TECH00 §8.2).
  - `OrgSwitcher` (không prop):
    - nút kích hoạt: chữ viết tắt + tên tổ chức đang làm việc (`auth.organizationName`), hoặc `organizations.personalWorkspace` khi `organizationId === null`; `aria-haspopup="menu"`, `aria-expanded`;
    - mở lần đầu gọi `fetchMine()` nếu `loaded === false`;
    - máy tính và máy tính bảng: `n-popover` nền `raised`; điện thoại: `AppSheet` từ dưới (bảng *AppShell* § Điện thoại);
    - tiêu đề "Tổ chức của bạn"; mỗi tổ chức: chữ viết tắt, tên, dòng phụ là vai trò của mình (`myRoles` qua `roleDisplayName`, nối ", "); `role="menuitemradio"`, `aria-checked` cho tổ chức đang làm việc;
    - tổ chức `status === false`: làm mờ, `StatusLabel tone="neutral"` *Tạm ngừng*, `aria-disabled="true"`, bấm không làm gì (AC-73, §6.5 bước 4);
    - chọn tổ chức khác → `switchOrganization(id)` (trang tải lại, AC-04); lỗi → `showErrorMessage(errorMessage(...))`;
    - mục chữ *Về trang chào* ngay trên *Tạo tổ chức*, luôn hiện (AC-201): gọi `switchOrganization(null)`, trang tải lại về `useHomeRoute()` (không về `/welcome`, vì `/welcome` chỉ có từ phần 8 Task 2); lỗi xử lý như chọn tổ chức khác. Việc vẽ mục này trên bảng *AppShell* giao lại cho ui-designer;
    - mục cuối *Tạo tổ chức* → đóng bộ chọn, `layout.createOrganizationOpen = true`.
  - Factory `makeOrganization` thêm `myRoles: [{ id: 'role-owner', name: 'Owner', systemKind: 'Owner' }]`.
  - Locale: `organizations.switcherTitle` (vi "Tổ chức của bạn", en "Your organizations"), `organizations.suspended` (vi "Tạm ngừng", en "Suspended"), `organizations.backToWelcome` (vi "Về trang chào", en "Back to welcome page"), `organizations.create` (vi "Tạo tổ chức", en "Create organization").

- [x] **Step 1: Viết test thất bại**

```ts
describe('roleDisplayName')
it('names system roles from systemKind in the interface language')   // vi 'Chủ sở hữu', en 'Owner'
it('keeps the stored name of a custom role')
describe('OrgSwitcher')
it('lists only the organizations the API returns')                   // AC-05
it('shows each organization with my role names in the interface language')
it('switches to the picked organization')                            // POST switch-organization { organizationId: 'org-2' }
it('dims a suspended organization, labels it and does not switch')   // AC-73, Review Focus 3
it('still opens when every organization is suspended')
it('marks the current organization as checked')
it('shows the personal workspace label without an active organization')
it('opens as a bottom sheet at 390 px and as a popover at 834 and 1280 px')
it('asks for the create-organization sheet from its last item')
it('puts Back to welcome page directly above Create organization')            // P7-2
it('switches to no organization from Back to welcome page')   // AC-201: chỉ assert switchOrganization(null) được gọi; không assert /welcome
it('shows Back to welcome page in vi and en')                                // 'Về trang chào', 'Back to welcome page'
it('keeps Back to welcome page available when every organization is suspended')
it('shows the mapped error and stays open when switching fails')
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- OrgSwitcher role-name`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces, đặt `OrgSwitcher` vào slot `organization` của `AppTopBar`. Bàn phím theo mẫu menu WAI-ARIA như `UserMenu.vue` (mũi tên di chuyển, `Esc` đóng và trả tiêu điểm về nút).

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [x] **Step 5: Commit** `feat(frontend): add the organization switcher with suspended organizations dimmed`

### Task 6: `ProfilePanel.vue` thay `ProfileDialog.vue` (*ProfileEmail*)

**Files:**
- Move: `frontend/src/components/ProfileDialog.vue` → `frontend/src/components/ProfilePanel.vue` (`git mv`)
- Move: `frontend/tests/components/ProfileDialog.test.ts` → `frontend/tests/components/ProfilePanel.test.ts` (`git mv`)
- Modify: `frontend/src/layouts/AppLayout.vue`, `frontend/tests/layouts/AppLayout.test.ts`, `docs/feature-map.md` (mục *Hồ sơ và phiên đăng nhập*: tên tệp, "bảng hồ sơ" thay "dialog")

**Interfaces:**
- Consumes: Task 2 (`AppSheet`, `useControlSize`), API-29–API-31 qua `getProfile()` (phần 1), miễn trừ 401 cấp form trong `api/client.ts` (`FORM_LEVEL_401_CODES`, phần 1).
- Produces: `ProfilePanel` với `v-model:visible` (giữ tên prop để `AppLayout` chỉ đổi tên thẻ). Giữ nguyên:
  - bốn tab *Thông tin*, *Mật khẩu* (ẩn khi `hasPassword === false`), *Phiên đăng nhập*, *Thông báo*;
  - mọi testid: `profile-avatar*`, `profile-pending-email`, `profile-pending-email-resend`, `profile-pending-email-cancel`, `profile-email-current-password`, `profile-sessions-list`;
  - dòng chờ xác nhận là chữ thường dưới ô email, khoá `profile.pendingEmail`, kèm hai nút chữ *Gửi lại* (API-30) và *Huỷ* (API-31), không hộp thoại, không hỏi mật khẩu (AC-342, AC-358);
  - ô *Mật khẩu hiện tại* hiện ngay dưới ô email chỉ khi `hasPassword` và email gõ khác cả email đã lưu lẫn email đang chờ (AC-352, AC-354, AC-357); lỗi `InvalidCurrentPassword`, `FieldRequired`, `EmailChangeTemporarilyLocked` nằm dưới ô đó theo locale, không bật toast (AC-353, AC-355, AC-356);
  - không gọi refresh khi API-29 trả 401 cấp form (dựa vào miễn trừ trong `api/client.ts`, không thêm xử lý riêng);
  - mỗi lần mở: tải lại hồ sơ, xoá ô mật khẩu hiện tại.
  - Khác duy nhất: vỏ là `AppSheet` (`title = t('profile.title')`) thay `n-modal`; nút trong form cỡ theo `useControlSize()`.

- [x] **Step 1: Đổi tên và viết test thất bại**

`git mv` hai tệp, đổi `import`, `describe('ProfilePanel')`, `openDialog` → `openPanel` trong test; giữ nguyên mọi `it` hiện có (cả nhóm `email change` và `avatar`). Thêm:

```ts
it('opens as a side panel on the right at 1280 px')                       // stubViewport(1280)
it('opens as a bottom sheet at 834 and 390 px')
it('keeps the session when the current password is wrong')                // Review Focus 5
// PUT /api/profile → 401 { code: 'InvalidCurrentPassword' }; POST /api/auth/refresh không được gọi; storeIn(pinia, useAuthStore).clearAuth không được gọi
it('reloads the profile and clears the current password when reopened')  // Review Focus 5
```

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- ProfilePanel AppLayout`
Expected: FAIL (bốn test mới; vỏ vẫn là `n-modal`).

- [x] **Step 3: Cài đặt**

Thay `n-modal` bằng `AppSheet`; `AppLayout` gắn `<ProfilePanel v-model:visible="layout.profileOpen" />` (vẫn gắn ở `AppLayout`, không trong menu tài khoản, để đổi khoảng màn hình không làm mất form đang sửa). Cập nhật `docs/feature-map.md` mục *Hồ sơ và phiên đăng nhập*.

- [x] **Step 4: Chạy lại, phải qua**

Run: `bun run --cwd frontend test:run`, rồi ba lệnh kiểm còn lại.
Expected: PASS, gồm `tests/unit/api/client.test.ts` (401 cấp form không refresh).

- [x] **Step 5: Commit** `refactor(profile): move the profile dialog into a side panel and bottom sheet`

### Task 7: `AccessProblemState`, trang 404 trong khung, kiểm tổng

**Files:**
- Create: `frontend/src/components/AccessProblemState.vue`, `frontend/src/lib/access-problem.ts`
- Modify:
  - `frontend/src/views/NotFoundView.vue`
  - `frontend/src/router/index.ts` (route `not-found` chuyển vào con của layout `/`)
  - `frontend/src/layouts/AppLayout.vue` (hiện *Không đủ quyền* khi `routeAccess === 'forbidden'`)
  - `frontend/src/locales/{vi,en}.ts` (nhóm `accessProblem`; bỏ khoá `errors.notFound*` không còn dùng)
  - `frontend/e2e/guards.spec.ts`
  - `docs/feature-map.md`
- Test: `frontend/tests/components/AccessProblemState.test.ts`, `frontend/tests/unit/lib/access-problem.test.ts`, `frontend/tests/layouts/AppLayout.test.ts`, `frontend/tests/unit/router/guard-integration.test.ts`

**Interfaces:**
- Consumes: Task 3 (`routeAccess`), Task 4 (`AppLayout`).
- Produces:
  - `accessProblemOf(error: unknown): 'not-found' | 'forbidden' | null`: `ApiError` 404 → `'not-found'`; 403 với `code` `OrganizationAccessDenied` hoặc `SystemAdministratorRequired` → `'forbidden'`; mọi trường hợp khác (gồm 403 `OrganizationSuspended`, đi theo §6.5 ở phần 8) → `null`. Màn hình phần 8 gọi hàm này với lỗi của request tải trang.
  - `AccessProblemState`: props `kind: 'not-found' | 'forbidden'`; biểu tượng trung tính, `h1`, một dòng, một nút *Về trang chủ* (`router.push(useHomeRoute())`). Không prop, slot hay đường nào đưa tên, mã, đường dẫn đối tượng hay `detail` vào. Không đổi `document.title`.
  - Chữ (bảng *OrgContext*):
    - `accessProblem.notFoundTitle`: vi "Không tìm thấy", en "Not found";
    - `accessProblem.notFoundText`: vi "Nội dung này không tồn tại hoặc bạn không có quyền xem.", en "This content doesn't exist or you don't have access to it.";
    - `accessProblem.forbiddenTitle`: vi "Không đủ quyền", en "No permission";
    - `accessProblem.forbiddenText`: vi "Tài khoản của bạn không có quyền mở trang này.", en "Your account doesn't have permission to open this page.";
    - `accessProblem.goHome`: vi "Về trang chủ", en "Go to home".
  - Route `not-found` (`/:pathMatch(.*)*`) là con của layout `/`, `meta.requiresAuth: true`; `NotFoundView` chỉ render `<AccessProblemState kind="not-found" />`. Khách gõ đường dẫn lạ → đăng nhập → quay lại thấy 404 trong khung.

- [x] **Step 1: Viết test thất bại**

```ts
describe('accessProblemOf')
it.each([[404, undefined, 'not-found'], [403, 'OrganizationAccessDenied', 'forbidden'],
         [403, 'SystemAdministratorRequired', 'forbidden'], [403, 'OrganizationSuspended', null],
         [500, undefined, null]])('maps %s %s to %s')
it('returns null for a non-ApiError')
describe('AccessProblemState')
it('shows the not-found title, line and a single home button')
it('shows the forbidden title and line with the same layout')
it('goes to the home route from its button')
describe('AppLayout')
it('shows the forbidden state in the shell for a page whose permission the user lacks')  // URL giữ nguyên
it('shows the not-found state inside the shell for an unknown path')
it('never renders the problem detail, title or extensions of a 404')                      // Review Focus 4
// MSW trả 404 { title: 'Sao Mai Tech', detail: 'Role Trưởng IT', code: 'X', organizationName: 'Sao Mai Tech' } cho trang thử;
// wrapper.html() không chứa 'Sao Mai Tech' hay 'Trưởng IT'; document.title không đổi
```

`guard-integration.test.ts`: route lạ khi đã đăng nhập → `not-found` trong layout; khi chưa đăng nhập → `login?redirect=<đường dẫn>`.

- [x] **Step 2: Chạy test, phải thất bại**

Run: `bun run --cwd frontend test:run -- access-problem AccessProblemState AppLayout guard-integration`
Expected: FAIL.

- [x] **Step 3: Cài đặt** theo Interfaces và bảng *OrgContext* (§ Không tìm thấy, § Không đủ quyền, ba khoảng). Thêm vào `e2e/guards.spec.ts`:
  - `signed-in user opening an unknown path sees Not found inside the app shell` (thấy rail và chữ "Not found");
  - đổi test cũ `/organizations` sang một đường dẫn còn tồn tại có `requiresAuth`, vì phần 8 Task 1 xoá route `organizations`.

Thêm mục `## Khung ứng dụng và design system` vào `docs/feature-map.md`:
- vào bằng: mọi trang sau đăng nhập;
- tệp: `theme/tokens.ts`, `layouts/AppRail.vue`, `AppTopBar.vue`, `OrgSwitcher.vue`, `components/AccessProblemState.vue`, `lib/route-access.ts`;
- testid chính;
- test đã có;
- cách kiểm: ba khoảng màn hình, sáng/tối, giảm trong suốt, tổ chức tạm ngừng mờ trong bộ chọn.

- [x] **Step 4: Chạy lại, phải qua**

Run: lệnh ở Step 2, rồi bốn lệnh kiểm.
Expected: PASS.

- [x] **Step 5: Kiểm e2e cục bộ**

Run: `scripts/dev-up.sh`, rồi `bun run --cwd frontend test:e2e`
Expected: PASS (`auth`, `guards`, `logout`, `profile-sessions`).

- [x] **Step 6: Chạy gate đầy đủ**

Run: `scripts/test-gate.sh`
Expected: PASS.

- [x] **Step 7: Commit** `feat(frontend): show shared not-found and no-permission states inside the app shell`

---

## Lệch thiết kế khai trong PR (nhỏ, không đổi hành vi nghiệp vụ)

- Chân trang, link *Hướng dẫn sử dụng* trong menu tài khoản và mục *Về trang chào* đã có trên bảng *AppShell* (ui-designer bổ sung, người dùng duyệt 2026-10-23), nên không còn là lệch.
- Thanh trên không còn tiêu đề trang hay breadcrumb (bảng *AppShell* không có). Mỗi trang tự đặt `h1`.

## Câu hỏi mở

- **Đã chốt (SA, 2026-10-23):** Giữ chân trang một dòng hiện có (tên ứng dụng, phiên bản, chấm kết nối) dưới nội dung. Chuyển *Hướng dẫn sử dụng* vào menu tài khoản, ngay trên *Đăng xuất*. Ui-designer bổ sung cả hai vào bảng *AppShell* (Task 4).
- **Đã chốt (SA, 2026-10-23):** Thêm mục chữ *Về trang chào* (en "Back to welcome page") ngay trên *Tạo tổ chức* trong bộ chọn tổ chức; mục gọi `switchOrganization(null)` rồi tải lại về `/welcome`. Ui-designer bổ sung vào bảng *AppShell* (Task 5).

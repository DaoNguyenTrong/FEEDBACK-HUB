# Nhật ký lỗi agent lặp lại

Ghi lại các lỗi agent mắc phải khi làm việc trên repo này. Mục tiêu theo `agent-automation-playbook.md` (mục 4.4): sau 2–3 tuần, mỗi lỗi lặp lại phải được chuyển thành lint rule, test kiến trúc, test hồi quy, hoặc generator, không chỉ thêm một dòng vào prompt.

Mỗi mục có: ngày, việc đang làm, lỗi, nguyên nhân, và hành động đã làm hoặc cần làm.

Quy ước: thêm mục mới ở trên cùng. Khi một lỗi đã được tự động hóa, ghi "Đã chuyển thành: …" vào mục đó. Retro cuối mỗi module (`docs/process/lifecycle.md` § Lỗi lặp và retro cuối module) phân loại commit `fix` của module và thêm mục vào đây.

## Chờ retro

Ý cải tiến quy trình phát sinh giữa module ghi ở đây, mỗi ý một dòng kèm ngày. Retro cuối module xử lý rồi xoá chúng.

_(trống)_

---

### 2026-11-12 — Retro module 00: bảy nhóm lỗi lặp lại

- **Việc đang làm:** module 00, nhánh `feature/module-00` (PR #32). 42 trên 158 commit là `fix`. Phần lớn commit không ghi khâu nào bắt được lỗi; chỉ `b25f4e7` (so với bảng thiết kế) và `e7f65f7` (review cuối) có ghi.
- **Các nhóm và việc cần làm** (mỗi nhóm gặp từ hai lần, nên phải tự động hoá theo §8; chờ #32 merge vì mã nằm trên nhánh đó):
  - **A. Đường phát hành lại token bỏ sót trạng thái phiên** (4: `f349e23`, `c622aee`, `57cb046`, `a69edeb`). `switch-organization` và `TokenIssuer` phát token mà không mang theo remember-me, giờ đăng nhập, trạng thái thu hồi/hết hạn hay kiểm tra khoá tài khoản. Đây là lần lặp lại của mục 2026-10-19 "Coi cookie là định danh phiên". Cần: một `[Theory]` hợp đồng chạy qua mọi nơi gọi `ITokenIssuer` (đăng nhập, refresh, đổi tổ chức, đăng nhập ngoài, đăng ký), khẳng định tài khoản bị khoá bị từ chối và remember-me, giờ đăng nhập được giữ; thêm kiểm thử kiến trúc chỉ cho `TokenIssuer` ký JWT và refresh token. Rẻ.
  - **B. Interceptor 401/refresh/replay ở `frontend/src/api/client.ts`** (4: `766bf72`, `984fae2`, `6e41188`, `149e1d7`). Một interceptor được vá từng trường hợp. Cần: ma trận trạng thái dạng bảng trong `frontend/tests/unit/api/client.test.ts` (401/403 × refresh đạt/hỏng × đang đổi tổ chức × lỗi mật khẩu), mỗi dòng khẳng định có replay không, có đăng xuất không, replay vào tổ chức nào; `testing.md` ghi mỗi thay đổi interceptor thêm một dòng. Vừa.
  - **C. Đồng thời và quyền cũ dưới khoá** (6: `e5d7934`, `3f5a72a`, `ed082f1`, `e72152d`, `d822e35`, `b3f5a1e`). Kiểm rồi làm mà không khoá dòng, không kiểm lại sau khi khoá, hoặc vi phạm unique index không thành 409. Cần: mở rộng `UnitOfWorkUniqueViolationTests` để duyệt mọi unique index trong model EF và khẳng định mỗi cái thành 409 (rẻ, bắt được `b3f5a1e`, `d822e35`); kiểm thử kiến trúc cho các service ghi dữ liệu theo tổ chức phải đi qua khoá tổ chức (rẻ); bộ test gọi song song hai request cho mỗi endpoint ghi (lớn hơn).
  - **D. Tin email của nhà cung cấp ngoài (Microsoft)** (4: `1fa77b9`, `b6fefb1`, `a9d96fe`, `3e041e1`). Quy tắc "email đã xác minh" áp sai chỗ, rộng rồi hẹp, phải thêm migration dữ liệu. Cần: test dạng bảng theo ma trận claim (đã/chưa xác minh × có/không email × đã liên kết/mới) trong test của `AuthService`. Rẻ.
  - **E. Độ bền hạ tầng: Redis, rate limiter** (4: `47f3a93`, `50d11c5`, `21b68ca`, `abb8581`). Hành vi khi Redis sập hoặc bị evict không được đặc tả hay kiểm trước. Cần: một lớp test hợp đồng cho mọi nơi dùng Redis (cache, limiter, backplane SignalR) khi Redis không có: thất bại nhanh trong thời gian giới hạn và có đường dự phòng. Lớn hơn.
  - **F. Frontend lệch bảng thiết kế hoặc token** (3: `b25f4e7`, `becd88d`, `511d326`). Màn hình và màu của guide làm theo trí nhớ thay vì bảng và token. Cần: mở rộng `tokens.test.ts` và `no-raw-colors.test.ts` sang CSS của `frontend/user-guide/site` và thang radius (rẻ); so ảnh chụp với bảng thiết kế ngay trong task, trước commit, thay vì ở cuối phase (quy trình).
  - **G. Luồng đổi email** (4: `0058c65`, `600981e`, `c15c9f0`, `4f2a060`). Làm trước khi chốt thiết kế link xác nhận và ràng buộc token. Không có kiểm tra mã nào phù hợp: lỗ hổng nằm ở quy trình (xem bên dưới).
- **Lỗ hổng quy trình:**
  - G và một phần D: thiết kế nhạy cảm bảo mật được sửa sau khi đã làm. Tài liệu kỹ thuật của module nhạy cảm cần một bảng mọi đường phát token và xác minh email để `appsec` review trước kế hoạch.
  - B: cùng một cổng bắt từng trường hợp một; reviewer cần checklist ma trận trạng thái thay vì đọc diff.
  - F: so với bảng thiết kế nên nằm ở review từng task, không phải cuối phase.
  - `e7f65f7`: review cuối còn bắt được mã 409 thiếu trong OpenAPI. Cần xác nhận kiểm tra lệch hợp đồng trong CI có so mã phản hồi đã khai báo.
  - `51f79f9`: timer của toast rò rỉ dù đã bật `enableAutoUnmount`. Cân nhắc `afterEach` chung trong `tests/setup.ts` khẳng định `vi.getTimerCount() === 0`.
- **Lỗi lẻ (một lần, không cần tự động hoá):** `51f79f9`, `dfa16b7`, `7dae5d3`, `d260ea2`, `41bbaec`, `374c7b0`, `0e67f79`, `e7f65f7`.

---

### 2026-10-19 — Đoán route và copy UI thay vì đọc router và locale

- **Việc đang làm:** chạy suite e2e cho luồng hồ sơ và phiên đăng nhập.
- **Lỗi:** viết `guards.spec.ts` theo route `/accounts` (lấy từ tài liệu cũ), và spec profile-sessions theo copy "Login sessions", "Sign out other devices". Route không tồn tại trong router, copy đã đổi trong `locales/en.ts`.
- **Nguyên nhân:** không kiểm tra router và locale trước khi viết selector và assertion.
- **Hành động:** đã xóa test `/accounts`, sửa copy theo locale. Cần: quy định trong `testing.md` là chọn selector theo `data-testid`, và đối chiếu route với `router/index.ts` trước khi viết test điều hướng.

### 2026-10-19 — Sửa race bằng một lệnh không làm gì

- **Việc đang làm:** sửa flaky của logout.
- **Lỗi:** thêm `waitForLoadState('load')` sau logout và báo là đã sửa, nhưng nó trả về ngay trên document cũ. Test vẫn flaky.
- **Nguyên nhân:** `reloadTo` đổi URL bằng `replaceState` trước khi `reload()`, nên URL đúng trước khi reload bắt đầu.
- **Hành động:** đã sửa bằng `waitForEvent('load')` trước khi bấm, và kiểm chứng bằng `--repeat-each=10 --retries=0` (10/10). Cần: mọi sửa flaky phải có bằng chứng chạy lặp không retry, không chỉ một lần pass.

### 2026-10-19 — Coi cookie là định danh phiên

- **Việc đang làm:** sửa "đăng xuất thiết bị khác".
- **Lỗi:** giả định phiên hiện tại được xác định bằng cookie `refresh_token`. Client chỉ có Bearer token bị thu hồi luôn phiên đang dùng, trái với mô tả endpoint.
- **Nguyên nhân:** không đọc hết các đường phát hành token (switch-organization, rotate refresh) trước khi thiết kế.
- **Hành động:** đã chuyển sang claim `session_id` trong access token, có regression test và bằng chứng Bearer-only. Cần: trước khi đổi một định danh, liệt kê mọi nơi tạo nó.

### 2026-10-19 — Ghi file tạm vào `/tmp`

- **Việc đang làm:** kiểm tra response đăng nhập của API.
- **Lỗi:** ghi response (có token) vào `/tmp/claude-login.json`, vi phạm quy tắc dùng thư mục scratchpad.
- **Nguyên nhân:** không chọn thư mục scratchpad cho file tạm.
- **Hành động:** đã xóa file. Cần: dùng thư mục scratchpad hoặc `verify-output/` (đã gitignore) cho mọi file tạm.

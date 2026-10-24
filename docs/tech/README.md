# Thiết kế kỹ thuật

Mỗi module một tài liệu, viết từ `_template.md` bởi agent `tech-lead`, sau khi PRD của module đã *Approved* và thiết kế giao diện đã duyệt. Tài liệu này trả lời "làm theo kiến trúc nào"; việc chia thành task nhỏ là của kế hoạch triển khai (`docs/superpowers/plans/`).

## Quy trình

1. **Soạn**: `tech-lead` soạn từ PRD, thiết kế giao diện và code hiện có; trả về câu hỏi mở, câu hỏi cho PRD và các quyết định cần ghi.
2. **Câu hỏi nghiệp vụ** quay về `product-manager` để sửa PRD, không quyết ở đây.
3. **Review bảo mật**: module nhạy cảm (định danh, token, cookie, phiên, link riêng, khoá công khai, mọi thứ lộ ra ẩn danh) qua `appsec` trước khi duyệt.
4. **Duyệt**: hết câu hỏi mở thì `tech-lead` đặt trạng thái *In review*; người dùng duyệt; các quyết định trong §12 được duyệt cùng tài liệu, không cần duyệt riêng. Phiên chính commit tài liệu qua PR chỉ chứa tài liệu. Sửa tài liệu do lệch lớn khi triển khai (`docs/process/lifecycle.md` § Lệch thiết kế khi triển khai) đi chung PR với code.
5. **Kế hoạch triển khai**: `writing-plans` chia thành task, dẫn chiếu mã FR/AC của PRD và các mục của tài liệu này.

Ngoại lệ về thứ tự: module nhạy cảm (PRD02) có thể viết tài liệu kỹ thuật sớm, song song với thiết kế giao diện.

Mọi thay đổi trên tài liệu đã duyệt đưa nó về *In review*.

## Ràng buộc đã biết

Quy tắc hiện có trong `.claude/rules/` viết cho ứng dụng nội bộ dùng cookie, sẽ va chạm với các module dưới đây. `tech-lead` phải giải quyết từng mục trong tài liệu của module, và quy tắc tương ứng chỉ được sửa khi tài liệu đó được duyệt. Rà ngày 2026-10-23.

| Mục | Ràng buộc hiện tại | Vì sao va chạm | Module |
|---|---|---|---|
| Khoá công khai của widget | API key (`X-Api-Key`) là credential đầy đủ: được `[Authorize]` chấp nhận, bỏ qua CSRF, không có claim tổ chức (`authentication.md`) | Làm khoá widget theo mẫu này sẽ đưa một credential dùng được vào JavaScript công khai. Khoá widget chỉ là định danh, không xác thực | TECH02 |
| Client HTTP của widget | Chỉ được dùng `apiClient` (`frontend-conventions.md`), vốn gửi cookie và khi 401 thì làm mới token, xoá phiên, chuyển về đăng nhập | Bên trong iframe của khách, hành vi đó sai. Cần một client riêng, không cookie, không refresh | TECH02 |
| Cấu hình runtime của loader | `getRuntimeConfig()` bắt buộc, đọc `/config.json` (`frontend-conventions.md`) | Trên trang của sản phẩm khách, đường dẫn đó trỏ về domain của khách. Loader phải lấy cấu hình từ URL của script hoặc thuộc tính `data-*` | TECH02 |
| Header `X-TimeZone` | Bắt buộc với mọi `/api` trừ hai đường dẫn (`authentication.md`) | Request của widget khách, link trong email, webhook sẽ bị 400 nếu không gửi. Cần miễn trừ hoặc mặc định UTC | TECH02, TECH04 |
| Ngôn ngữ email cho khách | Văn bản backend theo culture của request, mặc định `vi` (`localization.md`); chưa entity nào lưu ngôn ngữ người nhận | Email gửi từ Hangfire không có request. Cần lưu ngôn ngữ của người nhận và render theo đó | TECH04, TECH05 |
| Danh sách endpoint ẩn danh | `authentication.md` liệt kê từng endpoint `[AllowAnonymous]` | Controller widget đầu tiên làm danh sách lỗi thời. Nên chuyển thành quy tắc "được ghim bằng test" | TECH02 |
| Token SignalR cho app | JWT chỉ đọc từ header hoặc cookie | Client Flutter thường gửi token qua query string `access_token` khi nối `/hubs` | TECH05 (khi làm SDK Flutter) |
| Quyền gán vai trò | Đã giải quyết ở module 00 phần 4: mọi phép gán, gỡ vai trò và gỡ thành viên (`MemberService`) khoá tổ chức, đọc lại người gọi từ cơ sở dữ liệu rồi tính BR-58 qua `RoleGrantPolicy`; chỉ Owner gán, gỡ được Owner. Thu hồi, mời lại lời mời theo BR-58 làm ở module 00 phần 5 | PRD-00 cấm leo quyền: chỉ Chủ sở hữu gán, gỡ được Owner; không ai cấp, gỡ hay làm mất hiệu lực quyền mình không có, kể cả khi gỡ một thành viên khỏi tổ chức hay thu hồi, mời lại một lời mời (BR-58, AC-158, AC-253, AC-254, AC-258) | TECH00 — giải quyết ở `00-nen-tang.md` §5.3, §12 (đã duyệt 2026-10-23); thành viên đã triển khai ở module 00 phần 4, lời mời ở phần 5 |
| Quyền sửa vai trò tự tạo | Đã giải quyết ở module 00 phần 4: `RoleService.CreateAsync`, `UpdateAsync` chỉ nhận quyền người gọi đang có, và chỉ sửa vai trò mà người gọi có mọi quyền trong đó (BR-58 điểm 2, 3) | PRD-00 chỉ cho đưa vào hay bỏ ra quyền mình có (BR-58 điểm 2 và 3, AC-156, AC-198) | TECH00 — giải quyết ở `00-nen-tang.md` §5.3, §12 (đã duyệt 2026-10-23); đã triển khai ở module 00 phần 4 |
| Xoá vai trò tự tạo | Đã giải quyết ở module 00 phần 4: `RoleService.DeleteAsync` khoá tổ chức, từ chối vai trò hệ thống (403), kiểm BR-58 `CanTakeAway` (403), rồi trả 409 `RoleHasHolders` khi còn thành viên đang hoạt động giữ vai trò; route `DELETE api/organization/roles/{roleId}` | PRD-00 chỉ cho xoá khi không thành viên nào giữ vai trò; còn người giữ thì từ chối, không đổi gì; người xoá phải có mọi quyền trong vai trò. Lời mời đang chờ không tính là giữ vai trò; người đã bị gỡ khỏi tổ chức cũng không (BR-58 điểm 3, BR-60, FR-52, AC-247 đến AC-252, AC-256, AC-257, BR-63 điểm 5; khi vi phạm cả hai, báo "không đủ quyền" trước, D2) | TECH00 — giải quyết ở `00-nen-tang.md` §5.3, §12 (đã duyệt 2026-10-23); đã triển khai ở module 00 phần 4 |
| Quyền đọc và tải file | Mọi thành viên đang hoạt động (`ActiveOrganizationMember`) tải lên (`POST /api/files`), xem danh sách, xem và tải mọi file của tổ chức theo id (`GET /api/files/{id}`, `/download`); chỉ `DELETE` cần `files.manage` (`FilesController`). `FileDto` trả kèm URL công khai; với Local storage (mặc định) URL đó đọc được không cần đăng nhập (`UseStaticFiles`, `authentication.md`) | Nếu ảnh chụp của feedback lưu thành `StoredFile`, thành viên ngoài phạm vi sản phẩm xem được ảnh qua id, và ai có URL cũng xem được không cần đăng nhập, trái phạm vi xem của PRD-00 (AC-195) | TECH00 (phần nền giải quyết ở `00-nen-tang.md` §7.5, QĐ-22, đã duyệt 2026-10-23), TECH02 (entity và endpoint ảnh feedback) |
| Kiểm thử nhúng widget | E2E không chạy trong CI và chỉ phủ đăng nhập, phiên (`testing.md`) | Chưa có tầng test cho loader và iframe khác origin, vốn là đường nhận feedback chính | TECH02 |

## Danh mục

| Mã | Module | PRD | Trạng thái | Phiên bản | appsec |
|---|---|---|---|---|---|
| TECH00 | Nền tảng: mô hình sản phẩm, quyền theo sản phẩm, đa tổ chức, chuyển design system mới vào frontend (`00-nen-tang.md`) | PRD00 v1.4 | Approved | 1.0 | Đạt v0.9 |
| TECH01 | Quản lý sản phẩm | PRD01 | Chưa bắt đầu | — | — |
| TECH02 | Thu thập feedback (widget web), định danh, chế độ khách | PRD02 | Chưa bắt đầu | — | Bắt buộc |
| TECH03 | Xử lý feedback | PRD03 | Chưa bắt đầu | — | — |
| TECH04 | Cổng người báo, link riêng của khách | PRD04 | Chưa bắt đầu | — | Bắt buộc |
| TECH05 | Thông báo | PRD05 | Chưa bắt đầu | — | — |
| TECH06 | Tổng quan và chỉ số | PRD06 | Chưa bắt đầu | — | — |
| TECH07 | Khảo sát (quyền cấp hệ thống, ẩn danh) | PRD07 | Chưa bắt đầu | — | Bắt buộc |

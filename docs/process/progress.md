# Tiến độ module

Trạng thái từng giai đoạn của mỗi module (xem `lifecycle.md`). Giá trị: `—` chưa bắt đầu, `Draft`, `In review`, `Approved`, `Đang làm`, `Xong`. Cột *Code* và *Merged* dùng `—`, `Đang làm`, `Xong`.

Trạng thái chi tiết của PRD nằm ở `docs/prd/README.md` và của tài liệu kỹ thuật ở `docs/tech/README.md`; bảng này chỉ tổng hợp. Phiên chính cập nhật nó (`lifecycle.md` § Tiến độ module).

| Module | PRD | UI | Tech | appsec | Plan | Code | Merged |
|---|---|---|---|---|---|---|---|
| 00 Nền tảng | Approved | Approved | Approved | bắt buộc | — | — | — |
| 01 Quản lý sản phẩm | Approved | Approved | — | không cần | — | — | — |
| 02 Thu thập feedback (widget) | — | — | — | bắt buộc | — | — | — |
| 03 Xử lý feedback | — | — | — | không cần | — | — | — |
| 04 Cổng người báo | — | — | — | bắt buộc | — | — | — |
| 05 Thông báo | — | — | — | không cần | — | — | — |
| 06 Tổng quan và chỉ số | — | — | — | không cần | — | — | — |
| 07 Khảo sát | — | — | — | bắt buộc | — | — | — |

## Số đo

Ghi ở retro cuối module (`lifecycle.md` § Lỗi lặp và retro cuối module). Số ngày tính từ bản nháp PRD đầu tiên tới khi PR cuối của module merge; tỉ lệ `fix` là số commit `fix` trên tổng commit của các nhánh module; cột theo hạng lấy từ `bun scripts/risk-tier.ts --fix-ratio`.

| Module | Số ngày | Số PR | Tỉ lệ commit `fix` | `fix` theo hạng (0/1/2) | Câu hỏi tới người dùng | Ghi chú |
|---|---|---|---|---|---|---|
| 00 Nền tảng | 6 (22–27/10, tới lúc code xong) | 1 code (#32) + 6 tài liệu (#16, #20, #25, #27, #28, #29) | 42/158 (27%) | — | hơn 80, trước khi có SA | Số tạm, chốt khi #32 merge |

# Bối cảnh hệ thống

Các con số và giới hạn mà mọi quyết định kiến trúc, thiết kế dựa vào, chủ yếu do `solution-architect` dùng. Giá trị ghi **[Đề xuất]** là ước lượng chờ người dùng xác nhận hoặc sửa; giá trị ghi **[Người dùng]** đã được xác nhận. Đổi một con số ở đây thì ghi thêm một dòng vào *Lịch sử* ở cuối tệp.

## Người dùng

- **Ai dùng Hub:** team IT tiếp nhận và xử lý feedback, mỗi sản phẩm có một hoặc vài người phụ trách. Người báo là đồng nghiệp và tester trong công ty. Khách hàng ngoài internet gửi feedback qua widget ở chế độ khách. Nguồn: spec lõi §1, §8. **[Người dùng]**
- **Đa tổ chức:** nhiều công ty dùng chung một Hub, dữ liệu tách biệt. **[Người dùng]**
- **Ngôn ngữ:** tiếng Việt mặc định, có tiếng Anh (`.claude/rules/localization.md`). **[Người dùng]**
- **Thiết bị:** Hub dùng chủ yếu trên máy tính. Trang link riêng của khách và widget phải dùng tốt trên điện thoại. Hỗ trợ trình duyệt hiện đại, hai phiên bản gần nhất. **[Đề xuất]**

## Quy mô

Toàn Hub, trong 2 năm đầu. **[Đề xuất]**

| Mục | Ước lượng |
|---|---|
| Tổ chức | ≤ 20 |
| Tài khoản | ≤ 500 |
| Sản phẩm | ≤ 20 mỗi tổ chức, ≤ 100 toàn Hub |
| Feedback mới | ≤ 200 mỗi ngày toàn Hub; lúc cao điểm ≤ 30 mỗi phút |
| Ảnh mỗi feedback | ≤ 5 ảnh, mỗi ảnh ≤ 5 MB |
| Dung lượng file tăng thêm | ≤ 20 GB mỗi năm |
| Người dùng Hub đồng thời | ≤ 100 |

Hệ quả cho thiết kế:
- Danh sách cỡ trăm dòng thì phân trang đơn giản hoặc "Xem thêm" là đủ.
- Chưa cần tìm kiếm toàn văn riêng, phân vùng dữ liệu hay nhiều instance API.

## Triển khai và vận hành

- **Hạ tầng:** một máy chủ, chạy `docker-compose.prod.yml`:
  - PostgreSQL;
  - API, kèm Hangfire chạy trong cùng tiến trình;
  - nginx phục vụ frontend và chuyển tiếp `/api`, `/hubs`.

  File lưu trên ổ của máy chủ (provider Local). Cache và SignalR chạy trong bộ nhớ; Redis chỉ bật khi chạy nhiều instance (`.claude/decisions.md` 2026-08-25). **[Đề xuất: giữ như hiện tại]**
- **Theo dõi:** Prometheus và Grafana (`monitoring/`), log của API. **[Người dùng: đã có trong repo]**
- **Sẵn sàng:**
  - Hub quan trọng trong giờ hành chính.
  - Mất vài phút khi cập nhật phiên bản là chấp nhận được.
  - Không cần chạy song song nhiều máy. **[Đề xuất]**
- **Sao lưu:** cơ sở dữ liệu và thư mục file sao lưu mỗi ngày, giữ 14 ngày. Chấp nhận mất tối đa 24 giờ dữ liệu (RPO), khôi phục trong 4 giờ (RTO). **[Đề xuất]**
- **Cập nhật phiên bản:**
  - Migration phải chạy được trên dữ liệu đang có.
  - Không đổi tên hay xoá cột trong cùng bản phát hành mà code cũ còn dùng.
  - Lỗi thì quay về phiên bản trước bằng image cũ cộng bản sao lưu. **[Đề xuất]**

## Hiệu năng

**[Đề xuất]**
- **Trang Hub:** dữ liệu hiện ra trong 1 giây ở 95% lượt mở (p95), với quy mô trên.
- **Widget:**
  - không làm chậm trang của sản phẩm;
  - mã nạp ban đầu ≤ 50 KB sau nén;
  - phần còn lại chỉ nạp khi người dùng mở widget.
- **Gửi feedback:** kể cả tải ảnh, xong trong 3 giây trên mạng văn phòng.

## Chi phí

- Không dùng dịch vụ trả phí ngoài máy chủ và dịch vụ gửi email. Mọi dịch vụ trả phí mới phải hỏi người dùng. **[Đề xuất]**
- Không có gói trả phí, hạn mức hay billing cho tổ chức (spec lõi §2). **[Người dùng]**

## Dữ liệu và quyền riêng tư

- **Dữ liệu cá nhân:** tên và email của thành viên; email của khách; ảnh chụp màn hình có thể chứa dữ liệu của sản phẩm. Hub phải tuân thủ Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân. **[Đề xuất]**
- **Widget không gửi đi:** token, cookie, header, body request hay dữ liệu nhạy cảm của sản phẩm chủ (spec lõi §2, phản mục tiêu). **[Người dùng]**
- **Thời gian giữ dữ liệu:** feedback và file giữ đến khi tổ chức tự xoá. Chưa có chức năng xoá tự động. **[Đề xuất]**

## Lịch sử

| Ngày | Thay đổi | Nguồn |
|---|---|---|
| 2026-10-23 | Bản đầu, soạn từ spec lõi, `docker-compose.prod.yml`, `.claude/decisions.md`; các mục [Đề xuất] chờ người dùng xác nhận | Phiên chính |

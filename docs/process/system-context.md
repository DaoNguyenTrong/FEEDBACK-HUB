# Bối cảnh hệ thống

Các con số và giới hạn mà mọi quyết định kiến trúc, thiết kế dựa vào, chủ yếu do `solution-architect` dùng. Giá trị ghi **[Đề xuất]** là ước lượng chờ người dùng xác nhận hoặc sửa; giá trị ghi **[Người dùng]** đã được xác nhận. Đổi một con số ở đây thì ghi thêm một dòng vào *Lịch sử* ở cuối tệp.

## Người dùng

- **Ai dùng Hub:** team IT tiếp nhận và xử lý feedback, mỗi sản phẩm có một hoặc vài người phụ trách. Người báo là đồng nghiệp và tester trong công ty. Khách hàng ngoài internet gửi feedback qua widget ở chế độ khách. Nguồn: spec lõi §1, §8. **[Người dùng]**
- **Đa tổ chức:** nhiều công ty dùng chung một Hub, dữ liệu tách biệt. **[Người dùng]**
- **Ngôn ngữ:** tiếng Việt mặc định, có tiếng Anh (`.claude/rules/localization.md`). **[Người dùng]**
- **Thiết bị:** mọi màn hình, của Hub, trang link riêng và widget, đều phải tối ưu cho cả điện thoại, máy tính bảng và máy tính, không chỉ "dùng được". Hỗ trợ trình duyệt hiện đại, hai phiên bản gần nhất. **[Người dùng]**

## Quy mô

Toàn Hub, trong 2 năm đầu. **[Người dùng]**

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
- Chưa cần tìm kiếm toàn văn riêng hay phân vùng dữ liệu.
- Nhiều máy API là để chịu lỗi (§ Triển khai), không phải vì tải.

## Triển khai và vận hành

- **Hạ tầng: bắt buộc chạy song song nhiều máy chủ API** sau một bộ cân bằng tải. **[Người dùng]** Mọi trạng thái vì vậy phải dùng chung giữa các máy, không nằm trong bộ nhớ hay trên ổ của riêng một máy:
  - **PostgreSQL** dùng chung. Hangfire lưu job trên PostgreSQL, và khoá Data Protection lưu trong cơ sở dữ liệu; cả hai đã sẵn sàng.
  - **Redis bắt buộc**, không còn là tuỳ chọn: cache (`CacheSettings:Provider`) và SignalR backplane (`RealtimeSettings:Backplane`) chạy qua Redis, chế độ `noeviction` (`.claude/decisions.md` 2026-08-25, 2026-10-16).
  - **File lưu ở kho dùng chung kiểu S3** (provider SeaweedFS hoặc S3), không dùng provider Local trên production.
  - **Giới hạn tần suất request** (`AddRateLimiter`) hiện đếm riêng từng máy. Cần đếm chung qua Redis, hoặc chấp nhận và ghi rõ là giới hạn nhân theo số máy; `tech-lead` quyết ở tài liệu kỹ thuật.
  - Mỗi máy API không giữ trạng thái phiên; frontend vẫn do nginx phục vụ.
- **Theo dõi:** Prometheus và Grafana (`monitoring/`), log của API. **[Người dùng: đã có trong repo]**
- **Sẵn sàng:**
  - Hub quan trọng trong giờ hành chính.
  - Mất vài phút khi cập nhật phiên bản là chấp nhận được; có nhiều máy thì nên cập nhật lần lượt từng máy. **[Người dùng]**
  - Một máy API hỏng thì Hub vẫn chạy trên các máy còn lại. **[Người dùng]**
- **Sao lưu:** cơ sở dữ liệu và thư mục file sao lưu mỗi ngày, giữ 14 ngày. Chấp nhận mất tối đa 24 giờ dữ liệu (RPO), khôi phục trong 4 giờ (RTO). **[Người dùng]**
- **Cập nhật phiên bản:**
  - Migration phải chạy được trên dữ liệu đang có.
  - Không đổi tên hay xoá cột trong cùng bản phát hành mà code cũ còn dùng.
  - Lỗi thì quay về phiên bản trước bằng image cũ cộng bản sao lưu. **[Người dùng]**

## Hiệu năng

**[Người dùng]**
- **Trang Hub:** dữ liệu hiện ra trong 1 giây ở 95% lượt mở (p95), với quy mô trên.
- **Widget:**
  - không làm chậm trang của sản phẩm;
  - mã nạp ban đầu ≤ 50 KB sau nén;
  - phần còn lại chỉ nạp khi người dùng mở widget.
- **Gửi feedback:** kể cả tải ảnh, xong trong 3 giây trên mạng văn phòng.

## Chi phí

- Không dùng dịch vụ trả phí ngoài máy chủ và dịch vụ gửi email. Mọi dịch vụ trả phí mới phải hỏi người dùng. **[Người dùng]**
- Không có gói trả phí, hạn mức hay billing cho tổ chức (spec lõi §2). **[Người dùng]**

## Dữ liệu và quyền riêng tư

- **Dữ liệu cá nhân:** tên và email của thành viên; email của khách; ảnh chụp màn hình có thể chứa dữ liệu của sản phẩm. Hub phải tuân thủ Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân. **[Người dùng]**
- **Widget không gửi đi:** token, cookie, header, body request hay dữ liệu nhạy cảm của sản phẩm chủ (spec lõi §2, phản mục tiêu). **[Người dùng]**
- **Thời gian giữ dữ liệu:** feedback và file giữ đến khi tổ chức tự xoá. Chưa có chức năng xoá tự động. **[Người dùng]**

## Lịch sử

| Ngày | Thay đổi | Nguồn |
|---|---|---|
| 2026-10-23 | Người dùng quyết: tối ưu cho điện thoại, máy tính bảng và máy tính; bắt buộc chạy song song nhiều máy chủ API (kéo theo Redis bắt buộc, kho file dùng chung, giới hạn tần suất dùng chung); mọi mục còn lại đồng ý theo đề xuất | Quyết định của người dùng |
| 2026-10-23 | Bản đầu, soạn từ spec lõi, `docker-compose.prod.yml`, `.claude/decisions.md`; các mục [Đề xuất] chờ người dùng xác nhận | Phiên chính |

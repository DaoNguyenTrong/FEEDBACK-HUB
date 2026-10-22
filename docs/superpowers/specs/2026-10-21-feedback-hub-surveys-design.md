# FeedbackHub — Đợt khảo sát — thiết kế

> Dự án 2/4 của FeedbackHub, làm sau dự án 1 (`2026-10-21-feedback-hub-core-design.md`).
>
> Quy ước: **[Người dùng]** = người dùng đã quyết; **[Đề xuất]** = Claude đề xuất, người dùng đã đồng ý; **[Claude quyết]** = Claude tự quyết sau vòng review, người dùng chưa xác nhận riêng; **[Builder]** = để người xây quyết.

## 1. Vì sao làm

Thành công của FeedbackHub được đo bằng khảo sát định kỳ: mỗi tài khoản chấm điểm mức độ hài lòng với Hub. **[Người dùng]** Khảo sát xây ngay trong Hub, bật theo **đợt** khi admin cần. **[Người dùng]**

Đây là khảo sát **về Hub** cho người dùng Hub — không phải khảo sát NPS/CSAT gửi tới người dùng của các sản phẩm qua widget (ý đó đã được hỏi và loại).

## 2. Mục tiêu / không phải mục tiêu / phản mục tiêu

**Mục tiêu**
- Admin hệ thống tạo đợt khảo sát, chọn tổ chức nhận, xem kết quả tổng hợp.
- Người dùng trả lời nhanh trong Hub, tự chọn ẩn danh hay không.

**Không phải mục tiêu**
- Quản trị tổ chức xem kết quả. **[Người dùng]**
- Khảo sát gửi qua widget tới người dùng cuối của sản phẩm.
- Logic rẽ nhánh câu hỏi, khảo sát lặp tự động theo lịch.

- Chống suy luận ngược ở mức cơ sở dữ liệu (ví dụ "người tham gia trừ người gắn tên"), ngưỡng tối thiểu câu trả lời ẩn danh. Người vận hành không truy vấn ngược kiểu đó; chỉ cần ẩn danh trên giao diện và trong dữ liệu trả lời. **[Người dùng]**

**Phản mục tiêu**
- Câu trả lời ẩn danh hiện tên, tài khoản hay tổ chức của người trả lời ở bất kỳ màn hình, file xuất hay API nào.
- Lời mời khảo sát làm phiền đến mức người dùng né Hub.

## 3. Ai làm gì

- **Admin hệ thống** (người vận hành Hub, cấp trên mọi tổ chức) — tạo, sửa, mở/đóng đợt, xem kết quả. **[Người dùng]**
  - Starter kit hiện **chưa có** khái niệm admin cấp hệ thống: mọi quyền (`Permissions`, `SystemRoleKind` Owner/Admin/Member) đều theo tổ chức. Dự án này phải thêm vai trò/quyền cấp hệ thống; cách làm là **[Builder]** nhưng là thay đổi bảo mật → qua agent `appsec`; quyết định ghi theo `AGENTS.md` § Decision Log.
- **Người nhận**: mọi thành viên của các tổ chức được chọn. **[Người dùng]**
- **Quản trị tổ chức**: không xem kết quả. **[Người dùng]**

## 4. Đợt khảo sát **[Đề xuất]**

- Tên, mô tả ngắn, thời gian mở/đóng, danh sách tổ chức nhận (chọn từng tổ chức hoặc tất cả).
- Câu hỏi soạn tự do, có thứ tự, đánh dấu bắt buộc/tuỳ chọn; ba dạng:
  - **Chấm điểm**: thang 1–5 (sao) hoặc 0–10 (NPS), chọn khi tạo câu hỏi.
  - **Chọn đáp án**: một hoặc nhiều.
  - **Trả lời tự do**.
- Trạng thái đợt: Nháp → Đang mở → Đã đóng. Khi đã có người trả lời thì không sửa câu hỏi được nữa (chỉ đóng sớm/gia hạn).
- Đợt **tự mở và tự đóng** theo thời gian đặt (job nền); admin đóng sớm hoặc gia hạn được, kể cả mở lại một đợt đã đóng bằng cách gia hạn. **[Claude quyết]**
- Danh sách tổ chức nhận **chốt khi mở đợt** ("tất cả" = các tổ chức tồn tại lúc mở); thành viên của các tổ chức đó tính theo **hiện tại** — người vào giữa đợt vẫn được mời. Tỉ lệ tham gia lấy mẫu số theo thành viên hiện tại. **[Claude quyết]**

## 5. Trải nghiệm người trả lời **[Đề xuất]**

- Đăng nhập Hub trong thời gian đợt mở, là thành viên của tổ chức được chọn, chưa trả lời → hộp mời khảo sát.
- Lựa chọn: **Trả lời ngay** · **Để sau** (mời lại khi mở Hub sau ít nhất 24 giờ — không chờ đăng nhập lại, vì phiên tự gia hạn nên người dùng hằng ngày có thể không bao giờ đăng nhập lại **[Claude quyết]**) · **Bỏ qua** (không mời lại đợt này).
- Một tài khoản trả lời **một lần** mỗi đợt — kể cả khi thuộc nhiều tổ chức được chọn. **[Đề xuất]** Câu trả lời gắn tên được tính vào **tổ chức đang hoạt động** lúc trả lời. **[Claude quyết]**
- Toggle **"Gửi ẩn danh"** do người trả lời chọn. **[Người dùng]**

## 6. Ẩn danh **[Đề xuất + Người dùng]**

Khi ẩn danh, lưu **tách rời**:
- *Đã tham gia*: (đợt, tài khoản) — chỉ để không mời lại.
- *Câu trả lời*: không có khoá nào trỏ về tài khoản, và **không lưu tổ chức** của người trả lời. **[Người dùng]** Không cần biện pháp chống ghép theo thời gian/thứ tự (xem §2).
- Không ghi nội dung câu trả lời ẩn danh vào `AuditLog` hay log ứng dụng.
- Hệ quả: câu trả lời ẩn danh chỉ vào số liệu **toàn đợt**, không vào số liệu theo tổ chức.

Không ẩn danh: câu trả lời gắn tài khoản; admin hệ thống thấy tên.

## 7. Kết quả **[Đề xuất]**

Chỉ admin hệ thống:
- Tỉ lệ tham gia (theo tổng và theo tổ chức — dựa trên bản ghi *Đã tham gia*, nên vẫn đúng cho cả người ẩn danh).
- Điểm trung bình/phân bố cho câu chấm điểm (NPS tính theo chuẩn với thang 0–10), tỉ lệ cho câu chọn đáp án, danh sách câu trả lời tự do.
- Lọc theo tổ chức.
- Xuất CSV/Excel.

## 8. Còn lại cho builder

- Mô hình quyền cấp hệ thống (§3).
- Lưu trữ câu hỏi/câu trả lời (bảng chuẩn hoá hay JSON).
- Thông báo mở đợt qua kênh có sẵn (trong Hub/email/web push) hay chỉ hộp mời khi đăng nhập.
- Thêm mục vào `docs/feature-map.md`.

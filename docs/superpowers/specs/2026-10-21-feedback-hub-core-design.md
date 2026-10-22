# FeedbackHub lõi + widget web — thiết kế

> Dự án 1/4 của FeedbackHub. Các dự án tiếp theo, làm sau và có tài liệu riêng: (2) Đợt khảo sát — `2026-10-21-feedback-hub-surveys-design.md`; (3) SDK Flutter; (4) AI phân loại + connector Plane/GitHub/GitLab.
>
> Quy ước trong tài liệu: **[Người dùng]** = người dùng đã quyết; **[Đề xuất]** = Claude đề xuất, người dùng đã đồng ý; **[Claude quyết]** = Claude tự quyết sau vòng review, người dùng chưa xác nhận riêng; **[Builder]** = để người xây quyết.

## 1. Vì sao làm

Hiện nay lỗi và yêu cầu tính năng được báo qua tin nhắn Zalo: không có ảnh chụp màn hình, không biết thiết bị/trình duyệt, không có chỗ theo dõi — IT phải hỏi đi hỏi lại và feedback dễ bị trôi. Người báo là đồng nghiệp trong công ty và tester; ý kiến khách hàng thường đến gián tiếp qua họ. Team IT tiếp nhận; mỗi sản phẩm có một hoặc vài người phụ trách.

FeedbackHub thay kênh đó bằng: **widget gắn vào sản phẩm** tự thu ngữ cảnh kỹ thuật + ảnh màn hình, và **Hub** nơi feedback được thông báo, phân công, xử lý và theo dõi đến khi đóng — người báo cũng xem tiến độ ở đó.

## 2. Mục tiêu, không phải mục tiêu, phản mục tiêu

**Mục tiêu**
- Mỗi feedback đến kèm đủ ngữ cảnh để tái hiện mà không phải hỏi lại: ảnh (đánh dấu được), thiết bị/trình duyệt/màn hình, URL, lỗi console, request lỗi, dữ liệu sản phẩm truyền vào.
- Người phụ trách biết ngay khi có feedback mới.
- Hub quản lý **toàn bộ vòng đời** feedback; người báo xem tiến độ trong Hub (hoặc qua link riêng nếu là khách). **[Người dùng]**
- Đa tổ chức: nhiều công ty dùng chung một Hub, dữ liệu tách biệt. **[Người dùng]**

**Không phải mục tiêu (bản này)**
- Đồng bộ với Plane hay bất kỳ công cụ ngoài nào. **[Người dùng]**
- SDK Flutter / mobile (dự án 3) — nhưng API nhận feedback phải dùng được cho nó, kể cả nhận diện người báo là thành viên Hub (§8.1). **[Người dùng]**
- AI phân loại / mô tả dựa trên KB (dự án 4).
- Ghi chuỗi thao tác (breadcrumbs) / session replay. **[Người dùng]**
- Kênh thông báo chat (Zalo, Telegram, Slack…). **[Người dùng]**
- Gói trả phí, hạn mức, billing.

**Phản mục tiêu** — chạy đúng nhưng vẫn là thất bại nếu:
- Widget làm hỏng, làm chậm hay đổi giao diện trang sản phẩm chủ (CSS/JS rò rỉ).
- Widget gửi đi token, cookie, header, body request hay dữ liệu nhạy cảm của sản phẩm chủ.
- Người báo/khách thấy được feedback của người khác, hoặc tổ chức này thấy dữ liệu tổ chức kia.
- Báo lỗi qua widget phiền hơn nhắn Zalo (quá nhiều bước bắt buộc).

**Đo thành công:** khảo sát hài lòng định kỳ (dự án 2) **[Người dùng]**, đối chiếu với hai số Hub tự đo **[Đề xuất]**: tỉ lệ feedback từng vào *Cần thêm thông tin*, và thời gian từ lúc gửi đến lúc được xử lý lần đầu (*Đã tiếp nhận*, hoặc *Từ chối*/*Trùng lặp* thẳng từ *Mới*). Cả hai tính bằng **trung vị**. Feedback chưa được tiếp nhận sau **24 giờ** vào danh sách "cần chú ý" ở Tổng quan. **[Người dùng]**

## 3. Nền tảng có sẵn (repo hiện tại)

Xây trên starter kit trong repo này (.NET 10 Clean Architecture + Vue 3/naive-ui, xem `AGENTS.md`). Dùng lại:

| Có sẵn | Dùng cho |
|---|---|
| `Organization`, `OrganizationMember`, `Role`/`RolePermission` (vai trò tuỳ biến theo tổ chức), chuyển tổ chức | Đa tổ chức, vai trò cấp tổ chức |
| `Account`, đăng nhập JWT trong cookie `HttpOnly` (access + refresh, `AuthCookies`), `ExternalLogin` | Tài khoản Hub, SSO cho widget |
| `NotificationService` (lưu DB, đẩy SignalR, email qua Hangfire, `PushSubscription` web push) | Thông báo người phụ trách/người báo |
| `StoredFile` + storage Local/S3/SeaweedFS | Ảnh chụp, file đính kèm |
| `AuditLog` | Nền cho lịch sử thay đổi |
| Fallback policy "fail closed" (decision 2026-10-21) | Các endpoint công khai của widget phải opt-out tường minh |

## 4. Mô hình miền

```
Tổ chức
 ├─ Thành viên ── vai trò cấp tổ chức: Quản trị / Thành viên
 └─ Sản phẩm
      ├─ Cấu hình widget: khoá công khai, domain được phép, bật/tắt chế độ khách, giao diện nút
      ├─ Gán người: Phụ trách / Viewer (theo từng sản phẩm)
      └─ Feedback
           ├─ Loại, tiêu đề, mô tả, trạng thái, ưu tiên, người được giao, nhãn
           ├─ Người báo: tài khoản Hub (thành viên) HOẶC khách (tên/email/điện thoại — đều tuỳ chọn)
           ├─ Ngữ cảnh kỹ thuật (snapshot lúc gửi)
           ├─ Ảnh chụp + file đính kèm
           ├─ Bình luận (nội bộ | gửi người báo)
           ├─ Lịch sử thay đổi
           ├─ Liên kết trùng lặp → feedback gốc
           └─ Liên kết ra ngoài (để sẵn cho dự án 4; bản này chỉ lưu/hiển thị)
```

Tuân theo Entity Pattern (`static Create(XxxParams)`, validation trong `Update`) và `IHasOrganization` cho mọi entity thuộc tổ chức.

## 5. Vai trò và quyền **[Người dùng]**

| Vai trò | Cấp | Xem | Làm |
|---|---|---|---|
| **Quản trị** | Tổ chức | Mọi sản phẩm và feedback của tổ chức | Tạo/sửa sản phẩm, cấu hình widget, mời thành viên, gán Phụ trách/Viewer, mọi việc của Phụ trách |
| **Phụ trách** | Theo sản phẩm (một người có thể phụ trách nhiều sản phẩm) | Mọi feedback của sản phẩm được gán | Nhận thông báo, đổi trạng thái/loại/ưu tiên/nhãn, giao việc, bình luận (nội bộ + gửi người báo), đánh dấu trùng |
| **Viewer** | Theo sản phẩm | Mọi feedback của sản phẩm được gán | Chỉ đọc — không đổi gì, không bình luận **[Đề xuất]** |
| **Người báo** | Mọi thành viên tổ chức | Chỉ feedback **chính mình** gửi | Gửi qua widget, xem tiến độ, bình luận/bổ sung trên feedback của mình, **duyệt In review** (đạt / chưa đạt) trên feedback của mình |
| **Khách** | Không có tài khoản trong tổ chức | Chỉ feedback mình gửi, qua link riêng | Trả lời trên trang link riêng; duyệt In review và mở lại khi *Đã xử lý* trên trang đó **[Người dùng]** |

- Một người có thể vừa Phụ trách sản phẩm A vừa là người báo ở sản phẩm B. **[Đề xuất]**
- Một tài khoản thuộc được **nhiều tổ chức**, với vai trò khác nhau ở mỗi tổ chức (có sẵn: `OrganizationMember` + `OrganizationMemberRole`), và vai trò theo sản phẩm khác nhau ở mỗi sản phẩm. Phiên Hub luôn có **một tổ chức đang hoạt động** (`org_id` trong token, đổi qua `switch-organization`). **[Người dùng]**
- **Widget xét tư cách thành viên theo tổ chức sở hữu sản phẩm**, không theo tổ chức đang hoạt động của phiên: là thành viên tổ chức đó → gửi như thành viên, không cần chuyển tổ chức trước. **[Đề xuất]**
- Hộp feedback và mọi màn hình quản lý chỉ hiện dữ liệu của **tổ chức đang hoạt động**; "Feedback của tôi" cũng theo tổ chức đang hoạt động. **[Đề xuất]**
- Mọi thành viên tổ chức gửi được cho mọi sản phẩm của tổ chức. **[Đề xuất]**
- Người đã đăng nhập Hub nhưng **không thuộc** tổ chức sở hữu sản phẩm → gửi với tư cách **khách**, tên/email điền sẵn từ tài khoản Hub; không thấy gì bên trong tổ chức đó. **[Người dùng]**
- Người báo thấy bình luận *gửi người báo*, không bao giờ thấy bình luận *nội bộ*.
- **Người báo và khách thấy gì** **[Người dùng]**:
  - Mô tả, ảnh, file mình đã gửi — gắn nhãn "Bạn đã gửi".
  - Trạng thái và lịch sử **đổi trạng thái**; không thấy ưu tiên, người được giao hay lịch sử giao việc/đổi ưu tiên.
  - Tên người trả lời: thành viên tổ chức thấy tên thật của người IT; khách thấy "Đội hỗ trợ <tên sản phẩm>".
  - Không bình luận được khi feedback ở *Đóng* hoặc *Từ chối* — muốn trao đổi tiếp thì gửi feedback mới.
- Cách hiện thực Phụ trách/Viewer theo sản phẩm (bảng gán riêng vs mở rộng `Role`) là **[Builder]**, miễn là quyền được kiểm tra ở backend cho từng sản phẩm.

## 6. Vòng đời feedback

**Loại** (người báo chọn, IT sửa được): Lỗi · Yêu cầu tính năng · Góp ý/câu hỏi. **[Đề xuất]**

**Trạng thái** **[Đề xuất + Người dùng thêm In review]**

```
Mới ──► Đã tiếp nhận ──► Đang xử lý ──► In review ──► Đã xử lý ──► Đóng
 │           │                ▲    ▲         │              │
 │           │                │    └─────────┘ (review      │ (người báo báo chưa ổn
 │           │                │      không đạt)             │  → mở lại về Đang xử lý)
 │           └──► Cần thêm thông tin ──(người báo trả lời → về trạng thái trước)
 └──► Từ chối  /  Trùng lặp (gắn feedback gốc)
```

- **In review**: **bất kỳ Phụ trách nào của sản phẩm** (kể cả người đã sửa) **hoặc người báo** của feedback đó đều có thể kết thúc bước này — *đạt* → Đã xử lý, *chưa đạt* → Đang xử lý, **bắt buộc kèm lý do** (lưu thành bình luận gửi người báo). **[Người dùng]** Khi feedback vào In review, người báo được thông báo để kiểm tra lại. Bước này không bắt buộc: Phụ trách có thể chuyển thẳng Đang xử lý → Đã xử lý.
- **Chuyển trạng thái linh hoạt**: Phụ trách/Quản trị chuyển được giữa mọi trạng thái; sơ đồ trên là luồng chính, không phải ràng buộc cứng. Người báo chỉ có hai thao tác: duyệt In review và mở lại khi đang *Đã xử lý*. Tự động duy nhất: người báo trả lời → rời *Cần thêm thông tin*. **[Claude quyết]**
- **Mới** → thông báo người phụ trách sản phẩm.
- **Cần thêm thông tin**: khi người báo (tài khoản hoặc khách) gửi bình luận, tự quay về trạng thái ngay trước đó.
- **Trùng lặp**: gắn vào feedback gốc; khi gốc sang *Đã xử lý*, người báo của các bản trùng cũng được báo.
- **Đã xử lý → Đóng**: [Builder] quyết đóng thủ công hay tự đóng sau N ngày; người báo có thể mở lại khi còn ở *Đã xử lý*.
- Thuộc tính kèm: **ưu tiên** (IT đặt), **người được giao** (một người trong số Phụ trách của sản phẩm), **nhãn** tự do theo tổ chức.
- Mọi thay đổi trạng thái/thuộc tính ghi vào **lịch sử** (ai, lúc nào, từ → đến).

## 7. Widget web

### 7.1 Kiến trúc

Hai phần **[Đề xuất]**:

1. **Loader script** nhúng vào trang sản phẩm (`<script src=".../widget.js" data-key="<khoá công khai>">` hoặc tương đương). Nhiệm vụ: vẽ nút nổi; tạo iframe khi mở; **chụp màn hình trang chủ**; thu URL, viewport, lỗi console, request lỗi; nhận dữ liệu sản phẩm truyền vào; API JS để sản phẩm tự mở widget từ menu của mình. Phải cô lập hoàn toàn (Shadow DOM hoặc tương đương), không rò CSS/JS global, nhỏ, tải bất đồng bộ.
2. **Iframe** tải từ domain Hub: form, đăng nhập, chế độ khách, gửi dữ liệu. Iframe không đọc được DOM trang chủ — vì vậy chụp ảnh và thu lỗi nằm ở loader; hai bên trao đổi qua `postMessage` có kiểm tra origin.

Thư viện chụp ảnh (DOM-to-canvas vs `getDisplayMedia`) là **[Builder]**; yêu cầu: không hỏi quyền chia sẻ màn hình trong trường hợp thường, chấp nhận ảnh xấp xỉ.

### 7.2 Giao diện **[Người dùng chọn kiểu B]**

Mockup: `.superpowers/brainstorm/*/content/widget-flow.html` (bản nháp, không commit).

- **Nút nổi** góc màn hình; vị trí/màu/chữ cấu hình theo sản phẩm. **[Đề xuất]**
- Bấm → **hộp nhỏ bung lên từ nút nổi** (kiểu chat widget). **[Người dùng]**
- Form: dải định danh (người đang gửi + "Đổi"/"Không phải tôi"), chọn loại, tiêu đề, mô tả, ảnh chụp (Đánh dấu · Xoá · chụp lại), đính kèm file, khối "Thông tin tự thu thập" thu gọn (mở ra xem được), nút Gửi.
- **Đánh dấu ảnh** mở **toàn màn hình** rồi trở về hộp nhỏ: khoanh, mũi tên, vẽ tay, **che/làm mờ** thông tin nhạy cảm. **[Đề xuất]**
- Sau khi gửi: xác nhận + (thành viên) link tới feedback trong Hub.
- Trường bắt buộc: chỉ cần mô tả **hoặc** ảnh — giữ chi phí gửi thấp hơn nhắn Zalo. **[Đề xuất]**
- Đa ngôn ngữ vi/en theo chuẩn i18n của repo.

### 7.3 Thông tin tự thu thập **[Người dùng]**

- Trình duyệt + phiên bản, hệ điều hành, kích thước màn hình và viewport, tỉ lệ điểm ảnh, ngôn ngữ, múi giờ, URL trang.
- **Lỗi console** gần nhất (giới hạn, ví dụ 50). **[Đề xuất số]**
- **Request API lỗi** (4xx/5xx, lỗi mạng) gần nhất (giới hạn, ví dụ 20): chỉ phương thức, URL, mã lỗi, thời gian — **không header, không body**; xoá giá trị các tham số dạng `token`, `key`, `password`, `secret`… trong URL. **[Đề xuất]**
- **Dữ liệu sản phẩm truyền vào** qua API của loader: phiên bản app, môi trường, mã người dùng phía sản phẩm, và key/value tuỳ ý (có giới hạn kích thước).
- Người báo xem được mọi thứ sẽ gửi kèm và bỏ chọn từng nhóm trước khi gửi. **[Đề xuất]**

## 8. Định danh người gửi trong widget

Hub và các sản phẩm **không chung domain gốc**; email người dùng trong sản phẩm **không trùng** email tài khoản Hub. **[Người dùng]**

Thứ tự **[Đề xuất, người dùng đồng ý]**:

1. **Iframe dùng phiên Hub sẵn có** (cookie `HttpOnly` của Hub). Với sản phẩm khác site, cookie bên thứ ba bị chặn/phân vùng (Safari, Firefox) → dùng **Storage Access API**: iframe hiện nút "Tiếp tục với tài khoản Hub", người dùng bấm một lần cho mỗi sản phẩm. Điều kiện: người dùng đã từng mở Hub trực tiếp gần đây.
2. Không có phiên / trình duyệt từ chối → **popup đăng nhập Hub**, sau đó iframe nhận phiên. Cơ chế cụ thể (ví dụ cookie phân vùng CHIPS, token ngắn hạn trao qua `postMessage`) là **[Builder]**, với ràng buộc: token không bao giờ đọc được từ script của trang sản phẩm.
3. Hoặc chọn **gửi với tư cách khách** (§9).

Đã đăng nhập nhưng không thuộc tổ chức của sản phẩm → khách, điền sẵn từ tài khoản (§5).

### 8.1 Mobile (chuẩn bị cho dự án 3) **[Người dùng: app Flutter phải nhận ra thành viên]**

Cookie/iframe/Storage Access chỉ có trên trình duyệt. Để SDK Flutter làm sau mà không phải đổi hợp đồng API:
- API nhận feedback (và các API người báo dùng: feedback của tôi, bình luận) chấp nhận **cả** phiên cookie (web) **lẫn** access token dạng bearer (app) — thiết kế và kiểm thử ngay trong dự án này.
- Luồng đăng nhập cho app native (mở trình duyệt hệ thống, đăng nhập Hub, trả token về app — kiểu authorization code + PKCE) được **đặt chỗ trong hợp đồng** nhưng hiện thực ở dự án 3. **[Builder]** quyết có dựng endpoint ngay hay không.
- Ngữ cảnh thiết bị trong API ở dạng tổng quát (web: trình duyệt/viewport/URL; mobile: model máy, OS, phiên bản app, màn hình).
- Chống lạm dụng cho app (không có origin để kiểm tra) để dự án 3 quyết; hợp đồng không được giả định origin luôn có.

Thay đổi chính sách cookie/CORS/CSP (`frame-ancestors` theo domain được phép của sản phẩm, `SameSite`) là thay đổi bảo mật — phải qua agent `appsec`; quyết định ghi theo `AGENTS.md` § Decision Log.

## 9. Chế độ khách **[Người dùng]**

- Bật/tắt theo từng sản phẩm.
- Form thông tin người gửi: tên, email, số điện thoại — **tất cả tuỳ chọn**; gửi ẩn danh hoàn toàn được.
- Widget **ghi nhớ** thông tin trên trình duyệt đó cho sản phẩm đó, lần sau hiện "Đang gửi với tên … · Không phải tôi" — bấm để xoá và nhập lại (máy dùng chung).
- Có email → khách nhận email kèm **link riêng** tới trang theo dõi feedback đó: xem trạng thái + trao đổi (chỉ bình luận *gửi người báo*), trả lời không cần tài khoản. Link khó đoán, IT thu hồi được. **[Đề xuất]**
- Link hết hạn sau **90 ngày kể từ lần cập nhật cuối** của feedback. Link hết hạn hoặc bị thu hồi → trang báo link không còn hiệu lực và cho nhập email để nhận link mới (chỉ gửi nếu email khớp người gửi feedback; chịu giới hạn email/địa chỉ ở dưới). **[Người dùng]**
- Không email → gửi một chiều.
- Email cho khách chỉ ở các mốc: đã nhận, IT hỏi thêm (*Cần thêm thông tin* hoặc bình luận gửi người báo), *Đã xử lý*, *Từ chối*. **[Đề xuất]**
- Feedback khách hiển thị nhãn "Khách" trong Hub.

Chế độ khách **mở cho người ngoài internet**: khách hàng gửi trực tiếp, không cần qua đồng nghiệp. **[Người dùng]**

**Chống lạm dụng endpoint công khai** **[Claude quyết]**:
- **Captcha ẩn bật mặc định** cho mọi lượt gửi của khách (thành viên đã đăng nhập không cần); chọn nhà cung cấp là [Builder].
- Giới hạn tần suất theo IP + khoá sản phẩm, chặt hơn cho khách.
- Giới hạn kích thước ảnh/file/payload.
- **Giới hạn số email gửi tới cùng một địa chỉ** (theo giờ/ngày) — vì ai cũng có thể gõ email người khác, widget không được thành công cụ spam.
- Danh sách domain được phép dùng cho CSP `frame-ancestors` và kiểm tra origin, nhưng chỉ là **lớp phụ**: origin trang chủ do trình duyệt/script báo lên, có thể giả.

## 10. Thông báo **[Người dùng: 3 kênh có sẵn]**

Kênh: trong Hub (realtime), email, web push. Mỗi người chọn kênh nhận trong cài đặt cá nhân. **[Đề xuất]**

| Sự kiện | Ai nhận |
|---|---|
| Feedback mới | Mọi Phụ trách của sản phẩm |
| Được giao feedback | Người được giao |
| Người báo bình luận / trả lời | Người được giao (chưa giao → mọi Phụ trách) |
| Trạng thái đổi, bình luận *gửi người báo* | Người báo (thành viên: trong Hub + kênh họ chọn; khách: email theo §9) |
| Feedback gốc của bản trùng được xử lý | Người báo của bản trùng |
| Feedback vào In review | Người báo (để kiểm tra lại bản sửa) |
| Người báo duyệt In review (đạt/chưa đạt) | Người được giao |

Cài đặt kênh nhận theo từng người chưa có trong starter kit — xây mới.

Thông báo đến từ **mọi tổ chức** của người nhận, không chỉ tổ chức đang hoạt động; mỗi thông báo ghi rõ tổ chức. Bấm vào → Hub tự chuyển sang tổ chức đó rồi mở feedback. **[Đề xuất]**

Dùng `NotificationService`/`DedupeKey` sẵn có; email tiếng Việt theo decision 2026-08-04.

## 11. Màn hình Hub **[Đề xuất]**

| Màn hình | Cho ai | Nội dung |
|---|---|---|
| Hộp feedback | Quản trị, Phụ trách, Viewer | Danh sách lọc theo sản phẩm/trạng thái/loại/ưu tiên/người được giao/nhãn, tìm từ khoá, đếm chưa xử lý |
| Chi tiết feedback | như trên | Ảnh (phóng to), mô tả, ngữ cảnh kỹ thuật, đổi trạng thái/giao việc/ưu tiên/nhãn, bình luận nội bộ hoặc gửi người báo, lịch sử, đánh dấu trùng |
| Feedback của tôi | Người báo | Đã gửi gì, trạng thái, trả lời khi IT hỏi |
| Trang theo dõi qua link riêng | Khách | Trạng thái + trao đổi của một feedback |
| Quản lý sản phẩm | Quản trị | Tạo sản phẩm, mã nhúng widget, domain được phép, bật/tắt khách, giao diện nút, gán Phụ trách/Viewer |
| Tổng quan | Quản trị, Phụ trách | Mới/tồn theo sản phẩm, hai số đo ở §2 |

## 12. API

- API nhận feedback là **hợp đồng chung cho widget web và SDK Flutter sau này**: định danh sản phẩm bằng khoá công khai, nhận ngữ cảnh thiết bị theo dạng tổng quát (web và mobile đều điền được), ảnh/file upload tách hay gộp là **[Builder]**.
- Theo `api-contract.md`: export OpenAPI vào `shared/openapi/`, frontend sinh client bằng orval.
- Khoá công khai của sản phẩm **không phải** `ApiKey` hiện có (đó là bí mật của tổ chức) — nó công khai, nằm trong mã nhúng, chỉ cho phép gửi feedback vào đúng sản phẩm đó.
- Tạo khoá mới: khoá cũ **còn hiệu lực 24 giờ** để kịp cập nhật mã nhúng, rồi ngừng. **[Người dùng]**
- Domain được phép nhận wildcard `*.ten-mien.vn`; `localhost:<cổng>` chỉ nhận khi quản trị bật riêng cho sản phẩm đó. **[Người dùng]**

## 12b. Thiết kế giao diện

Đã duyệt trên Claude Design: https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn — design language "Lớp kính · WeatherPlus" (sáng + tối), bảng System là chuẩn token/component; Main (hộp feedback, danh sách/bảng kanban, chi tiết), Widget (các trạng thái), Mine, Guest/GuestPhone, Products, Overview. Thay đổi giao diện sau này qua agent `ui-designer` (`.claude/agents/ui-designer.md`). Thiết kế thay thế theme hiện tại của frontend (`tailwind.css`, `naive-theme.ts`).

## 13. Còn lại cho builder

- Thư viện chụp ảnh, công cụ đánh dấu, cách đóng gói loader script (build riêng trong `frontend/` hay package mới).
- Cơ chế phiên cụ thể trong iframe (§8), cách lưu Phụ trách/Viewer (§5), tự đóng sau *Đã xử lý* (§6).
- Thời hạn link riêng của khách, giới hạn tần suất/kích thước cụ thể.
- Chia nhỏ thành các phase/PR.
- Thêm mục tương ứng vào `docs/feature-map.md`.

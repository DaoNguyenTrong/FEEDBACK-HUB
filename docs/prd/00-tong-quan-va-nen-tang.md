# PRD-00 — Tổng quan và nền tảng: thuật ngữ, vai trò và quyền, vòng đời trạng thái, tổ chức và sản phẩm

| Mục | Giá trị |
|---|---|
| Mã | `PRD00` (tiền tố cho mọi mã trong tài liệu này) |
| Trạng thái | Draft |
| Phiên bản | 0.2 |
| Người duyệt | Người dùng (chủ sản phẩm FeedbackHub) |
| Cập nhật | 2026-10-22 |
| Phụ thuộc | Không có (mọi PRD khác phụ thuộc PRD này) |
| Thiết kế | Chưa thiết kế riêng cho PRD này. Canvas đã duyệt trước PRD: https://claude.ai/artifact/21QUWu78AZFfwSxcaWmjQn — bảng *System* (màu trạng thái, loại, ưu tiên, nhãn "Khách", hai kiểu bình luận) dùng làm tham chiếu |
| Kế hoạch | Chưa có |

Tài liệu này là **nơi duy nhất** định nghĩa thuật ngữ (§3.1), vai trò (§3.2), ma trận quyền (§3.3) và vòng đời trạng thái feedback (§3.4). Các PRD khác dẫn chiếu, ví dụ `xem PRD-00 §3.2 Vai trò`, và chỉ bổ sung phần riêng của module.

## 1. Bối cảnh và vấn đề

Hiện nay lỗi và yêu cầu tính năng được báo qua tin nhắn Zalo: không có ảnh chụp màn hình, không biết thiết bị hay trình duyệt, không có chỗ theo dõi. IT phải hỏi đi hỏi lại và feedback dễ bị trôi. Người báo là đồng nghiệp trong công ty và tester; ý kiến khách hàng thường đến gián tiếp qua họ. Team IT tiếp nhận; mỗi sản phẩm có một hoặc vài người phụ trách.

FeedbackHub thay kênh đó bằng một widget gắn vào từng sản phẩm và một Hub nơi feedback được thông báo, phân công, xử lý và theo dõi đến khi đóng. Nhiều công ty dùng chung một Hub nên cần một nền chung, định nghĩa một lần: ai là ai, ai được làm gì, feedback đi qua những trạng thái nào và dữ liệu của mỗi tổ chức được tách riêng ra sao. Nền tảng hiện có (tài khoản, tổ chức, vai trò tự tạo từ danh mục quyền cố định, thông báo) chưa có khái niệm sản phẩm, feedback, vai trò theo sản phẩm hay admin cấp hệ thống.

## 2. Mục tiêu, không phải mục tiêu, phản mục tiêu

- **Mục tiêu**:
  - Mọi PRD, thiết kế và tài liệu kỹ thuật dùng cùng một bộ thuật ngữ, vai trò, quyền và trạng thái.
  - Mỗi người chỉ thấy và làm đúng phần việc của vai trò mình; dữ liệu của tổ chức này không bao giờ lộ sang tổ chức khác. Ngoại lệ có chủ đích duy nhất ở cấp hệ thống: admin hệ thống (BR-21).
  - Một tài khoản làm việc được ở nhiều tổ chức, với vai trò khác nhau ở mỗi nơi.
  - Vòng đời feedback rõ ràng cho cả IT lẫn người báo: luôn biết feedback đang ở đâu và ai đang giữ bước tiếp theo.
- **Không phải mục tiêu**:
  - Màn hình quản lý sản phẩm, widget, hộp feedback, cổng người báo, thông báo, tổng quan, khảo sát: thuộc PRD01–PRD07.
  - Gói trả phí, hạn mức, billing.
  - Đồng bộ với công cụ ngoài (Plane, GitHub, GitLab), SDK Flutter, AI phân loại: để sau.
  - Đổi cơ chế đăng nhập, đăng ký, phiên của nền tảng.
- **Phản mục tiêu** (chạy đúng nhưng vẫn là thất bại nếu):
  - Người báo hoặc khách thấy được feedback của người khác.
  - Thành viên của tổ chức này thấy dữ liệu của tổ chức kia. Việc admin hệ thống thấy mọi tổ chức là ngoại lệ có chủ đích (BR-21), không bao giờ là đường để thành viên một tổ chức thấy dữ liệu tổ chức khác (BR-22).
  - Quyền admin hệ thống cấp được từ bên trong Hub (BR-20).
  - Một thao tác bị chặn trên giao diện nhưng vẫn làm được khi gọi thẳng vào hệ thống.
  - Báo lỗi qua widget phiền hơn nhắn Zalo.
- **Đo thành công** (chỉ số dùng chung, các PRD khác dẫn về đây; cách tính: BR-40, BR-44):
  - **Khảo sát hài lòng định kỳ** của người dùng Hub (PRD07).
  - **Tỉ lệ feedback từng vào *Cần thêm thông tin***. Càng thấp càng tốt: feedback đến đủ ngữ cảnh. Bằng số feedback gửi trong kỳ đã từng vào *Cần thêm thông tin* chia cho số feedback gửi trong kỳ.
  - **Thời gian đến lần xử lý đầu tiên**, tính bằng **trung vị**: từ lúc gửi đến lúc feedback lần đầu sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*.
  - Kỳ đo mặc định là 30 ngày gần nhất, đổi được trên Tổng quan. Mọi khoảng thời gian tính theo giờ lịch (24/7).
  - **Danh sách cần chú ý**: feedback chưa được xử lý lần đầu sau **24 giờ** lịch kể từ lúc gửi.

## 3. Người dùng và vai trò

### 3.1 Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| Hub | Ứng dụng web FeedbackHub nơi thành viên tổ chức xem, xử lý và theo dõi feedback. |
| Tài khoản | Tài khoản đăng nhập Hub của một người. Một tài khoản thuộc được nhiều tổ chức. |
| Tổ chức | Một công ty hoặc đơn vị dùng Hub. Dữ liệu (sản phẩm, feedback, nhãn, vai trò) của mỗi tổ chức tách biệt hoàn toàn với các tổ chức khác; chỉ admin hệ thống thấy được mọi tổ chức (BR-21). |
| Thành viên | Tài khoản đang hoạt động trong một tổ chức. Thành viên bị gỡ khỏi tổ chức không còn là thành viên. |
| Tổ chức đang làm việc | Tổ chức mà phiên làm việc của người dùng đang ở trong đó. Mỗi phiên có tối đa một tổ chức đang làm việc; người dùng tự chuyển được. |
| Vai trò tổ chức | Gói quyền gán cho thành viên trong một tổ chức. Nền tảng có sẵn ba vai trò hệ thống: *Chủ sở hữu* (Owner, có mọi quyền), *Admin*, *Member*; tổ chức tạo thêm vai trò riêng từ một danh mục quyền cố định. |
| Quyền Quản trị FeedbackHub | Quyền trong danh mục quyền cố định; thành viên có quyền này là Quản trị (BR-18). |
| Quyền quản lý thành viên | Quyền sẵn có của nền tảng để thêm, gỡ thành viên và đổi vai trò tổ chức (BR-19). |
| Lời mời | Email mời một người chưa có tài khoản vào một tổ chức với một vai trò tổ chức đã chọn; hết hạn sau 7 ngày, thu hồi được (BR-24). |
| Sản phẩm | Một ứng dụng hoặc website của tổ chức có gắn widget. Mỗi sản phẩm thuộc đúng một tổ chức. |
| Vai trò theo sản phẩm | Vai trò của một thành viên đối với một sản phẩm cụ thể: *Phụ trách* hoặc *Viewer* (§3.2). |
| Widget | Nút nổi và hộp gửi feedback gắn vào trang của sản phẩm. Chi tiết: PRD02. |
| Khoá công khai | Mã định danh sản phẩm nằm trong đoạn mã nhúng widget. Chỉ cho phép gửi feedback vào đúng sản phẩm đó, không phải mật khẩu hay bí mật. Chi tiết: PRD01. |
| Domain được phép | Danh sách tên miền nơi widget của sản phẩm được phép chạy. Chi tiết: PRD01. |
| Chế độ khách | Tuỳ chọn theo từng sản phẩm, cho phép người không phải thành viên gửi feedback. Chi tiết: PRD02. |
| Feedback | Một báo cáo gửi qua widget: loại, tiêu đề, mô tả, ảnh, file, ngữ cảnh kỹ thuật, trạng thái, ưu tiên, người được giao, nhãn, bình luận, lịch sử. |
| Mã feedback | Mã ngắn để gọi tên một feedback, ví dụ `FB-129`: tiền tố `FB-` và một số tăng dần theo tổ chức (BR-42). |
| Loại | Phân loại feedback, hiển thị *Lỗi*, *Tính năng*, *Góp ý* (BR-41). Người báo chọn khi gửi, Phụ trách và Quản trị sửa được. |
| Trạng thái | Vị trí của feedback trong vòng đời (§3.4). |
| In review | **Trong sản phẩm**, luôn là trạng thái feedback "đã sửa, chờ kiểm tra lại" (§3.4). Không nhầm với trạng thái *In review* của tài liệu PRD trong quy trình nội bộ. |
| Ưu tiên | Mức độ gấp do IT đặt: *Cao*, *Trung bình*, *Thấp*; mặc định chưa đặt (BR-41). Người báo và khách không thấy. |
| Người được giao | Một người trong số Phụ trách của sản phẩm, chịu trách nhiệm chính cho một feedback. Có thể để trống (*Chưa giao*). |
| Nhãn | Thẻ tự do theo tổ chức, gắn lên feedback để lọc và nhóm. |
| Người báo | Thành viên tổ chức đã gửi một feedback (§3.2). |
| Khách | Người gửi feedback mà không phải thành viên của tổ chức sở hữu sản phẩm (§3.2). |
| Link riêng | Đường link khó đoán gửi qua email cho khách, mở trang theo dõi đúng một feedback. Thu hồi được, hết hạn. Chi tiết: PRD04. |
| Bình luận nội bộ | Bình luận chỉ team trong tổ chức thấy; người báo và khách không bao giờ thấy. |
| Bình luận gửi người báo | Bình luận người báo (hoặc khách) thấy và được thông báo. |
| Ngữ cảnh kỹ thuật | Thông tin widget tự thu lúc gửi: thiết bị, trình duyệt, màn hình, URL, lỗi console, request lỗi, dữ liệu sản phẩm truyền vào. Chi tiết: PRD02. |
| Trùng lặp / feedback gốc | Feedback đã được báo trước đó; bản trùng gắn với một feedback gốc cùng sản phẩm (BR-37). |
| Lịch sử | Bản ghi mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. |
| Xử lý lần đầu | Lần đầu feedback sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*. |
| Hộp feedback | Màn hình danh sách và bảng feedback cho IT. Chi tiết: PRD03. |
| Feedback của tôi | Màn hình người báo xem feedback mình đã gửi. Chi tiết: PRD04. |
| Tổng quan | Màn hình số liệu theo sản phẩm. Chi tiết: PRD06. |
| Danh sách cần chú ý | Các feedback chưa được xử lý lần đầu sau 24 giờ lịch (§2). |
| Admin hệ thống | Người vận hành Hub, đứng trên mọi tổ chức, có toàn quyền và thấy toàn bộ hệ thống (BR-21); do người vận hành máy chủ chỉ định (BR-20). Chạy khảo sát (PRD07). |
| Đợt khảo sát | Một lần khảo sát hài lòng về Hub do admin hệ thống mở cho các tổ chức được chọn. Chi tiết: PRD07. |

### 3.2 Vai trò

| Vai trò | Cấp | Là ai | Phạm vi nhìn thấy | Nguồn |
|---|---|---|---|---|
| **Quản trị** | Tổ chức | Thành viên có quyền Quản trị FeedbackHub (BR-18): Chủ sở hữu luôn có, vai trò Admin có sẵn, Member không có, vai trò tự tạo có khi tổ chức thêm quyền này | Mọi sản phẩm và feedback của tổ chức, không cần được gán | Spec lõi §5; quyết định 2026-10-22 |
| **Phụ trách** | Theo sản phẩm | Thành viên được Quản trị gán Phụ trách cho một sản phẩm; một người phụ trách được nhiều sản phẩm | Mọi feedback của các sản phẩm mình phụ trách | Spec lõi §5 |
| **Viewer** | Theo sản phẩm | Thành viên được gán Viewer cho một sản phẩm | Mọi feedback của các sản phẩm mình được gán, kể cả bình luận nội bộ, và Tổng quan của các sản phẩm đó; chỉ đọc | Spec lõi §5; quyết định 2026-10-22 |
| **Người báo** | Tổ chức | Mọi thành viên của tổ chức, với feedback do chính mình gửi | Chỉ feedback chính mình gửi | Spec lõi §5 |
| **Khách** | Không thuộc tổ chức | Người không có tài khoản trong tổ chức sở hữu sản phẩm, kể cả người đã đăng nhập Hub nhưng thuộc tổ chức khác | Chỉ feedback mình gửi, qua link riêng | Spec lõi §5, §9 |
| **Admin hệ thống** | Hệ thống | Người vận hành Hub, do người vận hành máy chủ chỉ định, không cấp được qua Hub (BR-20) | Mọi tổ chức và toàn bộ dữ liệu của chúng (sản phẩm, feedback, thành viên, khảo sát), với toàn quyền (BR-21) | Spec khảo sát §3; quyết định 2026-10-22 |

Quy tắc kết hợp vai trò: §6 (BR-01 đến BR-08, BR-18 đến BR-22).

### 3.3 Ma trận quyền

Ký hiệu: **✓** được làm · **—** không được · **R** chỉ xem · **?n** chưa quyết, xem câu hỏi mở số *n* (đề xuất ghi trong câu hỏi). "Của mình" = feedback chính người đó gửi. Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức. **BR-19** ở một ô: thao tác đi theo quyền quản lý thành viên của nền tảng, không theo vai trò ở cột. Admin hệ thống có mọi quyền của mọi vai trò tổ chức ở mọi tổ chức (BR-21); các ô **—** ở cột này là thao tác gắn với danh tính người gửi feedback, không phải quyền.

| Thao tác | Quản trị | Phụ trách | Viewer | Người báo | Khách | Admin hệ thống |
|---|---|---|---|---|---|---|
| **Hệ thống** | | | | | | |
| Cấp, bỏ quyền admin hệ thống | — | — | — | — | — | — (chỉ người vận hành máy chủ, BR-20) |
| **Tổ chức** | | | | | | |
| Tạo tổ chức mới (mọi tài khoản đã xác minh email, BR-23) | ✓ | ✓ | ✓ | ✓ | ✓ nếu có tài khoản Hub | ✓ |
| Thêm, gỡ thành viên tổ chức, đổi vai trò tổ chức | BR-19 | BR-19 | BR-19 | BR-19 | — | ✓ |
| Gửi, thu hồi lời mời qua email | ?30 | ?30 | ?30 | ?30 | — | ✓ |
| Chuyển tổ chức đang làm việc (giữa các tổ chức mình là thành viên) | ✓ | ✓ | ✓ | ✓ | — | ✓ (vào tổ chức mình không là thành viên: ?27) |
| **Sản phẩm** | | | | | | |
| Tạo, sửa sản phẩm và cấu hình widget | ✓ | — | — | — | — | ✓ |
| Gán, bỏ Phụ trách và Viewer | ✓ | — | — | — | — | ✓ (tự gán mình: ?28) |
| **Feedback: xem** | | | | | | |
| Xem hộp feedback | ✓ mọi sản phẩm | ✓ sản phẩm được gán | ✓ sản phẩm được gán | — | — | ✓ mọi tổ chức |
| Xem chi tiết feedback, ảnh, file | ✓ | ✓ | R | R của mình | R của mình, qua link riêng | ✓ |
| Xem ngữ cảnh kỹ thuật | ✓ | ✓ | R | R của mình | — | ✓ |
| Xem ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên | ✓ | ✓ | R | — | — | ✓ |
| Xem trạng thái và lịch sử đổi trạng thái | ✓ | ✓ | R | R của mình | R của mình | ✓ |
| Xem bình luận gửi người báo | ✓ | ✓ | R | R của mình | R của mình | ✓ |
| Xem bình luận nội bộ | ✓ | ✓ | R | — | — | ✓ |
| Xem đầy đủ email, điện thoại của khách (BR-38) | ✓ ở chi tiết; danh sách che | ✓ ở chi tiết; danh sách che | — (chỉ bản che) | — | — | ✓ ở chi tiết; danh sách che |
| Xem Tổng quan | ✓ | ✓ | R sản phẩm được gán | — | — | ✓ |
| Xem "Feedback của tôi" | ✓ | ✓ | ✓ | ✓ | — | — |
| **Feedback: thao tác** | | | | | | |
| Gửi feedback qua widget | ✓ | ✓ | ✓ | ✓ mọi sản phẩm của tổ chức | ✓ khi sản phẩm bật chế độ khách | — (gửi theo tư cách tài khoản, BR-06) |
| Đổi trạng thái, giữa mọi trạng thái (§3.4, BR-25) | ✓ | ✓ | — | chỉ hai thao tác dưới | chỉ hai thao tác dưới | ✓ |
| Kết thúc In review (đạt / chưa đạt) | ✓ | ✓ | — | ✓ của mình | ✓ của mình, qua link riêng | ✓ |
| Mở lại khi *Đã xử lý* | ✓ | ✓ | — | ✓ của mình, kèm lý do | ✓ của mình, qua link riêng, kèm lý do | ✓ |
| Đổi loại, ưu tiên, nhãn | ✓ | ✓ | — | — | — | ✓ |
| Giao việc | ✓ | ✓ | — | — | — | ✓ (giao cho chính mình: ?28) |
| Đánh dấu trùng lặp | ✓ | ✓ | — | — | — | ✓ |
| Viết bình luận nội bộ | ✓ | ✓ | — | — | — | ✓ |
| Viết bình luận gửi người báo | ✓ | ✓ | — | — | — | ✓ (tên hiển thị: ?29) |
| Bình luận, bổ sung trên feedback của mình (trừ khi *Đóng* / *Từ chối* / *Trùng lặp*) | — | — | — | ✓ | ✓ qua link riêng | — |
| Thu hồi link riêng của khách | ✓ | ✓ | — | — | — | ✓ |
| **Khảo sát** (PRD07) | | | | | | |
| Tạo, mở, đóng đợt khảo sát; xem kết quả | — | — | — | — | — | ✓ |
| Trả lời khảo sát | ✓ | ✓ | ✓ | ✓ | — | — |

### 3.4 Vòng đời trạng thái

| Trạng thái | Nghĩa | Ai giữ bước tiếp theo |
|---|---|---|
| Mới | Vừa gửi, chưa ai xem xét | Phụ trách |
| Đã tiếp nhận | IT đã xem và nhận xử lý | Phụ trách |
| Cần thêm thông tin | IT cần người báo bổ sung | Người báo / khách |
| Đang xử lý | Đang sửa hoặc làm | Người được giao |
| In review | Đã sửa, chờ kiểm tra lại (không bắt buộc) | Phụ trách hoặc người báo |
| Đã xử lý | Đã xong; người báo còn mở lại được trong 7 ngày trước khi tự đóng | Người báo (nếu chưa ổn) |
| Đóng | Kết thúc | — |
| Từ chối | Không làm (có lý do) | — |
| Trùng lặp | Đã có feedback gốc | Theo feedback gốc |

Luồng chính (spec lõi §6):

```
Mới ──► Đã tiếp nhận ──► Đang xử lý ──► In review ──► Đã xử lý ──► Đóng (tay, hoặc tự đóng sau 7 ngày)
 │           │                ▲    ▲         │              │
 │           │                │    └─────────┘ chưa đạt     │ người báo mở lại
 │           │                └─────────────────────────────┘ (về Đang xử lý)
 │           └──► Cần thêm thông tin ──(người báo trả lời → về trạng thái trước)
 └──► Từ chối (kèm lý do)  /  Trùng lặp (gắn feedback gốc)
```

- Phụ trách chuyển thẳng được *Đang xử lý* → *Đã xử lý*, bỏ qua In review.
- Sơ đồ là luồng chính, không phải ràng buộc cứng: Phụ trách và Quản trị chuyển được giữa mọi trạng thái, kể cả mở lại từ *Đóng*, *Từ chối*, *Trùng lặp* (BR-25).
- Chỉ có hai chuyển tự động: rời *Cần thêm thông tin* khi người báo hoặc khách trả lời (BR-12), và tự đóng sau 7 ngày ở *Đã xử lý* (BR-27).

## 4. User story

| Mã | Là… | Tôi muốn… | Để… | Ưu tiên |
|---|---|---|---|---|
| PRD00-US-01 | thành viên của nhiều tổ chức | chuyển giữa các tổ chức mình thuộc về | chỉ thấy và làm việc với dữ liệu của tổ chức đang chọn | Must |
| PRD00-US-02 | Quản trị | thấy mọi sản phẩm và feedback của tổ chức, gán Phụ trách và Viewer cho từng sản phẩm | mỗi sản phẩm có đúng người nhận và xử lý feedback | Must |
| PRD00-US-03 | Phụ trách | chỉ thấy và xử lý feedback của các sản phẩm mình phụ trách | tập trung vào việc của mình | Must |
| PRD00-US-04 | Viewer | đọc feedback của sản phẩm được gán mà không thay đổi được gì | theo dõi tình hình mà không làm xáo trộn việc của IT | Must |
| PRD00-US-05 | người báo | chỉ thấy feedback mình đã gửi và trạng thái của chúng | biết feedback của mình đang ở đâu mà không phải hỏi | Must |
| PRD00-US-06 | khách | theo dõi feedback mình gửi qua link riêng mà không cần tài khoản | biết kết quả và trả lời khi IT hỏi | Must |
| PRD00-US-07 | Quản trị của một tổ chức | chắc chắn thành viên tổ chức khác không thấy dữ liệu của mình | dùng chung Hub mà vẫn an toàn | Must |
| PRD00-US-08 | bất kỳ ai dùng Hub | thấy cùng một bộ trạng thái với cùng một nghĩa | hiểu feedback đang ở bước nào và ai giữ bước tiếp theo | Must |
| PRD00-US-09 | người báo hoặc Phụ trách | xác nhận bản sửa ở bước In review là đạt hay chưa | feedback chỉ coi là xong khi đã kiểm tra lại | Must |
| PRD00-US-10 | Phụ trách | xem lịch sử ai đổi gì, lúc nào | truy vết khi có tranh cãi hay nhầm lẫn | Must |
| PRD00-US-11 | admin hệ thống | có vai trò riêng đứng trên các tổ chức | chạy khảo sát hài lòng trên nhiều tổ chức (PRD07) | Should |
| PRD00-US-12 | chủ sản phẩm FeedbackHub | có chỉ số thành công định nghĩa một lần | mọi màn hình số liệu tính giống nhau | Should |
| PRD00-US-13 | admin hệ thống | thấy và làm được mọi việc trong mọi tổ chức | vận hành và hỗ trợ toàn bộ Hub | Should |
| PRD00-US-14 | người quản lý thành viên của tổ chức | mời đồng nghiệp, tester chưa có tài khoản qua email | họ bắt đầu dùng widget mà không phải tự đăng ký rồi chờ được thêm | Should |
| PRD00-US-15 | Phụ trách | feedback đã xử lý tự đóng khi không ai mở lại | hộp feedback không tích tụ feedback *Đã xử lý* | Should |

## 5. Yêu cầu chức năng

| Mã | Yêu cầu | Story | Ưu tiên |
|---|---|---|---|
| PRD00-FR-01 | Hệ thống phải gắn mọi sản phẩm, feedback, nhãn và vai trò theo sản phẩm với đúng một tổ chức, và không cho ai ngoài tổ chức đó xem hay sửa chúng, trừ các ngoại lệ ghi trong ma trận quyền (khách qua link riêng; admin hệ thống theo BR-21). | US-07 | Must |
| PRD00-FR-02 | Hệ thống phải cho một tài khoản là thành viên của nhiều tổ chức, với vai trò tổ chức và vai trò theo sản phẩm riêng ở từng tổ chức. | US-01 | Must |
| PRD00-FR-03 | Hệ thống phải cho mỗi phiên làm việc có tối đa một tổ chức đang làm việc, và cho người dùng chuyển sang tổ chức khác mình là thành viên mà không phải đăng nhập lại. | US-01 | Must |
| PRD00-FR-04 | Hệ thống phải giới hạn hộp feedback, quản lý sản phẩm, Tổng quan và "Feedback của tôi" vào dữ liệu của tổ chức đang làm việc. | US-01, US-07 | Must |
| PRD00-FR-05 | Hệ thống phải cho Quản trị thấy mọi sản phẩm và feedback của tổ chức mà không cần được gán vào sản phẩm. | US-02 | Must |
| PRD00-FR-06 | Hệ thống phải cho Quản trị gán một thành viên làm Phụ trách hoặc Viewer của từng sản phẩm, và bỏ gán. | US-02 | Must |
| PRD00-FR-07 | Hệ thống phải giới hạn Phụ trách và Viewer vào feedback của các sản phẩm họ được gán. | US-03, US-04 | Must |
| PRD00-FR-08 | Hệ thống phải chặn Viewer khỏi mọi thao tác thay đổi feedback, kể cả bình luận. | US-04 | Must |
| PRD00-FR-09 | Hệ thống phải giới hạn người báo vào feedback chính mình gửi, và chỉ hiện cho họ các phần ma trận quyền cho phép (không ưu tiên, không người được giao, không bình luận nội bộ). | US-05 | Must |
| PRD00-FR-10 | Hệ thống phải cho khách xem và trao đổi trên đúng một feedback mình gửi qua link riêng, không cần tài khoản. | US-06 | Must |
| PRD00-FR-11 | Hệ thống phải xác định tư cách người gửi qua widget theo tổ chức sở hữu sản phẩm, không theo tổ chức đang làm việc của phiên. | US-05, US-06 | Must |
| PRD00-FR-12 | Hệ thống phải áp dụng ma trận quyền cho mọi thao tác, kể cả khi thao tác được gọi trực tiếp mà không qua màn hình của Hub. | US-03, US-04, US-05, US-07 | Must |
| PRD00-FR-13 | Hệ thống phải dùng đúng chín trạng thái ở §3.4, với tên giống nhau trên mọi màn hình, email và thông báo. | US-08 | Must |
| PRD00-FR-14 | Hệ thống phải cho Phụ trách và Quản trị đổi trạng thái feedback theo §3.4, giữa mọi trạng thái (BR-25). | US-03, US-08 | Must |
| PRD00-FR-15 | Hệ thống phải tự đưa feedback đang ở *Cần thêm thông tin* về trạng thái ngay trước đó khi người báo hoặc khách gửi bình luận. | US-05, US-06 | Must |
| PRD00-FR-16 | Hệ thống phải cho bất kỳ Phụ trách nào của sản phẩm (kể cả người đã sửa), Quản trị, hoặc người báo của feedback kết thúc bước In review: *đạt* → *Đã xử lý*, *chưa đạt* → *Đang xử lý* kèm lý do bắt buộc, lý do lưu thành bình luận gửi người báo. | US-09 | Must |
| PRD00-FR-17 | Hệ thống phải cho người báo và khách mở lại feedback của mình khi nó đang ở *Đã xử lý*, đưa về *Đang xử lý*. | US-05, US-06 | Must |
| PRD00-FR-18 | Hệ thống phải yêu cầu gắn một feedback gốc cùng sản phẩm khi chuyển feedback sang *Trùng lặp*. | US-08 | Must |
| PRD00-FR-19 | Hệ thống phải ghi vào lịch sử mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. | US-10 | Must |
| PRD00-FR-20 | Hệ thống phải chặn người báo và khách bình luận khi feedback ở *Đóng*, *Từ chối* hoặc *Trùng lặp*, và cho họ biết cần gửi feedback mới nếu muốn trao đổi tiếp. | US-05, US-06 | Must |
| PRD00-FR-21 | Hệ thống phải có vai trò admin hệ thống tách khỏi mọi tổ chức, có mọi quyền mà bất kỳ vai trò tổ chức nào có, ở mọi tổ chức, mà không cần là thành viên (BR-21), cộng với các quyền khảo sát trong ma trận quyền. | US-11, US-13 | Should |
| PRD00-FR-22 | Hệ thống phải cho người báo chọn loại feedback khi gửi, và cho Phụ trách, Quản trị sửa loại sau đó. | US-03, US-05 | Must |
| PRD00-FR-23 | Hệ thống phải có quyền Quản trị FeedbackHub trong danh mục quyền cố định, luôn có ở Chủ sở hữu, có sẵn ở vai trò Admin, không có ở Member, và cho tổ chức đưa quyền này vào vai trò tự tạo. | US-02 | Must |
| PRD00-FR-24 | Hệ thống phải cho mọi tài khoản đã xác minh email tạo tổ chức mới và trở thành Chủ sở hữu của tổ chức đó. | US-01 | Must |
| PRD00-FR-25 | Hệ thống phải cho mời người chưa có tài khoản vào tổ chức qua email với một vai trò tổ chức đã chọn; người nhận đăng ký xong thì vào thẳng tổ chức với vai trò đó; lời mời hết hạn sau 7 ngày và thu hồi được. | US-14 | Should |
| PRD00-FR-26 | Hệ thống phải tự chuyển feedback sang *Đóng* khi nó ở *Đã xử lý* đủ 7 ngày mà không ai mở lại. | US-15 | Should |
| PRD00-FR-27 | Hệ thống phải yêu cầu lý do khi chuyển feedback sang *Từ chối* và lưu lý do thành bình luận gửi người báo. | US-05, US-06, US-08 | Must |
| PRD00-FR-28 | Hệ thống phải yêu cầu người báo và khách ghi lý do ngắn khi mở lại, và lưu lý do thành bình luận gửi người báo. | US-05, US-06 | Must |
| PRD00-FR-29 | Hệ thống phải xử lý việc gỡ một thành viên khỏi tổ chức theo BR-30: giữ feedback họ đã gửi, bỏ vai trò theo sản phẩm, đưa feedback giao cho họ về *Chưa giao*, chặn họ xem và nhận thông báo của tổ chức. | US-07 | Must |
| PRD00-FR-30 | Hệ thống phải ngừng nhận feedback qua widget, vô hiệu link riêng và chặn chuyển vào tổ chức khi tổ chức bị vô hiệu (BR-31). | US-07 | Must |
| PRD00-FR-31 | Hệ thống phải hiện trang chào cho tài khoản chưa thuộc tổ chức nào, với lựa chọn tạo tổ chức hoặc chờ được thêm, và không hiện hộp feedback hay "Feedback của tôi". | US-01 | Must |
| PRD00-FR-32 | Hệ thống phải báo feedback mới cho Quản trị của tổ chức khi sản phẩm chưa có Phụ trách nào. | US-02 | Must |
| PRD00-FR-33 | Hệ thống phải che email và số điện thoại của khách trong danh sách, chỉ hiện đầy đủ ở màn hình chi tiết cho người có quyền theo BR-38. | US-03, US-04 | Must |
| PRD00-FR-34 | Hệ thống phải cấp cho mỗi feedback một mã `FB-<số>` tăng dần, không trùng trong tổ chức. | US-08 | Must |
| PRD00-FR-35 | Hệ thống phải chặn chuyển sang *Cần thêm thông tin* với feedback của khách không để email, kèm giải thích. | US-08 | Must |
| PRD00-FR-36 | Hệ thống phải không cho gửi qua widget khi người dùng đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm và sản phẩm tắt chế độ khách, kèm lời giải thích và gợi ý liên hệ người quản lý sản phẩm. | US-06 | Must |
| PRD00-FR-37 | Hệ thống phải trả về cùng một thông báo "không tìm thấy" khi người không có quyền mở một feedback hay sản phẩm, như khi nó không tồn tại. | US-07 | Must |
| PRD00-FR-38 | Hệ thống phải không cho cấp hay bỏ quyền admin hệ thống từ bên trong Hub; chỉ người vận hành máy chủ chỉ định được. | US-07, US-13 | Must |

## 6. Quy tắc nghiệp vụ

| Mã | Quy tắc | Áp dụng cho |
|---|---|---|
| PRD00-BR-01 | Mọi thành viên đang hoạt động của tổ chức là người báo tiềm năng và gửi được feedback cho mọi sản phẩm của tổ chức. | FR-09, FR-11 |
| PRD00-BR-02 | Một người có thể vừa là Phụ trách sản phẩm A vừa là người báo ở sản phẩm B, và có vai trò theo sản phẩm khác nhau ở các sản phẩm khác nhau. | FR-02, FR-06 |
| PRD00-BR-03 | Với mỗi sản phẩm, một thành viên có tối đa một vai trò theo sản phẩm: Phụ trách hoặc Viewer. | FR-06 |
| PRD00-BR-04 | Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức, kể cả kết thúc In review. | FR-05, FR-16 |
| PRD00-BR-05 | Người được giao phải là một trong các Phụ trách của sản phẩm. | FR-06, FR-14 |
| PRD00-BR-06 | Gửi qua widget: là thành viên của tổ chức sở hữu sản phẩm → gửi với tư cách người báo, không cần chuyển tổ chức trước. Đã đăng nhập Hub nhưng không thuộc tổ chức đó → gửi với tư cách khách, tên và email điền sẵn từ tài khoản, không thấy gì bên trong tổ chức đó. | FR-11 |
| PRD00-BR-07 | Người báo và khách thấy: mô tả, ảnh, file mình đã gửi (gắn nhãn "Bạn đã gửi"); trạng thái và lịch sử đổi trạng thái; bình luận gửi người báo. Không thấy: ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên, bình luận nội bộ. Ngữ cảnh kỹ thuật: BR-39. | FR-09, FR-10 |
| PRD00-BR-08 | Tên người trả lời hiện cho người báo là thành viên: tên thật của người IT. Hiện cho khách: "Đội hỗ trợ <tên sản phẩm>". Khi người trả lời là admin hệ thống: câu hỏi mở 29. | FR-09, FR-10 |
| PRD00-BR-09 | Người báo chỉ có hai thao tác đổi trạng thái: kết thúc In review trên feedback của mình, và mở lại khi feedback của mình đang ở *Đã xử lý*. Khách có đúng hai thao tác đó trên trang link riêng. | FR-14, FR-16, FR-17 |
| PRD00-BR-10 | Bước In review không bắt buộc: Phụ trách có thể chuyển thẳng *Đang xử lý* → *Đã xử lý*. | FR-14, FR-16 |
| PRD00-BR-11 | Kết thúc In review với *chưa đạt* phải có lý do; lý do được lưu thành bình luận gửi người báo. | FR-16 |
| PRD00-BR-12 | Rời *Cần thêm thông tin* tự động chỉ khi người báo hoặc khách gửi bình luận; feedback quay về đúng trạng thái ngay trước khi vào *Cần thêm thông tin*. | FR-15 |
| PRD00-BR-13 | Khi feedback gốc sang *Đã xử lý*, người báo của mọi bản trùng lặp của nó được thông báo (kênh: PRD05). | FR-18 |
| PRD00-BR-14 | Lịch sử chỉ được thêm, không ai sửa hay xoá được mục lịch sử. | FR-19 |
| PRD00-BR-15 | Hộp feedback và mọi màn hình quản lý chỉ hiện dữ liệu của tổ chức đang làm việc; "Feedback của tôi" cũng theo tổ chức đang làm việc. | FR-04 |
| PRD00-BR-16 | Quản trị tổ chức không xem được kết quả khảo sát. | FR-21 |
| PRD00-BR-17 | Xử lý lần đầu tính một lần duy nhất cho mỗi feedback, tại lần đầu nó sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*. | §2, PRD06 |
| PRD00-BR-18 | Thành viên là Quản trị khi một trong các vai trò tổ chức của họ có quyền Quản trị FeedbackHub. Chủ sở hữu luôn có quyền này; vai trò Admin có sẵn mặc định; Member không có; tổ chức đưa được quyền này vào vai trò tự tạo. | FR-23 |
| PRD00-BR-19 | Thêm, gỡ thành viên và đổi vai trò tổ chức đi theo quyền quản lý thành viên sẵn có của nền tảng, không theo quyền Quản trị FeedbackHub. | FR-23, FR-25 |
| PRD00-BR-20 | Quyền admin hệ thống chỉ do người vận hành máy chủ Hub chỉ định. Không ai cấp hay bỏ được quyền này từ bên trong Hub, kể cả Chủ sở hữu của một tổ chức hay chính admin hệ thống. | FR-38 |
| PRD00-BR-21 | Admin hệ thống có toàn quyền và thấy toàn bộ hệ thống: ở mọi tổ chức, có mọi quyền mà bất kỳ vai trò tổ chức nào có (Chủ sở hữu, Quản trị, Phụ trách), không cần là thành viên. Họ thấy và thao tác được trên sản phẩm, feedback, thành viên và khảo sát của mọi tổ chức. Đây là đường truy cập xuyên tổ chức **có chủ đích**, ngoại lệ duy nhất ở cấp hệ thống cho việc tách biệt dữ liệu; nền tảng hiện có không có admin cấp hệ thống và không có đường vượt quyền nào. Các thao tác gắn với danh tính người gửi (gửi feedback như người báo, "Feedback của tôi", bình luận như người báo, trả lời khảo sát) không thuộc quyền này: admin hệ thống làm các việc đó theo tư cách tài khoản của mình như mọi người. | FR-01, FR-21 |
| PRD00-BR-22 | Quyền của admin hệ thống không mở thêm đường nào để thành viên một tổ chức thấy dữ liệu của tổ chức khác. | FR-01, FR-21 |
| PRD00-BR-23 | Mọi tài khoản đã xác minh email tự tạo được tổ chức và trở thành Chủ sở hữu của tổ chức đó. | FR-24 |
| PRD00-BR-24 | Lời mời gửi tới một email chưa có tài khoản, kèm vai trò tổ chức đã chọn. Người nhận đăng ký xong thì vào thẳng tổ chức với vai trò đó. Lời mời hết hạn sau 7 ngày và thu hồi được trước khi được dùng; lời mời hết hạn hoặc đã thu hồi không đưa ai vào tổ chức. Ai gửi và thu hồi: câu hỏi mở 30. | FR-25 |
| PRD00-BR-25 | Phụ trách và Quản trị chuyển được feedback giữa mọi trạng thái, kể cả mở lại từ *Đóng*, *Từ chối*, *Trùng lặp*; sơ đồ §3.4 là luồng chính, không phải ràng buộc cứng. Các ràng buộc riêng vẫn áp dụng: *Trùng lặp* cần feedback gốc (FR-18), *Từ chối* cần lý do (BR-28), *Cần thêm thông tin* bị chặn với khách không để email (BR-43). | FR-14 |
| PRD00-BR-26 | Chỉ có hai chuyển trạng thái tự động: rời *Cần thêm thông tin* (BR-12) và tự đóng (BR-27). | FR-15, FR-26 |
| PRD00-BR-27 | Feedback ở *Đã xử lý* đủ 7 ngày liên tục mà không ai mở lại thì tự sang *Đóng*; lịch sử ghi người thực hiện là hệ thống. Phụ trách và Quản trị vẫn đóng tay được bất cứ lúc nào. | FR-26 |
| PRD00-BR-28 | Chuyển sang *Từ chối* phải có lý do; lý do được lưu thành bình luận gửi người báo. | FR-27 |
| PRD00-BR-29 | Người báo hoặc khách mở lại phải ghi một câu lý do ngắn; lý do được lưu thành bình luận gửi người báo, như *chưa đạt*. | FR-28 |
| PRD00-BR-30 | Thành viên bị gỡ khỏi tổ chức: feedback họ đã gửi giữ nguyên, tên hiện kèm "(đã rời tổ chức)"; họ mất quyền xem và không nhận thông báo của tổ chức; vai trò theo sản phẩm của họ bị bỏ; feedback đang giao cho họ về *Chưa giao*. | FR-29 |
| PRD00-BR-31 | Tổ chức bị vô hiệu: widget của mọi sản phẩm báo tạm ngừng nhận feedback; link riêng của khách báo hết hiệu lực; không ai chuyển vào tổ chức đó được (với admin hệ thống: câu hỏi mở 27). | FR-30 |
| PRD00-BR-32 | Tài khoản chưa thuộc tổ chức nào mở Hub thì thấy trang chào: tạo tổ chức (BR-23) hoặc chờ được thêm; không có hộp feedback hay "Feedback của tôi". | FR-31 |
| PRD00-BR-33 | Viewer xem được bình luận nội bộ, chỉ đọc. | FR-07, FR-08 |
| PRD00-BR-34 | Viewer xem được Tổng quan, chỉ với các sản phẩm mình được gán. | FR-07 |
| PRD00-BR-35 | Sản phẩm chưa có Phụ trách nào: feedback mới được báo cho Quản trị của tổ chức, cho đến khi sản phẩm có Phụ trách. | FR-32 |
| PRD00-BR-36 | Quản trị không được gán Phụ trách của sản phẩm thì không là người được giao được; muốn nhận việc, Quản trị tự gán mình làm Phụ trách sản phẩm đó (BR-05). | FR-06, FR-14 |
| PRD00-BR-37 | Feedback gốc của bản trùng phải cùng sản phẩm. Feedback ở *Trùng lặp* không nhận bình luận của người báo hay khách (như *Đóng*); người báo theo dõi qua thông báo khi gốc được xử lý (BR-13). | FR-18, FR-20 |
| PRD00-BR-38 | Email và số điện thoại của khách: trong danh sách luôn hiện bản che (ví dụ "an***@gmail.com"); ở màn hình chi tiết, Quản trị và Phụ trách thấy đầy đủ, Viewer chỉ thấy bản che. | FR-33 |
| PRD00-BR-39 | Ngữ cảnh kỹ thuật: người báo xem lại được trên feedback của mình (chỉ đọc); khách không thấy trên trang link riêng. | FR-09, FR-10 |
| PRD00-BR-40 | "24 giờ" của danh sách cần chú ý và thời gian đến lần xử lý đầu tiên tính theo giờ lịch (24/7), không theo giờ làm việc. | §2, PRD06 |
| PRD00-BR-41 | Tên hiển thị tiếng Việt của loại: *Lỗi*, *Tính năng*, *Góp ý*. Ưu tiên có ba mức *Cao*, *Trung bình*, *Thấp*, mặc định chưa đặt. Tên tiếng Anh của trạng thái: New, Acknowledged, Needs info, In progress, In review, Resolved, Closed, Rejected, Duplicate (theo thứ tự ở §3.4). Tên tiếng Anh của loại và ưu tiên: câu hỏi mở 32. | FR-13, FR-22 |
| PRD00-BR-42 | Mã feedback đánh số theo tổ chức, tăng dần, tiền tố `FB-`, dùng chung cho mọi sản phẩm của tổ chức; mỗi tổ chức có dãy số riêng. | FR-34 |
| PRD00-BR-43 | Không chuyển được sang *Cần thêm thông tin* khi feedback là của khách không để email; thao tác bị khoá kèm giải thích. | FR-35 |
| PRD00-BR-44 | Tỉ lệ từng vào *Cần thêm thông tin* = số feedback gửi trong kỳ đã từng vào *Cần thêm thông tin* ÷ số feedback gửi trong kỳ. Trung vị chỉ áp dụng cho thời gian đến lần xử lý đầu tiên. Kỳ đo mặc định 30 ngày gần nhất, đổi được trên Tổng quan. | §2, PRD06 |
| PRD00-BR-45 | Người không có quyền mở một feedback hay sản phẩm (khác tổ chức, sản phẩm không được gán, feedback của người khác) luôn thấy "không tìm thấy", giống hệt khi nó không tồn tại, để không lộ là nó có tồn tại. | FR-37 |
| PRD00-BR-46 | Người đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm, và sản phẩm tắt chế độ khách: widget không cho gửi, báo sản phẩm này chỉ nhận feedback từ thành viên của tổ chức, kèm gợi ý liên hệ người quản lý sản phẩm. | FR-36 |

## 7. Tiêu chí nghiệm thu

| Mã | Yêu cầu | Cho trước (Given) | Khi (When) | Thì (Then) |
|---|---|---|---|---|
| PRD00-AC-01 | FR-01, FR-37 | Tổ chức X có sản phẩm P; tài khoản A chỉ là thành viên tổ chức Y, không phải admin hệ thống | A mở đường dẫn trực tiếp tới một feedback của P | Hệ thống báo "không tìm thấy"; không hiện tiêu đề, tên sản phẩm hay bất kỳ nội dung nào của feedback |
| PRD00-AC-02 | FR-01 | Hai tổ chức X và Y cùng có sản phẩm tên "App Bán hàng" | Quản trị của X mở danh sách sản phẩm | Chỉ thấy sản phẩm của X |
| PRD00-AC-03 | FR-02 | Tài khoản A là Quản trị ở X và chỉ là thành viên thường ở Y | A làm việc ở Y | A không thấy màn hình quản lý sản phẩm của Y và không xem được feedback của sản phẩm Y mà A không được gán |
| PRD00-AC-04 | FR-03 | A là thành viên của X và Y, đang làm việc ở X | A chọn Y trong bộ chọn tổ chức | Hub chuyển sang Y mà không bắt đăng nhập lại; tên Y hiện ở vị trí tổ chức đang làm việc |
| PRD00-AC-05 | FR-03 | A là thành viên duy nhất của X | A mở bộ chọn tổ chức | Chỉ có X trong danh sách; không có tổ chức nào A không thuộc về |
| PRD00-AC-06 | FR-04 | A là Phụ trách sản phẩm P ở X và người báo ở Y; đang làm việc ở X | A mở hộp feedback và "Feedback của tôi" | Chỉ thấy feedback của X; feedback A đã gửi ở Y không hiện |
| PRD00-AC-07 | FR-05 | Q là Quản trị của X, không được gán vào sản phẩm nào; X có 20 sản phẩm | Q mở hộp feedback | Q thấy và lọc được feedback của cả 20 sản phẩm |
| PRD00-AC-08 | FR-06 | Q là Quản trị; M là thành viên của X | Q gán M làm Phụ trách sản phẩm P | M thấy feedback của P ngay ở lần tải tiếp theo; M không thấy sản phẩm khác |
| PRD00-AC-09 | FR-06 | M đang là Viewer của P | Q đổi M thành Phụ trách của P | M có đúng một vai trò ở P là Phụ trách, không còn là Viewer |
| PRD00-AC-10 | FR-06 | M là thành viên thường, không phải Quản trị | M thử gán người khác làm Phụ trách | Thao tác bị từ chối với thông báo không đủ quyền; không có gì thay đổi |
| PRD00-AC-11 | FR-07, FR-37 | M là Phụ trách sản phẩm A, không được gán vào B | M mở đường dẫn trực tiếp tới một feedback của B | Hệ thống báo "không tìm thấy"; không hiện nội dung nào của feedback |
| PRD00-AC-12 | FR-07 | M là Phụ trách sản phẩm A | Quản trị bỏ gán M khỏi A trong lúc M đang mở một feedback của A | Thao tác tiếp theo của M trên feedback đó bị từ chối; A biến khỏi hộp feedback của M khi tải lại |
| PRD00-AC-13 | FR-07 | M là Phụ trách của sản phẩm P, P chưa có feedback nào | M mở hộp feedback | Thấy trạng thái trống cho biết P chưa có feedback |
| PRD00-AC-14 | FR-08 | V là Viewer của P | V mở một feedback của P | Không có nút đổi trạng thái, giao việc, đổi ưu tiên, loại, nhãn, đánh dấu trùng hay ô bình luận |
| PRD00-AC-15 | FR-08, FR-12 | V là Viewer của P | V gửi yêu cầu bình luận hoặc đổi trạng thái trực tiếp, không qua màn hình | Hệ thống từ chối vì không đủ quyền; feedback không đổi, lịch sử không có mục mới |
| PRD00-AC-16 | FR-09 | R là người báo, đã gửi feedback F1; người báo khác gửi F2 cùng sản phẩm | R mở "Feedback của tôi" | Chỉ thấy F1 |
| PRD00-AC-17 | FR-09, FR-12, FR-37 | R là người báo của F1, không phải Phụ trách hay Viewer | R mở đường dẫn trực tiếp tới F2 do người khác gửi | Hệ thống báo "không tìm thấy"; không hiện nội dung nào của F2 |
| PRD00-AC-18 | FR-09 | F1 của R có ưu tiên Cao, đã giao cho H, có một bình luận nội bộ và một bình luận gửi người báo | R mở F1 | R thấy trạng thái, lịch sử đổi trạng thái và bình luận gửi người báo; không thấy ưu tiên, H, lịch sử giao việc hay bình luận nội bộ |
| PRD00-AC-19 | FR-09 | R chưa gửi feedback nào trong tổ chức đang làm việc | R mở "Feedback của tôi" | Thấy trạng thái trống hướng dẫn cách gửi feedback qua widget |
| PRD00-AC-20 | FR-10 | Khách K gửi feedback G có email, nhận link riêng | K mở link | K thấy đúng G: trạng thái, lịch sử đổi trạng thái, bình luận gửi người báo; người trả lời hiện là "Đội hỗ trợ <tên sản phẩm>" |
| PRD00-AC-21 | FR-10 | K có link riêng của G | K sửa đường link để thử mở feedback khác | Không mở được feedback nào khác |
| PRD00-AC-22 | FR-11 | A là thành viên tổ chức X, đang làm việc ở Y; sản phẩm P thuộc X | A gửi feedback qua widget của P | Feedback được ghi là của người báo A, hiện trong "Feedback của tôi" của A khi A làm việc ở X |
| PRD00-AC-23 | FR-11 | A đăng nhập Hub nhưng không thuộc X; P thuộc X và bật chế độ khách | A mở widget của P | A gửi với tư cách khách, tên và email điền sẵn từ tài khoản; feedback hiện nhãn "Khách" trong Hub; A không thấy gì bên trong X |
| PRD00-AC-24 | FR-12 | Người báo R (không phải Phụ trách) | R gửi yêu cầu đổi ưu tiên của feedback của mình trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; ưu tiên không đổi |
| PRD00-AC-25 | FR-13 | Một feedback ở *Cần thêm thông tin* | Phụ trách xem trong Hub, người báo xem trong "Feedback của tôi", khách nhận email | Cả ba nơi dùng cùng tên trạng thái *Cần thêm thông tin* (ở cùng một ngôn ngữ) |
| PRD00-AC-26 | FR-14 | Feedback F ở *Mới*; H là Phụ trách sản phẩm | H chuyển F sang *Đã tiếp nhận* | F ở *Đã tiếp nhận*; lịch sử có mục H, thời điểm, *Mới* → *Đã tiếp nhận* |
| PRD00-AC-27 | FR-14 | Người báo R của feedback F đang ở *Đang xử lý* | R tìm cách đổi trạng thái F | R không có thao tác đổi trạng thái nào ngoài kết thúc In review và mở lại khi *Đã xử lý* |
| PRD00-AC-28 | FR-15 | F đang ở *Đã tiếp nhận*, Phụ trách chuyển sang *Cần thêm thông tin* | Người báo của F gửi một bình luận | F tự về *Đã tiếp nhận*; lịch sử ghi người báo là người gây ra thay đổi |
| PRD00-AC-29 | FR-15 | F đang ở *Cần thêm thông tin* | Phụ trách gửi một bình luận gửi người báo | F vẫn ở *Cần thêm thông tin* |
| PRD00-AC-30 | FR-16 | F ở In review | Người báo của F chọn *chưa đạt* mà không ghi lý do | Không gửi được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-31 | FR-16 | F ở In review | Người báo chọn *chưa đạt* với lý do "Vẫn lỗi trên Safari" | F về *Đang xử lý*; lý do hiện thành bình luận gửi người báo; lịch sử ghi thay đổi |
| PRD00-AC-32 | FR-16 | F ở In review; H là Phụ trách đã sửa F | H chọn *đạt* | F sang *Đã xử lý* |
| PRD00-AC-33 | FR-16 | F ở In review; V là Viewer của sản phẩm | V tìm cách kết thúc In review | Không có thao tác; gửi trực tiếp bị từ chối vì không đủ quyền |
| PRD00-AC-34 | FR-17, FR-28 | F của người báo R ở *Đã xử lý* | R chọn *Mở lại* và ghi lý do "Lỗi xuất hiện lại" | F về *Đang xử lý*; lý do hiện thành bình luận gửi người báo; lịch sử ghi R là người mở lại |
| PRD00-AC-35 | FR-17 | F của người báo R ở *Đóng* | R mở F | Không có thao tác *Mở lại* |
| PRD00-AC-36 | FR-18 | Phụ trách H đang xem F | H chọn *Trùng lặp* mà không chọn feedback gốc | Không chuyển được; hệ thống yêu cầu chọn feedback gốc |
| PRD00-AC-37 | FR-18 | F được đánh dấu trùng với gốc G | G sang *Đã xử lý* | Người báo của F được thông báo |
| PRD00-AC-38 | FR-19 | F trải qua đổi trạng thái, đổi ưu tiên và giao việc | Phụ trách mở lịch sử của F | Mỗi thay đổi có một mục riêng với người thực hiện, thời điểm, giá trị cũ và mới, theo thứ tự thời gian |
| PRD00-AC-39 | FR-19 | Phụ trách H xem lịch sử F | H tìm cách sửa hoặc xoá một mục lịch sử | Không có thao tác nào; gửi trực tiếp bị từ chối |
| PRD00-AC-40 | FR-20 | F của người báo R ở *Từ chối* | R mở F | Ô bình luận bị khoá, kèm lời nhắn gửi feedback mới nếu muốn trao đổi tiếp |
| PRD00-AC-41 | FR-20 | F của khách K ở *Đóng* | K mở link riêng | K không bình luận được, thấy lời nhắn tương tự |
| PRD00-AC-42 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên của tổ chức X~~ | ~~S mở đường dẫn tới một feedback của X~~ | ~~Bị từ chối~~ — thay bằng AC-46 theo quyết định câu hỏi mở 2 |
| PRD00-AC-43 | FR-21 | Q là Quản trị của X, không phải admin hệ thống | Q tìm cách mở kết quả một đợt khảo sát | Bị từ chối vì không đủ quyền |
| PRD00-AC-44 | FR-22 | Người báo R gửi feedback chọn loại *Lỗi* | Phụ trách đổi loại sang *Tính năng* | Loại đổi; lịch sử có mục đổi loại; R thấy loại mới |
| PRD00-AC-45 | FR-22 | R gửi feedback | R mở widget | R chọn được đúng ba loại: *Lỗi*, *Tính năng*, *Góp ý* |
| PRD00-AC-46 | FR-21 | S là admin hệ thống, không là thành viên của tổ chức X; F là feedback của X có ưu tiên, người được giao và bình luận nội bộ | S mở F | S thấy toàn bộ F: nội dung, ảnh, file, ngữ cảnh kỹ thuật, ưu tiên, người được giao, bình luận nội bộ, lịch sử |
| PRD00-AC-47 | FR-21 | S là admin hệ thống; X có 20 sản phẩm và 30 thành viên | S xem sản phẩm và thành viên của X | S thấy cả 20 sản phẩm và 30 thành viên của X |
| PRD00-AC-48 | FR-21 | S là admin hệ thống, không là thành viên X; F của X ở *Mới* | S chuyển F sang *Đã tiếp nhận* | F ở *Đã tiếp nhận*; lịch sử có mục mới cho thay đổi này (cách hiện người thực hiện: câu hỏi mở 25) |
| PRD00-AC-49 | FR-21 | S là admin hệ thống, không là thành viên X; M là thành viên của X | S gỡ M khỏi X | M không còn là thành viên X; BR-30 áp dụng cho M |
| PRD00-AC-50 | FR-21, FR-01 | S là admin hệ thống đã mở feedback của X; A là Quản trị của Y, không thuộc X | A dùng Hub ở Y | A không thấy bất kỳ dữ liệu nào của X |
| PRD00-AC-51 | FR-38 | O là Chủ sở hữu của X | O tìm cách cấp quyền admin hệ thống cho một thành viên | Không có thao tác nào như vậy trong Hub; gửi trực tiếp bị từ chối |
| PRD00-AC-52 | FR-38 | S là admin hệ thống | S tìm cách cấp quyền admin hệ thống cho tài khoản khác qua Hub | Không có thao tác nào như vậy; gửi trực tiếp bị từ chối |
| PRD00-AC-53 | FR-23 | X có vai trò tự tạo "Trưởng IT" có quyền Quản trị FeedbackHub; M có vai trò đó, không được gán sản phẩm nào | M mở hộp feedback | M thấy feedback của mọi sản phẩm X như một Quản trị |
| PRD00-AC-54 | FR-23 | M chỉ có vai trò Member ở X, không được gán sản phẩm nào | M mở Hub ở X | M không thấy màn hình quản lý sản phẩm và không có hộp feedback của sản phẩm nào; chỉ có "Feedback của tôi" |
| PRD00-AC-55 | FR-23 | O là Chủ sở hữu của X | Một người có quyền quản lý vai trò tìm cách bỏ quyền Quản trị FeedbackHub khỏi Chủ sở hữu | Không bỏ được; O vẫn là Quản trị |
| PRD00-AC-56 | FR-24 | A là tài khoản đã xác minh email, chưa thuộc tổ chức nào | A tạo tổ chức "Công ty Z" | Z được tạo; A là Chủ sở hữu và là Quản trị của Z |
| PRD00-AC-57 | FR-25 | B chưa có tài khoản Hub; X gửi lời mời tới email của B với vai trò Member | B mở email trong vòng 7 ngày và đăng ký | B vào thẳng X với vai trò Member, không cần ai thêm lần nữa |
| PRD00-AC-58 | FR-25 | Lời mời tới B đã gửi được 8 ngày | B mở lời mời | B không vào được X; hệ thống báo lời mời đã hết hạn |
| PRD00-AC-59 | FR-25 | Lời mời tới B đã bị thu hồi | B mở lời mời | B không vào được X; hệ thống báo lời mời không còn hiệu lực |
| PRD00-AC-60 | FR-25 | M là thành viên X không có quyền gửi lời mời (câu hỏi mở 30) | M gửi lời mời trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; không email nào được gửi |
| PRD00-AC-61 | FR-14 | F ở *Đóng*; H là Phụ trách sản phẩm | H chuyển F sang *Đang xử lý* | F ở *Đang xử lý*; lịch sử ghi H, *Đóng* → *Đang xử lý* |
| PRD00-AC-62 | FR-14 | F ở *Trùng lặp* với gốc G; Q là Quản trị | Q chuyển F sang *Đã tiếp nhận* | F ở *Đã tiếp nhận*; lịch sử ghi thay đổi |
| PRD00-AC-63 | FR-26 | F sang *Đã xử lý* và không ai mở lại trong 7 ngày | Hết ngày thứ 7 | F tự sang *Đóng*; lịch sử ghi người thực hiện là hệ thống |
| PRD00-AC-64 | FR-26 | F sang *Đã xử lý*; người báo mở lại ở ngày thứ 5 | Đến mốc 7 ngày kể từ lúc F sang *Đã xử lý* | F vẫn ở *Đang xử lý*, không bị tự đóng |
| PRD00-AC-65 | FR-26 | F ở *Đã xử lý* được 2 ngày | Phụ trách chuyển F sang *Đóng* | F ở *Đóng* ngay |
| PRD00-AC-66 | FR-27 | H là Phụ trách, F ở *Mới* | H chọn *Từ chối* mà không ghi lý do | Không chuyển được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-67 | FR-27 | H là Phụ trách, F ở *Mới* | H chọn *Từ chối* với lý do "Đúng theo thiết kế" | F ở *Từ chối*; lý do hiện thành bình luận gửi người báo, người báo thấy được |
| PRD00-AC-68 | FR-28 | F của khách K ở *Đã xử lý* | K chọn *Mở lại* trên trang link riêng mà không ghi lý do | Không mở lại được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-69 | FR-29 | M là người báo của F1, là Phụ trách duy nhất của P và là người được giao F2 | Người có quyền quản lý thành viên gỡ M khỏi X | F1 vẫn còn, tên M hiện "(đã rời tổ chức)"; F2 về *Chưa giao*; M không còn là Phụ trách của P; P trở thành sản phẩm chưa có Phụ trách (BR-35) |
| PRD00-AC-70 | FR-29, FR-37 | M vừa bị gỡ khỏi X | M mở đường dẫn tới F1 mình từng gửi, và có feedback mới ở P | M thấy "không tìm thấy"; M không nhận thông báo nào của X |
| PRD00-AC-71 | FR-30 | Tổ chức X bị vô hiệu; P thuộc X | Một người mở widget của P | Widget báo tạm ngừng nhận feedback; không gửi được |
| PRD00-AC-72 | FR-30 | Tổ chức X bị vô hiệu; khách K có link riêng của G thuộc X | K mở link | Trang báo link hết hiệu lực; không hiện nội dung G |
| PRD00-AC-73 | FR-30 | Tổ chức X bị vô hiệu; A là thành viên X và Y | A mở bộ chọn tổ chức | Không chuyển được vào X |
| PRD00-AC-74 | FR-31 | A có tài khoản Hub, chưa thuộc tổ chức nào | A mở Hub | A thấy trang chào với lựa chọn tạo tổ chức hoặc chờ được thêm; không có hộp feedback hay "Feedback của tôi" |
| PRD00-AC-75 | FR-32 | Sản phẩm P của X chưa có Phụ trách | Một người báo gửi feedback vào P | Quản trị của X được báo có feedback mới |
| PRD00-AC-76 | FR-32 | P vừa được gán Phụ trách H | Có feedback mới ở P | Quản trị không nhận thông báo theo BR-35 cho feedback mới này của P |
| PRD00-AC-77 | FR-33 | Khách để email an.nguyen@gmail.com | Phụ trách mở hộp feedback dạng danh sách | Email hiện dạng che, ví dụ "an***@gmail.com" |
| PRD00-AC-78 | FR-33 | Như trên | Phụ trách mở chi tiết feedback | Email hiện đầy đủ |
| PRD00-AC-79 | FR-33 | Như trên; V là Viewer của sản phẩm | V mở chi tiết feedback | Email chỉ hiện dạng che |
| PRD00-AC-80 | FR-34 | Feedback gần nhất của X ở sản phẩm A có mã `FB-129` | Có feedback mới ở sản phẩm B của X | Feedback mới có mã `FB-130` |
| PRD00-AC-81 | FR-34 | X đã có feedback `FB-129`; Y là tổ chức khác | Có feedback mới ở Y | Mã của feedback ở Y theo dãy số riêng của Y, không phụ thuộc X |
| PRD00-AC-82 | FR-35 | Khách gửi G không để email | Phụ trách tìm cách chuyển G sang *Cần thêm thông tin* | Thao tác bị khoá kèm giải thích; gửi trực tiếp bị từ chối, G không đổi trạng thái |
| PRD00-AC-83 | FR-35 | Khách gửi G có email | Phụ trách chuyển G sang *Cần thêm thông tin* | G ở *Cần thêm thông tin* |
| PRD00-AC-84 | FR-36 | A đăng nhập Hub, không thuộc X; P thuộc X và tắt chế độ khách | A mở widget của P | Widget không cho gửi, báo P chỉ nhận feedback từ thành viên của tổ chức, kèm gợi ý liên hệ người quản lý sản phẩm |
| PRD00-AC-85 | FR-37 | M là Phụ trách sản phẩm A ở X; F thuộc sản phẩm B của X; `FB-99999` không tồn tại | M mở F, rồi mở `FB-99999` | Cả hai lần M thấy cùng một thông báo "không tìm thấy" |
| PRD00-AC-86 | FR-07 | V là Viewer của P; F của P có một bình luận nội bộ | V mở F | V thấy bình luận nội bộ, không có ô viết bình luận |
| PRD00-AC-87 | FR-07 | V là Viewer của P, không được gán Q; cả hai thuộc X | V mở Tổng quan | V chỉ thấy số liệu của P |
| PRD00-AC-88 | FR-06 | Q là Quản trị, không là Phụ trách của P | Q mở danh sách chọn người được giao cho một feedback của P | Q không có trong danh sách; sau khi Q tự gán mình làm Phụ trách P, Q có trong danh sách |
| PRD00-AC-89 | FR-18 | H là Phụ trách của A và B; F thuộc A | H đánh dấu F là *Trùng lặp* | Danh sách chọn feedback gốc chỉ có feedback của A |
| PRD00-AC-90 | FR-20 | F của người báo R ở *Trùng lặp* | R mở F | Ô bình luận bị khoá, kèm lời nhắn gửi feedback mới nếu muốn trao đổi tiếp |
| PRD00-AC-91 | FR-09 | R là người báo của F | R mở F | R thấy ngữ cảnh kỹ thuật của F, chỉ đọc |
| PRD00-AC-92 | FR-10 | Khách K gửi G | K mở link riêng của G | Không có ngữ cảnh kỹ thuật nào trên trang |
| PRD00-AC-93 | FR-22 | R vừa gửi F | Phụ trách mở F | Ưu tiên hiện "chưa đặt"; chọn được *Cao*, *Trung bình*, *Thấp* |

## 8. Trạng thái và trường hợp biên

- **Không có tổ chức**: tài khoản chưa thuộc tổ chức nào (ví dụ chỉ từng gửi với tư cách khách) mở Hub → trang chào (BR-32, AC-74).
- **Thành viên bị gỡ khỏi tổ chức** khi đang là người báo, Phụ trách hoặc người được giao → BR-30 (AC-69, AC-70). Nếu họ là Phụ trách duy nhất của một sản phẩm, sản phẩm đó trở thành chưa có Phụ trách (BR-35).
- **Tổ chức bị vô hiệu** (nền tảng đã có thao tác này): widget tạm ngừng, link riêng hết hiệu lực, không ai chuyển vào được (BR-31). Admin hệ thống có vào tổ chức bị vô hiệu được không: câu hỏi mở 27.
- **Quyền bị thu hồi** (bỏ gán sản phẩm, đổi vai trò tổ chức) có hiệu lực ở thao tác tiếp theo của người đó, theo cơ chế sẵn có của nền tảng (trong vài giây); không cần đăng xuất (AC-12).
- **Sản phẩm không có Phụ trách**: không ai được giao được; feedback mới báo cho Quản trị của tổ chức (BR-35).
- **Một người là người báo và cũng là Phụ trách** của cùng sản phẩm: được làm mọi thao tác của Phụ trách trên feedback mình gửi; ở "Feedback của tôi" vẫn thấy feedback đó.
- **Khách không để email**: không chuyển được feedback của họ sang *Cần thêm thông tin* (BR-43).
- **Người đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm, và sản phẩm tắt chế độ khách**: widget không cho gửi (BR-46).
- **Lời mời hết hạn hoặc đã thu hồi**: không đưa ai vào tổ chức (BR-24, AC-58, AC-59). Người đăng ký bằng email khác email được mời: câu hỏi mở 31.
- **Feedback ở *Đã xử lý* bị mở lại rồi quay về *Đã xử lý***: 7 ngày tự đóng tính lại từ lần vào *Đã xử lý* gần nhất (BR-27).
- **Admin hệ thống làm việc trong tổ chức mình không là thành viên**: cách vào tổ chức (câu hỏi mở 27), có được làm Phụ trách hay người được giao không (câu hỏi mở 28), dấu vết để lại cho tổ chức (câu hỏi mở 25, 26).
- **20 sản phẩm** trở lên mỗi tổ chức: bộ chọn sản phẩm, hộp feedback và phân quyền phải dùng tốt (theo canvas đã duyệt). Chưa có giới hạn tối đa nào được quyết.
- **Sửa đồng thời** (hai Phụ trách đổi trạng thái cùng lúc): thuộc PRD03.
- **Feedback gốc bị chuyển khỏi *Đã xử lý*** sau khi đã báo cho các bản trùng: thuộc PRD03.

## 9. Dữ liệu (mức nghiệp vụ)

| Trường | Ý nghĩa | Bắt buộc | Giới hạn | Ai được sửa |
|---|---|---|---|---|
| Tên tổ chức | Tên hiển thị của tổ chức | Có | Không để trống (nền tảng đã có) | Người có quyền quản lý tổ chức (nền tảng); admin hệ thống |
| Định danh ngắn của tổ chức | Mã viết thường, duy nhất toàn Hub | Có | Duy nhất (nền tảng đã có) | Như trên |
| Vai trò tổ chức của thành viên | Một hoặc nhiều vai trò tổ chức | Có, ít nhất một | Tổ chức luôn còn ít nhất một Chủ sở hữu (nền tảng đã có); Chủ sở hữu luôn có quyền Quản trị FeedbackHub | Người có quyền quản lý thành viên (BR-19); admin hệ thống |
| Lời mời | Email được mời, vai trò tổ chức, thời điểm gửi | Có email và vai trò | Hết hạn sau 7 ngày; thu hồi được trước khi dùng | Người gửi lời mời (câu hỏi mở 30); admin hệ thống |
| Vai trò theo sản phẩm | Phụ trách hoặc Viewer, cho một thành viên ở một sản phẩm | Không | Tối đa một vai trò mỗi người mỗi sản phẩm | Quản trị; admin hệ thống |
| Mã feedback | `FB-<số>` | Tự sinh | Tăng dần, không trùng trong tổ chức | Không ai |
| Loại | Lỗi / Tính năng / Góp ý | Có | Đúng ba giá trị | Người báo khi gửi; Phụ trách, Quản trị sau đó |
| Ưu tiên | Cao / Trung bình / Thấp | Không | Mặc định chưa đặt | Phụ trách, Quản trị |
| Trạng thái feedback | Một trong chín trạng thái §3.4 | Có | Mặc định *Mới* khi gửi | Phụ trách, Quản trị; người báo và khách chỉ qua hai thao tác BR-09; tự động theo BR-12, BR-27 |
| Lý do từ chối | Lý do khi chuyển sang *Từ chối*, hiện thành bình luận gửi người báo | Có khi chuyển sang *Từ chối* | Không để trống | Phụ trách, Quản trị |
| Lý do mở lại | Lý do người báo hoặc khách mở lại, hiện thành bình luận gửi người báo | Có khi mở lại | Một câu ngắn, không để trống | Người báo, khách |
| Feedback gốc | Feedback mà bản trùng gắn vào | Có khi trạng thái là *Trùng lặp* | Cùng sản phẩm với bản trùng | Phụ trách, Quản trị |
| Email, điện thoại của khách | Thông tin liên hệ khách để lại | Không | Danh sách luôn che; chi tiết theo BR-38 | Khách khi gửi |
| Mục lịch sử | Ai, lúc nào, trường nào, từ → đến | Tự sinh | Không sửa, không xoá | Không ai |

Mọi ô "Phụ trách, Quản trị" ở bảng trên cũng gồm admin hệ thống (BR-21).

## 10. Thông báo và sự kiện

Ai nhận gì qua kênh nào được định nghĩa ở PRD05. PRD này chỉ định nghĩa các sự kiện đổi trạng thái (§3.4) và giữ các ngoại lệ đã quyết sau, để chúng nằm cạnh quy tắc gốc:

| Sự kiện | Ai nhận | Kênh | Ghi chú |
|---|---|---|---|
| Feedback gốc sang *Đã xử lý* | Người báo của mọi bản trùng lặp | Theo PRD05 | BR-13 |
| Feedback mới ở sản phẩm chưa có Phụ trách | Quản trị của tổ chức | Theo PRD05 | BR-35, cho đến khi sản phẩm có Phụ trách |
| Lời mời vào tổ chức | Email được mời | Email | BR-24; nội dung email: PRD05 |

## 11. Yêu cầu phi chức năng

- **Tách biệt dữ liệu**: không màn hình, email, file xuất hay thông báo nào hiện dữ liệu của tổ chức cho người ngoài tổ chức, trừ ngoại lệ trong ma trận quyền (khách qua link riêng, admin hệ thống).
- **Đường truy cập xuyên tổ chức có chủ đích**: admin hệ thống là đường duy nhất thấy và thao tác trên dữ liệu của mọi tổ chức (BR-21). Đây là thay đổi bảo mật so với nền tảng hiện có (không có admin cấp hệ thống, không có đường vượt quyền), nên thiết kế kỹ thuật phải qua review bảo mật trước khi duyệt. Quyền này chỉ cấp được ngoài Hub (BR-20). Dấu vết của admin hệ thống trong tổ chức: câu hỏi mở 25, 26.
- **Kiểm tra quyền không phụ thuộc giao diện**: ẩn nút trên màn hình không thay cho việc từ chối thao tác; mọi thao tác bị chặn trong ma trận đều bị từ chối khi gọi trực tiếp (FR-12).
- **Không hiện nội dung khi không có quyền**: người không có quyền mở feedback hay sản phẩm không thấy bất kỳ nội dung nào của nó, và thấy "không tìm thấy" giống như khi nó không tồn tại (BR-45). Thiết kế kỹ thuật cần review bảo mật xác nhận cách làm.
- **Ngôn ngữ**: giao diện có tiếng Việt và tiếng Anh; tên trạng thái, loại, ưu tiên, vai trò dịch thống nhất ở mọi nơi (BR-41). Email mặc định tiếng Việt theo quyết định nền tảng 2026-08-04.
- **Truy vết**: mọi thay đổi trên feedback có lịch sử không sửa được (BR-14), kể cả thay đổi tự động (BR-27).
- **Quy mô**: một tổ chức có từ 20 sản phẩm trở lên vẫn dùng tốt (canvas đã duyệt).

## 12. Phụ thuộc

- **Nền tảng có sẵn**:
  - Đăng ký bằng email (bắt buộc xác minh email), đăng nhập Google và Microsoft, quản lý phiên đăng nhập.
  - Tổ chức: tạo tổ chức (người tạo thành Chủ sở hữu), vô hiệu tổ chức, chuyển tổ chức đang làm việc, quay về ngữ cảnh cá nhân không tổ chức.
  - Thành viên: thêm thành viên bằng email của một tài khoản **đã đăng ký**, đổi vai trò, gỡ thành viên; luôn còn ít nhất một Chủ sở hữu. PRD này thêm lời mời cho người chưa có tài khoản (FR-25).
  - Vai trò tổ chức tự tạo từ danh mục quyền cố định; ba vai trò hệ thống Owner (mọi quyền), Admin (hiện chỉ có quyền quản lý thành viên), Member (không có quyền nào). PRD này thêm quyền Quản trị FeedbackHub vào danh mục, có sẵn ở Admin (FR-23).
  - Hiện không có admin cấp hệ thống và không có đường vượt quyền giữa các tổ chức. PRD này cố ý thêm một đường như vậy: admin hệ thống (BR-20, BR-21).
  - Thông báo trong Hub, email, web push.
- **PRD phụ thuộc vào PRD này**: PRD01–PRD07.
- **Dịch vụ bên ngoài**: không có mới.

## 13. Câu hỏi mở

| # | Câu hỏi | Đề xuất trả lời | Vì sao quan trọng | Trạng thái |
|---|---|---|---|---|
| 25 | Thao tác của admin hệ thống trong một tổ chức (đổi trạng thái, gỡ thành viên, sửa sản phẩm…) có ghi vào lịch sử và tổ chức có thấy không? Hiện dưới tên gì? | Có: ghi như mọi thay đổi khác, người thực hiện hiện là "<tên> (Admin hệ thống)" cho mọi thành viên xem được lịch sử; tổ chức không tắt được việc ghi này. | "Toàn quyền" xuyên tổ chức không có dấu vết thì tổ chức không biết ai đã đổi dữ liệu của mình; ảnh hưởng FR-19, AC-48 và review bảo mật. | Mở |
| 26 | Việc admin hệ thống chỉ **xem** dữ liệu của một tổ chức (không thay đổi gì) có được ghi lại không, và ai xem được bản ghi đó? | Có: mỗi lần admin hệ thống vào một tổ chức được ghi (ai, lúc nào, tổ chức nào); Chủ sở hữu và Quản trị của tổ chức xem được nhật ký này. | Xem là phần lớn của "thấy toàn bộ hệ thống" nhưng không để lại mục lịch sử nào; quyết định mức tin cậy giữa các tổ chức dùng chung Hub. | Mở |
| 27 | Admin hệ thống vào một tổ chức mình không là thành viên bằng cách nào? Bộ chọn tổ chức có liệt kê mọi tổ chức, kể cả tổ chức bị vô hiệu không? Admin hệ thống có vô hiệu và kích hoạt lại tổ chức được không? | Bộ chọn tổ chức của admin hệ thống có thêm mục "Mọi tổ chức" liệt kê tất cả, tổ chức bị vô hiệu gắn nhãn; khi làm việc trong tổ chức không phải của mình, Hub luôn hiện rõ đang ở tư cách admin hệ thống. Vào được tổ chức bị vô hiệu, và vô hiệu, kích hoạt lại được (như Chủ sở hữu). | Ảnh hưởng FR-03, BR-31, AC-73 và thiết kế bộ chọn tổ chức. | Mở |
| 28 | Admin hệ thống có được tính là Chủ sở hữu (cho quy tắc "luôn còn ít nhất một Chủ sở hữu") không? Có tự làm Phụ trách hay người được giao ở tổ chức mình không là thành viên không? | Không tính là Chủ sở hữu: tổ chức vẫn phải có ít nhất một Chủ sở hữu là thành viên. Không làm Phụ trách hay người được giao trừ khi được thêm làm thành viên; vẫn gán Phụ trách và giao việc cho thành viên khác được. | BR-05 và BR-36 đòi người được giao là Phụ trách, mà Phụ trách là thành viên; ảnh hưởng danh sách chọn người ở PRD03. | Mở |
| 29 | Khi admin hệ thống viết bình luận gửi người báo, người báo và khách thấy tên gì? | Người báo là thành viên thấy "<tên> (Admin hệ thống)"; khách thấy "Đội hỗ trợ <tên sản phẩm>" như BR-08. | BR-08 chỉ quy định cho người IT trong tổ chức. | Mở |
| 30 | Ai gửi và thu hồi lời mời? Câu trả lời câu hỏi 4 ghi "Quản trị thu hồi được", còn câu hỏi 1 quyết định thêm, gỡ thành viên đi theo quyền quản lý thành viên, không theo quyền Quản trị FeedbackHub. | Theo quyền quản lý thành viên (BR-19): ai có quyền này gửi và thu hồi được lời mời; Quản trị không có quyền đó thì không. Lý do: mời là một cách thêm thành viên. | Hai câu trả lời đã chấp nhận mâu thuẫn nhau; ảnh hưởng ma trận quyền và AC-60. | Mở |
| 31 | Lời mời có bắt buộc người nhận đăng ký đúng email được mời không? | Có: lời mời chỉ dùng được bởi tài khoản có đúng email được mời; người khác mở thì thấy báo lời mời không dành cho mình. | Lời mời bị chuyển tiếp sẽ đưa người lạ vào tổ chức với vai trò đã chọn. | Mở |
| 32 | Tên tiếng Anh của loại và mức ưu tiên? | Loại: Bug / Feature / Feedback. Ưu tiên: High / Medium / Low. | Giao diện tiếng Anh cần tên thống nhất (BR-41); không chặn việc khác. | Mở |

## 14. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| 2026-10-22 | 0.1 | Bản nháp đầu từ spec lõi, spec khảo sát, `docs/tech/README.md` và nền tảng hiện có | Spec 2026-10-21 (quyết định của người dùng) |
| 2026-10-22 | 0.2 | Áp dụng câu trả lời của người dùng cho câu hỏi mở 1–24; bỏ các câu đã trả lời khỏi §13; thêm US-13–15, FR-23–38, BR-18–46, AC-46–93; bỏ AC-42; thêm câu hỏi mở 25–32 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 1: Quản trị là thành viên có quyền Quản trị FeedbackHub (Chủ sở hữu luôn có, Admin có sẵn, Member không, vai trò tự tạo thêm được); thêm, gỡ thành viên theo quyền quản lý thành viên → FR-23, BR-18, BR-19, AC-53–55 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 2 (người dùng **đổi** đề xuất): admin hệ thống có toàn quyền và thấy toàn bộ hệ thống (mọi tổ chức, sản phẩm, feedback, thành viên, khảo sát); giữ đề xuất chỉ người vận hành máy chủ chỉ định → FR-21 (sửa), FR-38, BR-20–22, AC-46–52, bỏ AC-42; sửa §2, §3.1, §3.2, §3.3, §11, §12; phát sinh câu hỏi 25–29 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 3: mọi tài khoản đã xác minh email tạo được tổ chức → FR-24, BR-23, AC-56 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 4: lời mời qua email, vào thẳng tổ chức, hết hạn 7 ngày, thu hồi được → US-14, FR-25, BR-24, AC-57–60; phát sinh câu hỏi 30, 31 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 5: Phụ trách, Quản trị chuyển giữa mọi trạng thái; chỉ có các chuyển tự động đã nêu → BR-25, BR-26, AC-61, AC-62 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 6: tự đóng sau 7 ngày ở *Đã xử lý*, đóng tay bất cứ lúc nào → US-15, FR-26, BR-27, AC-63–65 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 7: *Từ chối* bắt buộc lý do, lưu thành bình luận gửi người báo → FR-27, BR-28, AC-66, AC-67 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 8: gỡ thành viên và vô hiệu tổ chức → FR-29, FR-30, BR-30, BR-31, AC-69–73 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 9: trang chào cho tài khoản chưa thuộc tổ chức nào → FR-31, BR-32, AC-74 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 10: Viewer xem bình luận nội bộ (chỉ đọc) → BR-33, AC-86 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 11: Viewer xem Tổng quan của sản phẩm được gán → BR-34, AC-87 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 12: sản phẩm chưa có Phụ trách thì báo Quản trị → FR-32, BR-35, AC-75, AC-76, §10 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 13: mở lại bắt buộc lý do ngắn → FR-28, BR-29, AC-34 (sửa), AC-68 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 14: người được giao luôn là Phụ trách; Quản trị tự gán mình nếu muốn nhận việc → BR-36, AC-88 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 15: feedback gốc cùng sản phẩm; *Trùng lặp* không nhận bình luận của người báo → FR-18, FR-20 (sửa), BR-37, AC-89, AC-90 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 16: email, điện thoại khách che trong danh sách, đầy đủ ở chi tiết cho Quản trị, Phụ trách → FR-33, BR-38, AC-77–79 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 17: người báo xem ngữ cảnh kỹ thuật, khách không → BR-39, AC-91, AC-92 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 18: tính theo giờ lịch 24/7 → BR-40, §2 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 19: loại Lỗi / Tính năng / Góp ý; ưu tiên Cao / Trung bình / Thấp, mặc định chưa đặt; tên tiếng Anh của trạng thái → BR-41, AC-44, AC-45 (sửa), AC-93; phát sinh câu hỏi 32 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 20: mã feedback theo tổ chức, tăng dần, tiền tố `FB-` → FR-34, BR-42, AC-80, AC-81 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 21: chặn *Cần thêm thông tin* với khách không để email → FR-35, BR-43, AC-82, AC-83 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 22: công thức tỉ lệ, trung vị chỉ cho thời gian, kỳ mặc định 30 ngày → BR-44, §2 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 23: luôn báo "không tìm thấy"; review bảo mật xác nhận ở thiết kế kỹ thuật → FR-37, BR-45, AC-01, AC-11, AC-17 (sửa), AC-85, §11 | Quyết định của người dùng |
| 2026-10-22 | 0.2 | Câu 24: widget không cho gửi khi không thuộc tổ chức và chế độ khách tắt → FR-36, BR-46, AC-84 | Quyết định của người dùng |

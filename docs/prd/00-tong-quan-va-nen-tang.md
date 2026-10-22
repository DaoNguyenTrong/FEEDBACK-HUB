# PRD-00 — Tổng quan và nền tảng: thuật ngữ, vai trò và quyền, vòng đời trạng thái, tổ chức và sản phẩm

| Mục | Giá trị |
|---|---|
| Mã | `PRD00` (tiền tố cho mọi mã trong tài liệu này) |
| Trạng thái | Draft |
| Phiên bản | 0.1 |
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
  - Mỗi người chỉ thấy và làm đúng phần việc của vai trò mình; dữ liệu của tổ chức này không bao giờ lộ sang tổ chức khác.
  - Một tài khoản làm việc được ở nhiều tổ chức, với vai trò khác nhau ở mỗi nơi.
  - Vòng đời feedback rõ ràng cho cả IT lẫn người báo: luôn biết feedback đang ở đâu và ai đang giữ bước tiếp theo.
- **Không phải mục tiêu**:
  - Màn hình quản lý sản phẩm, widget, hộp feedback, cổng người báo, thông báo, tổng quan, khảo sát: thuộc PRD01–PRD07.
  - Gói trả phí, hạn mức, billing.
  - Đồng bộ với công cụ ngoài (Plane, GitHub, GitLab), SDK Flutter, AI phân loại: để sau.
  - Đổi cơ chế đăng nhập, đăng ký, phiên của nền tảng.
- **Phản mục tiêu** (chạy đúng nhưng vẫn là thất bại nếu):
  - Người báo hoặc khách thấy được feedback của người khác.
  - Tổ chức này thấy dữ liệu của tổ chức kia, kể cả qua admin hệ thống.
  - Một thao tác bị chặn trên giao diện nhưng vẫn làm được khi gọi thẳng vào hệ thống.
  - Báo lỗi qua widget phiền hơn nhắn Zalo.
- **Đo thành công** (chỉ số dùng chung, các PRD khác dẫn về đây):
  - **Khảo sát hài lòng định kỳ** của người dùng Hub (PRD07).
  - **Tỉ lệ feedback từng vào *Cần thêm thông tin***. Càng thấp càng tốt: feedback đến đủ ngữ cảnh.
  - **Thời gian đến lần xử lý đầu tiên**: từ lúc gửi đến lúc feedback lần đầu sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*.
  - Spec ghi "cả hai tính bằng **trung vị**". Cách tính cụ thể cho tỉ lệ và độ dài kỳ đo: câu hỏi mở 22.
  - **Danh sách cần chú ý**: feedback chưa được xử lý lần đầu sau **24 giờ** kể từ lúc gửi (cách tính giờ: câu hỏi mở 18).

## 3. Người dùng và vai trò

### 3.1 Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| Hub | Ứng dụng web FeedbackHub nơi thành viên tổ chức xem, xử lý và theo dõi feedback. |
| Tài khoản | Tài khoản đăng nhập Hub của một người. Một tài khoản thuộc được nhiều tổ chức. |
| Tổ chức | Một công ty hoặc đơn vị dùng Hub. Dữ liệu (sản phẩm, feedback, nhãn, vai trò) của mỗi tổ chức tách biệt hoàn toàn. |
| Thành viên | Tài khoản đang hoạt động trong một tổ chức. Thành viên bị gỡ khỏi tổ chức không còn là thành viên. |
| Tổ chức đang làm việc | Tổ chức mà phiên làm việc của người dùng đang ở trong đó. Mỗi phiên có tối đa một tổ chức đang làm việc; người dùng tự chuyển được. |
| Vai trò tổ chức | Gói quyền gán cho thành viên trong một tổ chức. Nền tảng có sẵn ba vai trò hệ thống: *Chủ sở hữu* (Owner, có mọi quyền), *Admin*, *Member*; tổ chức tạo thêm vai trò riêng từ một danh mục quyền cố định. |
| Sản phẩm | Một ứng dụng hoặc website của tổ chức có gắn widget. Mỗi sản phẩm thuộc đúng một tổ chức. |
| Vai trò theo sản phẩm | Vai trò của một thành viên đối với một sản phẩm cụ thể: *Phụ trách* hoặc *Viewer* (§3.2). |
| Widget | Nút nổi và hộp gửi feedback gắn vào trang của sản phẩm. Chi tiết: PRD02. |
| Khoá công khai | Mã định danh sản phẩm nằm trong đoạn mã nhúng widget. Chỉ cho phép gửi feedback vào đúng sản phẩm đó, không phải mật khẩu hay bí mật. Chi tiết: PRD01. |
| Domain được phép | Danh sách tên miền nơi widget của sản phẩm được phép chạy. Chi tiết: PRD01. |
| Chế độ khách | Tuỳ chọn theo từng sản phẩm, cho phép người không phải thành viên gửi feedback. Chi tiết: PRD02. |
| Feedback | Một báo cáo gửi qua widget: loại, tiêu đề, mô tả, ảnh, file, ngữ cảnh kỹ thuật, trạng thái, ưu tiên, người được giao, nhãn, bình luận, lịch sử. |
| Mã feedback | Mã ngắn để gọi tên một feedback, ví dụ `FB-129` (phạm vi đánh số: câu hỏi mở 20). |
| Loại | Phân loại feedback: *Lỗi*, *Yêu cầu tính năng*, *Góp ý/câu hỏi* (tên hiển thị: câu hỏi mở 19). Người báo chọn khi gửi, Phụ trách và Quản trị sửa được. |
| Trạng thái | Vị trí của feedback trong vòng đời (§3.4). |
| In review | **Trong sản phẩm**, luôn là trạng thái feedback "đã sửa, chờ kiểm tra lại" (§3.4). Không nhầm với trạng thái *In review* của tài liệu PRD trong quy trình nội bộ. |
| Ưu tiên | Mức độ gấp do IT đặt; người báo và khách không thấy. Các mức: câu hỏi mở 19. |
| Người được giao | Một người trong số Phụ trách của sản phẩm, chịu trách nhiệm chính cho một feedback. Có thể để trống (*Chưa giao*). |
| Nhãn | Thẻ tự do theo tổ chức, gắn lên feedback để lọc và nhóm. |
| Người báo | Thành viên tổ chức đã gửi một feedback (§3.2). |
| Khách | Người gửi feedback mà không phải thành viên của tổ chức sở hữu sản phẩm (§3.2). |
| Link riêng | Đường link khó đoán gửi qua email cho khách, mở trang theo dõi đúng một feedback. Thu hồi được, hết hạn. Chi tiết: PRD04. |
| Bình luận nội bộ | Bình luận chỉ team trong tổ chức thấy; người báo và khách không bao giờ thấy. |
| Bình luận gửi người báo | Bình luận người báo (hoặc khách) thấy và được thông báo. |
| Ngữ cảnh kỹ thuật | Thông tin widget tự thu lúc gửi: thiết bị, trình duyệt, màn hình, URL, lỗi console, request lỗi, dữ liệu sản phẩm truyền vào. Chi tiết: PRD02. |
| Trùng lặp / feedback gốc | Feedback đã được báo trước đó; bản trùng gắn với một feedback gốc. |
| Lịch sử | Bản ghi mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. |
| Xử lý lần đầu | Lần đầu feedback sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*. |
| Hộp feedback | Màn hình danh sách và bảng feedback cho IT. Chi tiết: PRD03. |
| Feedback của tôi | Màn hình người báo xem feedback mình đã gửi. Chi tiết: PRD04. |
| Tổng quan | Màn hình số liệu theo sản phẩm. Chi tiết: PRD06. |
| Danh sách cần chú ý | Các feedback chưa được xử lý lần đầu sau 24 giờ (§2). |
| Admin hệ thống | Người vận hành Hub, đứng trên mọi tổ chức, chỉ để chạy khảo sát (§3.2, PRD07). |
| Đợt khảo sát | Một lần khảo sát hài lòng về Hub do admin hệ thống mở cho các tổ chức được chọn. Chi tiết: PRD07. |

### 3.2 Vai trò

| Vai trò | Cấp | Là ai | Phạm vi nhìn thấy | Nguồn |
|---|---|---|---|---|
| **Quản trị** | Tổ chức | Thành viên quản lý FeedbackHub cho tổ chức (cách xác định từ vai trò tổ chức: câu hỏi mở 1) | Mọi sản phẩm và feedback của tổ chức, không cần được gán | Spec lõi §5 |
| **Phụ trách** | Theo sản phẩm | Thành viên được Quản trị gán Phụ trách cho một sản phẩm; một người phụ trách được nhiều sản phẩm | Mọi feedback của các sản phẩm mình phụ trách | Spec lõi §5 |
| **Viewer** | Theo sản phẩm | Thành viên được gán Viewer cho một sản phẩm | Mọi feedback của các sản phẩm mình được gán, chỉ đọc | Spec lõi §5 |
| **Người báo** | Tổ chức | Mọi thành viên của tổ chức, với feedback do chính mình gửi | Chỉ feedback chính mình gửi | Spec lõi §5 |
| **Khách** | Không thuộc tổ chức | Người không có tài khoản trong tổ chức sở hữu sản phẩm, kể cả người đã đăng nhập Hub nhưng thuộc tổ chức khác | Chỉ feedback mình gửi, qua link riêng | Spec lõi §5, §9 |
| **Admin hệ thống** | Hệ thống | Người vận hành Hub (cách chỉ định: câu hỏi mở 2) | Đợt khảo sát và kết quả; phạm vi với dữ liệu tổ chức: câu hỏi mở 2 | Spec khảo sát §3 |

Quy tắc kết hợp vai trò: §6 (BR-01 đến BR-08).

### 3.3 Ma trận quyền

Ký hiệu: **✓** được làm · **—** không được · **R** chỉ xem · **?n** chưa quyết, xem câu hỏi mở số *n* (đề xuất ghi trong câu hỏi). "Của mình" = feedback chính người đó gửi. Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức.

| Thao tác | Quản trị | Phụ trách | Viewer | Người báo | Khách | Admin hệ thống |
|---|---|---|---|---|---|---|
| **Tổ chức** | | | | | | |
| Tạo tổ chức mới | ?3 | ?3 | ?3 | ?3 | — | ?3 |
| Thêm, gỡ thành viên tổ chức | ✓ (?1, ?4) | — | — | — | — | ?2 |
| Chuyển tổ chức đang làm việc (giữa các tổ chức mình là thành viên) | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| **Sản phẩm** | | | | | | |
| Tạo, sửa sản phẩm và cấu hình widget | ✓ | — | — | — | — | ?2 |
| Gán, bỏ Phụ trách và Viewer | ✓ | — | — | — | — | ?2 |
| **Feedback: xem** | | | | | | |
| Xem hộp feedback | ✓ mọi sản phẩm | ✓ sản phẩm được gán | ✓ sản phẩm được gán | — | — | ?2 |
| Xem chi tiết feedback, ảnh, file | ✓ | ✓ | R | R của mình | R của mình, qua link riêng | ?2 |
| Xem ngữ cảnh kỹ thuật | ✓ | ✓ | R | ?17 | ?17 | ?2 |
| Xem ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên | ✓ | ✓ | R | — | — | ?2 |
| Xem trạng thái và lịch sử đổi trạng thái | ✓ | ✓ | R | R của mình | R của mình | ?2 |
| Xem bình luận gửi người báo | ✓ | ✓ | R | R của mình | R của mình | ?2 |
| Xem bình luận nội bộ | ✓ | ✓ | ?10 | — | — | ?2 |
| Xem đầy đủ email, điện thoại của khách | ?16 | ?16 | ?16 | — | — | ?2 |
| Xem Tổng quan | ✓ | ✓ | ?11 | — | — | ?2 |
| Xem "Feedback của tôi" | ✓ | ✓ | ✓ | ✓ | — | — |
| **Feedback: thao tác** | | | | | | |
| Gửi feedback qua widget | ✓ | ✓ | ✓ | ✓ mọi sản phẩm của tổ chức | ✓ khi sản phẩm bật chế độ khách | — |
| Đổi trạng thái (§3.4) | ✓ (?5) | ✓ (?5) | — | chỉ hai thao tác dưới | chỉ hai thao tác dưới | — |
| Kết thúc In review (đạt / chưa đạt) | ✓ | ✓ | — | ✓ của mình | ✓ của mình, qua link riêng | — |
| Mở lại khi *Đã xử lý* | ?5 | ?5 | — | ✓ của mình | ✓ của mình, qua link riêng | — |
| Đổi loại, ưu tiên, nhãn | ✓ | ✓ | — | — | — | — |
| Giao việc | ✓ | ✓ | — | — | — | — |
| Đánh dấu trùng lặp | ✓ | ✓ | — | — | — | — |
| Viết bình luận nội bộ | ✓ | ✓ | — | — | — | — |
| Viết bình luận gửi người báo | ✓ | ✓ | — | — | — | — |
| Bình luận, bổ sung trên feedback của mình (trừ khi *Đóng* / *Từ chối*) | — | — | — | ✓ | ✓ qua link riêng | — |
| Thu hồi link riêng của khách | ✓ | ✓ | — | — | — | — |
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
| Đã xử lý | Đã xong; người báo còn mở lại được | Người báo (nếu chưa ổn) |
| Đóng | Kết thúc | — |
| Từ chối | Không làm | — |
| Trùng lặp | Đã có feedback gốc | Theo feedback gốc |

Luồng chính (spec lõi §6):

```
Mới ──► Đã tiếp nhận ──► Đang xử lý ──► In review ──► Đã xử lý ──► Đóng
 │           │                ▲    ▲         │              │
 │           │                │    └─────────┘ chưa đạt     │ người báo mở lại
 │           │                └─────────────────────────────┘ (về Đang xử lý)
 │           └──► Cần thêm thông tin ──(người báo trả lời → về trạng thái trước)
 └──► Từ chối  /  Trùng lặp (gắn feedback gốc)
```

- Phụ trách chuyển thẳng được *Đang xử lý* → *Đã xử lý*, bỏ qua In review.
- Sơ đồ là luồng chính; việc Phụ trách và Quản trị có được chuyển giữa **mọi** trạng thái hay không: câu hỏi mở 5.
- Cách sang *Đóng* (thủ công hay tự động): câu hỏi mở 6.

## 4. User story

| Mã | Là… | Tôi muốn… | Để… | Ưu tiên |
|---|---|---|---|---|
| PRD00-US-01 | thành viên của nhiều tổ chức | chuyển giữa các tổ chức mình thuộc về | chỉ thấy và làm việc với dữ liệu của tổ chức đang chọn | Must |
| PRD00-US-02 | Quản trị | thấy mọi sản phẩm và feedback của tổ chức, gán Phụ trách và Viewer cho từng sản phẩm | mỗi sản phẩm có đúng người nhận và xử lý feedback | Must |
| PRD00-US-03 | Phụ trách | chỉ thấy và xử lý feedback của các sản phẩm mình phụ trách | tập trung vào việc của mình | Must |
| PRD00-US-04 | Viewer | đọc feedback của sản phẩm được gán mà không thay đổi được gì | theo dõi tình hình mà không làm xáo trộn việc của IT | Must |
| PRD00-US-05 | người báo | chỉ thấy feedback mình đã gửi và trạng thái của chúng | biết feedback của mình đang ở đâu mà không phải hỏi | Must |
| PRD00-US-06 | khách | theo dõi feedback mình gửi qua link riêng mà không cần tài khoản | biết kết quả và trả lời khi IT hỏi | Must |
| PRD00-US-07 | Quản trị của một tổ chức | chắc chắn tổ chức khác không thấy dữ liệu của mình | dùng chung Hub mà vẫn an toàn | Must |
| PRD00-US-08 | bất kỳ ai dùng Hub | thấy cùng một bộ trạng thái với cùng một nghĩa | hiểu feedback đang ở bước nào và ai giữ bước tiếp theo | Must |
| PRD00-US-09 | người báo hoặc Phụ trách | xác nhận bản sửa ở bước In review là đạt hay chưa | feedback chỉ coi là xong khi đã kiểm tra lại | Must |
| PRD00-US-10 | Phụ trách | xem lịch sử ai đổi gì, lúc nào | truy vết khi có tranh cãi hay nhầm lẫn | Must |
| PRD00-US-11 | admin hệ thống | có vai trò riêng đứng trên các tổ chức | chạy khảo sát hài lòng trên nhiều tổ chức (PRD07) | Should |
| PRD00-US-12 | chủ sản phẩm FeedbackHub | có chỉ số thành công định nghĩa một lần | mọi màn hình số liệu tính giống nhau | Should |

## 5. Yêu cầu chức năng

| Mã | Yêu cầu | Story | Ưu tiên |
|---|---|---|---|
| PRD00-FR-01 | Hệ thống phải gắn mọi sản phẩm, feedback, nhãn và vai trò theo sản phẩm với đúng một tổ chức, và không cho ai ngoài tổ chức đó xem hay sửa chúng, trừ các ngoại lệ ghi trong ma trận quyền (khách qua link riêng). | US-07 | Must |
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
| PRD00-FR-14 | Hệ thống phải cho Phụ trách và Quản trị đổi trạng thái feedback theo §3.4. | US-03, US-08 | Must |
| PRD00-FR-15 | Hệ thống phải tự đưa feedback đang ở *Cần thêm thông tin* về trạng thái ngay trước đó khi người báo hoặc khách gửi bình luận. | US-05, US-06 | Must |
| PRD00-FR-16 | Hệ thống phải cho bất kỳ Phụ trách nào của sản phẩm (kể cả người đã sửa), Quản trị, hoặc người báo của feedback kết thúc bước In review: *đạt* → *Đã xử lý*, *chưa đạt* → *Đang xử lý* kèm lý do bắt buộc, lý do lưu thành bình luận gửi người báo. | US-09 | Must |
| PRD00-FR-17 | Hệ thống phải cho người báo và khách mở lại feedback của mình khi nó đang ở *Đã xử lý*, đưa về *Đang xử lý*. | US-05, US-06 | Must |
| PRD00-FR-18 | Hệ thống phải yêu cầu gắn một feedback gốc khi chuyển feedback sang *Trùng lặp*. | US-08 | Must |
| PRD00-FR-19 | Hệ thống phải ghi vào lịch sử mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. | US-10 | Must |
| PRD00-FR-20 | Hệ thống phải chặn người báo và khách bình luận khi feedback ở *Đóng* hoặc *Từ chối*, và cho họ biết cần gửi feedback mới nếu muốn trao đổi tiếp. | US-05, US-06 | Must |
| PRD00-FR-21 | Hệ thống phải có vai trò admin hệ thống tách khỏi mọi tổ chức, chỉ với các quyền ghi trong ma trận quyền. | US-11 | Should |
| PRD00-FR-22 | Hệ thống phải cho người báo chọn loại feedback khi gửi, và cho Phụ trách, Quản trị sửa loại sau đó. | US-03, US-05 | Must |

## 6. Quy tắc nghiệp vụ

| Mã | Quy tắc | Áp dụng cho |
|---|---|---|
| PRD00-BR-01 | Mọi thành viên đang hoạt động của tổ chức là người báo tiềm năng và gửi được feedback cho mọi sản phẩm của tổ chức. | FR-09, FR-11 |
| PRD00-BR-02 | Một người có thể vừa là Phụ trách sản phẩm A vừa là người báo ở sản phẩm B, và có vai trò theo sản phẩm khác nhau ở các sản phẩm khác nhau. | FR-02, FR-06 |
| PRD00-BR-03 | Với mỗi sản phẩm, một thành viên có tối đa một vai trò theo sản phẩm: Phụ trách hoặc Viewer. | FR-06 |
| PRD00-BR-04 | Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức, kể cả kết thúc In review. | FR-05, FR-16 |
| PRD00-BR-05 | Người được giao phải là một trong các Phụ trách của sản phẩm. | FR-06, FR-14 |
| PRD00-BR-06 | Gửi qua widget: là thành viên của tổ chức sở hữu sản phẩm → gửi với tư cách người báo, không cần chuyển tổ chức trước. Đã đăng nhập Hub nhưng không thuộc tổ chức đó → gửi với tư cách khách, tên và email điền sẵn từ tài khoản, không thấy gì bên trong tổ chức đó. | FR-11 |
| PRD00-BR-07 | Người báo và khách thấy: mô tả, ảnh, file mình đã gửi (gắn nhãn "Bạn đã gửi"); trạng thái và lịch sử đổi trạng thái; bình luận gửi người báo. Không thấy: ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên, bình luận nội bộ. | FR-09, FR-10 |
| PRD00-BR-08 | Tên người trả lời hiện cho người báo là thành viên: tên thật của người IT. Hiện cho khách: "Đội hỗ trợ <tên sản phẩm>". | FR-09, FR-10 |
| PRD00-BR-09 | Người báo chỉ có hai thao tác đổi trạng thái: kết thúc In review trên feedback của mình, và mở lại khi feedback của mình đang ở *Đã xử lý*. Khách có đúng hai thao tác đó trên trang link riêng. | FR-14, FR-16, FR-17 |
| PRD00-BR-10 | Bước In review không bắt buộc: Phụ trách có thể chuyển thẳng *Đang xử lý* → *Đã xử lý*. | FR-14, FR-16 |
| PRD00-BR-11 | Kết thúc In review với *chưa đạt* phải có lý do; lý do được lưu thành bình luận gửi người báo. | FR-16 |
| PRD00-BR-12 | Rời *Cần thêm thông tin* tự động chỉ khi người báo hoặc khách gửi bình luận; feedback quay về đúng trạng thái ngay trước khi vào *Cần thêm thông tin*. | FR-15 |
| PRD00-BR-13 | Khi feedback gốc sang *Đã xử lý*, người báo của mọi bản trùng lặp của nó được thông báo (kênh: PRD05). | FR-18 |
| PRD00-BR-14 | Lịch sử chỉ được thêm, không ai sửa hay xoá được mục lịch sử. | FR-19 |
| PRD00-BR-15 | Hộp feedback và mọi màn hình quản lý chỉ hiện dữ liệu của tổ chức đang làm việc; "Feedback của tôi" cũng theo tổ chức đang làm việc. | FR-04 |
| PRD00-BR-16 | Quản trị tổ chức không xem được kết quả khảo sát. | FR-21 |
| PRD00-BR-17 | Xử lý lần đầu tính một lần duy nhất cho mỗi feedback, tại lần đầu nó sang *Đã tiếp nhận*, hoặc sang thẳng *Từ chối* / *Trùng lặp* từ *Mới*. | §2, PRD06 |

## 7. Tiêu chí nghiệm thu

| Mã | Yêu cầu | Cho trước (Given) | Khi (When) | Thì (Then) |
|---|---|---|---|---|
| PRD00-AC-01 | FR-01 | Tổ chức X có sản phẩm P; tài khoản A chỉ là thành viên tổ chức Y | A mở đường dẫn trực tiếp tới một feedback của P | Bị từ chối; không hiện tiêu đề, tên sản phẩm hay bất kỳ nội dung nào của feedback (cách báo lỗi: câu hỏi mở 23) |
| PRD00-AC-02 | FR-01 | Hai tổ chức X và Y cùng có sản phẩm tên "App Bán hàng" | Quản trị của X mở danh sách sản phẩm | Chỉ thấy sản phẩm của X |
| PRD00-AC-03 | FR-02 | Tài khoản A là Quản trị ở X và chỉ là thành viên thường ở Y | A làm việc ở Y | A không thấy màn hình quản lý sản phẩm của Y và không xem được feedback của sản phẩm Y mà A không được gán |
| PRD00-AC-04 | FR-03 | A là thành viên của X và Y, đang làm việc ở X | A chọn Y trong bộ chọn tổ chức | Hub chuyển sang Y mà không bắt đăng nhập lại; tên Y hiện ở vị trí tổ chức đang làm việc |
| PRD00-AC-05 | FR-03 | A là thành viên duy nhất của X | A mở bộ chọn tổ chức | Chỉ có X trong danh sách; không có tổ chức nào A không thuộc về |
| PRD00-AC-06 | FR-04 | A là Phụ trách sản phẩm P ở X và người báo ở Y; đang làm việc ở X | A mở hộp feedback và "Feedback của tôi" | Chỉ thấy feedback của X; feedback A đã gửi ở Y không hiện |
| PRD00-AC-07 | FR-05 | Q là Quản trị của X, không được gán vào sản phẩm nào; X có 20 sản phẩm | Q mở hộp feedback | Q thấy và lọc được feedback của cả 20 sản phẩm |
| PRD00-AC-08 | FR-06 | Q là Quản trị; M là thành viên của X | Q gán M làm Phụ trách sản phẩm P | M thấy feedback của P ngay ở lần tải tiếp theo; M không thấy sản phẩm khác |
| PRD00-AC-09 | FR-06 | M đang là Viewer của P | Q đổi M thành Phụ trách của P | M có đúng một vai trò ở P là Phụ trách, không còn là Viewer |
| PRD00-AC-10 | FR-06 | M là thành viên thường, không phải Quản trị | M thử gán người khác làm Phụ trách | Thao tác bị từ chối với thông báo không đủ quyền; không có gì thay đổi |
| PRD00-AC-11 | FR-07 | M là Phụ trách sản phẩm A, không được gán vào B | M mở đường dẫn trực tiếp tới một feedback của B | Bị từ chối; không hiện nội dung nào của feedback |
| PRD00-AC-12 | FR-07 | M là Phụ trách sản phẩm A | Quản trị bỏ gán M khỏi A trong lúc M đang mở một feedback của A | Thao tác tiếp theo của M trên feedback đó bị từ chối; A biến khỏi hộp feedback của M khi tải lại |
| PRD00-AC-13 | FR-07 | M là Phụ trách của sản phẩm P, P chưa có feedback nào | M mở hộp feedback | Thấy trạng thái trống cho biết P chưa có feedback |
| PRD00-AC-14 | FR-08 | V là Viewer của P | V mở một feedback của P | Không có nút đổi trạng thái, giao việc, đổi ưu tiên, loại, nhãn, đánh dấu trùng hay ô bình luận |
| PRD00-AC-15 | FR-08, FR-12 | V là Viewer của P | V gửi yêu cầu bình luận hoặc đổi trạng thái trực tiếp, không qua màn hình | Hệ thống từ chối vì không đủ quyền; feedback không đổi, lịch sử không có mục mới |
| PRD00-AC-16 | FR-09 | R là người báo, đã gửi feedback F1; người báo khác gửi F2 cùng sản phẩm | R mở "Feedback của tôi" | Chỉ thấy F1 |
| PRD00-AC-17 | FR-09, FR-12 | R là người báo của F1, không phải Phụ trách hay Viewer | R mở đường dẫn trực tiếp tới F2 do người khác gửi | Bị từ chối; không hiện nội dung nào của F2 |
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
| PRD00-AC-34 | FR-17 | F của người báo R ở *Đã xử lý* | R chọn *Mở lại* | F về *Đang xử lý*; lịch sử ghi R là người mở lại |
| PRD00-AC-35 | FR-17 | F của người báo R ở *Đóng* | R mở F | Không có thao tác *Mở lại* |
| PRD00-AC-36 | FR-18 | Phụ trách H đang xem F | H chọn *Trùng lặp* mà không chọn feedback gốc | Không chuyển được; hệ thống yêu cầu chọn feedback gốc |
| PRD00-AC-37 | FR-18 | F được đánh dấu trùng với gốc G | G sang *Đã xử lý* | Người báo của F được thông báo |
| PRD00-AC-38 | FR-19 | F trải qua đổi trạng thái, đổi ưu tiên và giao việc | Phụ trách mở lịch sử của F | Mỗi thay đổi có một mục riêng với người thực hiện, thời điểm, giá trị cũ và mới, theo thứ tự thời gian |
| PRD00-AC-39 | FR-19 | Phụ trách H xem lịch sử F | H tìm cách sửa hoặc xoá một mục lịch sử | Không có thao tác nào; gửi trực tiếp bị từ chối |
| PRD00-AC-40 | FR-20 | F của người báo R ở *Từ chối* | R mở F | Ô bình luận bị khoá, kèm lời nhắn gửi feedback mới nếu muốn trao đổi tiếp |
| PRD00-AC-41 | FR-20 | F của khách K ở *Đóng* | K mở link riêng | K không bình luận được, thấy lời nhắn tương tự |
| PRD00-AC-42 | FR-21 | S là admin hệ thống, không là thành viên của tổ chức X | S mở đường dẫn tới một feedback của X | Bị từ chối; không hiện nội dung nào của feedback (theo đề xuất câu hỏi mở 2; chờ quyết định) |
| PRD00-AC-43 | FR-21 | Q là Quản trị của X, không phải admin hệ thống | Q tìm cách mở kết quả một đợt khảo sát | Bị từ chối vì không đủ quyền |
| PRD00-AC-44 | FR-22 | Người báo R gửi feedback chọn loại *Lỗi* | Phụ trách đổi loại sang *Yêu cầu tính năng* | Loại đổi; lịch sử có mục đổi loại; R thấy loại mới |
| PRD00-AC-45 | FR-22 | R gửi feedback | R mở widget | R chọn được đúng ba loại ở §3.1 |

## 8. Trạng thái và trường hợp biên

- **Không có tổ chức**: tài khoản chưa thuộc tổ chức nào (ví dụ chỉ từng gửi với tư cách khách) mở Hub → câu hỏi mở 9.
- **Thành viên bị gỡ khỏi tổ chức** khi đang là người báo, Phụ trách hoặc người được giao → câu hỏi mở 8.
- **Tổ chức bị vô hiệu** (nền tảng đã có thao tác này): widget, link riêng của khách, thành viên đang làm việc trong tổ chức đó → câu hỏi mở 8.
- **Quyền bị thu hồi** (bỏ gán sản phẩm, đổi vai trò tổ chức) có hiệu lực ở thao tác tiếp theo của người đó, theo cơ chế sẵn có của nền tảng (trong vài giây); không cần đăng xuất (AC-12).
- **Sản phẩm không có Phụ trách**: không ai được giao được, và theo canvas thì feedback mới không báo cho ai → câu hỏi mở 12.
- **Một người là người báo và cũng là Phụ trách** của cùng sản phẩm: được làm mọi thao tác của Phụ trách trên feedback mình gửi; ở "Feedback của tôi" vẫn thấy feedback đó.
- **Khách không để email** không trả lời được khi feedback ở *Cần thêm thông tin* → câu hỏi mở 21.
- **Người đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm, và sản phẩm tắt chế độ khách**: widget làm gì → câu hỏi mở 24.
- **20 sản phẩm** trở lên mỗi tổ chức: bộ chọn sản phẩm, hộp feedback và phân quyền phải dùng tốt (theo canvas đã duyệt). Chưa có giới hạn tối đa nào được quyết.
- **Sửa đồng thời** (hai Phụ trách đổi trạng thái cùng lúc): thuộc PRD03.
- **Feedback gốc bị chuyển khỏi *Đã xử lý*** sau khi đã báo cho các bản trùng: thuộc PRD03.

## 9. Dữ liệu (mức nghiệp vụ)

| Trường | Ý nghĩa | Bắt buộc | Giới hạn | Ai được sửa |
|---|---|---|---|---|
| Tên tổ chức | Tên hiển thị của tổ chức | Có | Không để trống (nền tảng đã có) | Người có quyền quản lý tổ chức (nền tảng) |
| Định danh ngắn của tổ chức | Mã viết thường, duy nhất toàn Hub | Có | Duy nhất (nền tảng đã có) | Như trên |
| Vai trò tổ chức của thành viên | Một hoặc nhiều vai trò tổ chức | Có, ít nhất một | Tổ chức luôn còn ít nhất một Chủ sở hữu (nền tảng đã có) | Người có quyền quản lý thành viên (câu hỏi mở 1) |
| Vai trò theo sản phẩm | Phụ trách hoặc Viewer, cho một thành viên ở một sản phẩm | Không | Tối đa một vai trò mỗi người mỗi sản phẩm | Quản trị |
| Trạng thái feedback | Một trong chín trạng thái §3.4 | Có | Mặc định *Mới* khi gửi | Phụ trách, Quản trị; người báo và khách chỉ qua hai thao tác BR-09; tự động theo BR-12 |
| Feedback gốc | Feedback mà bản trùng gắn vào | Có khi trạng thái là *Trùng lặp* | Phạm vi chọn: câu hỏi mở 15 | Phụ trách, Quản trị |
| Loại | Lỗi / Yêu cầu tính năng / Góp ý/câu hỏi | Có | Đúng ba giá trị | Người báo khi gửi; Phụ trách, Quản trị sau đó |
| Mục lịch sử | Ai, lúc nào, trường nào, từ → đến | Tự sinh | Không sửa, không xoá | Không ai |

## 10. Thông báo và sự kiện

None. PRD này chỉ định nghĩa các sự kiện đổi trạng thái (§3.4); ai nhận gì qua kênh nào được định nghĩa ở PRD05. Ngoại lệ đã quyết, ghi ở BR-13 để giữ cạnh quy tắc trùng lặp.

## 11. Yêu cầu phi chức năng

- **Tách biệt dữ liệu**: không màn hình, email, file xuất hay thông báo nào hiện dữ liệu của tổ chức cho người ngoài tổ chức, trừ ngoại lệ trong ma trận quyền.
- **Kiểm tra quyền không phụ thuộc giao diện**: ẩn nút trên màn hình không thay cho việc từ chối thao tác; mọi thao tác bị chặn trong ma trận đều bị từ chối khi gọi trực tiếp (FR-12).
- **Không hiện nội dung khi không có quyền**: người không có quyền mở feedback hay sản phẩm không thấy bất kỳ nội dung nào của nó. Báo "không tìm thấy" hay "không đủ quyền": câu hỏi mở 23.
- **Ngôn ngữ**: giao diện có tiếng Việt và tiếng Anh; tên trạng thái, loại, vai trò dịch thống nhất ở mọi nơi (tên tiếng Anh: câu hỏi mở 19). Email mặc định tiếng Việt theo quyết định nền tảng 2026-08-04.
- **Truy vết**: mọi thay đổi trên feedback có lịch sử không sửa được (BR-14).
- **Quy mô**: một tổ chức có từ 20 sản phẩm trở lên vẫn dùng tốt (canvas đã duyệt).

## 12. Phụ thuộc

- **Nền tảng có sẵn**:
  - Đăng ký bằng email (bắt buộc xác minh email), đăng nhập Google và Microsoft, quản lý phiên đăng nhập.
  - Tổ chức: tạo tổ chức (người tạo thành Chủ sở hữu), vô hiệu tổ chức, chuyển tổ chức đang làm việc, quay về ngữ cảnh cá nhân không tổ chức.
  - Thành viên: thêm thành viên bằng email của một tài khoản **đã đăng ký**, đổi vai trò, gỡ thành viên; luôn còn ít nhất một Chủ sở hữu.
  - Vai trò tổ chức tự tạo từ danh mục quyền cố định; ba vai trò hệ thống Owner (mọi quyền), Admin (hiện chỉ có quyền quản lý thành viên), Member (không có quyền nào). Không có admin cấp hệ thống.
  - Thông báo trong Hub, email, web push.
- **PRD phụ thuộc vào PRD này**: PRD01–PRD07.
- **Dịch vụ bên ngoài**: không có mới.

## 13. Câu hỏi mở

| # | Câu hỏi | Đề xuất trả lời | Vì sao quan trọng | Trạng thái |
|---|---|---|---|---|
| 1 | "Quản trị" (spec lõi: vai trò tổ chức Quản trị / Thành viên) ứng với gì trong mô hình vai trò hiện có (Owner / Admin / Member và vai trò tự tạo)? | Thêm một quyền "Quản trị FeedbackHub" vào danh mục quyền cố định. Ai có quyền này là Quản trị. Chủ sở hữu luôn có; vai trò Admin có sẵn mặc định; Member không có; tổ chức đưa được quyền này vào vai trò tự tạo. Thêm và gỡ thành viên vẫn theo quyền quản lý thành viên sẵn có. | Quyết định ai thấy mọi sản phẩm và feedback của tổ chức, và ai cấu hình được widget. | Mở |
| 2 | Admin hệ thống được chỉ định thế nào, và được thấy gì của các tổ chức? | Chỉ người vận hành máy chủ Hub chỉ định, không cấp được qua giao diện Hub. Admin hệ thống chỉ thấy tên tổ chức và số thành viên (để chọn tổ chức nhận khảo sát và tính tỉ lệ tham gia); không thấy sản phẩm, feedback, thành viên cụ thể; không quản lý tổ chức. | Phản mục tiêu "tổ chức này thấy dữ liệu tổ chức kia"; nền tảng hiện cố ý không có quyền vượt cấp. Ảnh hưởng PRD07 và review bảo mật. | Mở |
| 3 | Ai được tạo tổ chức mới? Nền tảng hiện cho mọi tài khoản tự tạo và thành Chủ sở hữu. | Giữ như hiện tại: mọi tài khoản đã xác minh email tự tạo được tổ chức. | Hub mở cho người ngoài internet (chế độ khách); nếu chỉ admin hệ thống được tạo thì cần thêm luồng xin cấp tổ chức. | Mở |
| 4 | Thêm thành viên chưa có tài khoản thế nào? Nền tảng chỉ thêm được tài khoản đã đăng ký; spec lõi nói Quản trị "mời thành viên". | Thêm lời mời qua email: người nhận đăng ký rồi vào thẳng tổ chức với vai trò đã chọn; lời mời hết hạn sau 7 ngày, Quản trị thu hồi được. | Quyết định đồng nghiệp và tester bắt đầu dùng widget dễ hay khó; ảnh hưởng PRD05 (email mời). | Mở |
| 5 | Phụ trách và Quản trị có được chuyển giữa **mọi** trạng thái (kể cả mở lại từ *Đóng*, *Từ chối*, *Trùng lặp*) không? Và "người báo trả lời → rời *Cần thêm thông tin*" có phải là chuyển tự động **duy nhất** không? (Spec ghi [Claude quyết], chưa được xác nhận.) | Có, cả hai: sơ đồ là luồng chính, không phải ràng buộc cứng; chỉ có một chuyển tự động đó (cộng với tự đóng nếu câu 6 chọn tự động). | Quyết định thiết kế nút đổi trạng thái và các tiêu chí nghiệm thu của PRD03. | Mở |
| 6 | *Đã xử lý* → *Đóng*: thủ công hay tự động sau N ngày? | Tự đóng sau 7 ngày ở *Đã xử lý* nếu không ai mở lại; Phụ trách vẫn đóng tay được bất cứ lúc nào. | Không tự đóng thì *Đã xử lý* tích tụ mãi; quyết định thời gian người báo còn mở lại được. | Mở |
| 7 | Có bắt buộc lý do khi chuyển sang *Từ chối* không? | Có, lý do lưu thành bình luận gửi người báo. | Người báo và khách nhận email khi *Từ chối*; không lý do thì dễ bị coi là phớt lờ. | Mở |
| 8 | Thành viên bị gỡ khỏi tổ chức, hoặc tổ chức bị vô hiệu: feedback, vai trò sản phẩm, việc được giao, widget, link riêng ra sao? | Gỡ thành viên: feedback họ gửi giữ nguyên với tên kèm "(đã rời tổ chức)"; họ mất quyền xem và không nhận thông báo của tổ chức; vai trò theo sản phẩm bị bỏ; feedback giao cho họ về *Chưa giao*. Vô hiệu tổ chức: widget báo tạm ngừng nhận feedback, link riêng báo hết hiệu lực, không ai chuyển vào tổ chức đó được. | Tránh feedback mồ côi và lộ dữ liệu cho người đã rời đi. | Mở |
| 9 | Tài khoản chưa thuộc tổ chức nào mở Hub thì thấy gì? | Trang chào: tạo tổ chức (nếu câu 3 cho phép) hoặc chờ được thêm; không có hộp feedback hay "Feedback của tôi". | Khách đã đăng nhập (BR-06) có thể rơi vào trường hợp này. | Mở |
| 10 | Viewer có thấy bình luận nội bộ không? | Có (chỉ đọc). Viewer là người trong team theo dõi tình hình. | Ảnh hưởng ma trận quyền và màn hình chi tiết (PRD03). | Mở |
| 11 | Viewer có xem Tổng quan không? Spec lõi ghi Tổng quan cho Quản trị và Phụ trách; canvas Overview có nhóm "Phụ trách hoặc Viewer" trong bộ chọn sản phẩm. | Có, chỉ với sản phẩm được gán. | Ảnh hưởng PRD06. | Mở |
| 12 | Sản phẩm chưa có Phụ trách: feedback mới báo cho ai? Canvas Products ghi "sẽ không báo cho ai". | Báo cho Quản trị của tổ chức cho đến khi có Phụ trách. | Vấn đề gốc là feedback bị trôi; ảnh hưởng PRD01, PRD05. | Mở |
| 13 | Người báo hoặc khách *Mở lại* có bắt buộc lý do không? Canvas Mine không có ô lý do, trong khi *chưa đạt* bắt buộc. | Có, một câu ngắn, lưu thành bình luận như *chưa đạt*. | IT cần biết còn gì chưa ổn; hai thao tác giống nhau nên nên đối xử giống nhau. | Mở |
| 14 | Người được giao có thể là Quản trị không được gán Phụ trách sản phẩm? Spec: người được giao là một trong các Phụ trách. | Không; muốn nhận việc thì Quản trị tự gán mình làm Phụ trách. | Giữ BR-05 đơn giản; ảnh hưởng danh sách chọn người ở PRD03. | Mở |
| 15 | Feedback gốc của bản trùng có phải cùng sản phẩm không? Người báo bản trùng có bình luận tiếp được không? | Cùng sản phẩm; bản trùng không nhận bình luận của người báo (như *Đóng*), người báo theo dõi qua thông báo khi gốc xử lý. | Phụ trách sản phẩm A không thấy feedback sản phẩm B nên không chọn được gốc ở B. | Mở |
| 16 | Ai thấy đầy đủ email, số điện thoại của khách? Canvas Main che email ("an***@gmail.com") trong danh sách. | Quản trị và Phụ trách thấy đầy đủ ở chi tiết; danh sách che; Viewer chỉ thấy bản che. | Dữ liệu cá nhân của người ngoài; ảnh hưởng PRD03. | Mở |
| 17 | Sau khi gửi, người báo và khách có xem lại ngữ cảnh kỹ thuật không? Spec chỉ liệt kê mô tả, ảnh, file. | Người báo thấy (chỉ đọc); khách không thấy trên trang link riêng. | Link riêng có thể bị chuyển tiếp; lỗi console và URL có thể chứa dữ liệu nội bộ. | Mở |
| 18 | "24 giờ" và thời gian đến lần xử lý đầu tiên tính theo giờ lịch hay giờ làm việc? | Giờ lịch (24/7) cho bản đầu. | Cuối tuần sẽ làm danh sách cần chú ý đầy lên; giờ làm việc cần thêm lịch làm việc theo tổ chức. | Mở |
| 19 | Tên hiển thị: loại (spec "Yêu cầu tính năng", "Góp ý/câu hỏi"; canvas "Tính năng", "Góp ý"), các mức ưu tiên (chỉ canvas có: Cao / Trung bình / Thấp), và tên tiếng Anh của trạng thái. | Loại hiển thị ngắn như canvas (Lỗi / Tính năng / Góp ý); ưu tiên ba mức Cao / Trung bình / Thấp, mặc định chưa đặt; tiếng Anh: New, Acknowledged, Needs info, In progress, In review, Resolved, Closed, Rejected, Duplicate. | Mọi PRD, thiết kế và email dùng cùng tên. | Mở |
| 20 | Mã feedback (`FB-129` trên canvas) đánh số theo tổ chức hay theo sản phẩm? | Theo tổ chức, tăng dần, tiền tố `FB-`. | Mã hiện ở widget, email, bình luận; đánh theo sản phẩm sẽ trùng mã giữa sản phẩm. | Mở |
| 21 | Có cho chuyển sang *Cần thêm thông tin* khi feedback là của khách không để email (không ai trả lời được) không? | Không; thao tác bị khoá kèm giải thích. | Nếu không, feedback kẹt mãi và làm sai chỉ số "từng vào *Cần thêm thông tin*". | Mở |
| 22 | Spec ghi hai chỉ số "cả hai tính bằng trung vị", nhưng tỉ lệ từng vào *Cần thêm thông tin* là một tỉ lệ, không có trung vị. Tính thế nào, và kỳ đo dài bao lâu? | Tỉ lệ = số feedback gửi trong kỳ đã từng vào *Cần thêm thông tin* ÷ số feedback gửi trong kỳ; trung vị chỉ áp dụng cho thời gian đến lần xử lý đầu tiên; kỳ mặc định 30 ngày gần nhất, đổi được trên Tổng quan. | §2 là nguồn duy nhất PRD06 tính theo. | Mở |
| 23 | Người không có quyền mở một feedback hay sản phẩm (khác tổ chức, sản phẩm không được gán, feedback của người khác) thấy "không tìm thấy" hay "không đủ quyền"? | "Không tìm thấy" cho mọi trường hợp, để không lộ là feedback có tồn tại; cần `appsec` xác nhận khi thiết kế kỹ thuật. | Ảnh hưởng mọi AC "bị từ chối" (AC-01, 11, 17, 42) và thông điệp lỗi trên giao diện. | Mở |
| 24 | Người đã đăng nhập Hub nhưng không thuộc tổ chức sở hữu sản phẩm, và sản phẩm **tắt** chế độ khách: widget làm gì? | Widget không cho gửi, báo sản phẩm này chỉ nhận feedback từ thành viên của tổ chức, kèm gợi ý liên hệ người quản lý sản phẩm. | BR-06 và AC-23 chỉ phủ trường hợp chế độ khách bật; ảnh hưởng PRD02. | Mở |

## 14. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| 2026-10-22 | 0.1 | Bản nháp đầu từ spec lõi, spec khảo sát, `docs/tech/README.md` và nền tảng hiện có | Spec 2026-10-21 (quyết định của người dùng) |

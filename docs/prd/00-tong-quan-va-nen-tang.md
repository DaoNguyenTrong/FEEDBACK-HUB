# PRD-00 — Tổng quan và nền tảng: thuật ngữ, vai trò và quyền, vòng đời trạng thái, tổ chức và sản phẩm

| Mục | Giá trị |
|---|---|
| Mã | `PRD00` (tiền tố cho mọi mã trong tài liệu này) |
| Trạng thái | Draft |
| Phiên bản | 0.7 |
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
  - Mỗi người chỉ thấy và làm đúng phần việc của vai trò mình; dữ liệu của tổ chức này không bao giờ lộ sang tổ chức khác, kể cả với admin hệ thống (BR-21).
  - Một tài khoản làm việc được ở nhiều tổ chức, với vai trò khác nhau ở mỗi nơi.
  - Vòng đời feedback rõ ràng cho cả IT lẫn người báo: luôn biết feedback đang ở đâu và ai đang giữ bước tiếp theo.
- **Không phải mục tiêu**:
  - Màn hình quản lý sản phẩm, widget, hộp feedback, cổng người báo, thông báo, tổng quan, khảo sát: thuộc PRD01–PRD07.
  - Gói trả phí, hạn mức, billing.
  - Đồng bộ với công cụ ngoài (Plane, GitHub, GitLab), SDK Flutter, AI phân loại: để sau.
  - Đổi cơ chế đăng nhập, đăng ký, phiên của nền tảng.
- **Phản mục tiêu** (chạy đúng nhưng vẫn là thất bại nếu):
  - Người báo hoặc khách thấy được feedback của người khác.
  - Thành viên của tổ chức này, hay admin hệ thống, thấy dữ liệu bên trong một tổ chức mình không là thành viên (BR-21, BR-22).
  - Quyền admin hệ thống cấp được từ bên trong Hub (BR-20).
  - Một thao tác bị chặn trên giao diện nhưng vẫn làm được khi gọi thẳng vào hệ thống.
  - Báo lỗi qua widget phiền hơn nhắn Zalo.
- **Đo thành công** (chỉ số dùng chung, các PRD khác dẫn về đây; cách tính: BR-40, BR-44):
  - **Khảo sát hài lòng định kỳ** của người dùng Hub (PRD07).
  - **Tỉ lệ feedback từng vào *Cần thêm thông tin***. Càng thấp càng tốt: feedback đến đủ ngữ cảnh. Bằng số feedback gửi trong kỳ đã từng vào *Cần thêm thông tin* chia cho số feedback gửi trong kỳ.
  - **Thời gian đến lần xử lý đầu tiên**, tính bằng **trung vị**: từ lúc gửi đến lần đầu một người chuyển feedback ra khỏi *Mới*, sang bất kỳ trạng thái nào (BR-17).
  - Kỳ đo mặc định là 30 ngày gần nhất, đổi được trên Tổng quan. Mọi khoảng thời gian tính theo giờ lịch (24/7).
  - **Danh sách cần chú ý**: feedback chưa được xử lý lần đầu sau **24 giờ** lịch kể từ lúc gửi.
  - Cách tính áp dụng như nhau trên mọi màn hình số liệu (FR-47).

## 3. Người dùng và vai trò

### 3.1 Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| Hub | Ứng dụng web FeedbackHub nơi thành viên tổ chức xem, xử lý và theo dõi feedback. |
| Tài khoản | Tài khoản đăng nhập Hub của một người. Một tài khoản thuộc được nhiều tổ chức. |
| Tổ chức | Một công ty hoặc đơn vị dùng Hub. Dữ liệu (sản phẩm, feedback, nhãn, vai trò) của mỗi tổ chức tách biệt hoàn toàn với các tổ chức khác. Về một tổ chức, admin hệ thống chỉ thấy tên, số thành viên, dấu hiệu đã vô hiệu và kết quả khảo sát theo PRD07 (BR-21). |
| Thành viên | Tài khoản đang hoạt động trong một tổ chức. Thành viên bị gỡ khỏi tổ chức không còn là thành viên. |
| Tổ chức đang làm việc | Tổ chức mà phiên làm việc của người dùng đang ở trong đó. Mỗi phiên có tối đa một tổ chức đang làm việc; người dùng tự chuyển được. |
| Ngữ cảnh cá nhân | Phiên làm việc không có tổ chức đang làm việc (nền tảng đã có). Trong ngữ cảnh này người dùng luôn thấy trang chào, kèm bộ chọn tổ chức nếu họ có tổ chức đang hoạt động (BR-68). |
| Trang chào | Màn hình của ngữ cảnh cá nhân (BR-68): lựa chọn tạo tổ chức hoặc chờ được thêm; kèm bộ chọn tổ chức khi tài khoản có tổ chức đang hoạt động; kèm lời giải thích khi mọi tổ chức của mình đang bị vô hiệu (BR-64). Tài khoản chưa thuộc tổ chức nào luôn thấy màn hình này (BR-32). |
| Vai trò tổ chức | Gói quyền gán cho thành viên trong một tổ chức. Nền tảng có sẵn ba vai trò hệ thống: *Chủ sở hữu* (Owner, có mọi quyền), *Admin* (quyền quản lý thành viên và quyền Quản trị FeedbackHub), *Member* (không có quyền nào); vai trò hệ thống không sửa, không xoá được (BR-60). Tổ chức tạo thêm vai trò tự tạo từ một danh mục quyền cố định. |
| Quyền Quản trị FeedbackHub | Quyền trong danh mục quyền cố định; thành viên có quyền này là Quản trị (BR-18). |
| Quyền quản lý thành viên | Quyền sẵn có của nền tảng để thêm, gỡ thành viên, đổi vai trò tổ chức, gửi và thu hồi lời mời (BR-19), trong giới hạn chống leo thang quyền (BR-58). |
| Quyền quản lý vai trò | Quyền sẵn có của nền tảng để tạo, sửa, xoá vai trò tự tạo của tổ chức (BR-60), trong giới hạn chống leo thang quyền (BR-58). |
| Quyền quản lý tổ chức | Quyền sẵn có của nền tảng để quản lý chính tổ chức: đổi tên tổ chức (BR-65) và vô hiệu tổ chức (BR-52). |
| Các quyền nền tảng khác | Bốn quyền sẵn có của nền tảng ngoài các quyền trên: quản lý khoá API, xem nhật ký kiểm toán, quản lý file, quản lý cài đặt. Không quyền nào mở đường tới feedback ngoài ma trận quyền (BR-59). |
| Chống leo thang quyền | Quy tắc chung cho mọi thao tác cấp hoặc gỡ quyền: chỉ Chủ sở hữu động tới vai trò Chủ sở hữu, không ai cấp được quyền mình không có (BR-58). |
| Lời mời | Email mời một người vào một tổ chức với một vai trò tổ chức đã chọn, dù email đó đã có tài khoản Hub hay chưa; hết hạn sau 7 ngày, thu hồi được (BR-24, BR-63). |
| Vô hiệu tổ chức | Tạm ngừng toàn bộ hoạt động của một tổ chức (BR-31); dữ liệu giữ nguyên. Ngược lại là *kích hoạt lại*, chỉ admin hệ thống làm được (BR-49). |
| Người vận hành máy chủ | Người quản trị máy chủ chạy Hub, đứng ngoài Hub. Là người duy nhất chỉ định được admin hệ thống (BR-20). |
| Sản phẩm | Một ứng dụng hoặc website của tổ chức có gắn widget. Mỗi sản phẩm thuộc đúng một tổ chức. Sản phẩm chỉ lưu trữ được, không xoá hẳn (§3.3; chi tiết: PRD01). |
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
| Người được giao | Một người trong số Phụ trách của sản phẩm, chịu trách nhiệm chính cho một feedback. Có thể để trống (*Chưa giao*). Người được giao mất vai trò Phụ trách thì feedback về *Chưa giao* (BR-61). |
| Nhãn | Thẻ tự do theo tổ chức, gắn lên feedback để lọc và nhóm. |
| Người báo | Thành viên tổ chức đã gửi một feedback (§3.2). |
| Khách | Người gửi feedback mà không phải thành viên của tổ chức sở hữu sản phẩm (§3.2). |
| Link riêng | Đường link khó đoán gửi qua email cho khách, mở trang theo dõi đúng một feedback. Thu hồi được, hết hạn. Chi tiết: PRD04. |
| Bình luận nội bộ | Bình luận chỉ team trong tổ chức thấy; người báo và khách không bao giờ thấy. |
| Bình luận gửi người báo | Bình luận người báo (hoặc khách) thấy và được thông báo. |
| Bình luận lưu lý do | Bình luận gửi người báo chứa lý do *Từ chối*, *chưa đạt* hoặc mở lại, lưu cùng lần đổi trạng thái đó (BR-11, BR-28, BR-29). Không ai sửa hay xoá được (BR-70). |
| Ngữ cảnh kỹ thuật | Thông tin widget tự thu lúc gửi: thiết bị, trình duyệt, màn hình, URL, lỗi console, request lỗi, dữ liệu sản phẩm truyền vào. Chi tiết: PRD02. |
| Trùng lặp / feedback gốc | Feedback đã được báo trước đó; bản trùng gắn với một feedback gốc cùng sản phẩm (BR-37). |
| Lịch sử | Bản ghi mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. |
| Xử lý lần đầu | Lần đầu một người chuyển feedback ra khỏi *Mới*, sang bất kỳ trạng thái nào (BR-17). |
| Hộp feedback | Màn hình danh sách và bảng feedback cho IT. Chi tiết: PRD03. |
| Feedback của tôi | Màn hình người báo xem feedback mình đã gửi. Chi tiết: PRD04. |
| Tổng quan | Màn hình số liệu theo sản phẩm. Chi tiết: PRD06. |
| Danh sách cần chú ý | Các feedback chưa được xử lý lần đầu sau 24 giờ lịch (§2). |
| Admin hệ thống | Người vận hành Hub, đứng ngoài mọi tổ chức, do người vận hành máy chủ chỉ định (BR-20). Chỉ có quyền cấp hệ thống: xem danh sách tổ chức (tên, số thành viên, dấu hiệu đã vô hiệu), chạy khảo sát và xem kết quả (PRD07), vô hiệu và kích hoạt lại tổ chức; không truy cập dữ liệu bên trong tổ chức nào (BR-21). |
| Đợt khảo sát | Một lần khảo sát hài lòng về Hub do admin hệ thống mở cho các tổ chức được chọn. Chi tiết: PRD07. |

### 3.2 Vai trò

| Vai trò | Cấp | Là ai | Phạm vi nhìn thấy | Nguồn |
|---|---|---|---|---|
| **Quản trị** | Tổ chức | Thành viên có quyền Quản trị FeedbackHub (BR-18): Chủ sở hữu luôn có, vai trò Admin có sẵn, Member không có, vai trò tự tạo có khi tổ chức thêm quyền này | Mọi sản phẩm và feedback của tổ chức, không cần được gán | Spec lõi §5; quyết định 2026-10-22 |
| **Phụ trách** | Theo sản phẩm | Thành viên được Quản trị gán Phụ trách cho một sản phẩm; một người phụ trách được nhiều sản phẩm | Mọi feedback của các sản phẩm mình phụ trách | Spec lõi §5 |
| **Viewer** | Theo sản phẩm | Thành viên được gán Viewer cho một sản phẩm | Mọi feedback của các sản phẩm mình được gán, kể cả bình luận nội bộ, và Tổng quan của các sản phẩm đó; chỉ đọc, trừ feedback chính mình gửi, nơi Viewer có đủ quyền người báo (BR-62) | Spec lõi §5; quyết định 2026-10-22 |
| **Người báo** | Tổ chức | Mọi thành viên của tổ chức, với feedback do chính mình gửi | Chỉ feedback chính mình gửi | Spec lõi §5 |
| **Khách** | Không thuộc tổ chức | Người không có tài khoản trong tổ chức sở hữu sản phẩm, kể cả người đã đăng nhập Hub nhưng thuộc tổ chức khác | Chỉ feedback mình gửi, qua link riêng | Spec lõi §5, §9 |
| **Admin hệ thống** | Hệ thống | Người vận hành Hub, do người vận hành máy chủ chỉ định, không cấp được qua Hub (BR-20) | Danh sách mọi tổ chức (tên, số thành viên, dấu hiệu đã vô hiệu) và khảo sát, kể cả kết quả (PRD07); vô hiệu, kích hoạt lại tổ chức (BR-49). Không thấy sản phẩm, feedback, thành viên hay cài đặt của tổ chức nào (BR-21). Ở tổ chức đã thêm họ làm thành viên, họ làm việc đúng như thành viên đó (BR-53) | Spec khảo sát §3; quyết định 2026-10-22 |

Quy tắc kết hợp vai trò: §6 (BR-01 đến BR-08, BR-12, BR-18 đến BR-22, BR-49, BR-52, BR-53, BR-58 đến BR-60, BR-62).

### 3.3 Ma trận quyền

Ký hiệu: **✓** được làm · **—** không được · **R** chỉ xem · **?n** chưa quyết, xem câu hỏi mở số *n* (đề xuất ghi trong câu hỏi). "Của mình" = feedback chính người đó gửi (hoặc, ở dòng bình luận, bình luận chính người đó viết). Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức. Viewer trên feedback chính mình gửi có đủ quyền người báo (BR-62). **BR-19** ở một ô: thao tác đi theo quyền quản lý thành viên của nền tảng, không theo vai trò ở cột; **BR-52** và **BR-65**: tương tự, theo quyền quản lý tổ chức; **BR-60**: theo quyền quản lý vai trò; **BR-59**: theo bốn quyền nền tảng khác (§3.1). Mọi thao tác cấp hoặc gỡ quyền còn theo **BR-58** (chống leo thang quyền). Cột Admin hệ thống chỉ ghi quyền cấp hệ thống (BR-21): ở mọi tổ chức họ không là thành viên, admin hệ thống không thấy và không làm được gì bên trong tổ chức. Ở tổ chức đã thêm họ làm thành viên, họ có đúng quyền của vai trò mình ở đó như mọi thành viên, không theo cột này (BR-53). Ô ghi "như mọi tài khoản" hoặc "như mọi thành viên" là thao tác đi theo tài khoản hoặc tư cách thành viên của người đó, không phải quyền admin hệ thống. Các dòng xoá feedback, bình luận, nhãn chỉ chốt ai được làm và các quy tắc BR-70, BR-71; chi tiết thuộc PRD03. *Bình luận lưu lý do*: §3.1. Dòng lưu trữ sản phẩm: chi tiết thuộc PRD01.

| Thao tác | Quản trị | Phụ trách | Viewer | Người báo | Khách | Admin hệ thống |
|---|---|---|---|---|---|---|
| **Hệ thống** | | | | | | |
| Cấp, bỏ quyền admin hệ thống | — | — | — | — | — | — (chỉ người vận hành máy chủ, BR-20) |
| Xem danh sách mọi tổ chức của Hub: tên, số thành viên, dấu hiệu đã vô hiệu (BR-21, BR-49) | — | — | — | — | — | ✓ |
| **Tổ chức** | | | | | | |
| Tạo tổ chức mới (mọi tài khoản đã xác minh email, BR-23) | ✓ | ✓ | ✓ | ✓ | ✓ nếu có tài khoản Hub | như mọi tài khoản |
| Đổi tên tổ chức | BR-65 | BR-65 | BR-65 | BR-65 | — | — |
| Thêm, gỡ thành viên tổ chức, đổi vai trò tổ chức | BR-19, BR-58 | BR-19, BR-58 | BR-19, BR-58 | BR-19, BR-58 | — | — (kể cả tự thêm mình, BR-53) |
| Gán, gỡ vai trò Chủ sở hữu, kể cả qua lời mời; gỡ khỏi tổ chức một thành viên đang là Chủ sở hữu | chỉ Chủ sở hữu (BR-58) | chỉ Chủ sở hữu (BR-58) | chỉ Chủ sở hữu (BR-58) | chỉ Chủ sở hữu (BR-58) | — | — |
| Gửi, thu hồi lời mời qua email | BR-19, BR-58 | BR-19, BR-58 | BR-19, BR-58 | BR-19, BR-58 | — | — |
| Tạo, sửa, xoá vai trò tự tạo | BR-60, BR-58 | BR-60, BR-58 | BR-60, BR-58 | BR-60, BR-58 | — | — |
| Sửa, xoá vai trò hệ thống (Chủ sở hữu, Admin, Member) | — | — | — | — | — | — (BR-60) |
| Quản lý khoá API, file, cài đặt; xem nhật ký kiểm toán | BR-59 | BR-59 | BR-59 | BR-59 | — | — |
| Chuyển tổ chức đang làm việc (giữa các tổ chức mình là thành viên) | ✓ | ✓ | ✓ | ✓ | — | như mọi tài khoản (BR-53); không vào được tổ chức mình không là thành viên |
| Vô hiệu tổ chức | BR-52 | BR-52 | BR-52 | BR-52 | — | ✓ kèm lý do (BR-49, BR-54) |
| Kích hoạt lại tổ chức đã vô hiệu | — | — | — | — | — | ✓ (BR-49) |
| **Sản phẩm** | | | | | | |
| Tạo, sửa sản phẩm và cấu hình widget | ✓ | — | — | — | — | — |
| Lưu trữ sản phẩm (không ai xoá hẳn được sản phẩm; chi tiết: PRD01) | ✓ | — | — | — | — | — |
| Gán, bỏ Phụ trách và Viewer | ✓ | — | — | — | — | — |
| **Feedback: xem** | | | | | | |
| Xem hộp feedback | ✓ mọi sản phẩm | ✓ sản phẩm được gán | ✓ sản phẩm được gán | — | — | — |
| Xem chi tiết feedback, ảnh, file | ✓ | ✓ | R | R của mình | R của mình, qua link riêng | — |
| Xem ngữ cảnh kỹ thuật | ✓ | ✓ | R | R của mình | — | — |
| Xem ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên | ✓ | ✓ | R | — | — | — |
| Xem trạng thái và lịch sử đổi trạng thái | ✓ | ✓ | R | R của mình | R của mình | — |
| Xem bình luận gửi người báo | ✓ | ✓ | R | R của mình | R của mình | — |
| Xem bình luận nội bộ | ✓ | ✓ | R | — | — | — |
| Xem đầy đủ email, điện thoại của khách (BR-38) | ✓ ở chi tiết; danh sách che | ✓ ở chi tiết; danh sách che | — (chỉ bản che) | — | — | — |
| Xem Tổng quan | ✓ | ✓ | R sản phẩm được gán | — | — | — |
| Xem "Feedback của tôi" | ✓ | ✓ | ✓ | ✓ | — | như mọi thành viên (BR-53) |
| **Feedback: thao tác** | | | | | | |
| Gửi feedback qua widget | ✓ | ✓ | ✓ | ✓ mọi sản phẩm của tổ chức | ✓ khi sản phẩm bật chế độ khách | như mọi tài khoản (BR-06) |
| Đổi trạng thái, giữa mọi trạng thái (§3.4, BR-25) | ✓ | ✓ | chỉ hai thao tác dưới, của mình (BR-62) | chỉ hai thao tác dưới | chỉ hai thao tác dưới | — |
| Kết thúc In review (đạt / chưa đạt) | ✓ | ✓ | ✓ của mình (BR-62) | ✓ của mình | ✓ của mình, qua link riêng | — |
| Mở lại khi *Đã xử lý* | ✓ | ✓ | ✓ của mình, kèm lý do (BR-62) | ✓ của mình, kèm lý do | ✓ của mình, qua link riêng, kèm lý do | — |
| Đổi loại, ưu tiên, gắn nhãn | ✓ | ✓ | — | — | — | — |
| Giao việc | ✓ | ✓ | — | — | — | — |
| Đánh dấu trùng lặp | ✓ | ✓ | — | — | — | — |
| Viết bình luận nội bộ | ✓ | ✓ | — | — | — | — |
| Viết bình luận gửi người báo | ✓ | ✓ | — | — | — | — |
| Bình luận, bổ sung trên feedback của mình (trừ khi *Đóng* / *Từ chối* / *Trùng lặp*) | dùng hai dòng trên; bình luận gửi người báo tính là trả lời của người báo, bình luận nội bộ thì không (BR-12) | dùng hai dòng trên; bình luận gửi người báo tính là trả lời của người báo, bình luận nội bộ thì không (BR-12) | ✓ (BR-62) | ✓ | ✓ qua link riêng | như mọi tài khoản |
| Sửa bình luận của chính mình, trừ bình luận lưu lý do (BR-70) | ✓ | ✓ | ✓ (BR-62) | ✓ | ✓ qua link riêng | — |
| Xoá bình luận (BR-70) | ✓ mọi bình luận, trừ bình luận lưu lý do | — | — | — | — | — |
| Xoá feedback: xoá hẳn (BR-71; chi tiết: PRD03) | ✓ | — | — | — | — | — |
| Tạo, sửa, xoá nhãn của tổ chức | ✓ | ✓ | — | — | — | — |
| Thu hồi link riêng của khách | ✓ | ✓ | — | — | — | — |
| **Khảo sát** (PRD07) | | | | | | |
| Tạo, mở, đóng đợt khảo sát; chọn tổ chức nhận (trừ tổ chức đang bị vô hiệu, BR-72); xem kết quả | — | — | — | — | — | ✓ |
| Trả lời khảo sát | ✓ | ✓ | ✓ | ✓ | — | như mọi thành viên của tổ chức được chọn (BR-53) |

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
- Chỉ có hai chuyển tự động: rời *Cần thêm thông tin* khi người báo hoặc khách trả lời (BR-12), và tự đóng sau 7 ngày (7×24 giờ lịch) ở *Đã xử lý* (BR-27).

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
| PRD00-US-13 | ~~Removed~~ (2026-10-22) | ~~thấy và làm được mọi việc trong mọi tổ chức~~ | ~~vận hành và hỗ trợ toàn bộ Hub~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. Thay bằng US-17 | — |
| PRD00-US-14 | người quản lý thành viên của tổ chức | mời đồng nghiệp, tester chưa có tài khoản qua email | họ bắt đầu dùng widget mà không phải tự đăng ký rồi chờ được thêm | Should |
| PRD00-US-15 | Phụ trách | feedback đã xử lý tự đóng khi không ai mở lại | hộp feedback không tích tụ feedback *Đã xử lý* | Should |
| PRD00-US-16 | ~~Removed~~ (2026-10-22) | ~~biết khi nào admin hệ thống vào tổ chức mình và họ đã thay đổi gì~~ | ~~tin tưởng dùng chung Hub dù có người toàn quyền ở cấp hệ thống~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. | — |
| PRD00-US-17 | admin hệ thống | xem danh sách các tổ chức (tên, số thành viên), vô hiệu và kích hoạt lại tổ chức | vận hành Hub mà không chạm vào dữ liệu bên trong tổ chức nào | Must |
| PRD00-US-18 | người quản lý thành viên của tổ chức | thêm admin hệ thống làm thành viên khi cần họ giúp, và họ chỉ làm được đúng vai trò mình gán | nhận hỗ trợ mà không phải mở dữ liệu cho người ngoài tổ chức | Should |
| PRD00-US-19 | Chủ sở hữu của một tổ chức | nhận email khi người khác vô hiệu tổ chức mình (kèm lý do khi là admin hệ thống) hoặc khi admin hệ thống kích hoạt lại nó | không chỉ biết khi widget ngừng nhận feedback, và biết vì sao tổ chức bị ngừng | Must |
| PRD00-US-20 | người có quyền quản lý tổ chức | đổi tên tổ chức | tên hiển thị trong Hub luôn đúng tên hiện tại của công ty | Should |
| PRD00-US-21 | Chủ sở hữu của một tổ chức | chắc chắn không ai tự nâng quyền của mình, tự thành Chủ sở hữu hay cấp cho người khác quyền mà chính họ không có | phân quyền cho người quản lý thành viên mà không mất kiểm soát tổ chức | Must |

## 5. Yêu cầu chức năng

| Mã | Yêu cầu | Story | Ưu tiên |
|---|---|---|---|
| PRD00-FR-01 | Hệ thống phải gắn mọi sản phẩm, feedback, nhãn và vai trò theo sản phẩm với đúng một tổ chức, và không cho ai ngoài tổ chức đó xem hay sửa chúng, trừ ngoại lệ khách qua link riêng ghi trong ma trận quyền; admin hệ thống không phải ngoại lệ (BR-21). | US-07 | Must |
| PRD00-FR-02 | Hệ thống phải cho một tài khoản là thành viên của nhiều tổ chức, với vai trò tổ chức và vai trò theo sản phẩm riêng ở từng tổ chức. | US-01 | Must |
| PRD00-FR-03 | Hệ thống phải cho mỗi phiên làm việc có tối đa một tổ chức đang làm việc, và cho người dùng chuyển sang tổ chức khác mình là thành viên mà không phải đăng nhập lại. | US-01 | Must |
| PRD00-FR-04 | Hệ thống phải giới hạn hộp feedback, quản lý sản phẩm, Tổng quan và "Feedback của tôi" vào dữ liệu của tổ chức đang làm việc. | US-01, US-07 | Must |
| PRD00-FR-05 | Hệ thống phải cho Quản trị thấy mọi sản phẩm và feedback của tổ chức mà không cần được gán vào sản phẩm. | US-02 | Must |
| PRD00-FR-06 | Hệ thống phải cho Quản trị gán một thành viên làm Phụ trách hoặc Viewer của từng sản phẩm, và bỏ gán. | US-02 | Must |
| PRD00-FR-07 | Hệ thống phải giới hạn Phụ trách và Viewer vào feedback của các sản phẩm họ được gán. | US-03, US-04 | Must |
| PRD00-FR-08 | Hệ thống phải chặn Viewer khỏi mọi thao tác thay đổi feedback, kể cả bình luận, trừ các thao tác của người báo trên feedback chính Viewer đã gửi (BR-62). | US-04 | Must |
| PRD00-FR-09 | Hệ thống phải giới hạn người báo vào feedback chính mình gửi, và chỉ hiện cho họ các phần ma trận quyền cho phép (không ưu tiên, không người được giao, không bình luận nội bộ). | US-05 | Must |
| PRD00-FR-10 | Hệ thống phải cho khách xem và trao đổi trên đúng một feedback mình gửi qua link riêng, không cần tài khoản. | US-06 | Must |
| PRD00-FR-11 | Hệ thống phải xác định tư cách người gửi qua widget theo tổ chức sở hữu sản phẩm, không theo tổ chức đang làm việc của phiên. | US-05, US-06 | Must |
| PRD00-FR-12 | Hệ thống phải áp dụng ma trận quyền cho mọi thao tác, kể cả khi thao tác được gọi trực tiếp mà không qua màn hình của Hub, và áp dụng mọi thay đổi quyền từ thao tác tiếp theo của người bị đổi quyền (BR-67). | US-03, US-04, US-05, US-07 | Must |
| PRD00-FR-13 | Hệ thống phải dùng đúng chín trạng thái ở §3.4, với tên giống nhau trên mọi màn hình, email và thông báo. | US-08 | Must |
| PRD00-FR-14 | Hệ thống phải cho Phụ trách và Quản trị đổi trạng thái feedback theo §3.4, giữa mọi trạng thái (BR-25). | US-03, US-08 | Must |
| PRD00-FR-15 | Hệ thống phải tự đưa feedback đang ở *Cần thêm thông tin* về trạng thái ngay trước đó khi người báo hoặc khách gửi bình luận gửi người báo, kể cả khi người báo đồng thời là Phụ trách của sản phẩm hoặc là Quản trị; bình luận nội bộ không làm feedback rời *Cần thêm thông tin* (BR-12). | US-05, US-06 | Must |
| PRD00-FR-16 | Hệ thống phải cho bất kỳ Phụ trách nào của sản phẩm (kể cả người đã sửa), Quản trị, người báo của feedback, hoặc khách đã gửi feedback (qua link riêng) kết thúc bước In review: *đạt* → *Đã xử lý*, *chưa đạt* → *Đang xử lý* kèm lý do bắt buộc (BR-66), lý do lưu thành bình luận gửi người báo. | US-06, US-09 | Must |
| PRD00-FR-17 | Hệ thống phải cho người báo và khách mở lại feedback của mình khi nó đang ở *Đã xử lý*, đưa về *Đang xử lý*. | US-05, US-06 | Must |
| PRD00-FR-18 | Hệ thống phải yêu cầu gắn một feedback gốc cùng sản phẩm, không đang ở *Trùng lặp*, khi chuyển feedback sang *Trùng lặp* (BR-37); và khi một feedback gốc bị chuyển sang *Trùng lặp* thì chuyển mọi bản trùng của nó sang gắn với gốc mới (BR-69). | US-08 | Must |
| PRD00-FR-19 | Hệ thống phải ghi vào lịch sử mọi thay đổi trạng thái và thuộc tính của feedback: ai, lúc nào, từ giá trị nào sang giá trị nào. Lịch sử chỉ mất cùng feedback khi Quản trị xoá feedback đó (BR-14, BR-71). | US-10 | Must |
| PRD00-FR-20 | Hệ thống phải chặn người báo và khách bình luận khi feedback ở *Đóng*, *Từ chối* hoặc *Trùng lặp*, và cho họ biết cần gửi feedback mới nếu muốn trao đổi tiếp. | US-05, US-06 | Must |
| PRD00-FR-21 | Hệ thống phải có vai trò admin hệ thống tách khỏi mọi tổ chức, chỉ với các quyền cấp hệ thống ghi ở cột Admin hệ thống của ma trận quyền (danh sách tổ chức, khảo sát, vô hiệu và kích hoạt lại tổ chức), và không cho vai trò này xem hay thao tác trên bất kỳ dữ liệu nào bên trong tổ chức (BR-21). Khi tạo đợt khảo sát, admin hệ thống không chọn được tổ chức đang bị vô hiệu (BR-72). | US-11, US-17 | Must |
| PRD00-FR-22 | Hệ thống phải cho người báo chọn loại feedback khi gửi, và cho Phụ trách, Quản trị sửa loại sau đó. | US-03, US-05 | Must |
| PRD00-FR-23 | Hệ thống phải có quyền Quản trị FeedbackHub trong danh mục quyền cố định, luôn có ở Chủ sở hữu và ở vai trò Admin (không ai bỏ được, vì vai trò hệ thống không sửa được, BR-60), không có ở Member, và cho tổ chức đưa quyền này vào vai trò tự tạo (trong giới hạn BR-58). | US-02 | Must |
| PRD00-FR-24 | Hệ thống phải cho mọi tài khoản đã xác minh email tạo tổ chức mới và trở thành Chủ sở hữu của tổ chức đó. | US-01 | Must |
| PRD00-FR-25 | Hệ thống phải cho mời một người vào tổ chức qua email với một vai trò tổ chức đã chọn, dù email đó đã có tài khoản Hub hay chưa: người chưa có tài khoản đăng ký xong (bằng email hoặc Google, Microsoft cùng email) thì vào thẳng tổ chức với vai trò đó; người đã có tài khoản đăng nhập rồi chấp nhận. Lời mời hết hạn sau 7 ngày, thu hồi được, chỉ dùng được bởi tài khoản có đúng email được mời, và xử lý các trường hợp biên ở BR-63. Gửi và thu hồi theo quyền quản lý thành viên, trong giới hạn chống leo thang quyền (BR-19, BR-24, BR-51, BR-58). | US-14 | Should |
| PRD00-FR-26 | Hệ thống phải tự chuyển feedback sang *Đóng* khi nó ở *Đã xử lý* đủ 7 ngày (7×24 giờ lịch) mà không ai mở lại, chậm nhất 1 giờ sau mốc đó (BR-27). | US-15 | Should |
| PRD00-FR-27 | Hệ thống phải yêu cầu lý do (tối đa 500 ký tự, BR-66) khi chuyển feedback sang *Từ chối* và lưu lý do thành bình luận gửi người báo. | US-05, US-06, US-08 | Must |
| PRD00-FR-28 | Hệ thống phải yêu cầu người báo và khách ghi lý do ngắn (tối đa 500 ký tự, BR-66) khi mở lại, và lưu lý do thành bình luận gửi người báo. | US-05, US-06 | Must |
| PRD00-FR-29 | Hệ thống phải xử lý việc gỡ một thành viên khỏi tổ chức theo BR-30: giữ feedback họ đã gửi, bỏ vai trò theo sản phẩm, đưa feedback giao cho họ về *Chưa giao* (lịch sử ghi người gỡ, kèm ghi chú "do rời tổ chức"), chặn họ xem và nhận thông báo của tổ chức. | US-07 | Must |
| PRD00-FR-30 | Hệ thống phải ngừng nhận feedback qua widget, vô hiệu link riêng và chặn mọi người chuyển vào tổ chức khi tổ chức bị vô hiệu (BR-31, BR-57), nhưng vẫn cho thành viên trả lời đợt khảo sát đã mở trước đó (BR-72). | US-07 | Must |
| PRD00-FR-31 | Hệ thống phải hiện trang chào trong ngữ cảnh cá nhân, với lựa chọn tạo tổ chức hoặc chờ được thêm, kèm bộ chọn tổ chức khi tài khoản có tổ chức đang hoạt động, và không hiện hộp feedback hay "Feedback của tôi"; tài khoản chưa thuộc tổ chức nào luôn ở màn hình này (BR-32, BR-68). | US-01 | Must |
| PRD00-FR-32 | Hệ thống phải báo feedback mới cho Quản trị của tổ chức khi sản phẩm chưa có Phụ trách nào. | US-02 | Must |
| PRD00-FR-33 | Hệ thống phải che email và số điện thoại của khách trong danh sách, chỉ hiện đầy đủ ở màn hình chi tiết cho người có quyền theo BR-38. | US-03, US-04 | Must |
| PRD00-FR-34 | Hệ thống phải cấp cho mỗi feedback một mã `FB-<số>` tăng dần, không trùng trong tổ chức, và không dùng lại mã của feedback đã bị xoá (BR-42, BR-71). | US-08 | Must |
| PRD00-FR-35 | Hệ thống phải chặn chuyển sang *Cần thêm thông tin* với feedback của khách không để email, kèm giải thích. | US-08 | Must |
| PRD00-FR-36 | Hệ thống phải không cho gửi qua widget khi người dùng đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm và sản phẩm tắt chế độ khách, kèm lời giải thích và gợi ý liên hệ người quản lý sản phẩm; lời nhắn chỉ nêu tên sản phẩm, không nêu tên hay cách liên hệ của người nào trong tổ chức (BR-46). | US-06 | Must |
| PRD00-FR-37 | Hệ thống phải trả về cùng một thông báo "không tìm thấy" khi người không được thấy một feedback, sản phẩm hay dữ liệu khác bên trong tổ chức (như danh sách thành viên) mở hoặc thao tác trên nó, như khi nó không tồn tại (BR-45). | US-07 | Must |
| PRD00-FR-38 | Hệ thống phải không cho cấp hay bỏ quyền admin hệ thống từ bên trong Hub; chỉ người vận hành máy chủ chỉ định được. | US-07, US-11 | Must |
| PRD00-FR-39 | ~~Removed~~ (2026-10-22) — ~~ghi thay đổi của admin hệ thống trên feedback vào lịch sử với nhãn riêng (BR-47)~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. | — | — |
| PRD00-FR-40 | ~~Removed~~ (2026-10-22) — ~~ghi mỗi lần admin hệ thống vào một tổ chức vào nhật ký riêng của tổ chức (BR-48)~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. | — | — |
| PRD00-FR-41 | ~~Removed~~ (2026-10-22) — ~~cho admin hệ thống vào mọi tổ chức qua bộ chọn tổ chức, với dấu hiệu tư cách admin hệ thống (BR-49 cũ)~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. Danh sách tổ chức của admin hệ thống: FR-44 | — | — |
| PRD00-FR-42 | Hệ thống phải cho admin hệ thống vô hiệu và kích hoạt lại bất kỳ tổ chức nào, và khi kích hoạt lại thì đưa tổ chức về như trước khi vô hiệu, trừ những gì đã hết hạn trong thời gian vô hiệu (BR-49, BR-55). | US-17 | Must |
| PRD00-FR-43 | ~~Removed~~ (2026-10-22) — ~~giới hạn quyền của admin hệ thống trong tổ chức mình không là thành viên (BR-50)~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. | — | — |
| PRD00-FR-44 | Hệ thống phải cho admin hệ thống xem danh sách mọi tổ chức của Hub, mỗi tổ chức với tên, số thành viên hiện tại và dấu hiệu đã vô hiệu nếu có; danh sách không hiện thành viên, sản phẩm hay feedback nào (BR-21, BR-49). | US-17 | Must |
| PRD00-FR-45 | Hệ thống phải xử lý admin hệ thống được một tổ chức thêm làm thành viên đúng như mọi thành viên khác của tổ chức đó: quyền theo vai trò được gán, tên thường trong lịch sử và bình luận, không nhãn hay nhật ký riêng (BR-53). | US-18 | Must |
| PRD00-FR-46 | Hệ thống phải yêu cầu admin hệ thống ghi lý do (tối đa 500 ký tự) khi vô hiệu một tổ chức, kể cả khi admin hệ thống đồng thời là Chủ sở hữu của tổ chức đó; gửi email cho mọi Chủ sở hữu khác người thực hiện khi tổ chức bị vô hiệu, dù ai vô hiệu (kèm lý do nếu có; admin hệ thống không là thành viên thì email ghi "admin hệ thống FeedbackHub", không nêu tên cá nhân); và gửi email cho mọi Chủ sở hữu khác người thực hiện khi admin hệ thống kích hoạt lại tổ chức (BR-54). | US-17, US-19 | Must |
| PRD00-FR-47 | Hệ thống phải tính các chỉ số thành công dùng chung (§2) theo đúng một định nghĩa trên mọi màn hình số liệu: xử lý lần đầu (BR-17), giờ lịch 24/7 (BR-40), tỉ lệ từng vào *Cần thêm thông tin*, trung vị thời gian đến lần xử lý đầu tiên, kỳ đo mặc định 30 ngày (BR-44), và danh sách cần chú ý (24 giờ). Cách hiển thị: PRD06. | US-12 | Should |
| PRD00-FR-48 | Hệ thống phải báo cho người báo của mọi bản trùng lặp, và cho khách đã để email của bản trùng, khi feedback gốc lần đầu sang *Đã xử lý*, *Đóng* hoặc *Từ chối*, hoặc ngay lúc bản trùng được gắn vào một gốc đã ở một trong ba trạng thái đó; mỗi bản trùng chỉ được báo một lần (BR-13, BR-69). Kênh và nội dung: PRD05. | US-05, US-06 | Must |
| PRD00-FR-49 | Hệ thống phải chỉ cho admin hệ thống xem kết quả khảo sát; thành viên tổ chức, kể cả Quản trị và Chủ sở hữu, không xem được (BR-16). Chi tiết: PRD07. | US-11 | Should |
| PRD00-FR-50 | Hệ thống phải cho Phụ trách và Quản trị giao một feedback cho một Phụ trách của sản phẩm, đổi người được giao, hoặc để *Chưa giao* (BR-05, BR-36), và tự đưa feedback về *Chưa giao* khi người được giao mất vai trò Phụ trách của sản phẩm, lịch sử ghi người làm thao tác gây ra việc đó kèm ghi chú "do mất vai trò Phụ trách" (BR-61). | US-02, US-03 | Must |
| PRD00-FR-51 | Hệ thống phải cho Phụ trách và Quản trị đặt và đổi ưu tiên của feedback (*Cao*, *Trung bình*, *Thấp*), mặc định chưa đặt, với tên hiển thị theo ngôn ngữ giao diện (BR-41). | US-03 | Must |
| PRD00-FR-52 | Hệ thống phải chặn leo thang quyền trong mọi thao tác thêm, gỡ thành viên, đổi vai trò tổ chức, gửi lời mời, tạo và sửa vai trò tự tạo: chỉ Chủ sở hữu động tới vai trò Chủ sở hữu, không ai cấp được quyền mình không có, kể cả cho chính mình (BR-58). | US-21, US-07 | Must |
| PRD00-FR-53 | Hệ thống phải cho người có quyền quản lý tổ chức đổi tên tổ chức; tên mới hiện ở mọi nơi tên tổ chức xuất hiện, kể cả danh sách tổ chức của admin hệ thống; định danh ngắn của tổ chức không đổi theo và không ai sửa được (BR-65). | US-20 | Should |
| PRD00-FR-54 | Hệ thống phải cho người dùng biết khi tổ chức của mình đang bị vô hiệu: báo "tổ chức tạm ngừng" ở thao tác tiếp theo của người đang làm việc trong tổ chức đó rồi đưa họ về ngữ cảnh cá nhân (BR-68), giữ tổ chức trong bộ chọn tổ chức nhưng làm mờ kèm nhãn và không cho chọn, hiện trang chào có lời giải thích cho tài khoản mà mọi tổ chức đều đang bị vô hiệu, và không đưa ai vào tổ chức qua lời mời trong lúc vô hiệu (BR-57, BR-64). | US-01, US-07 | Must |
| PRD00-FR-55 | Hệ thống phải không gửi thông báo nào phát sinh từ một tổ chức trong lúc tổ chức đó bị vô hiệu, trừ email cho Chủ sở hữu theo BR-54, và không gửi bù khi kích hoạt lại (BR-56). | US-05, US-06 | Must |
| PRD00-FR-56 | Hệ thống phải bảo đảm bốn quyền nền tảng khác (quản lý khoá API, xem nhật ký kiểm toán, quản lý file, quản lý cài đặt) không cho người giữ chúng thấy hay thao tác trên feedback, ảnh, file hay bình luận ngoài những gì ma trận quyền cho phép (BR-59). | US-07 | Must |
| PRD00-FR-57 | Hệ thống phải cho tác giả sửa bình luận của chính mình, kể cả khách qua link riêng; chỉ cho Quản trị xoá bình luận; và không cho ai sửa hay xoá bình luận lưu lý do *Từ chối*, *chưa đạt* hoặc mở lại (BR-70). Thời hạn sửa, dấu "đã sửa": PRD03. | US-05, US-06, US-10 | Should |
| PRD00-FR-58 | Hệ thống phải cho Quản trị xoá hẳn một feedback: feedback biến khỏi mọi màn hình, mã của nó không bao giờ được dùng lại, người báo không được báo, và việc xoá được ghi lại ở một nơi ngoài feedback để truy vết (BR-71). Chi tiết: PRD03. | US-02, US-10 | Should |

## 6. Quy tắc nghiệp vụ

| Mã | Quy tắc | Áp dụng cho |
|---|---|---|
| PRD00-BR-01 | Mọi thành viên đang hoạt động của tổ chức là người báo tiềm năng và gửi được feedback cho mọi sản phẩm của tổ chức. | FR-09, FR-11 |
| PRD00-BR-02 | Một người có thể vừa là Phụ trách sản phẩm A vừa là người báo ở sản phẩm B, và có vai trò theo sản phẩm khác nhau ở các sản phẩm khác nhau. | FR-02, FR-06 |
| PRD00-BR-03 | Với mỗi sản phẩm, một thành viên có tối đa một vai trò theo sản phẩm: Phụ trách hoặc Viewer. | FR-06 |
| PRD00-BR-04 | Quản trị có mọi quyền của Phụ trách trên mọi sản phẩm của tổ chức, kể cả kết thúc In review. | FR-05, FR-16 |
| PRD00-BR-05 | Người được giao phải là một trong các Phụ trách của sản phẩm. | FR-06, FR-50 |
| PRD00-BR-06 | Gửi qua widget: là thành viên của tổ chức sở hữu sản phẩm → gửi với tư cách người báo, không cần chuyển tổ chức trước. Đã đăng nhập Hub nhưng không thuộc tổ chức đó → gửi với tư cách khách, tên và email điền sẵn từ tài khoản, không thấy gì bên trong tổ chức đó: về tổ chức, khách chỉ thấy tên sản phẩm, không thấy tên hay cách liên hệ của bất kỳ ai trong tổ chức (BR-46). | FR-11, FR-36 |
| PRD00-BR-07 | Người báo và khách thấy: mô tả, ảnh, file mình đã gửi (gắn nhãn "Bạn đã gửi"); trạng thái và lịch sử đổi trạng thái; bình luận gửi người báo. Không thấy: ưu tiên, người được giao, lịch sử giao việc và đổi ưu tiên, bình luận nội bộ. Ngữ cảnh kỹ thuật: BR-39. | FR-09, FR-10 |
| PRD00-BR-08 | Tên người trả lời hiện cho người báo là thành viên: tên thật của người IT. Hiện cho khách: "Đội hỗ trợ <tên sản phẩm>". Áp dụng cả cho lý do *Từ chối*, *chưa đạt* vì chúng lưu thành bình luận gửi người báo. | FR-09, FR-10 |
| PRD00-BR-09 | Người báo không có vai trò xử lý trên sản phẩm (không là Phụ trách của sản phẩm, không là Quản trị; kể cả khi là Viewer, BR-62) chỉ có hai thao tác đổi trạng thái: kết thúc In review trên feedback của mình, và mở lại khi feedback của mình đang ở *Đã xử lý*. Khách có đúng hai thao tác đó trên trang link riêng. | FR-08, FR-14, FR-16, FR-17 |
| PRD00-BR-10 | Bước In review không bắt buộc: Phụ trách có thể chuyển thẳng *Đang xử lý* → *Đã xử lý*. | FR-14, FR-16 |
| PRD00-BR-11 | Kết thúc In review với *chưa đạt* phải có lý do (tối đa 500 ký tự, BR-66); lý do được lưu thành bình luận gửi người báo. | FR-16 |
| PRD00-BR-12 | Rời *Cần thêm thông tin* tự động chỉ khi người báo hoặc khách gửi bình luận gửi người báo; feedback quay về đúng trạng thái ngay trước khi vào *Cần thêm thông tin*. Người báo đồng thời là Phụ trách của sản phẩm, hoặc là Quản trị: bình luận gửi người báo của họ trên feedback chính mình gửi tính là trả lời của người báo và cũng đưa feedback rời *Cần thêm thông tin*; bình luận nội bộ của họ không tính, feedback vẫn ở *Cần thêm thông tin*. Bình luận của Phụ trách hay Quản trị khác người báo không làm feedback rời *Cần thêm thông tin*. | FR-15 |
| PRD00-BR-13 | Khi feedback gốc lần đầu sang một trong ba trạng thái *Đã xử lý*, *Đóng* hoặc *Từ chối*, người báo của mọi bản trùng lặp của nó được thông báo, kể cả khách đã để email (khách không để email thì không có cách báo). Bản trùng được gắn vào một gốc **đã** ở một trong ba trạng thái đó: người báo được báo ngay lúc gắn, với kết quả hiện tại của gốc. Mỗi bản trùng chỉ được báo một lần theo quy tắc này: gốc sau đó mở lại rồi lại sang một trong ba trạng thái thì không báo nữa. Gốc tự nó bị đánh dấu trùng lặp: BR-69. Kênh và nội dung: PRD05. | FR-48 |
| PRD00-BR-14 | Lịch sử chỉ được thêm, không ai sửa hay xoá được mục lịch sử. Ngoại lệ duy nhất: Quản trị xoá một feedback thì lịch sử của feedback đó mất cùng nó, và việc xoá được ghi lại ở ngoài feedback (BR-71). | FR-19, FR-58 |
| PRD00-BR-15 | Hộp feedback và mọi màn hình quản lý chỉ hiện dữ liệu của tổ chức đang làm việc; "Feedback của tôi" cũng theo tổ chức đang làm việc. | FR-04 |
| PRD00-BR-16 | Quản trị tổ chức không xem được kết quả khảo sát. | FR-21, FR-49 |
| PRD00-BR-17 | Xử lý lần đầu tính một lần duy nhất cho mỗi feedback, tại lần đầu một người chuyển nó ra khỏi *Mới*, sang bất kỳ trạng thái nào (BR-25 cho phép mọi chuyển, ví dụ *Mới* → *Đang xử lý*). Feedback sau đó quay lại *Mới* rồi lại rời *Mới* không làm thay đổi thời điểm xử lý lần đầu. | FR-47, §2, PRD06 |
| PRD00-BR-18 | Thành viên là Quản trị khi một trong các vai trò tổ chức của họ có quyền Quản trị FeedbackHub. Chủ sở hữu luôn có quyền này; vai trò Admin luôn có, không ai bỏ được (BR-60); Member không có; tổ chức đưa được quyền này vào vai trò tự tạo, trong giới hạn BR-58. | FR-23 |
| PRD00-BR-19 | Thêm, gỡ thành viên, đổi vai trò tổ chức, gửi và thu hồi lời mời đi theo quyền quản lý thành viên sẵn có của nền tảng, không theo quyền Quản trị FeedbackHub, và luôn trong giới hạn chống leo thang quyền (BR-58). | FR-23, FR-25, FR-52 |
| PRD00-BR-20 | Quyền admin hệ thống chỉ do người vận hành máy chủ Hub chỉ định. Không ai cấp hay bỏ được quyền này từ bên trong Hub, kể cả Chủ sở hữu của một tổ chức hay chính admin hệ thống. | FR-38 |
| PRD00-BR-21 | Admin hệ thống chỉ có quyền ở cấp hệ thống: xem danh sách mọi tổ chức của Hub với tên, số thành viên và dấu hiệu đã vô hiệu (BR-49); tạo, mở, đóng đợt khảo sát, chọn tổ chức nhận và xem kết quả (chi tiết: PRD07); vô hiệu và kích hoạt lại tổ chức (BR-49). Admin hệ thống không thấy và không thao tác được trên sản phẩm, feedback, thành viên hay cài đặt của bất kỳ tổ chức nào; họ mở feedback hay sản phẩm của tổ chức thì thấy "không tìm thấy" như mọi người ngoài tổ chức (BR-45). Không có đường truy cập xuyên tổ chức nào, kể cả cho admin hệ thống. Thông tin về người trả lời khảo sát mà admin hệ thống thấy trong kết quả do PRD07 quy định; ngoài đó admin hệ thống không thấy thành viên của tổ chức. Các thao tác gắn với tài khoản (tạo tổ chức, gửi feedback qua widget, trả lời khảo sát khi là thành viên của tổ chức được chọn) admin hệ thống làm như mọi tài khoản. | FR-01, FR-21, FR-44 |
| PRD00-BR-22 | Quyền của admin hệ thống không mở thêm đường nào để bất kỳ ai, kể cả chính admin hệ thống, thấy dữ liệu bên trong một tổ chức mình không là thành viên. | FR-01, FR-21 |
| PRD00-BR-23 | Mọi tài khoản đã xác minh email tự tạo được tổ chức và trở thành Chủ sở hữu của tổ chức đó. | FR-24 |
| PRD00-BR-24 | Lời mời gửi tới một email, có hoặc chưa có tài khoản Hub, kèm vai trò tổ chức đã chọn. Người chưa có tài khoản đăng ký xong thì vào thẳng tổ chức với vai trò đó; người đã có tài khoản đăng nhập rồi chấp nhận lời mời (BR-63). Lời mời hết hạn sau 7 ngày, kể cả khi tổ chức đang bị vô hiệu (BR-55), và thu hồi được trước khi được dùng; lời mời hết hạn hoặc đã thu hồi không đưa ai vào tổ chức. Ai gửi và thu hồi: người có quyền quản lý thành viên (BR-19), vì mời là một cách thêm thành viên, nên vai trò trong lời mời cũng theo giới hạn chống leo thang quyền (BR-58); Quản trị không có quyền đó thì không gửi, không thu hồi được. Ai dùng được lời mời: BR-51. Các trường hợp biên: BR-63. | FR-25, FR-42 |
| PRD00-BR-25 | Phụ trách và Quản trị chuyển được feedback giữa mọi trạng thái, kể cả mở lại từ *Đóng*, *Từ chối*, *Trùng lặp*; sơ đồ §3.4 là luồng chính, không phải ràng buộc cứng. Các ràng buộc riêng vẫn áp dụng: *Trùng lặp* cần feedback gốc (FR-18), *Từ chối* cần lý do (BR-28), *Cần thêm thông tin* bị chặn với khách không để email (BR-43). | FR-14 |
| PRD00-BR-26 | Chỉ có hai chuyển trạng thái tự động: rời *Cần thêm thông tin* (BR-12) và tự đóng (BR-27). | FR-15, FR-26 |
| PRD00-BR-27 | Feedback ở *Đã xử lý* đủ 7 ngày liên tục (7×24 giờ lịch, tính từ lần vào *Đã xử lý* gần nhất) mà không ai mở lại thì tự sang *Đóng*, chậm nhất 1 giờ sau mốc đó; lịch sử ghi người thực hiện là hệ thống. 7 ngày vẫn chạy khi tổ chức đang bị vô hiệu (BR-55). Phụ trách và Quản trị vẫn đóng tay được bất cứ lúc nào. | FR-26, FR-42 |
| PRD00-BR-28 | Chuyển sang *Từ chối* phải có lý do (tối đa 500 ký tự, BR-66); lý do được lưu thành bình luận gửi người báo. | FR-27 |
| PRD00-BR-29 | Người báo hoặc khách mở lại phải ghi một câu lý do ngắn (tối đa 500 ký tự, BR-66); lý do được lưu thành bình luận gửi người báo, như *chưa đạt*. | FR-28 |
| PRD00-BR-30 | Thành viên bị gỡ khỏi tổ chức: feedback họ đã gửi giữ nguyên, tên hiện kèm "(đã rời tổ chức)"; họ mất quyền xem và không nhận thông báo của tổ chức; vai trò theo sản phẩm của họ bị bỏ; feedback đang giao cho họ về *Chưa giao*, lịch sử ghi người thực hiện là người gỡ thành viên, kèm ghi chú "do rời tổ chức". | FR-29 |
| PRD00-BR-31 | Tổ chức bị vô hiệu: widget của mọi sản phẩm báo tạm ngừng nhận feedback; link riêng của khách báo hết hiệu lực, và khách không xin được link riêng mới (PRD04); không ai chuyển vào tổ chức đó được, kể cả Chủ sở hữu và admin hệ thống (cách Hub báo cho người dùng: BR-64); không ai vào được qua lời mời (BR-57); không thông báo nào của tổ chức được gửi (BR-56); đợt khảo sát gửi cho "mọi tổ chức" không gồm tổ chức đang bị vô hiệu (PRD07); khảo sát chọn đích danh tổ chức: BR-72. Chỉ admin hệ thống kích hoạt lại được (BR-49). | FR-30, FR-54 |
| PRD00-BR-32 | Tài khoản chưa thuộc tổ chức nào mở Hub thì thấy trang chào: tạo tổ chức (BR-23) hoặc chờ được thêm; không có hộp feedback hay "Feedback của tôi". Trang chào cũng là màn hình của ngữ cảnh cá nhân cho mọi tài khoản (BR-68). | FR-31 |
| PRD00-BR-33 | Viewer xem được bình luận nội bộ, chỉ đọc. | FR-07, FR-08 |
| PRD00-BR-34 | Viewer xem được Tổng quan, chỉ với các sản phẩm mình được gán. | FR-07 |
| PRD00-BR-35 | Sản phẩm chưa có Phụ trách nào: feedback mới được báo cho Quản trị của tổ chức, cho đến khi sản phẩm có Phụ trách. | FR-32 |
| PRD00-BR-36 | Quản trị không được gán Phụ trách của sản phẩm thì không là người được giao được; muốn nhận việc, Quản trị tự gán mình làm Phụ trách sản phẩm đó (BR-05). | FR-06, FR-50 |
| PRD00-BR-37 | Feedback gốc của bản trùng phải cùng sản phẩm và không được đang ở *Trùng lặp*: danh sách chọn gốc chỉ có feedback không ở *Trùng lặp*. Feedback ở *Trùng lặp* không nhận bình luận của người báo hay khách (như *Đóng*); người báo theo dõi qua thông báo khi gốc được xử lý (BR-13). | FR-18, FR-20 |
| PRD00-BR-38 | Email và số điện thoại của khách: trong danh sách luôn hiện bản che (ví dụ "an***@gmail.com"); ở màn hình chi tiết, Quản trị và Phụ trách thấy đầy đủ, Viewer chỉ thấy bản che. | FR-33 |
| PRD00-BR-39 | Ngữ cảnh kỹ thuật: người báo xem lại được trên feedback của mình (chỉ đọc); khách không thấy trên trang link riêng. | FR-09, FR-10 |
| PRD00-BR-40 | "24 giờ" của danh sách cần chú ý và thời gian đến lần xử lý đầu tiên tính theo giờ lịch (24/7), không theo giờ làm việc. | FR-47, §2, PRD06 |
| PRD00-BR-41 | Tên hiển thị tiếng Việt của loại: *Lỗi*, *Tính năng*, *Góp ý*. Ưu tiên có ba mức *Cao*, *Trung bình*, *Thấp*, mặc định chưa đặt. Tên tiếng Anh của trạng thái: New, Acknowledged, Needs info, In progress, In review, Resolved, Closed, Rejected, Duplicate (theo thứ tự ở §3.4). Tên tiếng Anh của loại: Bug, Feature, Feedback (theo thứ tự *Lỗi*, *Tính năng*, *Góp ý*); của ưu tiên: High, Medium, Low (theo thứ tự *Cao*, *Trung bình*, *Thấp*). | FR-13, FR-22, FR-51 |
| PRD00-BR-42 | Mã feedback đánh số theo tổ chức, tăng dần, tiền tố `FB-`, dùng chung cho mọi sản phẩm của tổ chức; mỗi tổ chức có dãy số riêng. Mã của feedback đã bị xoá không bao giờ được dùng lại (BR-71). | FR-34, FR-58 |
| PRD00-BR-43 | Không chuyển được sang *Cần thêm thông tin* khi feedback là của khách không để email; thao tác bị khoá kèm giải thích. | FR-35 |
| PRD00-BR-44 | Tỉ lệ từng vào *Cần thêm thông tin* = số feedback gửi trong kỳ đã từng vào *Cần thêm thông tin* ÷ số feedback gửi trong kỳ. Trung vị chỉ áp dụng cho thời gian đến lần xử lý đầu tiên. Kỳ đo mặc định 30 ngày gần nhất, đổi được trên Tổng quan. | FR-47, §2, PRD06 |
| PRD00-BR-45 | Người không được thấy một feedback, sản phẩm hay dữ liệu khác bên trong tổ chức (khác tổ chức, sản phẩm không được gán, feedback của người khác, danh sách thành viên của tổ chức mình không thuộc) mà mở **hoặc thao tác** trên nó thì luôn thấy "không tìm thấy", giống hệt khi nó không tồn tại, để không lộ là nó có tồn tại; thao tác không có tác dụng gì. Người thấy được dữ liệu nhưng không có quyền làm thao tác (ví dụ Viewer bình luận trên feedback của người khác) thì thấy "không đủ quyền". Tổ chức đang bị vô hiệu: thông báo riêng "tổ chức tạm ngừng" (BR-64). | FR-37 |
| PRD00-BR-46 | Người đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm, và sản phẩm tắt chế độ khách: widget không cho gửi, báo sản phẩm này chỉ nhận feedback từ thành viên của tổ chức, kèm gợi ý liên hệ người quản lý sản phẩm. Lời nhắn chỉ nêu tên sản phẩm; không nêu tên, email hay cách liên hệ của bất kỳ ai trong tổ chức. | FR-36 |
| PRD00-BR-47 | ~~Removed~~ (2026-10-22) — ~~thay đổi của admin hệ thống trên feedback hiện với nhãn riêng trong lịch sử~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. Admin hệ thống không thay đổi được feedback nào nếu không là thành viên; khi là thành viên: BR-53 | — |
| PRD00-BR-48 | ~~Removed~~ (2026-10-22) — ~~nhật ký mỗi lần admin hệ thống vào một tổ chức~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. | — |
| PRD00-BR-49 | Admin hệ thống vô hiệu và kích hoạt lại được mọi tổ chức. Chỉ admin hệ thống kích hoạt lại được: Chủ sở hữu và thành viên không vào được tổ chức đã vô hiệu (BR-31), muốn kích hoạt lại thì liên hệ admin hệ thống. Admin hệ thống phải ghi lý do khi vô hiệu; các Chủ sở hữu được báo qua email khi tổ chức bị vô hiệu, dù ai vô hiệu, và khi admin hệ thống kích hoạt lại (BR-54). Kích hoạt lại đưa tổ chức về như trước khi vô hiệu, trừ những gì đã hết hạn trong thời gian vô hiệu (BR-55): thành viên vào lại được và giữ nguyên vai trò tổ chức và vai trò theo sản phẩm, widget nhận feedback lại, link riêng của khách còn hạn hoạt động lại. Trong danh sách tổ chức của admin hệ thống, tổ chức đã vô hiệu được đánh dấu, để admin hệ thống biết tổ chức nào cần kích hoạt lại. | FR-30, FR-42, FR-44, FR-46 |
| PRD00-BR-50 | ~~Removed~~ (2026-10-22) — ~~admin hệ thống không tính là Chủ sở hữu, không làm Phụ trách hay người được giao ở tổ chức mình không là thành viên~~. Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. Admin hệ thống không thao tác được gì bên trong tổ chức mình không là thành viên (BR-21) | — |
| PRD00-BR-51 | Lời mời chỉ dùng được bởi tài khoản có đúng email được mời. Người khác mở lời mời (đăng ký bằng email khác, hoặc đang đăng nhập tài khoản có email khác) thấy báo lời mời không dành cho mình và không được đưa vào tổ chức. | FR-25 |
| PRD00-BR-52 | Vô hiệu tổ chức đi theo quyền quản lý tổ chức sẵn có của nền tảng, không theo quyền Quản trị FeedbackHub. Người vô hiệu theo quyền này không bắt buộc ghi lý do, trừ khi người đó cũng là admin hệ thống (BR-54). Các Chủ sở hữu khác người thực hiện được báo qua email (BR-54). Admin hệ thống vô hiệu được mọi tổ chức (BR-49), kèm lý do (BR-54). | FR-30, FR-42, FR-46 |
| PRD00-BR-53 | Muốn admin hệ thống giúp bên trong một tổ chức, tổ chức thêm họ làm thành viên như mọi tài khoản (người có quyền quản lý thành viên, BR-19); admin hệ thống không tự thêm mình được. Ở tổ chức đó họ làm việc thuần tuý như thành viên ấy: quyền theo vai trò tổ chức và vai trò theo sản phẩm được gán, lịch sử và bình luận ghi tên như mọi thành viên, không có nhãn hay nhật ký riêng. Quyền admin hệ thống không cộng thêm quyền nào bên trong tổ chức. | FR-02, FR-21, FR-45 |
| PRD00-BR-54 | Admin hệ thống vô hiệu một tổ chức thì phải ghi lý do (tối đa 500 ký tự, BR-66); không có lý do thì không vô hiệu được. Quy tắc này áp dụng cả khi admin hệ thống đồng thời là Chủ sở hữu hoặc có quyền quản lý tổ chức ở tổ chức đó, dù vô hiệu từ danh sách tổ chức hay từ bên trong tổ chức (quy tắc chặt hơn thắng). Dù ai vô hiệu tổ chức (admin hệ thống, Chủ sở hữu, hay thành viên có quyền quản lý tổ chức, BR-52), mọi Chủ sở hữu **khác người thực hiện** nhận email báo tổ chức bị vô hiệu, có lý do nếu có; khi người vô hiệu là thành viên của tổ chức, email nêu tên người đó; khi người vô hiệu là admin hệ thống không là thành viên, email không nêu tên cá nhân mà ghi "admin hệ thống FeedbackHub", kèm lý do. Khi admin hệ thống kích hoạt lại một tổ chức, mọi Chủ sở hữu **khác người thực hiện** nhận email báo tổ chức đã hoạt động lại: admin hệ thống đồng thời là Chủ sở hữu thì không nhận email về lần kích hoạt lại do chính mình làm. Nội dung email: PRD05. | FR-46 |
| PRD00-BR-55 | Trong thời gian tổ chức bị vô hiệu, mọi mốc thời gian vẫn chạy theo giờ lịch (như BR-40): 7 ngày tự đóng ở *Đã xử lý* (BR-27), hạn 7 ngày của lời mời (BR-24), hạn của link riêng (PRD04). Kích hoạt lại chỉ khôi phục những gì còn hạn: feedback đã đủ 7 ngày ở *Đã xử lý* trong lúc vô hiệu thì ở *Đóng* khi kích hoạt lại, lịch sử ghi người thực hiện là hệ thống (BR-27); lời mời đã hết hạn thì không dùng được, muốn mời lại phải gửi lời mời mới; link riêng đã hết hạn thì vẫn hết hạn. | FR-25, FR-26, FR-42 |
| PRD00-BR-56 | Trong lúc tổ chức bị vô hiệu, không thông báo nào phát sinh từ tổ chức (trong Hub, email, web push) được gửi cho bất kỳ ai (người báo, khách, Phụ trách, Quản trị), kể cả khi feedback tự đóng (BR-55); lịch sử vẫn ghi đủ. Ngoại lệ duy nhất là email cho Chủ sở hữu theo BR-54. Kích hoạt lại không gửi bù thông báo nào của thời gian vô hiệu. Lời mời trả lời khảo sát (gửi theo tài khoản, PRD07) không thuộc quy tắc này. | FR-55 |
| PRD00-BR-57 | Người được mời mở một lời mời còn hạn trong lúc tổ chức bị vô hiệu: không được đưa vào tổ chức, thấy báo tổ chức đang tạm ngừng hoạt động. Lời mời không bị huỷ: nếu còn hạn sau khi tổ chức được kích hoạt lại (hạn vẫn chạy, BR-55) thì dùng được như thường. | FR-25, FR-54 |
| PRD00-BR-58 | Chống leo thang quyền. (1) Chỉ Chủ sở hữu gán hoặc gỡ được vai trò Chủ sở hữu, cho người khác hay cho chính mình, kể cả gửi lời mời với vai trò Chủ sở hữu. (2) Không ai cấp được quyền mình không có: khi thêm thành viên, đổi vai trò tổ chức (của người khác hay của chính mình) hoặc gửi lời mời, người thao tác chỉ chọn được vai trò mà mọi quyền trong đó mình đều có; khi tạo hoặc sửa vai trò tự tạo, chỉ đưa vào được quyền mình có. Chủ sở hữu có mọi quyền nên không bị giới hạn ở (2). (3) Gỡ cũng theo cùng giới hạn: chỉ Chủ sở hữu gỡ được khỏi tổ chức một thành viên đang là Chủ sở hữu; không ai gỡ khỏi người khác một vai trò, hay bỏ khỏi vai trò tự tạo một quyền, mà mình không có. Thao tác vi phạm bị từ chối vì không đủ quyền và không có gì thay đổi. Mục đích: không ai tự thành Chủ sở hữu, tự nâng quyền, hay gỡ các Chủ sở hữu khác để vô hiệu tổ chức mà không Chủ sở hữu nào được báo (BR-54). | FR-23, FR-25, FR-52 |
| PRD00-BR-59 | Bốn quyền nền tảng khác (quản lý khoá API, xem nhật ký kiểm toán, quản lý file, quản lý cài đặt) không mở đường nào tới feedback ngoài ma trận quyền: người giữ chúng không thấy được tiêu đề, nội dung, ảnh, file, ngữ cảnh kỹ thuật hay bình luận của feedback mà vai trò của họ (§3.2) không cho thấy, và không thay đổi được feedback nào qua các quyền này. | FR-56 |
| PRD00-BR-60 | Tạo, sửa, xoá vai trò tự tạo đi theo quyền quản lý vai trò sẵn có của nền tảng, trong giới hạn BR-58. Ba vai trò hệ thống không ai sửa hay xoá được: Chủ sở hữu luôn có mọi quyền; Admin luôn có quyền quản lý thành viên và quyền Quản trị FeedbackHub; Member không có quyền nào. | FR-23, FR-52 |
| PRD00-BR-61 | Khi người được giao của một feedback mất vai trò Phụ trách của sản phẩm đó (bị bỏ gán, hoặc đổi thành Viewer), feedback về *Chưa giao*; lịch sử ghi thay đổi người được giao từ người đó sang *Chưa giao*, với người thực hiện là người đã làm thao tác gây ra việc đó (Quản trị bỏ gán hoặc đổi vai trò theo sản phẩm), kèm ghi chú "do mất vai trò Phụ trách". Trạng thái feedback không đổi. Thành viên bị gỡ khỏi tổ chức: BR-30. | FR-50 |
| PRD00-BR-62 | Viewer của một sản phẩm, trên feedback chính mình gửi ở sản phẩm đó, có đủ quyền người báo: bình luận, bổ sung (BR-12 áp dụng), kết thúc In review, mở lại khi *Đã xử lý* (BR-09). Trên feedback người khác gửi, Viewer chỉ đọc (FR-08). | FR-08, FR-15, FR-16, FR-17 |
| PRD00-BR-63 | Trường hợp biên của lời mời. (1) Email đã có tài khoản Hub: người nhận đăng nhập rồi chấp nhận lời mời thì vào tổ chức với vai trò được mời. (2) Đăng ký bằng Google hoặc Microsoft với đúng email được mời được chấp nhận như đăng ký bằng email. (3) Mời lại một email đang có lời mời chờ: gửi lời mời mới, lời mời cũ hết hiệu lực ngay. (4) Email đã là thành viên của tổ chức thì không mời được; người gửi thấy báo người này đã là thành viên. (5) Vai trò trong lời mời bị xoá trước khi lời mời được dùng: lời mời hết hiệu lực. (6) Đăng ký qua lời mời bằng email và mật khẩu: việc mở lời mời từ email được tính là đã xác minh email, không cần xác minh lần nữa. | FR-25 |
| PRD00-BR-64 | Tổ chức bị vô hiệu, với người dùng của nó: người đang làm việc trong tổ chức khi nó bị vô hiệu thấy báo "tổ chức tạm ngừng" ở thao tác tiếp theo, và thao tác đó không có tác dụng; sau đó Hub đưa họ về ngữ cảnh cá nhân (BR-68). Tổ chức vẫn nằm trong bộ chọn tổ chức của thành viên, làm mờ kèm nhãn tạm ngừng, không chọn được. Tài khoản mà mọi tổ chức mình thuộc về đều đang bị vô hiệu mở Hub thì thấy trang chào (BR-32) kèm lời giải thích các tổ chức của mình đang tạm ngừng. | FR-30, FR-54 |
| PRD00-BR-65 | Đổi tên tổ chức đi theo quyền quản lý tổ chức sẵn có của nền tảng, không theo quyền Quản trị FeedbackHub. Tên mới không để trống và hiện ở mọi nơi tên tổ chức xuất hiện (bộ chọn tổ chức, danh sách tổ chức của admin hệ thống, email). Định danh ngắn của tổ chức đặt khi tạo tổ chức và không bao giờ đổi: không đổi theo khi đổi tên, và không ai sửa riêng được. | FR-53 |
| PRD00-BR-66 | Mọi lý do (từ chối, *chưa đạt*, mở lại, vô hiệu tổ chức) dài tối đa 500 ký tự; dài hơn thì không gửi được. Lý do bắt buộc thì không để trống. | FR-16, FR-27, FR-28, FR-46 |
| PRD00-BR-67 | Mọi thay đổi quyền (bỏ gán sản phẩm, đổi vai trò theo sản phẩm, đổi vai trò tổ chức, sửa vai trò tự tạo, gỡ thành viên) có hiệu lực từ thao tác tiếp theo của người bị đổi quyền, không cần đăng xuất. | FR-12 |
| PRD00-BR-68 | Ngữ cảnh cá nhân luôn hiện trang chào (BR-32), cộng với bộ chọn tổ chức nếu người dùng có tổ chức đang hoạt động. Người đang làm việc trong một tổ chức vừa bị vô hiệu, sau khi thấy "tổ chức tạm ngừng" (BR-64), được Hub đưa về ngữ cảnh cá nhân: nếu còn tổ chức đang hoạt động, họ chọn tổ chức khác từ bộ chọn; nếu không, họ thấy trang chào kèm lời giải thích các tổ chức của mình đang tạm ngừng (BR-64). | FR-31, FR-54 |
| PRD00-BR-69 | Khi một feedback gốc G bị đánh dấu trùng lặp với G2, mọi bản trùng của G chuyển sang gắn với G2 và được báo theo G2 (BR-13). Việc chuyển này tính như gắn bản trùng vào G2: G2 đã ở *Đã xử lý*, *Đóng* hoặc *Từ chối* thì người báo của bản trùng chưa từng được báo được báo ngay; bản trùng đã được báo (theo G) thì không báo lại. Vì gốc không được ở *Trùng lặp* (BR-37), không có chuỗi gốc lồng nhau hay vòng lặp. | FR-18, FR-48 |
| PRD00-BR-70 | Bình luận: tác giả sửa được bình luận của chính mình, kể cả khách qua link riêng. Không tác giả nào xoá được bình luận của mình; chỉ Quản trị xoá được bình luận. Bình luận lưu lý do *Từ chối*, *chưa đạt* hoặc mở lại (BR-11, BR-28, BR-29) không ai sửa hay xoá được, kể cả tác giả và Quản trị, vì nó là lý do của một lần đổi trạng thái đã ghi trong lịch sử (BR-14). Thời hạn sửa, dấu "đã sửa": PRD03. | FR-57 |
| PRD00-BR-71 | Quản trị xoá một feedback thì feedback bị xoá hẳn, cùng ảnh, file, bình luận và lịch sử của nó, khỏi mọi màn hình (hộp feedback, "Feedback của tôi", link riêng của khách); mở lại đường dẫn cũ thì thấy "không tìm thấy" (BR-45). Mã `FB-<số>` của nó không bao giờ được dùng lại (BR-42). Người báo không được báo về việc xoá. Việc xoá được ghi lại ở một nơi ngoài feedback để truy vết; nội dung bản ghi và ai xem được: PRD03. Xoá feedback đang là gốc của bản trùng: câu hỏi mở 72. | FR-58, FR-19, FR-34 |
| PRD00-BR-72 | Khảo sát và tổ chức bị vô hiệu: khi tạo đợt khảo sát, admin hệ thống không chọn được tổ chức đang bị vô hiệu làm tổ chức nhận. Tổ chức bị vô hiệu sau khi đợt đã mở thì thành viên của nó vẫn trả lời được, vì lời mời khảo sát gửi theo tài khoản (BR-56). Chi tiết: PRD07. | FR-21, FR-30 |

## 7. Tiêu chí nghiệm thu

| Mã | Yêu cầu | Cho trước (Given) | Khi (When) | Thì (Then) |
|---|---|---|---|---|
| PRD00-AC-01 | FR-01, FR-37 | Tổ chức X có sản phẩm P; tài khoản A chỉ là thành viên tổ chức Y | A mở đường dẫn trực tiếp tới một feedback của P | Hệ thống báo "không tìm thấy"; không hiện tiêu đề, tên sản phẩm hay bất kỳ nội dung nào của feedback |
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
| PRD00-AC-12 | FR-07, FR-12, FR-37 | M là Phụ trách sản phẩm A | Quản trị bỏ gán M khỏi A trong lúc M đang mở một feedback của A, rồi M đổi trạng thái feedback đó | M thấy "không tìm thấy", feedback không đổi; A biến khỏi hộp feedback của M khi tải lại |
| PRD00-AC-13 | FR-07 | M là Phụ trách của sản phẩm P, P chưa có feedback nào | M mở hộp feedback | Thấy trạng thái trống cho biết P chưa có feedback |
| PRD00-AC-14 | FR-08 | V là Viewer của P | V mở một feedback của P không do V gửi | Không có nút đổi trạng thái, giao việc, đổi ưu tiên, loại, nhãn, đánh dấu trùng hay ô bình luận |
| PRD00-AC-15 | FR-08, FR-12 | V là Viewer của P; F là feedback của P không do V gửi | V gửi yêu cầu bình luận hoặc đổi trạng thái F trực tiếp, không qua màn hình | Hệ thống từ chối vì không đủ quyền; feedback không đổi, lịch sử không có mục mới |
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
| PRD00-AC-27 | FR-14 | Người báo R (không là Phụ trách hay Quản trị của sản phẩm) của feedback F đang ở *Đang xử lý* | R tìm cách đổi trạng thái F | R không có thao tác đổi trạng thái nào ngoài kết thúc In review và mở lại khi *Đã xử lý* |
| PRD00-AC-28 | FR-15 | F đang ở *Đã tiếp nhận*, Phụ trách chuyển sang *Cần thêm thông tin* | Người báo của F gửi một bình luận | F tự về *Đã tiếp nhận*; lịch sử ghi người báo là người gây ra thay đổi |
| PRD00-AC-29 | FR-15 | F đang ở *Cần thêm thông tin*; H là Phụ trách của sản phẩm, không phải người báo của F | H gửi một bình luận gửi người báo | F vẫn ở *Cần thêm thông tin* |
| PRD00-AC-30 | FR-16 | F ở In review | Người báo của F chọn *chưa đạt* mà không ghi lý do | Không gửi được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-31 | FR-16 | F ở In review | Người báo chọn *chưa đạt* với lý do "Vẫn lỗi trên Safari" | F về *Đang xử lý*; lý do hiện thành bình luận gửi người báo; lịch sử ghi thay đổi |
| PRD00-AC-32 | FR-16 | F ở In review; H là Phụ trách đã sửa F | H chọn *đạt* | F sang *Đã xử lý* |
| PRD00-AC-33 | FR-16 | F ở In review, không do V gửi; V là Viewer của sản phẩm | V tìm cách kết thúc In review | Không có thao tác; gửi trực tiếp bị từ chối vì không đủ quyền |
| PRD00-AC-34 | FR-17, FR-28 | F của người báo R ở *Đã xử lý* | R chọn *Mở lại* và ghi lý do "Lỗi xuất hiện lại" | F về *Đang xử lý*; lý do hiện thành bình luận gửi người báo; lịch sử ghi R là người mở lại |
| PRD00-AC-35 | FR-17 | F của người báo R (không là Phụ trách hay Quản trị của sản phẩm) ở *Đóng* | R mở F | Không có thao tác *Mở lại* |
| PRD00-AC-36 | FR-18 | Phụ trách H đang xem F | H chọn *Trùng lặp* mà không chọn feedback gốc | Không chuyển được; hệ thống yêu cầu chọn feedback gốc |
| PRD00-AC-37 | FR-48 | F của người báo R được đánh dấu trùng với gốc G; G chưa từng ở *Đã xử lý*, *Đóng* hay *Từ chối* | G sang *Đã xử lý* | R được thông báo |
| PRD00-AC-38 | FR-19 | F trải qua đổi trạng thái, đổi ưu tiên và giao việc | Phụ trách mở lịch sử của F | Mỗi thay đổi có một mục riêng với người thực hiện, thời điểm, giá trị cũ và mới, theo thứ tự thời gian |
| PRD00-AC-39 | FR-19 | Phụ trách H xem lịch sử F | H tìm cách sửa hoặc xoá một mục lịch sử | Không có thao tác nào; gửi trực tiếp bị từ chối |
| PRD00-AC-40 | FR-20 | F của người báo R (không là Phụ trách hay Quản trị của sản phẩm) ở *Từ chối* | R mở F | Ô bình luận bị khoá, kèm lời nhắn gửi feedback mới nếu muốn trao đổi tiếp |
| PRD00-AC-41 | FR-20 | F của khách K ở *Đóng* | K mở link riêng | K không bình luận được, thấy lời nhắn tương tự |
| PRD00-AC-42 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên của tổ chức X~~ | ~~S mở đường dẫn tới một feedback của X~~ | ~~Bị từ chối~~ — thay bằng AC-46 theo quyết định câu hỏi mở 2 (v0.2); AC-46 bị bỏ ở v0.4, hành vi này nay ở AC-121 |
| PRD00-AC-43 | FR-49, FR-12 | Q là Quản trị của X, không phải admin hệ thống | Q tìm cách mở kết quả một đợt khảo sát | Bị từ chối vì không đủ quyền |
| PRD00-AC-44 | FR-22 | Người báo R gửi feedback chọn loại *Lỗi* | Phụ trách đổi loại sang *Tính năng* | Loại đổi; lịch sử có mục đổi loại; R thấy loại mới |
| PRD00-AC-45 | FR-22 | R gửi feedback | R mở widget | R chọn được đúng ba loại: *Lỗi*, *Tính năng*, *Góp ý* |
| PRD00-AC-46 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên của tổ chức X; F là feedback của X có ưu tiên, người được giao và bình luận nội bộ~~ | ~~S mở F~~ | ~~S thấy toàn bộ F: nội dung, ảnh, file, ngữ cảnh kỹ thuật, ưu tiên, người được giao, bình luận nội bộ, lịch sử~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-47 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống; X có 20 sản phẩm và 30 thành viên~~ | ~~S xem sản phẩm và thành viên của X~~ | ~~S thấy cả 20 sản phẩm và 30 thành viên của X~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-48 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; F của X ở *Mới*~~ | ~~S chuyển F sang *Đã tiếp nhận*~~ | ~~F ở *Đã tiếp nhận*; lịch sử có mục mới cho thay đổi này, người thực hiện hiện là "<tên S> (Admin hệ thống)"~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-49 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; M là thành viên của X~~ | ~~S gỡ M khỏi X~~ | ~~M không còn là thành viên X; BR-30 áp dụng cho M~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-50 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống đã mở feedback của X; A là Quản trị của Y, không thuộc X~~ | ~~A dùng Hub ở Y~~ | ~~A không thấy bất kỳ dữ liệu nào của X~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-51 | FR-38 | O là Chủ sở hữu của X | O tìm cách cấp quyền admin hệ thống cho một thành viên | Không có thao tác nào như vậy trong Hub; gửi trực tiếp bị từ chối |
| PRD00-AC-52 | FR-38 | S là admin hệ thống | S tìm cách cấp quyền admin hệ thống cho tài khoản khác qua Hub | Không có thao tác nào như vậy; gửi trực tiếp bị từ chối |
| PRD00-AC-53 | FR-23 | X có vai trò tự tạo "Trưởng IT" có quyền Quản trị FeedbackHub; M có vai trò đó, không được gán sản phẩm nào | M mở hộp feedback | M thấy feedback của mọi sản phẩm X như một Quản trị |
| PRD00-AC-54 | FR-23 | M chỉ có vai trò Member ở X, không được gán sản phẩm nào | M mở Hub ở X | M không thấy màn hình quản lý sản phẩm và không có hộp feedback của sản phẩm nào; chỉ có "Feedback của tôi" |
| PRD00-AC-55 | FR-23, FR-52 | O là Chủ sở hữu của X; A có quyền quản lý vai trò và quyền quản lý thành viên, không phải Chủ sở hữu | A tìm cách sửa vai trò Chủ sở hữu để bỏ quyền Quản trị FeedbackHub | Bị từ chối vì vai trò hệ thống không sửa được; O vẫn là Quản trị |
| PRD00-AC-56 | FR-24 | A là tài khoản đã xác minh email, chưa thuộc tổ chức nào | A tạo tổ chức "Công ty Z" | Z được tạo; A là Chủ sở hữu và là Quản trị của Z |
| PRD00-AC-57 | FR-25 | B chưa có tài khoản Hub; X gửi lời mời tới email của B với vai trò Member | B mở email trong vòng 7 ngày và đăng ký | B vào thẳng X với vai trò Member, không cần ai thêm lần nữa |
| PRD00-AC-58 | FR-25 | Lời mời tới B đã gửi được 8 ngày | B mở lời mời | B không vào được X; hệ thống báo lời mời đã hết hạn |
| PRD00-AC-59 | FR-25 | Lời mời tới B đã bị thu hồi | B mở lời mời | B không vào được X; hệ thống báo lời mời không còn hiệu lực |
| PRD00-AC-60 | FR-25 | M là thành viên X không có quyền quản lý thành viên (BR-19) | M gửi lời mời trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; không email nào được gửi |
| PRD00-AC-61 | FR-14 | F ở *Đóng*; H là Phụ trách sản phẩm | H chuyển F sang *Đang xử lý* | F ở *Đang xử lý*; lịch sử ghi H, *Đóng* → *Đang xử lý* |
| PRD00-AC-62 | FR-14 | F ở *Trùng lặp* với gốc G; Q là Quản trị | Q chuyển F sang *Đã tiếp nhận* | F ở *Đã tiếp nhận*; lịch sử ghi thay đổi |
| PRD00-AC-63 | FR-26 | F sang *Đã xử lý* lúc 09:00 ngày 1 và không ai mở lại | Đến 10:00 ngày 8 (7×24 giờ cộng 1 giờ) | F đã ở *Đóng*; lịch sử ghi người thực hiện là hệ thống, thời điểm không trước 09:00 ngày 8 |
| PRD00-AC-64 | FR-26 | F sang *Đã xử lý*; người báo mở lại ở ngày thứ 5 | Đến mốc 7 ngày kể từ lúc F sang *Đã xử lý* | F vẫn ở *Đang xử lý*, không bị tự đóng |
| PRD00-AC-65 | FR-26 | F ở *Đã xử lý* được 2 ngày | Phụ trách chuyển F sang *Đóng* | F ở *Đóng* ngay |
| PRD00-AC-66 | FR-27 | H là Phụ trách, F ở *Mới* | H chọn *Từ chối* mà không ghi lý do | Không chuyển được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-67 | FR-27 | H là Phụ trách, F ở *Mới* | H chọn *Từ chối* với lý do "Đúng theo thiết kế" | F ở *Từ chối*; lý do hiện thành bình luận gửi người báo, người báo thấy được |
| PRD00-AC-68 | FR-28 | F của khách K ở *Đã xử lý* | K chọn *Mở lại* trên trang link riêng mà không ghi lý do | Không mở lại được; hệ thống yêu cầu nhập lý do |
| PRD00-AC-69 | FR-29 | M có vai trò Member ở X, là người báo của F1, là Phụ trách duy nhất của P và là người được giao F2 | Người có quyền quản lý thành viên gỡ M khỏi X | F1 vẫn còn, tên M hiện "(đã rời tổ chức)"; F2 về *Chưa giao*; M không còn là Phụ trách của P; P trở thành sản phẩm chưa có Phụ trách (BR-35) |
| PRD00-AC-70 | FR-29, FR-37 | M vừa bị gỡ khỏi X | M mở đường dẫn tới F1 mình từng gửi, và có feedback mới ở P | M thấy "không tìm thấy"; M không nhận thông báo nào của X |
| PRD00-AC-71 | FR-30 | Tổ chức X bị vô hiệu; P thuộc X | Một người mở widget của P | Widget báo tạm ngừng nhận feedback; không gửi được |
| PRD00-AC-72 | FR-30 | Tổ chức X bị vô hiệu; khách K có link riêng của G thuộc X | K mở link | Trang báo link hết hiệu lực; không hiện nội dung G |
| PRD00-AC-73 | FR-30, FR-54 | Tổ chức X bị vô hiệu; A là thành viên X và Y | A mở bộ chọn tổ chức | X vẫn có trong danh sách, làm mờ kèm nhãn tạm ngừng; A không chọn được X |
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
| PRD00-AC-84 | FR-36 | A đăng nhập Hub, không thuộc X; P thuộc X và tắt chế độ khách | A mở widget của P | Widget không cho gửi, báo P chỉ nhận feedback từ thành viên của tổ chức, kèm gợi ý liên hệ người quản lý sản phẩm; lời nhắn có tên P, không có tên, email hay cách liên hệ của người nào trong X |
| PRD00-AC-85 | FR-37 | M là Phụ trách sản phẩm A ở X; F thuộc sản phẩm B của X; `FB-99999` không tồn tại | M mở F, rồi mở `FB-99999` | Cả hai lần M thấy cùng một thông báo "không tìm thấy" |
| PRD00-AC-86 | FR-07 | V là Viewer của P; F của P không do V gửi, có một bình luận nội bộ | V mở F | V thấy bình luận nội bộ, không có ô viết bình luận |
| PRD00-AC-87 | FR-07 | V là Viewer của P, không được gán Q; cả hai thuộc X | V mở Tổng quan | V chỉ thấy số liệu của P |
| PRD00-AC-88 | FR-50 | Q là Quản trị, không là Phụ trách của P | Q mở danh sách chọn người được giao cho một feedback của P | Q không có trong danh sách; sau khi Q tự gán mình làm Phụ trách P, Q có trong danh sách |
| PRD00-AC-89 | FR-18 | H là Phụ trách của A và B; F thuộc A | H đánh dấu F là *Trùng lặp* | Danh sách chọn feedback gốc chỉ có feedback của A |
| PRD00-AC-90 | FR-20 | F của người báo R (không là Phụ trách hay Quản trị của sản phẩm) ở *Trùng lặp* | R mở F | Ô bình luận bị khoá, kèm lời nhắn gửi feedback mới nếu muốn trao đổi tiếp |
| PRD00-AC-91 | FR-09 | R là người báo của F | R mở F | R thấy ngữ cảnh kỹ thuật của F, chỉ đọc |
| PRD00-AC-92 | FR-10 | Khách K gửi G | K mở link riêng của G | Không có ngữ cảnh kỹ thuật nào trên trang |
| PRD00-AC-93 | FR-51 | R vừa gửi F | Phụ trách mở F | Ưu tiên hiện "chưa đặt"; chọn được *Cao*, *Trung bình*, *Thấp* |
| PRD00-AC-94 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; S đổi ưu tiên của F thuộc X từ *Thấp* sang *Cao*~~ | ~~H, Phụ trách của sản phẩm, mở lịch sử của F~~ | ~~Có mục đổi ưu tiên *Thấp* → *Cao*, người thực hiện hiện là "<tên S> (Admin hệ thống)"~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-95 | ~~Removed~~ (2026-10-22) | ~~O là Chủ sở hữu của X~~ | ~~O tìm cách tắt việc ghi thay đổi của admin hệ thống vào lịch sử~~ | ~~Không có thiết lập nào như vậy; gửi trực tiếp bị từ chối~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-96 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X, đang làm việc ở tổ chức khác~~ | ~~S chuyển vào X~~ | ~~Nhật ký admin hệ thống của X có mục mới: S, thời điểm vào, X~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-97 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X, đang không làm việc ở X~~ | ~~S mở đường dẫn trực tiếp tới feedback F của X~~ | ~~S thấy F (AC-46); nhật ký admin hệ thống của X có mục mới cho lần vào này~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-98 | ~~Removed~~ (2026-10-22) | ~~S đã vào X và Y; Q là Quản trị của X, không thuộc Y~~ | ~~Q mở nhật ký admin hệ thống~~ | ~~Q thấy các lần S vào X; không thấy mục nào của Y~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-99 | ~~Removed~~ (2026-10-22) | ~~Chưa admin hệ thống nào từng vào X; Q là Quản trị của X~~ | ~~Q mở nhật ký admin hệ thống~~ | ~~Thấy trạng thái trống cho biết chưa có admin hệ thống nào vào tổ chức~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-100 | ~~Removed~~ (2026-10-22) | ~~H là Phụ trách ở X, không phải Quản trị~~ | ~~H tìm cách mở nhật ký admin hệ thống, rồi gửi yêu cầu xem trực tiếp, không qua màn hình~~ | ~~Không có màn hình nhật ký cho H; yêu cầu trực tiếp bị từ chối vì không đủ quyền~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-101 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống; Hub có X đang hoạt động và Z bị vô hiệu, S không là thành viên của cả hai~~ | ~~S mở bộ chọn tổ chức~~ | ~~Có mục *Mọi tổ chức* liệt kê cả X và Z; Z gắn nhãn đã vô hiệu~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-102 | ~~Removed~~ (2026-10-22) | ~~Như trên~~ | ~~S chọn Z~~ | ~~S vào được Z và thấy dữ liệu của Z~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-103 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, đang làm việc ở X mà S không là thành viên~~ | ~~S mở bất kỳ màn hình nào của X~~ | ~~Màn hình hiện rõ S đang ở tư cách admin hệ thống~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-104 | ~~Removed~~ (2026-10-22) | ~~A là thành viên của X, không phải admin hệ thống~~ | ~~A mở bộ chọn tổ chức~~ | ~~Không có mục *Mọi tổ chức*; chỉ có các tổ chức A là thành viên (AC-05)~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-105 | FR-42 | S là admin hệ thống; X đang hoạt động, có sản phẩm P | S vô hiệu X, ghi lý do "Tổ chức yêu cầu tạm dừng" | X bị vô hiệu; widget của P báo tạm ngừng nhận feedback (BR-31) |
| PRD00-AC-106 | FR-42 | S là admin hệ thống; Z đang bị vô hiệu | S kích hoạt lại Z | Z hết trạng thái vô hiệu; danh sách tổ chức của S không còn đánh dấu Z đã vô hiệu |
| PRD00-AC-107 | FR-30, FR-12 | M là thành viên X không có quyền quản lý tổ chức của nền tảng (BR-52) | M gửi yêu cầu vô hiệu X trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; X vẫn hoạt động |
| PRD00-AC-108 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; P là sản phẩm của X~~ | ~~S tìm cách gán chính mình làm Phụ trách của P~~ | ~~Không có thao tác đó; gửi trực tiếp bị từ chối, S không thành Phụ trách của P~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-109 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; H là Phụ trách của P~~ | ~~S mở danh sách chọn người được giao cho feedback F của P~~ | ~~S không có trong danh sách; H có~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-110 | ~~Removed~~ (2026-10-22) | ~~Như trên~~ | ~~S giao F cho H~~ | ~~F được giao cho H; lịch sử ghi "<tên S> (Admin hệ thống)" là người giao~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-111 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống, không là thành viên X; M là thành viên X~~ | ~~S gán M làm Phụ trách của P~~ | ~~M là Phụ trách của P~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-112 | ~~Removed~~ (2026-10-22) | ~~X có đúng một Chủ sở hữu là O; S là admin hệ thống, không là thành viên X~~ | ~~S tìm cách gỡ O khỏi X hoặc bỏ vai trò Chủ sở hữu của O~~ | ~~Bị từ chối vì tổ chức phải còn ít nhất một Chủ sở hữu là thành viên; O vẫn là Chủ sở hữu~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-113 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống; F là feedback của người báo R, thành viên X~~ | ~~S viết một bình luận gửi người báo trên F~~ | ~~R thấy bình luận với tên "<tên S> (Admin hệ thống)"~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-114 | ~~Removed~~ (2026-10-22) | ~~S là admin hệ thống; G là feedback của khách K ở sản phẩm "App Bán hàng"~~ | ~~S viết một bình luận gửi người báo trên G~~ | ~~K thấy bình luận với tên "Đội hỗ trợ App Bán hàng"; không thấy tên S~~ — Bỏ vì câu hỏi 2 đổi sang mô hình admin hệ thống giới hạn. |
| PRD00-AC-115 | FR-25 | Q là Quản trị của X nhưng không có quyền quản lý thành viên | Q tìm cách gửi hoặc thu hồi lời mời | Không có thao tác nào; gửi trực tiếp bị từ chối |
| PRD00-AC-116 | FR-25, FR-52 | N có quyền quản lý thành viên ở X, không có quyền Quản trị FeedbackHub, không phải Chủ sở hữu | N gửi lời mời tới email của B với vai trò Member (không có quyền nào N không có) | Lời mời được gửi; B nhận email mời |
| PRD00-AC-117 | FR-25 | N đã gửi lời mời tới B, B chưa dùng | N thu hồi lời mời | Lời mời hết hiệu lực; B mở thì thấy như AC-59 |
| PRD00-AC-118 | FR-25 | Lời mời gửi tới b@congty.vn bị chuyển tiếp cho C | C mở lời mời và đăng ký bằng c@gmail.com | C không vào X; hệ thống báo lời mời không dành cho C |
| PRD00-AC-119 | FR-25 | Lời mời gửi tới b@congty.vn; C đang đăng nhập Hub bằng tài khoản có email khác | C mở lời mời | C không vào X; hệ thống báo lời mời không dành cho C |
| PRD00-AC-120 | FR-22, FR-51 | Người dùng đặt giao diện Hub sang tiếng Anh | Mở một feedback có loại và ưu tiên | Loại hiện là Bug, Feature hoặc Feedback; ưu tiên hiện là High, Medium hoặc Low |
| PRD00-AC-121 | FR-01, FR-21, FR-37 | S là admin hệ thống, không là thành viên của tổ chức X; F là feedback của X | S mở đường dẫn trực tiếp tới F | Hệ thống báo "không tìm thấy", giống hệt khi F không tồn tại; không hiện tiêu đề, tên sản phẩm hay bất kỳ nội dung nào của F |
| PRD00-AC-122 | FR-21, FR-12, FR-37 | S là admin hệ thống, không là thành viên X | S gửi yêu cầu xem danh sách thành viên của X trực tiếp, không qua màn hình | Hệ thống báo "không tìm thấy" (BR-45); không có tên thành viên nào của X được trả về |
| PRD00-AC-123 | FR-21, FR-12, FR-37 | S là admin hệ thống, không là thành viên X; F của X ở *Mới* | S gửi yêu cầu đổi trạng thái F trực tiếp, không qua màn hình | Hệ thống báo "không tìm thấy" như khi F không tồn tại; F vẫn ở *Mới*, lịch sử không có mục mới |
| PRD00-AC-124 | FR-44 | Hub có X (12 thành viên, đang hoạt động) và Z (đã vô hiệu); S là admin hệ thống, không là thành viên của cả hai | S mở danh sách tổ chức | Thấy X và Z, mỗi tổ chức với tên và số thành viên; Z được đánh dấu đã vô hiệu; không hiện tên thành viên, sản phẩm hay feedback nào |
| PRD00-AC-125 | FR-44 | X có 12 thành viên trong danh sách của S | Người có quyền quản lý thành viên gỡ một thành viên có vai trò Member khỏi X | Lần tải tiếp theo, danh sách của S hiện X có 11 thành viên |
| PRD00-AC-126 | FR-44, FR-12 | O là Chủ sở hữu của X, không phải admin hệ thống | O gửi yêu cầu xem danh sách mọi tổ chức trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; không tổ chức nào được liệt kê |
| PRD00-AC-127 | FR-44 | Hub chưa có tổ chức nào; S là admin hệ thống | S mở danh sách tổ chức | Thấy trạng thái trống cho biết Hub chưa có tổ chức nào |
| PRD00-AC-128 | FR-42 | Z bị vô hiệu; trước đó M là thành viên Z với vai trò Admin và là Phụ trách sản phẩm P của Z | S kích hoạt lại Z, rồi M chọn Z trong bộ chọn tổ chức | M vào được Z, vẫn có vai trò Admin và vẫn là Phụ trách của P |
| PRD00-AC-129 | FR-42 | Z bị vô hiệu; P thuộc Z | S kích hoạt lại Z, rồi một người báo mở widget của P | Widget nhận feedback như trước khi vô hiệu |
| PRD00-AC-130 | FR-42 | Z bị vô hiệu; khách K có link riêng của G thuộc Z, link còn hạn | S kích hoạt lại Z, rồi K mở link | K thấy G như trước khi vô hiệu |
| PRD00-AC-131 | FR-42, FR-12 | Z bị vô hiệu; O là Chủ sở hữu của Z, không phải admin hệ thống | O gửi yêu cầu kích hoạt lại Z trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; Z vẫn bị vô hiệu |
| PRD00-AC-132 | FR-45 | S là admin hệ thống; X thêm S làm thành viên với vai trò Member và gán S làm Phụ trách của P | S chuyển feedback F của P từ *Mới* sang *Đã tiếp nhận* | F ở *Đã tiếp nhận*; lịch sử ghi tên S như mọi thành viên, không kèm nhãn nào |
| PRD00-AC-133 | FR-45 | Như trên; F là feedback của người báo R, thành viên X | S viết một bình luận gửi người báo trên F | R thấy bình luận với tên thật của S như mọi người IT (BR-08), không kèm nhãn nào |
| PRD00-AC-134 | FR-45, FR-37 | Như trên; Q là sản phẩm khác của X, S không được gán vào Q | S mở đường dẫn trực tiếp tới một feedback của Q | Hệ thống báo "không tìm thấy"; quyền admin hệ thống không mở thêm gì |
| PRD00-AC-135 | FR-45, FR-12 | S là admin hệ thống, không là thành viên X | S gửi yêu cầu tự thêm mình làm thành viên X trực tiếp, không qua màn hình | Bị từ chối; S không thành thành viên X |
| PRD00-AC-136 | FR-21, FR-12, FR-37 | S là admin hệ thống, không là thành viên X; X có 20 sản phẩm | S gửi yêu cầu xem danh sách sản phẩm của X trực tiếp, không qua màn hình | Hệ thống báo "không tìm thấy" (BR-45); không có tên sản phẩm nào của X được trả về |
| PRD00-AC-137 | FR-46 | X đang hoạt động, có hai Chủ sở hữu O1 và O2; S là admin hệ thống | S vô hiệu X, ghi lý do "Tổ chức yêu cầu tạm dừng" | O1 và O2 mỗi người nhận một email báo X bị vô hiệu, trong email có lý do "Tổ chức yêu cầu tạm dừng" và người vô hiệu ghi là "admin hệ thống FeedbackHub", không có tên S |
| PRD00-AC-138 | FR-46 | X đang hoạt động; S là admin hệ thống | S chọn vô hiệu X mà không ghi lý do | Không vô hiệu được; hệ thống yêu cầu nhập lý do; X vẫn hoạt động, không email nào được gửi |
| PRD00-AC-139 | FR-46, FR-12 | X đang hoạt động; S là admin hệ thống | S gửi yêu cầu vô hiệu X trực tiếp, không qua màn hình, không kèm lý do | Bị từ chối; X vẫn hoạt động, không email nào được gửi |
| PRD00-AC-140 | FR-46 | Z bị vô hiệu, có hai Chủ sở hữu O1 và O2; S là admin hệ thống | S kích hoạt lại Z | O1 và O2 mỗi người nhận một email báo Z đã hoạt động lại |
| PRD00-AC-141 | FR-46 | X đang hoạt động, có hai Chủ sở hữu O1 và O2 | O1 vô hiệu X (BR-52), không ghi lý do | X bị vô hiệu; O2 nhận một email báo X bị vô hiệu, nêu tên O1; O1 không nhận email nào |
| PRD00-AC-142 | FR-26, FR-42 | F ở *Đã xử lý* được 5 ngày thì tổ chức X bị vô hiệu; X bị vô hiệu 3 ngày | S kích hoạt lại X, rồi Phụ trách mở F | F ở *Đóng*; lịch sử ghi người thực hiện việc tự đóng là hệ thống |
| PRD00-AC-143 | FR-26, FR-42 | F ở *Đã xử lý* được 2 ngày thì X bị vô hiệu; X bị vô hiệu 1 ngày rồi được kích hoạt lại; không ai mở lại F | Đến 1 giờ sau mốc 7×24 giờ kể từ lúc F sang *Đã xử lý* (4 ngày sau khi kích hoạt lại) | F đã ở *Đóng*; không có ngày nào được cộng thêm vì thời gian vô hiệu |
| PRD00-AC-144 | FR-25, FR-42 | Lời mời tới B gửi 3 ngày trước khi X bị vô hiệu, B chưa dùng; X bị vô hiệu 5 ngày rồi được kích hoạt lại | B mở lời mời | B không vào được X; hệ thống báo lời mời đã hết hạn (như AC-58) |
| PRD00-AC-145 | FR-42 | Khách K có link riêng của G thuộc Z; link hết hạn trong lúc Z bị vô hiệu | S kích hoạt lại Z, rồi K mở link | Trang báo link đã hết hạn như mọi link hết hạn (PRD04); không hiện nội dung G |
| PRD00-AC-146 | FR-46 | X đang hoạt động, có hai Chủ sở hữu O1 và O2; M có quyền quản lý tổ chức, không phải Chủ sở hữu | M vô hiệu X, không ghi lý do | X bị vô hiệu; O1 và O2 mỗi người nhận một email báo X bị vô hiệu, nêu tên M |
| PRD00-AC-147 | FR-46 | S là admin hệ thống, đồng thời là Chủ sở hữu của X cùng O2 | S vô hiệu X từ bên trong X mà không ghi lý do | Không vô hiệu được; hệ thống yêu cầu nhập lý do; X vẫn hoạt động, không email nào được gửi |
| PRD00-AC-148 | FR-46 | Như trên | S vô hiệu X với lý do "Khách hàng yêu cầu tạm dừng" | X bị vô hiệu; O2 nhận một email có lý do đó; S không nhận email nào báo X bị vô hiệu |
| PRD00-AC-149 | FR-55 | F của người báo R ở *Đã xử lý* được 5 ngày thì X bị vô hiệu; H là Phụ trách của sản phẩm | F tự sang *Đóng* trong lúc X bị vô hiệu | R và H không nhận thông báo nào (trong Hub, email, web push); lịch sử của F có mục tự đóng do hệ thống thực hiện |
| PRD00-AC-150 | FR-55 | Như trên | S kích hoạt lại X | R và H không nhận thông báo bù nào về việc F đã đóng; Chủ sở hữu của X nhận email báo X hoạt động lại (BR-54) |
| PRD00-AC-151 | FR-54, FR-25 | Lời mời tới B còn hạn; X bị vô hiệu | B mở lời mời | B không vào được X; hệ thống báo tổ chức đang tạm ngừng hoạt động |
| PRD00-AC-152 | FR-25 | Lời mời tới B gửi 2 ngày trước khi X bị vô hiệu; X bị vô hiệu 1 ngày rồi được kích hoạt lại | B mở lời mời ở ngày thứ 4 kể từ lúc gửi | B vào X với vai trò được mời |
| PRD00-AC-153 | FR-52 | N có vai trò tự tạo "Nhân sự" chỉ gồm quyền quản lý thành viên | N gửi lời mời tới email của B với vai trò Admin | Bị từ chối vì không đủ quyền (Admin có quyền Quản trị FeedbackHub mà N không có); không email nào được gửi |
| PRD00-AC-154 | FR-52 | Như trên | N gửi yêu cầu đổi vai trò tổ chức của chính mình thành Admin, trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; N giữ nguyên vai trò "Nhân sự" |
| PRD00-AC-155 | FR-52 | A có vai trò Admin ở X, không phải Chủ sở hữu; M là Member | A gán vai trò Chủ sở hữu cho M | Bị từ chối vì không đủ quyền; M không thành Chủ sở hữu |
| PRD00-AC-156 | FR-52 | R có quyền quản lý vai trò, không có quyền Quản trị FeedbackHub | R tạo vai trò tự tạo có quyền Quản trị FeedbackHub | Bị từ chối vì không đủ quyền; không vai trò nào được tạo |
| PRD00-AC-157 | FR-52 | O là Chủ sở hữu của X; M là Member | O gán vai trò Chủ sở hữu cho M | M là Chủ sở hữu và là Quản trị của X |
| PRD00-AC-158 | FR-52 | X có hai Chủ sở hữu O1 và O2; A có vai trò Admin, không phải Chủ sở hữu | A gỡ O2 khỏi X (BR-58 điểm 3) | Bị từ chối vì không đủ quyền; O2 vẫn là thành viên và Chủ sở hữu của X |
| PRD00-AC-159 | FR-23 | O là Chủ sở hữu của X; M có vai trò Admin | O tìm cách bỏ quyền Quản trị FeedbackHub khỏi vai trò Admin | Không bỏ được vì vai trò hệ thống không sửa được; M vẫn là Quản trị |
| PRD00-AC-160 | FR-56 | M có quyền quản lý file, không là Quản trị, không được gán sản phẩm P; F của P có ảnh chụp màn hình | M mở phần quản lý file | Không thấy ảnh hay file nào của F |
| PRD00-AC-161 | FR-56 | M có quyền xem nhật ký kiểm toán, không là Quản trị, không được gán P; H vừa đổi trạng thái và bình luận trên F của P | M mở nhật ký kiểm toán | M không thấy tiêu đề, nội dung hay bình luận nào của F |
| PRD00-AC-162 | FR-47 | Trong kỳ đo chỉ có một feedback F, gửi lúc 09:00; H chuyển F *Mới* → *Đang xử lý* lúc 12:00, về lại *Mới* lúc 13:00, sang *Đã tiếp nhận* lúc 14:00 | Phụ trách xem thời gian đến lần xử lý đầu tiên trên Tổng quan | Trung vị là 3 giờ |
| PRD00-AC-163 | FR-47 | F gửi 25 giờ trước, G gửi 23 giờ trước, cả hai vẫn ở *Mới* | Phụ trách mở danh sách cần chú ý | F có trong danh sách, G không có |
| PRD00-AC-164 | FR-47 | Trong kỳ đo có 10 feedback được gửi; 2 trong số đó từng vào *Cần thêm thông tin*, một đã rời trạng thái này | Tổng quan tính tỉ lệ từng vào *Cần thêm thông tin* | Kết quả là 20% |
| PRD00-AC-165 | FR-47 | Trong kỳ đo có 3 feedback được xử lý lần đầu sau 1 giờ, 2 giờ và 10 giờ kể từ lúc gửi | Tổng quan tính thời gian đến lần xử lý đầu tiên | Kết quả là 2 giờ (trung vị) |
| PRD00-AC-166 | FR-50 | F của sản phẩm P đang giao cho H, ở *Đang xử lý* | Quản trị Q đổi H thành Viewer của P | F về *Chưa giao*, vẫn ở *Đang xử lý*; lịch sử có mục người được giao H → *Chưa giao*, người thực hiện là Q, kèm ghi chú "do mất vai trò Phụ trách" |
| PRD00-AC-167 | FR-50 | Như trên | Quản trị Q bỏ gán H khỏi P | F về *Chưa giao*, vẫn ở *Đang xử lý*; lịch sử có mục người được giao H → *Chưa giao*, người thực hiện là Q, kèm ghi chú "do mất vai trò Phụ trách" |
| PRD00-AC-168 | FR-50 | F của P đang giao cho H; H2 cũng là Phụ trách của P | H giao F cho H2 | Người được giao của F là H2; lịch sử có mục H → H2 |
| PRD00-AC-169 | FR-51 | F đang có ưu tiên chưa đặt; H là Phụ trách của sản phẩm | H đặt ưu tiên *Cao* | F có ưu tiên *Cao*; lịch sử có mục chưa đặt → *Cao* |
| PRD00-AC-170 | FR-08, FR-16 | V là Viewer của P và đã gửi F ở P; F ở In review | V chọn *chưa đạt* với lý do "Vẫn lỗi" | F về *Đang xử lý*; lý do hiện thành bình luận gửi người báo |
| PRD00-AC-171 | FR-08, FR-15 | V đã gửi F ở P; F chuyển từ *Đã tiếp nhận* sang *Cần thêm thông tin* | V gửi một bình luận trên F | Bình luận được lưu; F tự về *Đã tiếp nhận* |
| PRD00-AC-172 | FR-08, FR-17 | V đã gửi F ở P; F ở *Đã xử lý* | V chọn *Mở lại* với lý do "Lỗi xuất hiện lại" | F về *Đang xử lý*; lịch sử ghi V là người mở lại |
| PRD00-AC-173 | FR-15 | H là Phụ trách của P và là người báo của F ở P; F chuyển từ *Đã tiếp nhận* sang *Cần thêm thông tin* | H gửi một bình luận gửi người báo trên F | F tự về *Đã tiếp nhận* |
| PRD00-AC-174 | FR-48 | Khách K để email khi gửi G1; G1 được đánh dấu trùng với gốc G | G sang *Từ chối* lần đầu | K nhận email báo feedback gốc đã có kết quả |
| PRD00-AC-175 | FR-48 | F của người báo R trùng với gốc G; R đã được báo khi G sang *Đã xử lý* | G được mở lại, rồi sang *Đã xử lý* lần nữa, rồi sang *Đóng* | R không nhận thêm thông báo nào theo BR-13 |
| PRD00-AC-176 | FR-37, FR-12 | R là người báo của F1; F2 do người khác gửi, R không thấy được F2 | R gửi yêu cầu bình luận trên F2 trực tiếp, không qua màn hình | Hệ thống báo "không tìm thấy" như khi F2 không tồn tại; F2 không có bình luận mới |
| PRD00-AC-177 | FR-25 | B đã có tài khoản Hub với email b@congty.vn; X gửi lời mời tới b@congty.vn với vai trò Member | B mở lời mời, đăng nhập rồi chấp nhận | B vào X với vai trò Member |
| PRD00-AC-178 | FR-25 | B chưa có tài khoản; X gửi lời mời tới b@congty.vn | B mở lời mời và đăng ký bằng Google với đúng b@congty.vn | B vào thẳng X với vai trò được mời |
| PRD00-AC-179 | FR-25 | N gửi lời mời L1 tới b@congty.vn, B chưa dùng | N gửi lời mời L2 tới cùng email, rồi B mở L1 | B không vào được X; hệ thống báo lời mời không còn hiệu lực |
| PRD00-AC-180 | FR-25 | M đã là thành viên X với email m@congty.vn | N gửi lời mời tới m@congty.vn | Không gửi được; hệ thống báo người này đã là thành viên; không email nào được gửi |
| PRD00-AC-181 | FR-25 | Lời mời tới B với vai trò tự tạo "Tester"; vai trò "Tester" bị xoá trước khi B dùng lời mời | B mở lời mời | B không vào được X; hệ thống báo lời mời không còn hiệu lực |
| PRD00-AC-182 | FR-25 | B chưa có tài khoản; X gửi lời mời tới b@congty.vn | B mở lời mời và đăng ký bằng b@congty.vn và mật khẩu | B vào thẳng X; email của B được coi là đã xác minh, B tạo được tổ chức mới (BR-23) mà không phải xác minh thêm |
| PRD00-AC-183 | FR-54 | A đang làm việc ở X và đang mở một feedback; X bị vô hiệu | A đổi trạng thái feedback đó | Hệ thống báo "tổ chức tạm ngừng"; feedback không đổi |
| PRD00-AC-184 | FR-54 | A chỉ là thành viên của X; X bị vô hiệu | A mở Hub | A thấy trang chào kèm lời giải thích tổ chức X đang tạm ngừng; không có hộp feedback hay "Feedback của tôi" |
| PRD00-AC-185 | FR-53 | M có quyền quản lý tổ chức ở X tên "Công ty A"; S là admin hệ thống | M đổi tên X thành "Công ty B" | Lần tải tiếp theo, mọi thành viên X thấy "Công ty B" trong bộ chọn tổ chức; danh sách tổ chức của S hiện "Công ty B" |
| PRD00-AC-186 | FR-53, FR-12 | M là thành viên X không có quyền quản lý tổ chức | M gửi yêu cầu đổi tên X trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; tên X không đổi |
| PRD00-AC-187 | FR-53 | M có quyền quản lý tổ chức ở X | M đổi tên X thành tên trống | Không đổi được; hệ thống yêu cầu nhập tên; tên X không đổi |
| PRD00-AC-188 | FR-27 | H là Phụ trách, F ở *Mới* | H chọn *Từ chối* với lý do dài đúng 500 ký tự | F ở *Từ chối*; lý do hiện đủ 500 ký tự trong bình luận gửi người báo |
| PRD00-AC-189 | FR-27 | Như trên | H chọn *Từ chối* với lý do dài 501 ký tự | Không chuyển được; hệ thống báo lý do vượt 500 ký tự; F vẫn ở *Mới* |
| PRD00-AC-190 | FR-12 | Q có vai trò Admin ở X, không được gán sản phẩm P, đang mở F của P | Chủ sở hữu đổi vai trò của Q thành Member, rồi Q đổi ưu tiên của F | Hệ thống báo "không tìm thấy"; ưu tiên của F không đổi; Q không phải đăng xuất |
| PRD00-AC-191 | FR-16, FR-10 | Khách K gửi G; G ở In review | K chọn *đạt* trên trang link riêng | G sang *Đã xử lý*; lịch sử ghi thay đổi |
| PRD00-AC-192 | FR-16, FR-10 | Như trên | K chọn *chưa đạt* với lý do "Vẫn không đăng nhập được" | G về *Đang xử lý*; lý do hiện thành bình luận gửi người báo |
| PRD00-AC-193 | FR-52 | O là Chủ sở hữu của X; A có quyền quản lý thành viên, không phải Chủ sở hữu | A tìm cách gỡ vai trò Chủ sở hữu khỏi O | Bị từ chối vì không đủ quyền; O vẫn là Chủ sở hữu và là Quản trị |
| PRD00-AC-194 | FR-52, FR-25 | A có vai trò Admin ở X, không phải Chủ sở hữu | A gửi lời mời tới email của B với vai trò Chủ sở hữu | Bị từ chối vì không đủ quyền; không email nào được gửi |
| PRD00-AC-195 | FR-56, FR-37 | M có quyền quản lý file, không là Quản trị, không được gán sản phẩm P; F của P có ảnh chụp màn hình | M mở đường dẫn trực tiếp tới ảnh của F | Hệ thống báo "không tìm thấy"; ảnh không hiện |
| PRD00-AC-196 | FR-25 | Như AC-179 | B mở L2 | B vào X với vai trò được mời |
| PRD00-AC-197 | FR-52 | N có vai trò tự tạo "Nhân sự" chỉ gồm quyền quản lý thành viên; Q có vai trò Admin ở X | N đổi vai trò tổ chức của Q từ Admin thành Member | Bị từ chối vì không đủ quyền (N không có quyền Quản trị FeedbackHub của vai trò Admin); Q vẫn có vai trò Admin và là Quản trị |
| PRD00-AC-198 | FR-52 | Vai trò tự tạo "Trưởng IT" của X có quyền Quản trị FeedbackHub; R có quyền quản lý vai trò, không có quyền Quản trị FeedbackHub | R sửa "Trưởng IT" để bỏ quyền Quản trị FeedbackHub | Bị từ chối vì không đủ quyền; "Trưởng IT" vẫn có quyền Quản trị FeedbackHub |
| PRD00-AC-199 | FR-54 | A là thành viên của X và Y, cả hai đang hoạt động; A đang làm việc ở X; X bị vô hiệu | A làm một thao tác trong X | A thấy "tổ chức tạm ngừng", rồi được đưa về ngữ cảnh cá nhân: trang chào kèm bộ chọn tổ chức, chọn được Y, X làm mờ kèm nhãn tạm ngừng |
| PRD00-AC-200 | FR-54 | A chỉ là thành viên của X; A đang làm việc ở X; X bị vô hiệu | A làm một thao tác trong X | A thấy "tổ chức tạm ngừng", rồi thấy trang chào kèm lời giải thích X đang tạm ngừng; không có tổ chức nào chọn được |
| PRD00-AC-201 | FR-31 | A là thành viên của X và Y, cả hai đang hoạt động | A chuyển về ngữ cảnh cá nhân | A thấy trang chào kèm bộ chọn tổ chức có X và Y; không có hộp feedback hay "Feedback của tôi" |
| PRD00-AC-202 | FR-18 | F và G1 thuộc sản phẩm P; G1 ở *Trùng lặp* với gốc G; H là Phụ trách của P | H đánh dấu F là *Trùng lặp* | Danh sách chọn feedback gốc có G, không có G1 |
| PRD00-AC-203 | FR-18, FR-12 | Như trên | H gửi yêu cầu đánh dấu F trùng với G1 trực tiếp, không qua màn hình | Bị từ chối; F không đổi trạng thái, lịch sử không có mục mới |
| PRD00-AC-204 | FR-18 | F của người báo R là bản trùng của gốc G; G2 cùng sản phẩm, ở *Đang xử lý*; H là Phụ trách | H đánh dấu G là *Trùng lặp* với gốc G2 | Feedback gốc của F hiện là G2, không còn là G |
| PRD00-AC-205 | FR-48 | Như AC-204, sau khi F đã chuyển sang G2 | G2 lần đầu sang *Đã xử lý* | R được thông báo |
| PRD00-AC-206 | FR-48 | F của người báo R là bản trùng của gốc G, R chưa được báo theo BR-13; G2 cùng sản phẩm, đang ở *Đóng* | H đánh dấu G là *Trùng lặp* với gốc G2 | R được thông báo ngay, với kết quả *Đóng* của G2 |
| PRD00-AC-207 | FR-48 | F của người báo R là bản trùng của gốc G; R đã được báo khi G sang *Đã xử lý*; G sau đó được mở lại | H đánh dấu G là *Trùng lặp* với gốc G2, rồi G2 sang *Đã xử lý* | R không nhận thêm thông báo nào theo BR-13 |
| PRD00-AC-208 | FR-48 | Gốc G đang ở *Từ chối*; F của người báo R ở *Mới*, cùng sản phẩm | H đánh dấu F là *Trùng lặp* với gốc G | R được thông báo ngay, với kết quả *Từ chối* của G |
| PRD00-AC-209 | FR-48 | Như AC-208, R đã được báo | G được mở lại, rồi sang *Đã xử lý* | R không nhận thêm thông báo nào theo BR-13 |
| PRD00-AC-210 | FR-15 | H là Phụ trách của P và là người báo của F ở P; F chuyển từ *Đã tiếp nhận* sang *Cần thêm thông tin* | H viết một bình luận nội bộ trên F | F vẫn ở *Cần thêm thông tin* |
| PRD00-AC-211 | FR-15 | Q là Quản trị của X, không là Phụ trách của P, và là người báo của F ở P; F chuyển từ *Đã tiếp nhận* sang *Cần thêm thông tin* | Q gửi một bình luận gửi người báo trên F | F tự về *Đã tiếp nhận* |
| PRD00-AC-212 | FR-29 | M là người được giao của F2 ở P; N có quyền quản lý thành viên ở X | N gỡ M khỏi X | Lịch sử của F2 có mục người được giao M → *Chưa giao*, người thực hiện là N, kèm ghi chú "do rời tổ chức" |
| PRD00-AC-213 | FR-57, FR-10 | Khách K đã viết một bình luận trên G qua link riêng; G ở *Đang xử lý* | K sửa bình luận đó trên trang link riêng | Bình luận hiện nội dung mới, cho cả K và Phụ trách của sản phẩm |
| PRD00-AC-214 | FR-57, FR-12 | Người báo R đã viết một bình luận trên F | R tìm cách xoá bình luận đó, rồi gửi yêu cầu xoá trực tiếp, không qua màn hình | Không có thao tác xoá; yêu cầu trực tiếp bị từ chối vì không đủ quyền; bình luận vẫn còn |
| PRD00-AC-215 | FR-57, FR-12 | Phụ trách H đã viết một bình luận gửi người báo trên F | H gửi yêu cầu xoá bình luận đó trực tiếp, không qua màn hình | Bị từ chối vì không đủ quyền; bình luận vẫn còn |
| PRD00-AC-216 | FR-57 | H chuyển F sang *Từ chối* với lý do "Đúng theo thiết kế" | H tìm cách sửa bình luận lưu lý do đó | Không có thao tác sửa; gửi trực tiếp bị từ chối; nội dung bình luận không đổi |
| PRD00-AC-217 | FR-57 | Như trên; Q là Quản trị | Q tìm cách xoá bình luận lưu lý do đó | Không có thao tác xoá; gửi trực tiếp bị từ chối; bình luận vẫn còn |
| PRD00-AC-218 | FR-57 | Phụ trách H đã viết một bình luận gửi người báo trên F của người báo R; Q là Quản trị | Q xoá bình luận đó | Bình luận không còn trên F, với cả H và R |
| PRD00-AC-219 | FR-58 | F của người báo R thuộc sản phẩm P; Q là Quản trị | Q xoá F | F không còn trong hộp feedback của Phụ trách P và không còn trong "Feedback của tôi" của R |
| PRD00-AC-220 | FR-58, FR-37 | Như trên, F đã bị xoá | R mở đường dẫn cũ tới F | Hệ thống báo "không tìm thấy", giống hệt khi F không tồn tại |
| PRD00-AC-221 | FR-58 | Như AC-219 | Q xoá F | R không nhận thông báo nào về việc xoá |
| PRD00-AC-222 | FR-58, FR-34 | `FB-130` là feedback mới nhất của X; Q là Quản trị | Q xoá `FB-130`, rồi có feedback mới ở X | Feedback mới có mã `FB-131`; `FB-130` không được cấp lại |
| PRD00-AC-223 | FR-58, FR-12 | H là Phụ trách của P; F thuộc P | H tìm cách xoá F, rồi gửi yêu cầu xoá trực tiếp, không qua màn hình | Không có thao tác xoá; yêu cầu trực tiếp bị từ chối vì không đủ quyền; F vẫn còn |
| PRD00-AC-224 | FR-46 | S là admin hệ thống, đồng thời là Chủ sở hữu của Z cùng O2; Z đang bị vô hiệu | S kích hoạt lại Z | O2 nhận một email báo Z đã hoạt động lại; S không nhận email nào về việc này |
| PRD00-AC-225 | FR-53 | X tên "Công ty A", định danh ngắn `cong-ty-a`; M có quyền quản lý tổ chức | M đổi tên X thành "Công ty B" | Tên X là "Công ty B"; định danh ngắn vẫn là `cong-ty-a` |
| PRD00-AC-226 | FR-53, FR-12 | O là Chủ sở hữu của X | O gửi yêu cầu đổi định danh ngắn của X trực tiếp, không qua màn hình | Bị từ chối; định danh ngắn không đổi |
| PRD00-AC-227 | FR-21 | Hub có X đang hoạt động và Z đang bị vô hiệu; S là admin hệ thống | S tạo một đợt khảo sát và mở danh sách chọn tổ chức nhận | Danh sách có X, không có Z |
| PRD00-AC-228 | FR-21, FR-12 | Như trên | S gửi yêu cầu tạo đợt khảo sát chọn Z trực tiếp, không qua màn hình | Bị từ chối; không thành viên nào của Z nhận lời mời khảo sát |
| PRD00-AC-229 | FR-30 | Một đợt khảo sát đã mở cho X; M là thành viên X; sau đó X bị vô hiệu | M mở lời mời khảo sát và trả lời | Câu trả lời của M được ghi nhận như với tổ chức đang hoạt động |

## 8. Trạng thái và trường hợp biên

- **Không có tổ chức**: tài khoản chưa thuộc tổ chức nào (ví dụ chỉ từng gửi với tư cách khách) mở Hub → trang chào (BR-32, AC-74). Tài khoản mà mọi tổ chức đều đang bị vô hiệu → trang chào kèm lời giải thích (BR-64, AC-184). Ngữ cảnh cá nhân luôn hiện trang chào, kèm bộ chọn tổ chức khi có tổ chức đang hoạt động (BR-68, AC-201).
- **Thành viên bị gỡ khỏi tổ chức** khi đang là người báo, Phụ trách hoặc người được giao → BR-30 (AC-69, AC-70); lịch sử ghi người gỡ, kèm ghi chú "do rời tổ chức" (AC-212). Nếu họ là Phụ trách duy nhất của một sản phẩm, sản phẩm đó trở thành chưa có Phụ trách (BR-35).
- **Tổ chức bị vô hiệu** (nền tảng đã có thao tác này): widget tạm ngừng, link riêng hết hiệu lực, không ai chuyển vào được, kể cả Chủ sở hữu và admin hệ thống (BR-31). Admin hệ thống phải ghi lý do khi vô hiệu, Chủ sở hữu được báo qua email (BR-54, AC-137–141). Chỉ admin hệ thống kích hoạt lại được; kích hoạt lại đưa tổ chức về như trước (BR-49, AC-106, AC-128–131). Các mốc thời gian (tự đóng 7 ngày, hạn lời mời, hạn link riêng) vẫn chạy trong lúc vô hiệu; kích hoạt lại chỉ khôi phục những gì còn hạn (BR-55, AC-142–145). Dù ai vô hiệu, các Chủ sở hữu khác người thực hiện được báo; admin hệ thống luôn phải ghi lý do, kể cả khi đồng thời là Chủ sở hữu (BR-54, AC-146–148). Trong lúc vô hiệu không thông báo nào của tổ chức được gửi, không gửi bù khi kích hoạt lại (BR-56, AC-149, AC-150). Lời mời còn hạn không dùng được trong lúc vô hiệu nhưng không bị huỷ (BR-57, AC-151, AC-152). Người đang làm việc trong tổ chức thấy "tổ chức tạm ngừng" ở thao tác tiếp theo; tổ chức nằm mờ trong bộ chọn (BR-64, AC-73, AC-183); sau đó Hub đưa họ về ngữ cảnh cá nhân (BR-68, AC-199, AC-200). Admin hệ thống vô hiệu mà không là thành viên thì email ghi "admin hệ thống FeedbackHub", không nêu tên; admin hệ thống đồng thời là Chủ sở hữu tự kích hoạt lại thì không nhận email kích hoạt lại (BR-54, AC-137, AC-224). Khách không xin được link riêng mới (PRD04); đợt khảo sát gửi "mọi tổ chức" bỏ qua tổ chức này (PRD07); khi tạo đợt không chọn đích danh được tổ chức đang bị vô hiệu, còn tổ chức bị vô hiệu sau khi đợt đã mở thì thành viên vẫn trả lời được (BR-72, AC-227–229).
- **Quyền bị thu hồi** (bỏ gán sản phẩm, đổi vai trò tổ chức, sửa vai trò tự tạo) có hiệu lực ngay từ thao tác tiếp theo của người đó; không cần đăng xuất (BR-67, AC-12, AC-190). Người được giao mất vai trò Phụ trách → feedback về *Chưa giao*, lịch sử ghi người làm thao tác gây ra việc đó kèm ghi chú "do mất vai trò Phụ trách" (BR-61, AC-166, AC-167).
- **Leo thang quyền**: không ai tự thành Chủ sở hữu, tự nâng quyền hay cấp quyền mình không có, kể cả qua lời mời hay vai trò tự tạo (BR-58, AC-55, AC-153–157, AC-193, AC-194); việc gỡ cũng theo cùng giới hạn: gỡ Chủ sở hữu, gỡ vai trò hay bỏ quyền mình không có (BR-58 điểm 3, AC-158, AC-197, AC-198). Vai trò hệ thống không sửa, không xoá được (BR-60, AC-159).
- **Sản phẩm không có Phụ trách**: không ai được giao được; feedback mới báo cho Quản trị của tổ chức (BR-35).
- **Một người là người báo và cũng là Phụ trách** của cùng sản phẩm: được làm mọi thao tác của Phụ trách trên feedback mình gửi; ở "Feedback của tôi" vẫn thấy feedback đó; bình luận gửi người báo của họ trên feedback đó tính là trả lời của người báo, bình luận nội bộ thì không (BR-12, AC-173, AC-210). Người báo là Quản trị được tính như người báo là Phụ trách (AC-211).
- **Một người là người báo và là Viewer** của cùng sản phẩm: trên feedback mình gửi có đủ quyền người báo (BR-62, AC-170–172).
- **Khách không để email**: không chuyển được feedback của họ sang *Cần thêm thông tin* (BR-43).
- **Người đã đăng nhập Hub, không thuộc tổ chức sở hữu sản phẩm, và sản phẩm tắt chế độ khách**: widget không cho gửi (BR-46).
- **Lời mời hết hạn hoặc đã thu hồi**: không đưa ai vào tổ chức (BR-24, AC-58, AC-59). Mời lại, email đã là thành viên, vai trò bị xoá, email đã có tài khoản, đăng ký bằng Google/Microsoft: BR-63, AC-177–182.
- **Lời mời bị chuyển tiếp** hoặc mở bởi tài khoản có email khác email được mời: không đưa người đó vào tổ chức, báo lời mời không dành cho họ (BR-51, AC-118, AC-119).
- **Feedback ở *Đã xử lý* bị mở lại rồi quay về *Đã xử lý***: 7 ngày tự đóng tính lại từ lần vào *Đã xử lý* gần nhất (BR-27). Tự đóng xảy ra chậm nhất 1 giờ sau mốc 7×24 giờ (AC-63, AC-143).
- **Lý do dài**: mọi lý do tối đa 500 ký tự (BR-66, AC-188, AC-189).
- **Admin hệ thống và tổ chức mình không là thành viên**: không vào được, không thấy gì bên trong; mở hay thao tác trên feedback, sản phẩm, danh sách thành viên thì thấy "không tìm thấy" (BR-21, BR-45, AC-121–123, AC-136).
- **Admin hệ thống được tổ chức thêm làm thành viên**: làm việc thuần tuý như thành viên đó, theo vai trò được gán, không nhãn, không nhật ký riêng (BR-53, AC-132–134). Quyền cấp hệ thống của họ (BR-21) không phụ thuộc tổ chức đang làm việc.
- **20 sản phẩm** trở lên mỗi tổ chức: bộ chọn sản phẩm, hộp feedback và phân quyền phải dùng tốt (theo canvas đã duyệt). Chưa có giới hạn tối đa nào được quyết.
- **Sửa đồng thời** (hai Phụ trách đổi trạng thái cùng lúc): thuộc PRD03.
- **Feedback gốc bị chuyển khỏi *Đã xử lý*** sau khi đã báo cho các bản trùng: không báo lại (BR-13, AC-175); hiển thị trên bản trùng thuộc PRD03. Gốc đã có kết quả từ trước khi gắn bản trùng: báo ngay lúc gắn, một lần (BR-13, AC-208, AC-209). Gốc tự bị đánh dấu trùng: các bản trùng của nó chuyển sang gốc mới (BR-69, AC-204–207); feedback ở *Trùng lặp* không làm gốc được (BR-37, AC-202, AC-203).
- **Xoá feedback, bình luận; lưu trữ sản phẩm**: ai được làm ghi ở ma trận quyền; chi tiết thuộc PRD03 (feedback, bình luận, nhãn) và PRD01 (sản phẩm). Tác giả sửa được bình luận của mình, kể cả khách; không tác giả nào tự xoá được; bình luận lưu lý do không ai sửa, xoá được (BR-70, AC-213–218). Xoá feedback là xoá hẳn, kể cả lịch sử của nó (ngoại lệ của BR-14); mã không dùng lại; người báo không được báo; việc xoá được ghi lại ngoài feedback (BR-71, AC-219–223). Xoá feedback đang là gốc của bản trùng: câu hỏi mở 72.
- **Đổi tên tổ chức**: định danh ngắn không đổi theo, không ai sửa được (BR-65, AC-225, AC-226).

## 9. Dữ liệu (mức nghiệp vụ)

| Trường | Ý nghĩa | Bắt buộc | Giới hạn | Ai được sửa |
|---|---|---|---|---|
| Tên tổ chức | Tên hiển thị của tổ chức; admin hệ thống thấy trong danh sách tổ chức (FR-44) | Có | Không để trống (nền tảng đã có) | Người tạo tổ chức khi tạo; sau đó người có quyền quản lý tổ chức đổi được (BR-65; nền tảng chưa có thao tác đổi tên, PRD này thêm) |
| Định danh ngắn của tổ chức | Mã viết thường, duy nhất toàn Hub | Có | Duy nhất (nền tảng đã có); không bao giờ đổi, kể cả khi đổi tên tổ chức (BR-65) | Người tạo tổ chức, khi tạo; sau đó không ai |
| Vai trò tổ chức của thành viên | Một hoặc nhiều vai trò tổ chức | Có, ít nhất một | Tổ chức luôn còn ít nhất một Chủ sở hữu (nền tảng đã có); Chủ sở hữu và Admin luôn có quyền Quản trị FeedbackHub (BR-60); chỉ Chủ sở hữu gán, gỡ vai trò Chủ sở hữu; không ai gán vai trò có quyền mình không có (BR-58) | Người có quyền quản lý thành viên (BR-19), trong giới hạn BR-58 |
| Vai trò tự tạo | Tên và các quyền chọn từ danh mục quyền cố định | Có tên | Chỉ đưa vào được quyền người tạo, người sửa có (BR-58); vai trò hệ thống không sửa, không xoá được (BR-60) | Người có quyền quản lý vai trò (BR-60) |
| Số thành viên của tổ chức | Số thành viên hiện tại, hiện trong danh sách tổ chức của admin hệ thống (FR-44) | Tự tính | Không kèm tên thành viên | Không ai |
| Trạng thái tổ chức | Đang hoạt động hoặc đã vô hiệu | Có | Mặc định đang hoạt động | Vô hiệu: người có quyền quản lý tổ chức (BR-52), admin hệ thống; kích hoạt lại: chỉ admin hệ thống (BR-49) |
| Lý do vô hiệu tổ chức | Lý do ghi khi vô hiệu một tổ chức; có trong email gửi các Chủ sở hữu khác người thực hiện (BR-54) | Có khi người vô hiệu là admin hệ thống (kể cả khi đồng thời là Chủ sở hữu); không bắt buộc với người khác | Tối đa 500 ký tự (BR-66) | Người vô hiệu, khi vô hiệu |
| Lời mời | Email được mời, vai trò tổ chức, thời điểm gửi | Có email và vai trò | Hết hạn sau 7 ngày, kể cả khi tổ chức bị vô hiệu (BR-55); thu hồi được trước khi dùng; chỉ tài khoản có đúng email được mời dùng được (BR-51); mỗi email chỉ có một lời mời còn hiệu lực, không mời được email đã là thành viên, hết hiệu lực khi vai trò bị xoá (BR-63); vai trò theo BR-58 | Người có quyền quản lý thành viên (BR-19) |
| Vai trò theo sản phẩm | Phụ trách hoặc Viewer, cho một thành viên ở một sản phẩm | Không | Tối đa một vai trò mỗi người mỗi sản phẩm | Quản trị |
| Người được giao | Một Phụ trách của sản phẩm, hoặc *Chưa giao* | Không | Phải là Phụ trách của sản phẩm (BR-05); về *Chưa giao* khi người đó mất vai trò Phụ trách (BR-61) hoặc rời tổ chức (BR-30) | Phụ trách, Quản trị |
| Mã feedback | `FB-<số>` | Tự sinh | Tăng dần, không trùng trong tổ chức; mã của feedback đã xoá không dùng lại (BR-42, BR-71) | Không ai |
| Loại | Lỗi / Tính năng / Góp ý | Có | Đúng ba giá trị | Người báo khi gửi; Phụ trách, Quản trị sau đó |
| Ưu tiên | Cao / Trung bình / Thấp | Không | Mặc định chưa đặt | Phụ trách, Quản trị |
| Trạng thái feedback | Một trong chín trạng thái §3.4 | Có | Mặc định *Mới* khi gửi | Phụ trách, Quản trị; người báo và khách chỉ qua hai thao tác BR-09; tự động theo BR-12, BR-27 |
| Lý do từ chối | Lý do khi chuyển sang *Từ chối*, hiện thành bình luận gửi người báo | Có khi chuyển sang *Từ chối* | Không để trống, tối đa 500 ký tự (BR-66); sau khi lưu không sửa, không xoá được (BR-70) | Phụ trách, Quản trị, khi chuyển trạng thái |
| Lý do *chưa đạt* | Lý do khi kết thúc In review với *chưa đạt*, hiện thành bình luận gửi người báo | Có khi chọn *chưa đạt* | Không để trống, tối đa 500 ký tự (BR-66); sau khi lưu không sửa, không xoá được (BR-70) | Người kết thúc In review (FR-16), khi kết thúc |
| Lý do mở lại | Lý do người báo hoặc khách mở lại, hiện thành bình luận gửi người báo | Có khi mở lại | Một câu ngắn, không để trống, tối đa 500 ký tự (BR-66); sau khi lưu không sửa, không xoá được (BR-70) | Người báo, khách, khi mở lại |
| Bình luận | Bình luận nội bộ hoặc bình luận gửi người báo trên một feedback | Không | Bình luận lưu lý do theo ba dòng trên; chi tiết (thời hạn sửa, dấu "đã sửa"): PRD03 | Sửa: tác giả, kể cả khách qua link riêng; xoá: chỉ Quản trị (BR-70) |
| Feedback gốc | Feedback mà bản trùng gắn vào | Có khi trạng thái là *Trùng lặp* | Cùng sản phẩm với bản trùng, không đang ở *Trùng lặp* (BR-37); đổi sang gốc mới khi gốc cũ bị đánh dấu trùng (BR-69) | Phụ trách, Quản trị |
| Email, điện thoại của khách | Thông tin liên hệ khách để lại | Không | Danh sách luôn che; chi tiết theo BR-38 | Khách khi gửi |
| Mục lịch sử | Ai, lúc nào, trường nào, từ → đến; có thể kèm ghi chú lý do tự động (ví dụ "do mất vai trò Phụ trách", BR-61) | Tự sinh | Không sửa, không xoá; chỉ mất cùng feedback khi Quản trị xoá feedback đó (BR-14, BR-71) | Không ai |

## 10. Thông báo và sự kiện

Ai nhận gì qua kênh nào được định nghĩa ở PRD05. PRD này chỉ định nghĩa các sự kiện đổi trạng thái (§3.4) và giữ các ngoại lệ đã quyết sau, để chúng nằm cạnh quy tắc gốc. Quy tắc chung: trong lúc tổ chức bị vô hiệu, không thông báo nào của tổ chức được gửi, trừ email cho Chủ sở hữu theo BR-54, và không gửi bù khi kích hoạt lại (BR-56).

| Sự kiện | Ai nhận | Kênh | Ghi chú |
|---|---|---|---|
| Feedback gốc lần đầu sang *Đã xử lý*, *Đóng* hoặc *Từ chối*; hoặc bản trùng được gắn vào (kể cả chuyển sang, BR-69) một gốc đã ở một trong ba trạng thái đó | Người báo của bản trùng, kể cả khách đã để email | Theo PRD05 (khách: email) | BR-13, BR-69; mỗi bản trùng chỉ báo một lần |
| Feedback mới ở sản phẩm chưa có Phụ trách | Quản trị của tổ chức | Theo PRD05 | BR-35, cho đến khi sản phẩm có Phụ trách |
| Lời mời vào tổ chức | Email được mời | Email | BR-24; nội dung email: PRD05 |
| Tổ chức bị vô hiệu (dù ai vô hiệu) | Mọi Chủ sở hữu của tổ chức, trừ người thực hiện | Email | BR-54; email có lý do nếu có, nêu tên người vô hiệu khi người đó là thành viên; admin hệ thống không là thành viên thì ghi "admin hệ thống FeedbackHub", không nêu tên; nội dung email: PRD05 |
| Admin hệ thống kích hoạt lại tổ chức | Mọi Chủ sở hữu của tổ chức, trừ người thực hiện | Email | BR-54; nội dung email: PRD05 |
| Quản trị xoá feedback | Người báo **không** được báo | — | BR-71 |

## 11. Yêu cầu phi chức năng

- **Tách biệt dữ liệu**: không màn hình, email, file xuất hay thông báo nào hiện dữ liệu của tổ chức cho người ngoài tổ chức, trừ ngoại lệ khách qua link riêng trong ma trận quyền. Admin hệ thống không phải ngoại lệ: về một tổ chức, chỉ thấy tên, số thành viên, dấu hiệu đã vô hiệu và kết quả khảo sát theo PRD07 (BR-21, BR-22).
- **Vai trò cấp hệ thống không vượt quyền vào tổ chức**: admin hệ thống là vai trò cấp hệ thống mới so với nền tảng hiện có, chỉ cấp được ngoài Hub (BR-20), và không mở đường nào vào dữ liệu bên trong tổ chức (BR-21, BR-22). Đây vẫn là thay đổi bảo mật (spec khảo sát §3), nên thiết kế kỹ thuật phải qua review bảo mật trước khi duyệt.
- **Kiểm tra quyền không phụ thuộc giao diện**: ẩn nút trên màn hình không thay cho việc từ chối thao tác; mọi thao tác bị chặn trong ma trận đều bị từ chối khi gọi trực tiếp (FR-12).
- **Không hiện nội dung khi không có quyền**: người không được thấy một feedback, sản phẩm hay dữ liệu khác bên trong tổ chức mà mở hoặc thao tác trên nó không thấy bất kỳ nội dung nào của nó, và thấy "không tìm thấy" giống như khi nó không tồn tại (BR-45). Thiết kế kỹ thuật cần review bảo mật xác nhận cách làm.
- **Chống leo thang quyền**: không ai tự nâng quyền, tự thành Chủ sở hữu hay cấp quyền mình không có (BR-58); các quyền nền tảng khác không mở đường tới feedback (BR-59). Nền tảng hiện cho người có quyền quản lý thành viên gán mọi vai trò, kể cả Chủ sở hữu, kể cả cho chính mình, nên đây là thay đổi bảo mật cần review bảo mật ở thiết kế kỹ thuật.
- **Quyền đổi có hiệu lực ngay**: mọi thay đổi quyền có hiệu lực từ thao tác tiếp theo của người bị đổi (BR-67).
- **Ngôn ngữ**: giao diện có tiếng Việt và tiếng Anh; tên trạng thái, loại, ưu tiên, vai trò dịch thống nhất ở mọi nơi (BR-41). Email mặc định tiếng Việt theo quyết định nền tảng 2026-08-04.
- **Truy vết**: mọi thay đổi trên feedback có lịch sử không sửa được (BR-14), kể cả thay đổi tự động (BR-27); bình luận lưu lý do đổi trạng thái không sửa, không xoá được (BR-70). Ngoại lệ duy nhất là Quản trị xoá hẳn một feedback: lịch sử mất cùng feedback, còn việc xoá được ghi lại ở ngoài feedback (BR-71).
- **Quy mô**: một tổ chức có từ 20 sản phẩm trở lên vẫn dùng tốt (canvas đã duyệt).

## 12. Phụ thuộc

- **Nền tảng có sẵn**:
  - Đăng ký bằng email (bắt buộc xác minh email), đăng nhập Google và Microsoft, quản lý phiên đăng nhập.
  - Tổ chức: tạo tổ chức (người tạo thành Chủ sở hữu), vô hiệu tổ chức, chuyển tổ chức đang làm việc, quay về ngữ cảnh cá nhân không tổ chức (§3.1; điều người dùng thấy ở đó: trang chào, kèm bộ chọn tổ chức khi có tổ chức đang hoạt động, BR-68). Định danh ngắn của tổ chức đặt khi tạo; PRD này chốt nó không bao giờ đổi (BR-65). Nền tảng chưa có thao tác kích hoạt lại tổ chức; PRD này thêm cho admin hệ thống (FR-42). Nền tảng chưa có thao tác đổi tên tổ chức; PRD này thêm (FR-53).
  - Thành viên: thêm thành viên bằng email của một tài khoản **đã đăng ký**, đổi vai trò, gỡ thành viên; luôn còn ít nhất một Chủ sở hữu. Hiện người có quyền quản lý thành viên gán được mọi vai trò, kể cả Chủ sở hữu, kể cả cho chính mình; PRD này thêm giới hạn chống leo thang quyền (FR-52, BR-58). PRD này thêm lời mời qua email cho cả người chưa có và đã có tài khoản (FR-25, BR-63).
  - Vai trò tổ chức tự tạo từ danh mục quyền cố định (quyền quản lý tổ chức, quản lý thành viên, quản lý vai trò, quản lý khoá API, xem nhật ký kiểm toán, quản lý file, quản lý cài đặt); ba vai trò hệ thống Owner (mọi quyền), Admin (hiện chỉ có quyền quản lý thành viên), Member (không có quyền nào); vai trò hệ thống không sửa, không xoá được. PRD này thêm quyền Quản trị FeedbackHub vào danh mục, luôn có ở Admin (FR-23, BR-60), và chốt rằng bốn quyền khoá API, nhật ký kiểm toán, file, cài đặt không mở đường tới feedback (FR-56, BR-59).
  - Hiện không có admin cấp hệ thống và không có đường vượt quyền giữa các tổ chức. PRD này thêm vai trò admin hệ thống (BR-20, BR-21) nhưng giữ nguyên việc không có đường vượt quyền vào dữ liệu bên trong tổ chức.
  - Thông báo trong Hub, email, web push.
- **PRD phụ thuộc vào PRD này**: PRD01–PRD07. Ràng buộc chéo đã quyết, các PRD đó phải ghi lại:
  - PRD01: sản phẩm chỉ lưu trữ được, không xoá hẳn; chỉ Quản trị lưu trữ (§3.3).
  - PRD03: chỉ Quản trị xoá feedback, xoá hẳn, mã không dùng lại, người báo không được báo, việc xoá ghi lại ngoài feedback (BR-71; nội dung bản ghi và ai xem được do PRD03 quyết); tác giả sửa bình luận của mình, kể cả khách; chỉ Quản trị xoá bình luận; bình luận lưu lý do không ai sửa, xoá được (BR-70; thời hạn sửa, dấu "đã sửa" do PRD03 quyết); Quản trị và Phụ trách quản lý nhãn (§3.3); thông báo bản trùng (BR-13); gốc không ở *Trùng lặp*, gốc bị đánh dấu trùng thì các bản trùng chuyển sang gốc mới (BR-37, BR-69); lịch sử ghi người thực hiện và ghi chú khi feedback tự về *Chưa giao* (BR-30, BR-61).
  - PRD04: khi tổ chức bị vô hiệu, khách không xin được link riêng mới (BR-31); khách sửa được bình luận của mình qua link riêng (BR-70).
  - PRD05: tạm dừng mọi thông báo của tổ chức bị vô hiệu (BR-56); email vô hiệu (admin hệ thống không là thành viên ghi "admin hệ thống FeedbackHub"), kích hoạt lại (không gửi cho người thực hiện) (BR-54); thông báo bản trùng cho khách qua email, kể cả khi gắn vào gốc đã có kết quả (BR-13, BR-69); không báo người báo khi feedback bị xoá (BR-71).
  - PRD06: định nghĩa chỉ số (FR-47, BR-17, BR-40, BR-44).
  - PRD07: đợt khảo sát gửi "mọi tổ chức" không gồm tổ chức đang bị vô hiệu (BR-31); khi tạo đợt không chọn được tổ chức đang bị vô hiệu, tổ chức bị vô hiệu sau khi đợt đã mở thì thành viên vẫn trả lời được (BR-72); lời mời khảo sát không thuộc BR-56; chỉ admin hệ thống xem kết quả (FR-49).
- **Dịch vụ bên ngoài**: không có mới.

## 13. Câu hỏi mở

| # | Câu hỏi | Đề xuất trả lời | Vì sao quan trọng | Trạng thái |
|---|---|---|---|---|
| 72 | Quản trị xoá một feedback đang là gốc của bản trùng (còn bản trùng gắn vào nó). Các bản trùng đó ra sao? | Không cho xoá khi feedback còn bản trùng gắn vào: Hub báo số bản trùng và yêu cầu chuyển chúng sang gốc khác hoặc đổi trạng thái của chúng trước, rồi mới xoá được. Cùng tinh thần với câu 63 (bản trùng luôn có một gốc còn tồn tại). | Xoá hẳn (câu 68) để lại các feedback ở *Trùng lặp* không có gốc, trái FR-18 và BR-37; người báo của chúng không bao giờ được báo (BR-13) và cũng không bình luận được (BR-37). Ảnh hưởng BR-71, FR-58, PRD03. | Mở |
| 73 | Ưu tiên của hai yêu cầu mới ở v0.7: FR-57 (sửa, xoá bình luận, từ câu 56 và 67) và FR-58 (xoá feedback, từ câu 56 và 68). | FR-57 Should, FR-58 Should. | Đây là tiện ích, không phải điều kiện an toàn: phần chặn (không ai ngoài Quản trị xoá được, không ai sửa hay xoá bình luận lưu lý do) vẫn được bảo đảm bởi FR-12 (Must) dù hai yêu cầu này chưa làm ở bản đầu. Quyết định phần nào bắt buộc ở bản đầu. | Mở |

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
| 2026-10-22 | 0.3 | Áp dụng câu trả lời của người dùng cho câu hỏi mở 25–32 (đều theo đề xuất); bỏ các câu này khỏi §13; thêm US-16, FR-39–43, BR-47–52, AC-94–120, ba thuật ngữ ở §3.1 (Tư cách admin hệ thống, Nhật ký admin hệ thống, Quyền quản lý tổ chức), ba dòng ma trận quyền; thêm câu hỏi mở 33–38 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Xác nhận ưu tiên Must/Should đã đề xuất cho US-13–15 và FR-23–38, giữ nguyên như đã ghi | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 25: thay đổi của admin hệ thống trên feedback ghi vào lịch sử với tên "<tên> (Admin hệ thống)", tổ chức không tắt được → FR-39, BR-47, AC-48 (sửa), AC-94, AC-95, §8, §9, §11; phát sinh câu hỏi 33, 37 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 26: mỗi lần admin hệ thống vào một tổ chức được ghi vào nhật ký admin hệ thống, Chủ sở hữu và Quản trị xem được → US-16, FR-40, BR-48, AC-96–100, §3.1, §3.3, §9, §11; phát sinh câu hỏi 36 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 27: mục *Mọi tổ chức* trong bộ chọn của admin hệ thống, gắn nhãn tổ chức bị vô hiệu, luôn hiện rõ tư cách admin hệ thống, vào được tổ chức bị vô hiệu, vô hiệu và kích hoạt lại được → FR-03, FR-30 (sửa), FR-41, FR-42, BR-31 (sửa), BR-49, BR-52, AC-73 (sửa), AC-101–107, §3.1, §3.3, §8, §9, §12; phát sinh câu hỏi 34, 35 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 28: admin hệ thống không tính là Chủ sở hữu, không làm Phụ trách hay người được giao ở tổ chức mình không là thành viên, vẫn gán và giao việc cho thành viên khác → FR-21, BR-21 (thêm ngoại lệ BR-50), FR-43, BR-50, AC-108–112, §3.2, §3.3, §8, §9 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 29: bình luận gửi người báo của admin hệ thống hiện "<tên> (Admin hệ thống)" với người báo là thành viên, "Đội hỗ trợ <tên sản phẩm>" với khách → BR-08 (sửa), AC-113, AC-114, §3.3 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 30: gửi và thu hồi lời mời theo quyền quản lý thành viên → FR-25, BR-19, BR-24 (sửa), AC-60 (sửa), AC-115–117, §3.3, §9 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 31: lời mời chỉ dùng được bởi tài khoản có đúng email được mời → FR-25 (sửa), BR-51, AC-118, AC-119, §8, §9 | Quyết định của người dùng |
| 2026-10-22 | 0.3 | Câu 32: tên tiếng Anh của loại Bug / Feature / Feedback, của ưu tiên High / Medium / Low → BR-41 (sửa), AC-120 | Quyết định của người dùng |
| 2026-10-22 | 0.4 | Câu 2 (người dùng **đảo lại** câu trả lời ở v0.2): admin hệ thống **không** có toàn quyền. Mô hình giới hạn: chỉ thấy danh sách tổ chức (tên, số thành viên) và khảo sát (tạo, mở, đóng đợt, chọn tổ chức nhận, xem kết quả theo spec khảo sát; chi tiết PRD07); vô hiệu và kích hoạt lại tổ chức; không truy cập sản phẩm, feedback, thành viên hay cài đặt của tổ chức nào. Muốn giúp bên trong một tổ chức thì tổ chức thêm họ làm thành viên thường, họ làm việc đúng như thành viên đó, không nhãn, không nhật ký riêng. Giữ: chỉ người vận hành máy chủ chỉ định. Vì sao quan trọng: giữ nguyên việc tách biệt dữ liệu giữa các tổ chức, không còn đường truy cập xuyên tổ chức nào; khảo sát là nhu cầu duy nhất ở cấp hệ thống. → FR-01, FR-03, FR-21, FR-30, FR-38, FR-42 (sửa), BR-08, BR-21, BR-22, BR-31, BR-49 (sửa), thêm US-17, US-18, FR-44, FR-45, BR-53, AC-121–136; sửa §2, §3.1, §3.2, §3.3, §8, §9, §11, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.4 | Hệ quả của câu 2: các câu trả lời 25, 26, 28, 29 hết hiệu lực; câu 27 hết hiệu lực trừ phần vô hiệu và kích hoạt lại tổ chức. Bỏ các câu hỏi mở 33, 34, 36, 37, 38 vì không còn đối tượng. Câu 1, 3–24, 30–32 giữ nguyên. Phần admin hệ thống từng ghi thêm vào chúng do toàn quyền được bỏ: ô lời mời trong §3.3 và §9 (thêm khi áp dụng câu 30); điều kiện "không phải admin hệ thống" ở AC-01 (thêm cùng câu 2 ở v0.2); ngoại lệ admin hệ thống ở FR-30, BR-31, AC-73 (thêm cùng câu 27). Câu 8 và câu 23 tự thân không dựa vào toàn quyền | Quyết định của người dùng |
| 2026-10-22 | 0.4 | Câu 35 (theo đề xuất, người dùng chấp nhận cùng mô hình giới hạn): chỉ admin hệ thống kích hoạt lại; Chủ sở hữu không vào được tổ chức đã vô hiệu, muốn kích hoạt lại thì liên hệ admin hệ thống; kích hoạt lại đưa mọi thứ về như trước: thành viên giữ vai trò, widget nhận feedback lại, link riêng còn hạn hoạt động lại → BR-49, FR-42, ô *Kích hoạt lại* trong ma trận quyền (hết ô chưa quyết), §8, §9, AC-106 (sửa), AC-128–131 | Quyết định của người dùng |
| 2026-10-22 | 0.4 | Bỏ (~~Removed~~, không đánh lại số): US-13, US-16; FR-39, FR-40, FR-41, FR-43; BR-47, BR-48, BR-50; AC-46–50, AC-94–104, AC-108–114. Bỏ khỏi §3.1 các thuật ngữ *Tư cách admin hệ thống*, *Nhật ký admin hệ thống*; bỏ khỏi ma trận quyền dòng *Xem nhật ký admin hệ thống*; thêm dòng *Xem danh sách mọi tổ chức*. AC-121 khôi phục hành vi của AC-42 cũ, theo BR-45 ("không tìm thấy") | Quyết định của người dùng |
| 2026-10-22 | 0.4 | Danh sách tổ chức đánh dấu tổ chức đã vô hiệu (FR-44, BR-49): suy ra từ câu 35 (chỉ admin hệ thống kích hoạt lại được, nên cần biết tổ chức nào đang bị vô hiệu); nhãn này trước đây thuộc câu 27; chờ người dùng xác nhận khi duyệt. Thêm câu hỏi mở 39–41 | Suy ra từ quyết định của người dùng (câu 35) |
| 2026-10-22 | 0.5 | Áp dụng câu trả lời của người dùng cho câu hỏi mở 39–41 (đều theo đề xuất); bỏ các câu này khỏi §13; thêm US-19, FR-46, BR-54, BR-55, AC-137–145, trường *Lý do vô hiệu tổ chức* ở §9, hai sự kiện ở §10; thêm câu hỏi mở 42–45 | Quyết định của người dùng |
| 2026-10-22 | 0.5 | Câu 39: admin hệ thống phải ghi lý do khi vô hiệu tổ chức; mọi Chủ sở hữu nhận email khi admin hệ thống vô hiệu (kèm lý do) hoặc kích hoạt lại; Chủ sở hữu tự vô hiệu thì không báo → US-19, FR-46, BR-54, BR-49 (sửa), BR-52 (sửa), AC-105 (sửa), AC-137–141, ô *Vô hiệu tổ chức* của admin hệ thống trong ma trận quyền, §8, §9, §10; phát sinh câu hỏi 42, 45 | Quyết định của người dùng |
| 2026-10-22 | 0.5 | Câu 40: các mốc thời gian (tự đóng 7 ngày, hạn lời mời, hạn link riêng) vẫn chạy theo giờ lịch khi tổ chức bị vô hiệu; kích hoạt lại chỉ khôi phục những gì còn hạn; feedback tự đóng trong lúc vô hiệu ở *Đóng*; lời mời hết hạn phải gửi lại → BR-55, FR-42 (sửa), BR-24, BR-27, BR-49 (sửa), AC-142–145, §8, §9; phát sinh câu hỏi 43, 44 | Quyết định của người dùng |
| 2026-10-22 | 0.5 | Câu 41: giữ ưu tiên đã ghi: US-17 Should, US-18 Should, FR-42 Should, FR-44 Should, FR-45 Must | Quyết định của người dùng |
| 2026-10-22 | 0.5 | Xác nhận danh sách tổ chức của admin hệ thống đánh dấu tổ chức đã vô hiệu (FR-44, BR-49, AC-124), điểm ghi "chờ người dùng xác nhận" ở v0.4 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Áp dụng câu trả lời của người dùng cho câu hỏi mở 42–45 và các câu 46–58 từ review của business-analyst (đều theo đề xuất, 2026-10-22); bỏ 42–45 khỏi §13; thêm US-20, US-21, FR-47–56, BR-56–67, AC-146–196; thêm thuật ngữ ở §3.1 (Ngữ cảnh cá nhân, Trang chào, Quyền quản lý vai trò, Các quyền nền tảng khác, Chống leo thang quyền, Vô hiệu tổ chức, Người vận hành máy chủ); thêm dòng vào ma trận quyền; thêm câu hỏi mở 59–71 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 42: dù ai vô hiệu tổ chức, mọi Chủ sở hữu khác người thực hiện nhận email (sửa AC-141 của câu 39); admin hệ thống đồng thời là Chủ sở hữu vô hiệu thì vẫn bắt buộc lý do → BR-54, BR-49, BR-52 (sửa), FR-46 (sửa), US-19 (sửa), AC-141 (sửa), AC-146–148, §8, §9, §10; phát sinh câu hỏi 69 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 43: trong lúc vô hiệu không gửi thông báo nào của tổ chức, kể cả khi tự đóng; lịch sử ghi đủ; ngoại lệ email BR-54; không gửi bù; lời mời khảo sát (PRD07) nằm ngoài quy tắc → BR-56, FR-55, AC-149, AC-150, §8, §10 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 44: mở lời mời còn hạn trong lúc vô hiệu thì không vào được, báo tổ chức tạm ngừng; lời mời không bị huỷ → BR-57, FR-54, AC-151, AC-152, §8 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 45: FR-42 và FR-46 thành Must → §5; phát sinh câu hỏi 59 (FR-21, FR-44, US-17, US-19 vẫn Should) | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 46: chỉ Chủ sở hữu gán, gỡ vai trò Chủ sở hữu; không ai cấp quyền mình không có; bốn quyền nền tảng khác không mở đường tới feedback; thêm quyền quản lý vai trò → US-21, FR-52, FR-56, BR-58, BR-59, BR-60, BR-18, BR-19, BR-24 (sửa), FR-23, FR-25 (sửa), AC-55, AC-116 (sửa), AC-153–157, AC-159–161, AC-193–195, §3.1, ma trận quyền (dòng vai trò Chủ sở hữu, vai trò tự tạo, vai trò hệ thống, bốn quyền), §9, §11, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Từ mục đích câu 46: việc gỡ (gỡ thành viên đang là Chủ sở hữu, gỡ vai trò hay quyền mình không có) theo cùng giới hạn → BR-58 điểm (3), AC-158; chờ xác nhận ở câu hỏi 61 | Suy ra từ quyết định của người dùng (câu 46) |
| 2026-10-22 | 0.6 | Câu 47: xử lý lần đầu là lần đầu một người chuyển feedback ra khỏi *Mới*, sang bất kỳ trạng thái nào → BR-17 (sửa), §2, §3.1, FR-47, AC-162 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 48: người được giao mất vai trò Phụ trách (bỏ gán hoặc đổi thành Viewer) thì feedback về *Chưa giao*, ghi lịch sử → BR-61, FR-50, AC-166, AC-167, §3.1, §8, §9; phát sinh câu hỏi 66 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 49: Viewer trên feedback của mình có đủ quyền người báo; người báo đồng thời là Phụ trách bình luận trên feedback của mình thì tính là trả lời của người báo (BR-12) → BR-62, BR-09, BR-12 (sửa), FR-08, FR-15 (sửa), AC-14, AC-15, AC-29, AC-33 (sửa), AC-170–173, §3.2, ma trận quyền, §8; phát sinh câu hỏi 65 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 50: thông báo bản trùng cả cho khách đã để email, khi gốc lần đầu sang *Đã xử lý*, *Đóng* hoặc *Từ chối* → BR-13 (sửa), FR-48, AC-37 (sửa), AC-174, AC-175, §8, §10; phát sinh câu hỏi 63, 64 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 51: thao tác trên dữ liệu không được thấy cũng trả "không tìm thấy" → FR-37, BR-45 (sửa), AC-12, AC-122, AC-123, AC-136 (sửa), AC-176, §8, §11 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 52: lời mời cho email đã có tài khoản (đăng nhập rồi chấp nhận), đăng ký Google/Microsoft cùng email, mời lại làm lời mời cũ hết hiệu lực, không mời email đã là thành viên, vai trò bị xoá làm lời mời hết hiệu lực, chấp nhận qua email tính là xác minh email → BR-63, BR-24, FR-25 (sửa), AC-177–182, AC-196, §3.1, §8, §9, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 53: người đang làm việc thấy "tổ chức tạm ngừng" ở thao tác tiếp theo; tổ chức nằm mờ kèm nhãn trong bộ chọn; tài khoản mà mọi tổ chức đều bị vô hiệu thấy trang chào có lời giải thích → BR-64, FR-54, AC-73 (sửa), AC-183, AC-184, §3.1, §8, §12; phát sinh câu hỏi 62 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 54: người có quyền quản lý tổ chức đổi tên tổ chức; quyền Quản trị FeedbackHub ở vai trò Admin không bỏ được vì vai trò hệ thống không sửa được → US-20, FR-53, BR-65, BR-60, BR-18, FR-23 (sửa), AC-159, AC-185–187, §3.1, §9, §12; phát sinh câu hỏi 70 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 55: tự đóng chậm nhất 1 giờ sau mốc 7×24 giờ; lý do tối đa 500 ký tự; thay đổi quyền có hiệu lực từ thao tác tiếp theo (bỏ "vài giây" ở §8) → BR-27, BR-11, BR-28, BR-29, BR-54 (sửa), BR-66, BR-67, FR-12, FR-26, FR-27, FR-28 (sửa), AC-63 (sửa), AC-188–190, §8, §9, §11 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 56: thêm vào ma trận quyền: xoá feedback (chỉ Quản trị); tác giả sửa bình luận của mình, Quản trị xoá mọi bình luận; Quản trị và Phụ trách quản lý nhãn; sản phẩm chỉ lưu trữ, không xoá hẳn, do Quản trị; chi tiết thuộc PRD01, PRD03 → §3.1, §3.3, §8, §12; phát sinh câu hỏi 67, 68 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 57: trong lúc vô hiệu khách không xin được link riêng mới (PRD04); đợt khảo sát gửi "mọi tổ chức" bỏ qua tổ chức đang vô hiệu (PRD07); ghi là ràng buộc chéo → BR-31 (sửa), §8, §12; phát sinh câu hỏi 71 | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Câu 58: khách chỉ thấy tên sản phẩm, không thấy người quản lý sản phẩm → FR-36, BR-46, BR-06 (sửa), AC-84 (sửa) | Quyết định của người dùng |
| 2026-10-22 | 0.6 | Sửa theo review: FR-16 có khách (AC-191, AC-192); thêm FR-47 cho US-12 và BR-17, BR-40, BR-44 (AC-162–165); thêm FR-48 cho BR-13 và trỏ AC-37 về đó; thêm FR-49 cho BR-16 và trỏ AC-43 về đó; thêm FR-50 (giao việc) cho BR-05, BR-36 và trỏ AC-88 về đó (AC-168); thêm FR-51 (ưu tiên) và trỏ AC-93, AC-120 về đó (AC-169); thêm thuật ngữ còn thiếu ở §3.1; sửa §3.1 *Tổ chức*, *Admin hệ thống*, §3.2 và §11 vốn ghi admin hệ thống "chỉ thấy tên và số thành viên" (thiếu dấu hiệu đã vô hiệu và kết quả khảo sát, BR-21). Làm rõ Given của AC-27, AC-35, AC-40, AC-69, AC-86, AC-90, AC-125, AC-190 cho khớp BR-09, BR-58, BR-62; AC-143 theo dung sai 1 giờ; tách AC-55, AC-155, AC-160, AC-179 để mỗi tiêu chí một hành vi (AC-193–196) | BA review |
| 2026-10-22 | 0.7 | Áp dụng câu trả lời của người dùng cho câu hỏi mở 59–71 ("Đồng ý": đều theo đề xuất, kể cả ưu tiên ở câu 59 và 60); bỏ 59–71 khỏi §13; thêm FR-57, FR-58, BR-68–72, AC-197–229, thuật ngữ *Bình luận lưu lý do* ở §3.1, dòng *Bình luận* ở §9, dòng *Quản trị xoá feedback* ở §10; hết ô *?n* trong ma trận quyền; thêm câu hỏi mở 72, 73 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 59: US-17, US-19, FR-21, FR-44 lên Must (thay câu trả lời 41 về US-17, FR-44) | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 60: xác nhận ưu tiên đã ghi ở v0.6: US-20 Should, US-21 Must; FR-47 Should, FR-48 Must, FR-49 Should, FR-50–52 Must, FR-53 Should, FR-54–56 Must; không đổi ô nào | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 61: xác nhận BR-58 điểm (3), việc gỡ theo cùng giới hạn như việc cấp → BR-58, AC-158 (bỏ ghi chú "chờ xác nhận"), AC-197, AC-198, §8 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 62: người đang làm việc trong tổ chức vừa bị vô hiệu được đưa về ngữ cảnh cá nhân; ngữ cảnh cá nhân luôn hiện trang chào, kèm bộ chọn tổ chức khi có tổ chức đang hoạt động → BR-68, BR-32, BR-64, FR-31, FR-54 (sửa), AC-199–201, §3.1 (*Ngữ cảnh cá nhân*, *Trang chào*), §8, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 63: gốc bị đánh dấu trùng thì mọi bản trùng của nó chuyển sang gốc mới và được báo theo gốc mới; feedback ở *Trùng lặp* không làm gốc được → BR-69, BR-37, BR-13, FR-18 (sửa), AC-202–205, §8, §9, §10, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 64: bản trùng gắn vào gốc đã ở *Đã xử lý*, *Đóng* hoặc *Từ chối* thì người báo được báo ngay, một lần → BR-13, FR-48 (sửa), AC-208, AC-209, §8, §10, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Từ câu 63 và 64: việc chuyển bản trùng sang gốc mới tính như gắn vào gốc mới, nên bản trùng chưa được báo được báo ngay nếu gốc mới đã có kết quả, bản trùng đã được báo thì không báo lại → BR-69, AC-206, AC-207 | Suy ra từ quyết định của người dùng (câu 63, 64) |
| 2026-10-22 | 0.7 | Câu 65: chỉ bình luận gửi người báo mới đưa feedback rời *Cần thêm thông tin*, bình luận nội bộ thì không; người báo là Quản trị được tính như người báo là Phụ trách → BR-12, FR-15 (sửa), AC-210, AC-211, dòng *Bình luận, bổ sung trên feedback của mình* trong ma trận quyền, §8 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 66: feedback tự về *Chưa giao* thì lịch sử ghi người làm thao tác gây ra việc đó, kèm ghi chú "do mất vai trò Phụ trách" hoặc "do rời tổ chức" → BR-61, BR-30, FR-50, FR-29 (sửa), AC-166, AC-167 (sửa), AC-212, §8, §9, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 67: khách sửa được bình luận của mình qua link riêng; không tác giả nào tự xoá được bình luận, chỉ Quản trị xoá; bình luận lưu lý do không ai sửa, xoá được → FR-57, BR-70, AC-213–218, ma trận quyền (dòng sửa và xoá bình luận, hết ô *?67*; Quản trị xoá "mọi bình luận, trừ bình luận lưu lý do"), §8, §9, §11, §12; phát sinh câu hỏi 73 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 68: xoá feedback là xoá hẳn khỏi mọi màn hình, mã không dùng lại, người báo không được báo, việc xoá ghi lại ngoài feedback (chi tiết PRD03); thêm ngoại lệ vào BR-14 → FR-58, BR-71, BR-14, BR-42, FR-19, FR-34 (sửa), AC-219–223, ma trận quyền, §8, §9, §10, §11, §12; phát sinh câu hỏi 72, 73 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 69: email vô hiệu do admin hệ thống không là thành viên ghi "admin hệ thống FeedbackHub", không nêu tên; admin hệ thống đồng thời là Chủ sở hữu không nhận email kích hoạt lại do chính mình làm → BR-54, FR-46 (sửa), AC-137 (sửa), AC-224, §8, §10, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 70: định danh ngắn đặt khi tạo tổ chức và không bao giờ đổi, kể cả khi đổi tên → BR-65, FR-53 (sửa), AC-225, AC-226, §8, §9, §12 | Quyết định của người dùng |
| 2026-10-22 | 0.7 | Câu 71: khi tạo đợt khảo sát không chọn được tổ chức đang bị vô hiệu; tổ chức bị vô hiệu sau khi đợt đã mở thì thành viên vẫn trả lời được → BR-72, BR-31, FR-21, FR-30 (sửa), AC-227–229, ma trận quyền (dòng khảo sát), §8, §12 | Quyết định của người dùng |

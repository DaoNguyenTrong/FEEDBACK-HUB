# PRD-01 — Quản lý sản phẩm: sản phẩm, domain được phép, khoá công khai, nút widget, người phụ trách

| Mục | Giá trị |
|---|---|
| Mã | `PRD01` (tiền tố cho mọi mã trong tài liệu này) |
| Trạng thái | In review |
| Phiên bản | 0.2 |
| Người duyệt | Người dùng (chủ sản phẩm FeedbackHub) |
| Cập nhật | 2026-10-23 |
| Phụ thuộc | PRD-00 (v1.2, Approved) |
| Thiết kế | Chưa có; thiết kế ở giai đoạn 2 sau khi PRD được duyệt, mỗi màn hình một bảng sáng và một bảng tối |
| Kế hoạch | Chưa có |

Thuật ngữ (*Sản phẩm*, *Khoá công khai*, *Domain được phép*, *Widget*, *Chế độ khách*, *Vai trò theo sản phẩm*), vai trò, ma trận quyền và vòng đời feedback: xem PRD-00 §3. Tài liệu này chỉ quy định cách quản lý sản phẩm diễn ra.

## 1. Bối cảnh và vấn đề

Mỗi tổ chức có nhiều ứng dụng và website (tối đa khoảng 20 mỗi tổ chức, `docs/process/system-context.md`), mỗi cái do một hoặc vài người trong team IT phụ trách. Muốn feedback đến đúng người, Hub phải biết từng sản phẩm, widget của nó được chạy ở đâu, và ai nhận feedback của nó. Nền tảng hiện chưa có khái niệm sản phẩm: chỉ có tổ chức, thành viên, vai trò tổ chức và khoá API (khoá API là bí mật của tổ chức, không dùng được cho widget, spec lõi §12).

Quản trị cần một chỗ để tạo sản phẩm, lấy đoạn mã nhúng widget, giới hạn widget vào đúng tên miền của sản phẩm, chỉnh nút cho hợp giao diện sản phẩm, và gán Phụ trách, Viewer. Nếu thiếu chỗ này, feedback không có đường vào, hoặc vào rồi mà không ai nhận.

## 2. Mục tiêu, không phải mục tiêu, phản mục tiêu

- **Mục tiêu**:
  - Quản trị tự tạo sản phẩm và gắn widget vào trang của sản phẩm mà không cần ai trong team nền tảng giúp.
  - Widget chỉ chạy trên tên miền Quản trị cho phép.
  - Mỗi sản phẩm đang hoạt động có người nhận feedback; sản phẩm chưa có Phụ trách thì Quản trị thấy ngay và feedback không bị bỏ rơi.
  - Đổi giao diện nút, domain hay chế độ khách không bắt sửa mã của sản phẩm.
  - Sản phẩm thôi dùng thì được cất đi mà không mất feedback nào.
- **Không phải mục tiêu**:
  - Xoá hẳn sản phẩm: không có ở bất kỳ phiên bản nào (PRD-00 §3.3).
  - Cách widget hiện, thu thập và gửi feedback, và hành vi của chế độ khách: PRD02. PRD này chỉ có công tắc bật, tắt chế độ khách.
  - Thông báo khi được gán, khi sản phẩm chưa có Phụ trách: PRD05.
  - Chuyển sản phẩm sang tổ chức khác.
  - Nhiều khoá hay nhiều cấu hình cho một sản phẩm (ví dụ tách môi trường thử và thật): một sản phẩm có một khoá hiện hành; môi trường do sản phẩm truyền vào widget (PRD02).
  - Logo, mô tả hay trường thông tin khác của sản phẩm ngoài tên.
  - Màn hình xem một thành viên đang phụ trách những sản phẩm nào.
  - Giới hạn số sản phẩm mỗi tổ chức: không có hạn mức (PRD-00 §2, *gói trả phí, hạn mức, billing*).
- **Phản mục tiêu** (chạy đúng nhưng vẫn là thất bại nếu):
  - Ai đó có khoá công khai (đọc được trong mã của trang) mà xem được feedback hay cấu hình nào của tổ chức.
  - Lưu trữ một sản phẩm làm mất hay ẩn đi feedback mà người báo, Phụ trách đang theo dõi.
  - Feedback mới của một sản phẩm không đến được ai vì sản phẩm không có Phụ trách mà không ai biết.
  - Tạo khoá mới làm widget trên trang thật ngừng ngay, trước khi kịp cập nhật mã nhúng.
  - Người không phải Quản trị đổi được cấu hình widget hay gán người cho sản phẩm.
- **Đo thành công**: chỉ số dùng chung ở PRD-00 §2 (định nghĩa: PRD06). Riêng module này, không có chỉ số mới; Quản trị nhìn danh sách sản phẩm là biết: không sản phẩm đang hoạt động nào mang dấu *Chưa có Phụ trách* hay *Chưa có domain* lâu ngày.

## 3. Người dùng và vai trò

Vai trò: xem PRD-00 §3.2. Riêng ở module này:

- **Quản trị** là người duy nhất dùng phần *Sản phẩm*: tạo, sửa, lưu trữ, khôi phục sản phẩm, cấu hình widget, gán và bỏ gán Phụ trách, Viewer (PRD-00 §3.3, nhóm *Sản phẩm*).
- **Phụ trách, Viewer, người báo** không thấy phần *Sản phẩm*, kể cả mã nhúng, khoá và domain của sản phẩm mình được gán (BR-18); Phụ trách cần mã nhúng thì nhận từ Quản trị (quyết định 2). Họ chịu hệ quả của việc gán: thấy feedback của sản phẩm được gán (PRD-00 FR-07).
- **Admin hệ thống** không thấy sản phẩm nào của tổ chức mình không là thành viên (PRD-00 BR-21); ở tổ chức đã vào làm thành viên thì như mọi thành viên (PRD-00 BR-53).
- **Người có quyền xem nhật ký kiểm toán** thấy các mục nhật ký về sản phẩm (BR-19), không thấy cấu hình hay feedback qua quyền đó (PRD-00 BR-59).

## 4. User story

| Mã | Là… | Tôi muốn… | Để… | Ưu tiên |
|---|---|---|---|---|
| PRD01-US-01 | Quản trị | tạo sản phẩm và lấy đoạn mã nhúng widget | gắn widget vào trang của sản phẩm và bắt đầu nhận feedback | Must |
| PRD01-US-02 | Quản trị | chỉ cho widget chạy trên tên miền của sản phẩm | người khác không gắn được widget của sản phẩm vào trang lạ | Must |
| PRD01-US-03 | Quản trị (thường kiêm lập trình viên) | thử widget trên máy của mình (localhost) cho riêng một sản phẩm | kiểm tra trước khi đưa lên trang thật mà không mở localhost cho mọi sản phẩm | Should |
| PRD01-US-04 | Quản trị | tạo khoá công khai mới mà widget trên trang thật không ngừng ngay | có thời gian cập nhật mã nhúng | Should |
| PRD01-US-05 | Quản trị | chỉnh vị trí, màu, chữ của nút widget mà không sửa mã nhúng | nút hợp giao diện sản phẩm và không che nội dung | Should |
| PRD01-US-06 | Quản trị | gán, đổi, bỏ Phụ trách và Viewer cho từng sản phẩm | mỗi sản phẩm có đúng người nhận và người theo dõi feedback | Must |
| PRD01-US-07 | Quản trị | thấy ngay sản phẩm nào chưa có Phụ trách | không feedback nào bị bỏ rơi | Must |
| PRD01-US-08 | Quản trị | lưu trữ sản phẩm thôi dùng mà không mất feedback | danh sách gọn mà lịch sử vẫn còn | Must |
| PRD01-US-09 | Quản trị | khôi phục sản phẩm đã lưu trữ | dùng lại sản phẩm mà không phải tạo lại và gắn lại mã nhúng | Should |
| PRD01-US-10 | Quản trị | bật, tắt chế độ khách cho từng sản phẩm | chỉ sản phẩm hướng ra khách hàng mới nhận feedback từ người ngoài tổ chức | Must |
| PRD01-US-11 | Quản trị của tổ chức có 20 sản phẩm | tìm nhanh một sản phẩm theo tên | không phải lướt cả danh sách | Should |
| PRD01-US-12 | người có quyền xem nhật ký kiểm toán | biết ai đổi cấu hình sản phẩm, ai gán ai, lúc nào | truy lại khi widget ngừng chạy hay feedback đến nhầm người | Must |

## 5. Yêu cầu chức năng

| Mã | Yêu cầu | Story | Ưu tiên |
|---|---|---|---|
| PRD01-FR-01 | Hệ thống phải cho Quản trị tạo sản phẩm trong tổ chức đang làm việc chỉ với một tên (BR-02), và khi tạo thì tự cấp cho sản phẩm một khoá công khai (BR-03), danh sách domain trống (BR-07), chế độ khách tắt (BR-10), nút widget theo mặc định (BR-09), không Phụ trách, không Viewer. | US-01 | Must |
| PRD01-FR-02 | Hệ thống phải cho Quản trị đổi tên sản phẩm theo BR-02; đổi tên không đổi khoá, domain, nút hay người được gán. | US-01 | Must |
| PRD01-FR-03 | Hệ thống phải cho Quản trị xem danh sách sản phẩm của tổ chức đang làm việc: sản phẩm đang hoạt động sắp theo tên, sản phẩm đã lưu trữ ở mục riêng; mỗi sản phẩm kèm số Phụ trách và các dấu *Chưa có Phụ trách*, *Chưa có domain* khi đúng; có ô tìm theo tên (BR-21). | US-07, US-11 | Must |
| PRD01-FR-04 | Hệ thống phải hiện đoạn mã nhúng widget của sản phẩm, chứa khoá công khai hiện hành, kèm nút sao chép. | US-01 | Must |
| PRD01-FR-05 | Hệ thống phải cho Quản trị thêm, bỏ mục trong danh sách domain được phép của sản phẩm theo BR-05, BR-07, và áp dụng thay đổi theo BR-08. | US-02 | Must |
| PRD01-FR-06 | Hệ thống phải cho Quản trị bật, tắt *Cho phép localhost* riêng cho từng sản phẩm và thêm mục localhost theo BR-06. | US-03 | Should |
| PRD01-FR-07 | Hệ thống phải cho Quản trị tạo khoá công khai mới cho sản phẩm; khoá cũ còn nhận feedback 24 giờ rồi ngừng (BR-04); trang sản phẩm hiện thời điểm ngừng của mỗi khoá cũ còn hạn. | US-04 | Should |
| PRD01-FR-08 | Hệ thống phải cho Quản trị chỉnh nút widget (vị trí, màu, chữ trên nút) theo BR-09 và áp dụng cho trang đang gắn widget mà không cần sửa mã nhúng (BR-08). | US-05 | Should |
| PRD01-FR-09 | Hệ thống phải hiện bản xem trước của nút widget, đổi ngay theo lựa chọn của Quản trị trước khi lưu. | US-05 | Could |
| PRD01-FR-10 | Hệ thống phải cho Quản trị tắt nút nổi của sản phẩm, để sản phẩm tự mở widget từ giao diện của mình (cách mở: PRD02). | US-05 | Could |
| PRD01-FR-11 | Hệ thống phải cho Quản trị bật, tắt chế độ khách của từng sản phẩm (BR-10). | US-10 | Must |
| PRD01-FR-12 | Hệ thống phải cho Quản trị, trên trang của một sản phẩm, gán thành viên làm Phụ trách hoặc Viewer, đổi vai trò theo sản phẩm của người đã được gán, và bỏ gán, theo BR-11 và PRD-00 BR-03. | US-06 | Must |
| PRD01-FR-13 | Hệ thống phải dùng bộ chọn thành viên chỉ gồm thành viên đang hoạt động của tổ chức, hiện tên và vai trò tổ chức của từng người, với email theo BR-12. | US-06 | Must |
| PRD01-FR-14 | Hệ thống phải cho Quản trị chọn nhiều thành viên cùng lúc để gán cùng một vai trò theo sản phẩm (BR-11). | US-06 | Could |
| PRD01-FR-15 | Hệ thống phải đánh dấu *Chưa có Phụ trách* trên sản phẩm không có Phụ trách nào (BR-13), và báo hệ quả trước khi Quản trị bỏ gán hoặc đổi thành Viewer Phụ trách cuối cùng (BR-14). | US-07 | Must |
| PRD01-FR-16 | Hệ thống phải cho Quản trị lưu trữ sản phẩm, theo BR-15. | US-08 | Must |
| PRD01-FR-17 | Hệ thống phải cho Quản trị khôi phục sản phẩm đã lưu trữ, theo BR-16. | US-09 | Should |
| PRD01-FR-18 | Hệ thống phải không có thao tác xoá hẳn sản phẩm, và từ chối mọi yêu cầu xoá sản phẩm gửi trực tiếp, không thay đổi gì (BR-17). | US-08 | Must |
| PRD01-FR-19 | Hệ thống phải ẩn hẳn phần *Sản phẩm* với người không phải Quản trị, và kiểm mọi thao tác của phần này, qua màn hình hay gửi trực tiếp, theo thứ tự ở BR-18. | US-01, US-06 | Must |
| PRD01-FR-20 | Hệ thống phải ghi vào nhật ký kiểm toán của tổ chức mỗi lần tạo, đổi tên, lưu trữ, khôi phục sản phẩm, đổi cấu hình widget, tạo khoá mới, gán, đổi vai trò và bỏ gán (BR-19). | US-12 | Must |

## 6. Quy tắc nghiệp vụ

| Mã | Quy tắc | Áp dụng cho |
|---|---|---|
| PRD01-BR-01 | Sản phẩm thuộc tổ chức đang làm việc của người tạo, và mãi thuộc tổ chức đó; không có thao tác chuyển sản phẩm sang tổ chức khác. | FR-01 |
| PRD01-BR-02 | Tên sản phẩm: bắt buộc (khoảng trắng ở đầu và cuối không tính), tối đa 100 ký tự, không trùng tên một sản phẩm khác của cùng tổ chức, kể cả sản phẩm đã lưu trữ, không phân biệt hoa thường; hai tổ chức khác nhau có sản phẩm cùng tên được. Tên sản phẩm là thông tin duy nhất về tổ chức mà khách thấy (PRD-00 BR-06), nên ô nhập tên có một dòng ngắn nhắc điều này. | FR-01, FR-02 |
| PRD01-BR-03 | Khoá công khai do Hub tự cấp khi tạo sản phẩm, không ai tự đặt được, không trùng với khoá nào khác trên Hub, kể cả khoá đã ngừng; khoá đã ngừng không bị xoá, nên không khoá nào được cấp lại. Khoá là mã định danh, không phải bí mật (PRD-00 §3.1): nó chỉ cho gửi feedback vào đúng sản phẩm đó, trên domain được phép (cách gửi: PRD02); người có khoá không xem, không sửa được gì trong Hub nhờ nó. Mỗi sản phẩm luôn có đúng một khoá hiện hành; khoá hiện hành nằm trong mã nhúng. | FR-01, FR-04, FR-07 |
| PRD01-BR-04 | Tạo khoá mới: khoá mới thành khoá hiện hành ngay; khoá hiện hành trước đó thành khoá cũ, còn nhận feedback đúng 24 giờ (giờ lịch) tính từ lúc tạo khoá mới, rồi ngừng [Người dùng, spec lõi §12]. Tạo khoá mới lần nữa khi còn khoá cũ chưa ngừng: mỗi khoá cũ giữ mốc ngừng riêng của nó, đúng 24 giờ sau lúc chính nó bị thay. Không có thao tác ngừng khoá cũ trước mốc (quyết định 5). Khoá đã ngừng không bao giờ nhận feedback lại, kể cả khi khôi phục sản phẩm, và không bị xoá (BR-03). Mốc 24 giờ vẫn chạy khi tổ chức bị vô hiệu, như mọi mốc thời gian (PRD-00 BR-55). | FR-07 |
| PRD01-BR-05 | Mỗi mục trong danh sách domain được phép có một trong ba dạng [Người dùng, spec lõi §12; quyết định 4]: (a) tên miền đầy đủ, ví dụ `app.congty.vn`: widget chỉ chạy ở đúng tên miền đó, không chạy ở tên miền con của nó; (b) `*.` trước một tên miền, ví dụ `*.congty.vn`: widget chạy ở mọi tên miền con của nó, ở mọi cấp (`a.congty.vn`, `a.b.congty.vn`), nhưng không chạy ở chính `congty.vn`, muốn thì thêm mục riêng; (c) một địa chỉ IPv4, ví dụ `192.168.1.20`: widget chỉ chạy ở đúng địa chỉ đó; không dùng `*` với địa chỉ IP. Dấu `*` chỉ đứng ở đầu, ngay trước dấu chấm; không nhận `*` đứng một mình, `*` ở giữa, hay `*` ngay trước một đuôi tên miền công cộng (ví dụ `*.vn`, `*.com.vn`, `*.com`). Tên có ít nhất một dấu chấm là tên miền, kể cả tên nội bộ như `erp.congty.local`; tên máy không có dấu chấm (ví dụ `erp`) không được nhận, trừ localhost (BR-06). Mục `127.0.0.1` chỉ nhận theo BR-06. Mục domain không gồm giao thức: widget chạy được ở cả trang `http` lẫn `https` của mục đó. Cổng không tính với tên miền và địa chỉ IP: `app.congty.vn` và `192.168.1.20` khớp mọi cổng. Khi Quản trị dán cả một địa chỉ trang (ví dụ `https://App.CongTy.vn/dang-nhap?x=1`), Hub giữ lại tên miền hoặc địa chỉ IP, bỏ giao thức, cổng và đường dẫn, và đổi về chữ thường (`app.congty.vn`). Mục sai dạng không được thêm, kèm lời nhắc dạng đúng. Mục trùng một mục đã có của sản phẩm không được thêm lần nữa. Mỗi sản phẩm có tối đa 20 mục, kể cả mục localhost. Cùng một tên miền có thể nằm trong danh sách của nhiều sản phẩm, kể cả của tổ chức khác. | FR-05 |
| PRD01-BR-06 | Localhost: mục `localhost:<cổng>` hoặc `127.0.0.1:<cổng>` (cổng từ 1 đến 65535, bắt buộc ghi) chỉ thêm được khi sản phẩm đang bật *Cho phép localhost* [Người dùng, spec lõi §12]; widget chỉ chạy ở đúng cổng đã ghi. *Cho phép localhost* tắt khi tạo sản phẩm. Tắt lại thì các mục localhost vẫn nằm trong danh sách, hiện là đang không có hiệu lực, và widget không chạy ở localhost; bật lại thì chúng có hiệu lực lại. | FR-06 |
| PRD01-BR-07 | Sản phẩm mới có danh sách domain trống. Danh sách trống (hoặc chỉ còn mục localhost đang không có hiệu lực) thì widget không chạy ở trang nào, và sản phẩm mang dấu *Chưa có domain*. Domain không bắt buộc khi tạo sản phẩm. | FR-01, FR-03, FR-05 |
| PRD01-BR-08 | Thay đổi domain, *Cho phép localhost*, nút widget, chế độ khách, lưu trữ và khôi phục có hiệu lực với trang đang gắn widget chậm nhất 5 phút sau khi lưu, không cần sửa mã nhúng. Riêng tạo khoá mới thì mã nhúng phải được cập nhật khoá mới trước khi khoá cũ ngừng (BR-04). | FR-05, FR-06, FR-08, FR-10, FR-11, FR-16, FR-17 |
| PRD01-BR-09 | Nút widget: vị trí *góc dưới bên phải* (mặc định) hoặc *góc dưới bên trái*; màu do Quản trị chọn tự do, mặc định là màu chính của Hub, chữ trên nút tự là trắng hoặc đen tuỳ màu nào dễ đọc hơn trên màu nút; chữ trên nút có một bản tiếng Việt và một bản tiếng Anh, mỗi bản tối đa 24 ký tự, để trống thì dùng chữ mặc định *Góp ý* / *Feedback*; widget hiện bản theo ngôn ngữ của widget (PRD02). *Hiện nút nổi* bật khi tạo sản phẩm; tắt thì trang không có nút, sản phẩm tự mở widget (PRD02). | FR-08, FR-09, FR-10 |
| PRD01-BR-10 | Chế độ khách tắt khi tạo sản phẩm; chỉ Quản trị bật, tắt, cho từng sản phẩm. Bật chế độ khách là cho người ngoài tổ chức, kể cả người ngoài internet, gửi feedback vào sản phẩm đó; công tắc có một dòng ngắn nói điều này. Chế độ khách làm gì, và tắt nó thì feedback, link riêng đã có của khách ra sao: PRD02, PRD04 (§12). | FR-11 |
| PRD01-BR-11 | Gán vai trò theo sản phẩm: chỉ thành viên đang hoạt động của tổ chức sở hữu sản phẩm; không gán được người chỉ có lời mời đang chờ, người đã rời hoặc bị gỡ khỏi tổ chức, hay tài khoản ngoài tổ chức. Mỗi người tối đa một vai trò ở mỗi sản phẩm (PRD-00 BR-03): đổi vai trò làm trên dòng của người đó; trong bộ chọn, người đã có vai trò ở sản phẩm hiện kèm vai trò hiện tại và không chọn được, để không ai bị đổi vai trò khi Quản trị gán nhiều người cùng lúc. Không giới hạn số Phụ trách, Viewer mỗi sản phẩm. Quản trị gán được cả chính mình (muốn nhận việc thì tự gán mình làm Phụ trách, PRD-00 §12 PRD03 *Giao việc*). Gán Viewer cho một Quản trị được; vai trò đó chỉ có tác dụng khi người ấy thôi là Quản trị. Mọi thay đổi có hiệu lực từ thao tác tiếp theo của người được gán hay bị bỏ gán (PRD-00 BR-67). | FR-12, FR-14 |
| PRD01-BR-12 | Bộ chọn thành viên khi gán Phụ trách, Viewer hiện tên và vai trò tổ chức của từng thành viên. Email của thành viên khác chỉ hiện, và chỉ được trả về, khi người đang gán có quyền quản lý thành viên, theo đúng PRD-00 BR-74; Quản trị không có quyền đó không thấy email, kể cả khi gọi trực tiếp (quyết định 3). Danh sách Phụ trách, Viewer trên trang sản phẩm theo cùng quy tắc. | FR-13 |
| PRD01-BR-13 | Sản phẩm *chưa có Phụ trách* khi không thành viên đang hoạt động nào là Phụ trách của nó: khi vừa tạo mà chưa gán, khi Phụ trách cuối cùng bị bỏ gán, bị đổi thành Viewer, bị gỡ khỏi tổ chức hoặc tự rời tổ chức (PRD-00 BR-30, BR-80). Quản trị không tự tính là Phụ trách. Trong thời gian đó, feedback mới được báo cho Quản trị của tổ chức (PRD05) và không ai được giao được (PRD03) [Người dùng, PRD-00 §12 câu 12, 8]. Sản phẩm có Phụ trách trở lại thì dấu biến mất. Sản phẩm đã lưu trữ không mang dấu này. | FR-03, FR-15 |
| PRD01-BR-14 | Bỏ gán hoặc đổi thành Viewer Phụ trách cuối cùng của một sản phẩm đang hoạt động được phép. Trước khi thực hiện, một khối ngay dưới dòng của người đó (không phải hộp thoại) báo bằng lời thường: sau thao tác này sản phẩm chưa có Phụ trách, feedback mới sẽ báo cho Quản trị; kèm *Xác nhận* và *Huỷ*. Các lần bỏ gán khác làm ngay, không có bước xác nhận. Gỡ thành viên hay thành viên tự rời tổ chức thì không có khối này (PRD-00). | FR-15 |
| PRD01-BR-15 | Lưu trữ sản phẩm (quyết định 1): chỉ Quản trị (PRD-00 §3.3). Trước khi lưu trữ, một khối xác nhận ngay dưới tên sản phẩm báo widget sẽ ngừng nhận feedback; kèm *Lưu trữ* và *Huỷ*. Lưu trữ thì: widget của sản phẩm ngừng nhận feedback, với mọi khoá, kể cả khoá cũ còn trong 24 giờ; feedback, ảnh, file, bình luận, lịch sử giữ nguyên; Phụ trách, Viewer giữ vai trò và vẫn thấy, vẫn xử lý được feedback của sản phẩm theo ma trận quyền; người báo và khách vẫn theo dõi, trao đổi như thường; sản phẩm chuyển sang mục *Đã lưu trữ* của danh sách sản phẩm. Ở sản phẩm đã lưu trữ, tên và cấu hình widget chỉ xem được; gán, đổi, bỏ gán Phụ trách, Viewer vẫn làm được. Không trạng thái feedback nào tự đổi vì lưu trữ. | FR-16 |
| PRD01-BR-16 | Khôi phục sản phẩm (quyết định 1): chỉ Quản trị, bất cứ lúc nào, không có bước xác nhận; khôi phục thuộc dòng *Lưu trữ sản phẩm* của ma trận PRD-00 §3.3, không cần dòng riêng. Sản phẩm trở về danh sách đang hoạt động với nguyên khoá hiện hành, domain, nút, chế độ khách và người được gán như lúc lưu trữ; widget nhận feedback lại (BR-08). Khoá cũ đã tới mốc ngừng trong lúc lưu trữ thì vẫn ngừng (BR-04). | FR-17 |
| PRD01-BR-17 | Không ai xoá hẳn được sản phẩm, kể cả sản phẩm chưa từng có feedback, kể cả Chủ sở hữu (PRD-00 §3.3) [Người dùng, PRD-00 câu 56]. Sản phẩm tạo nhầm thì lưu trữ. | FR-18 |
| PRD01-BR-18 | Phần *Sản phẩm* chỉ dành cho Quản trị: ẩn hẳn với người khác, không có bản chỉ xem, giống các phần ở PRD-00 BR-77. Mọi thao tác của phần này, qua màn hình hay gửi trực tiếp, được kiểm theo thứ tự: (1) chưa đăng nhập; (2) không có tổ chức đang làm việc: "không đủ quyền"; (3) tổ chức đang làm việc bị vô hiệu: "tổ chức tạm ngừng", rồi về ngữ cảnh cá nhân (PRD-00 BR-64, BR-68); (4) không phải Quản trị: "không đủ quyền", kiểm trước khi tra sản phẩm, nên người không phải Quản trị không biết được sản phẩm nào có tồn tại; (5) sản phẩm, hay thành viên được gán, không thuộc tổ chức đang làm việc hoặc không tồn tại: "không tìm thấy" (PRD-00 BR-45); (6) các điều kiện còn lại (ví dụ BR-02, BR-05, BR-11, sản phẩm đã lưu trữ). Thao tác bị từ chối thì không có gì thay đổi. | FR-19 |
| PRD01-BR-19 | Nhật ký kiểm toán của tổ chức ghi mỗi lần: tạo sản phẩm, đổi tên, lưu trữ, khôi phục; thêm, bỏ domain; bật, tắt *Cho phép localhost*, chế độ khách, *Hiện nút nổi*; đổi nút widget; tạo khoá mới; gán, đổi vai trò theo sản phẩm, bỏ gán. Mỗi mục có ai, lúc nào, sản phẩm nào, thay đổi gì (với gán: tên thành viên và vai trò trước, sau). Người xem theo quyền xem nhật ký kiểm toán (PRD-00 *Các quyền nền tảng khác*); mục nhật ký không chứa nội dung feedback (PRD-00 BR-59). | FR-20 |
| PRD01-BR-20 | Hai Quản trị sửa cùng một sản phẩm cùng lúc: lần lưu sau được giữ, cho từng thao tác (ví dụ đổi tên, thêm một domain); mỗi lần lưu có mục nhật ký riêng (BR-19). Hai lần gán cùng một người ở cùng sản phẩm cùng lúc: người đó có đúng một vai trò, của lần lưu sau (PRD-00 BR-03). | FR-02, FR-05, FR-12 |
| PRD01-BR-21 | Danh sách sản phẩm sắp theo tên, không phân biệt hoa thường. Ô tìm lọc theo tên, không phân biệt hoa thường và không phân biệt dấu tiếng Việt (gõ "ban hang" ra "Bán hàng"). Không có giới hạn số sản phẩm; danh sách phải dùng tốt với 20 sản phẩm mỗi tổ chức và vẫn dùng được với 100. Tổ chức chưa có sản phẩm thì danh sách hiện trạng thái trống với lời mời tạo sản phẩm đầu tiên. | FR-03 |

## 7. Tiêu chí nghiệm thu

Q là Quản trị của tổ chức X, đang làm việc ở X, trừ khi ghi khác. P là một sản phẩm đang hoạt động của X.

| Mã | Yêu cầu | Cho trước (Given) | Khi (When) | Thì (Then) |
|---|---|---|---|---|
| PRD01-AC-01 | FR-01 | X chưa có sản phẩm tên "App Bán hàng" | Q nhập tên "App Bán hàng" và tạo | Sản phẩm xuất hiện trong danh sách của X, có khoá công khai và mã nhúng, danh sách domain trống, chế độ khách tắt, nút ở góc dưới bên phải, chữ mặc định, và mang dấu *Chưa có Phụ trách* và *Chưa có domain* |
| PRD01-AC-02 | FR-01 | — | Q để trống tên, hoặc chỉ gõ khoảng trắng, rồi tạo | Không có sản phẩm nào được tạo; Q được báo cần nhập tên |
| PRD01-AC-03 | FR-01, FR-02 | — | Q tạo sản phẩm với tên 101 ký tự; rồi với tên 100 ký tự | Lần đầu bị từ chối, kèm giới hạn 100 ký tự; lần sau tạo được |
| PRD01-AC-04 | FR-01, FR-02 | X có sản phẩm "App Bán hàng" đã lưu trữ; tổ chức Y có sản phẩm "App Kho"; tổ chức Z chưa có sản phẩm nào | Q tạo sản phẩm "app bán hàng" ở X; Q đổi tên một sản phẩm khác của X thành "APP BÁN HÀNG"; Quản trị của Z tạo "App Kho" | Hai thao tác ở X bị từ chối vì tên đã có; ở Z tạo được dù Y có sản phẩm cùng tên; thông báo trùng ở X không nêu gì về tổ chức khác |
| PRD01-AC-05 | FR-02 | P tên "App Kho", có khoá, 2 domain, 1 Phụ trách | Q đổi tên P thành "Kho vận" | Danh sách hiện "Kho vận"; khoá, domain, Phụ trách không đổi |
| PRD01-AC-06 | FR-03 | X có 20 sản phẩm đang hoạt động và 3 đã lưu trữ | Q mở danh sách sản phẩm | 20 sản phẩm hiện theo tên, không phân biệt hoa thường, mỗi cái kèm số Phụ trách; 3 sản phẩm nằm ở mục *Đã lưu trữ* |
| PRD01-AC-07 | FR-03 | X có "App Bán hàng", "Bán lẻ Web", "App Kho" | Q gõ "ban" vào ô tìm | Chỉ hiện "App Bán hàng" và "Bán lẻ Web" |
| PRD01-AC-08 | FR-03 | X chưa có sản phẩm nào | Q mở danh sách sản phẩm | Danh sách hiện trạng thái trống với lời mời tạo sản phẩm đầu tiên |
| PRD01-AC-09 | FR-04 | — | Q mở trang P và bấm sao chép mã nhúng | Mã nhúng, chứa khoá hiện hành của P, nằm trong bộ nhớ tạm và Q được báo đã sao chép; nếu trình duyệt không cho sao chép, mã nhúng được chọn sẵn để Q tự sao chép |
| PRD01-AC-10 | FR-04 | Một người chưa đăng nhập đọc được khoá công khai của P trong mã của một trang | Người đó dùng khoá để xem feedback, danh sách sản phẩm hay cấu hình của P | Không xem được gì; khoá chỉ dùng được để gửi feedback vào P trên domain được phép (PRD02) |
| PRD01-AC-11 | FR-05 | P có mục `app.congty.vn` | Widget được mở ở `app.congty.vn`, `app.congty.vn:8443`, `congty.vn` và `x.app.congty.vn` | Widget chạy ở hai trang đầu; không chạy ở hai trang sau |
| PRD01-AC-12 | FR-05 | P có mục `*.congty.vn` | Widget được mở ở `a.congty.vn`, `a.b.congty.vn` và `congty.vn` | Widget chạy ở hai trang đầu; không chạy ở `congty.vn` |
| PRD01-AC-13 | FR-05 | — | Q dán `https://App.CongTy.vn/dang-nhap?x=1` vào ô thêm domain | Mục được lưu là `app.congty.vn` |
| PRD01-AC-14 | FR-05 | — | Q thêm lần lượt `*`, `*.vn`, `*.com.vn`, `app.*.vn`, `cong ty`, `erp`, `192.168.1.*` | Không mục nào được thêm; mỗi lần Q được nhắc dạng đúng của một mục domain |
| PRD01-AC-15 | FR-05 | P đã có mục `app.congty.vn` | Q thêm `APP.congty.vn` | Không có mục thứ hai; Q được báo mục đã có |
| PRD01-AC-16 | FR-05 | P có 20 mục domain | Q thêm mục thứ 21 | Bị từ chối, kèm giới hạn 20 mục; danh sách không đổi |
| PRD01-AC-17 | FR-05, FR-03 | P không có mục domain nào | Một trang bất kỳ gắn mã nhúng của P | Widget không chạy; P mang dấu *Chưa có domain* trong danh sách và trên trang của nó |
| PRD01-AC-18 | FR-05 | P có mục `app.congty.vn`, widget đang chạy ở đó | Q bỏ mục đó | Chậm nhất 5 phút sau, widget không chạy ở `app.congty.vn` nữa |
| PRD01-AC-19 | FR-06 | P đang tắt *Cho phép localhost* | Q thêm `localhost:5173` | Bị từ chối; Q được nhắc cần bật *Cho phép localhost* trước |
| PRD01-AC-20 | FR-06 | P bật *Cho phép localhost*, có mục `localhost:5173` | Widget được mở ở `localhost:5173` và `localhost:3000` | Chạy ở cổng 5173, không chạy ở cổng 3000 |
| PRD01-AC-21 | FR-06 | P bật *Cho phép localhost*, có mục `localhost:5173` | Q tắt *Cho phép localhost*, rồi bật lại | Sau khi tắt: mục vẫn trong danh sách, hiện là không có hiệu lực, widget không chạy ở `localhost:5173`; sau khi bật lại: widget chạy lại ở đó |
| PRD01-AC-22 | FR-06 | Sản phẩm P2 khác của X không bật *Cho phép localhost* | Mã nhúng của P2 được mở ở `localhost:5173` | Widget không chạy, dù P đang cho phép cổng đó |
| PRD01-AC-23 | FR-07 | Khoá hiện hành của P là K1 | Q tạo khoá mới lúc 10:00 ngày 1 | Mã nhúng hiện khoá mới K2; trang P ghi K1 ngừng lúc 10:00 ngày 2; widget dùng K1 vẫn nhận feedback đến mốc đó và không nhận sau mốc đó |
| PRD01-AC-24 | FR-07 | Như AC-23 | Q tạo khoá mới lần nữa lúc 15:00 ngày 1 | Khoá hiện hành là K3; K1 vẫn ngừng lúc 10:00 ngày 2; K2 ngừng lúc 15:00 ngày 2 |
| PRD01-AC-25 | FR-07 | K1 là khoá cũ của P, còn hạn; tổ chức X bị vô hiệu lúc K1 còn 2 giờ, rồi được kích hoạt lại sau 1 ngày | Widget dùng K1 gửi feedback sau khi kích hoạt lại | Không nhận; K1 đã ngừng trong lúc vô hiệu |
| PRD01-AC-26 | FR-08 | P đang có nút mặc định, widget đang chạy ở `app.congty.vn` | Q đổi màu, chọn góc dưới bên trái, đổi chữ tiếng Việt thành "Báo lỗi" và lưu | Không sửa mã nhúng, chậm nhất 5 phút sau trang `app.congty.vn` hiện nút mới ở góc dưới bên trái với chữ "Báo lỗi" khi widget ở tiếng Việt |
| PRD01-AC-27 | FR-08 | — | Q nhập chữ trên nút 25 ký tự; rồi xoá trống chữ tiếng Anh và lưu | Lần đầu bị từ chối, kèm giới hạn 24 ký tự; lần sau lưu được và nút dùng chữ *Feedback* khi widget ở tiếng Anh |
| PRD01-AC-28 | FR-08 | — | Q chọn màu vàng nhạt, rồi màu xanh đậm | Với vàng nhạt chữ trên nút là đen; với xanh đậm chữ là trắng |
| PRD01-AC-29 | FR-09 | — | Q đổi màu và vị trí nút, chưa lưu | Bản xem trước đổi ngay theo lựa chọn; trang đang gắn widget chưa đổi |
| PRD01-AC-30 | FR-10 | P đang hiện nút nổi | Q tắt *Hiện nút nổi* | Chậm nhất 5 phút sau, trang gắn widget của P không còn nút nổi; widget vẫn nhận feedback khi sản phẩm tự mở nó (PRD02) |
| PRD01-AC-31 | FR-11 | P vừa tạo | Q mở phần chế độ khách của P, rồi bật | Lúc đầu chế độ khách tắt; sau khi bật, trang P ghi chế độ khách đang bật, kèm dòng ngắn nói người ngoài tổ chức gửi được feedback |
| PRD01-AC-32 | FR-12 | M là thành viên đang hoạt động của X, chưa có vai trò ở P; P chưa có Phụ trách | Q gán M làm Phụ trách của P | M nằm trong danh sách Phụ trách của P; dấu *Chưa có Phụ trách* biến mất; ở thao tác tiếp theo M thấy feedback của P (PRD-00 AC-08) |
| PRD01-AC-33 | FR-12 | M là Viewer của P | Q đổi vai trò của M trên dòng của M thành Phụ trách | M có đúng một vai trò ở P, là Phụ trách |
| PRD01-AC-34 | FR-12, FR-13 | X có thành viên đã rời tổ chức R, một lời mời đang chờ tới email E; tài khoản T chỉ thuộc tổ chức Y | Q mở bộ chọn thành viên của P; rồi gửi trực tiếp yêu cầu gán T làm Phụ trách của P | Bộ chọn không có R, E hay T; yêu cầu gán T nhận "không tìm thấy", không có gì thay đổi |
| PRD01-AC-35 | FR-12 | V là Viewer của P, đang mở một feedback của P | Q bỏ gán V | V không còn trong danh sách của P; thao tác tiếp theo của V trên feedback đó nhận "không tìm thấy" (PRD-00 BR-67) |
| PRD01-AC-36 | FR-12 | Q chưa có vai trò ở P | Q gán chính mình làm Phụ trách của P | Q nằm trong danh sách Phụ trách của P |
| PRD01-AC-37 | FR-13 | Q có quyền Quản trị FeedbackHub nhưng không có quyền quản lý thành viên | Q mở bộ chọn thành viên của P, và gửi trực tiếp yêu cầu lấy danh sách đó | Mỗi người hiện tên và vai trò tổ chức, không có email; yêu cầu trực tiếp cũng không trả về email của ai khác Q |
| PRD01-AC-38 | FR-13 | Q có cả quyền Quản trị FeedbackHub và quyền quản lý thành viên | Q mở bộ chọn thành viên của P | Mỗi người hiện tên, vai trò tổ chức và email |
| PRD01-AC-39 | FR-14 | A, B chưa có vai trò ở P; C là Phụ trách của P | Q mở bộ chọn để gán Viewer, chọn A và B | A và B thành Viewer của P; C hiện trong bộ chọn kèm "Phụ trách" và không chọn được, vẫn là Phụ trách |
| PRD01-AC-40 | FR-15 | M là Phụ trách duy nhất của P | Q chọn bỏ gán M | Ngay dưới dòng của M hiện khối báo sản phẩm sẽ chưa có Phụ trách và feedback mới sẽ báo cho Quản trị, kèm *Xác nhận*, *Huỷ*; chọn *Huỷ* thì không có gì thay đổi; chọn *Xác nhận* thì M bị bỏ gán và P mang dấu *Chưa có Phụ trách* |
| PRD01-AC-41 | FR-15 | M là Phụ trách duy nhất của P | Q đổi M thành Viewer | Hiện khối như AC-40; xác nhận thì M là Viewer và P mang dấu *Chưa có Phụ trách* |
| PRD01-AC-42 | FR-15 | P có hai Phụ trách M và N | Q bỏ gán M | M bị bỏ gán ngay, không có khối xác nhận; P không mang dấu *Chưa có Phụ trách* |
| PRD01-AC-43 | FR-15 | M là Phụ trách duy nhất của P | M tự rời tổ chức X | Ở lần tải tiếp theo, danh sách sản phẩm hiện P với dấu *Chưa có Phụ trách* |
| PRD01-AC-44 | FR-15 | Q là Quản trị, không là Phụ trách của P; P không có Phụ trách nào | Q mở danh sách sản phẩm | P mang dấu *Chưa có Phụ trách* |
| PRD01-AC-45 | FR-16 | P có Phụ trách M và một khoá cũ còn 10 giờ | Q chọn lưu trữ P | Ngay dưới tên P hiện khối báo widget sẽ ngừng nhận feedback, kèm *Lưu trữ*, *Huỷ*; chọn *Lưu trữ* thì P sang mục *Đã lưu trữ*, và chậm nhất 5 phút sau widget không nhận feedback với khoá hiện hành lẫn khoá cũ |
| PRD01-AC-46 | FR-16 | P đã lưu trữ; M là Phụ trách của P; người báo B có một feedback *Đang xử lý* ở P | M đổi trạng thái feedback đó; B bình luận trên feedback của mình | Cả hai làm được như khi P đang hoạt động; không trạng thái nào tự đổi vì P bị lưu trữ |
| PRD01-AC-47 | FR-16 | P đã lưu trữ | Q mở trang P | Tên và cấu hình widget chỉ xem, không sửa được; Q vẫn gán, đổi, bỏ gán Phụ trách, Viewer được; P không mang dấu *Chưa có Phụ trách* dù không có Phụ trách |
| PRD01-AC-48 | FR-17 | P đã lưu trữ, có khoá hiện hành K2 và khoá cũ K1 đã tới mốc ngừng trong lúc lưu trữ | Q khôi phục P | P về danh sách đang hoạt động với nguyên K2, domain, nút, chế độ khách, người được gán; chậm nhất 5 phút sau widget dùng K2 nhận feedback lại; K1 vẫn không nhận |
| PRD01-AC-49 | FR-18 | P có 30 feedback | Q tìm thao tác xoá trên trang P; rồi gửi trực tiếp yêu cầu xoá P | Không có thao tác xoá; yêu cầu trực tiếp bị từ chối; P và 30 feedback vẫn nguyên |
| PRD01-AC-50 | FR-19 | M là Phụ trách của P, không phải Quản trị | M tìm phần *Sản phẩm* trong Hub; rồi mở đường dẫn trực tiếp tới trang P và gửi trực tiếp yêu cầu đổi domain của P | M không thấy phần *Sản phẩm*; cả hai yêu cầu nhận "không đủ quyền", không hiện tên hay cấu hình của P, không có gì thay đổi |
| PRD01-AC-51 | FR-19 | M là thành viên thường của X; một mã sản phẩm của tổ chức Y và một mã không tồn tại | M gửi trực tiếp yêu cầu mở hai mã đó | Cả hai nhận "không đủ quyền", giống hệt nhau, như với sản phẩm của X |
| PRD01-AC-52 | FR-19 | P' là sản phẩm của tổ chức Y; Q không thuộc Y | Q mở đường dẫn tới P', và gửi trực tiếp yêu cầu gán người vào P' | Cả hai nhận "không tìm thấy", không hiện gì về P' (PRD-00 BR-45) |
| PRD01-AC-53 | FR-19 | Admin hệ thống S không là thành viên của X | S mở đường dẫn tới P | S không xem được gì về P (PRD-00 BR-21) |
| PRD01-AC-54 | FR-19 | Q đang mở trang P; X bị vô hiệu | Q lưu một thay đổi domain | Q thấy "tổ chức tạm ngừng", thay đổi không được lưu, Hub đưa Q về ngữ cảnh cá nhân (PRD-00 BR-64, BR-68) |
| PRD01-AC-55 | FR-19 | Q đang mở trang P; Q vừa bị đổi vai trò tổ chức, mất quyền Quản trị FeedbackHub | Q lưu đổi tên P | Q nhận "không đủ quyền"; tên P không đổi (PRD-00 BR-67) |
| PRD01-AC-56 | FR-20 | Q thêm domain `app.congty.vn` vào P, tạo khoá mới, gán M làm Phụ trách | Người có quyền xem nhật ký kiểm toán của X mở nhật ký | Có ba mục, mỗi mục ghi Q, thời điểm, P và thay đổi (domain thêm vào; khoá mới; M: chưa có vai trò → Phụ trách); không mục nào chứa nội dung feedback |
| PRD01-AC-57 | FR-02, FR-12 | Hai Quản trị Q1, Q2 cùng mở trang P | Q1 đổi tên P thành "A", Q2 sau đó đổi thành "B"; cùng lúc Q1 gán M làm Phụ trách, Q2 gán M làm Viewer, Q2 lưu sau | Tên P là "B"; M có đúng một vai trò ở P là Viewer; nhật ký có mục cho từng lần lưu |
| PRD01-AC-58 | FR-03, FR-04, FR-12 | Q dùng điện thoại, màn hình rộng khoảng 400 px | Q mở danh sách sản phẩm, tìm P, sao chép mã nhúng, thêm một domain và gán một Phụ trách | Q làm được hết mà không phải cuộn ngang trang và không phải phóng to |
| PRD01-AC-59 | FR-05 | P có mục `noibo.congty.vn` | Widget được mở ở `http://noibo.congty.vn` và `https://noibo.congty.vn` | Widget chạy ở cả hai trang |
| PRD01-AC-60 | FR-05 | — | Q thêm `192.168.1.20`; rồi widget được mở ở `http://192.168.1.20:8080` và `http://192.168.1.21` | Mục được lưu; widget chạy ở trang đầu, không chạy ở trang sau |
| PRD01-AC-61 | FR-05 | — | Q thêm `erp.congty.local`; rồi thêm `erp` | `erp.congty.local` được lưu như một tên miền; `erp` không được thêm, Q được nhắc dạng đúng |

## 8. Trạng thái và trường hợp biên

- **Tổ chức chưa có sản phẩm**: trạng thái trống (BR-21, AC-08).
- **Sản phẩm mới**: chưa có domain, chưa có Phụ trách, chế độ khách tắt; widget chưa chạy ở đâu (BR-07, BR-13, AC-01, AC-17).
- **20 sản phẩm** mỗi tổ chức: danh sách sắp theo tên, có ô tìm; không có giới hạn số sản phẩm (BR-21, AC-06, AC-07).
- **Giới hạn domain**: 20 mục mỗi sản phẩm (BR-05, AC-16). Mục sai dạng, trùng, wildcard quá rộng bị từ chối (AC-14, AC-15).
- **Sản phẩm chưa có Phụ trách**: do mới tạo, bỏ gán, đổi thành Viewer, gỡ hoặc rời tổ chức (BR-13, AC-40–44). Thông báo cho Quản trị: PRD05; không giao việc được: PRD03.
- **Tạo khoá mới nhiều lần trong 24 giờ**: mỗi khoá cũ giữ mốc riêng (BR-04, AC-24). Khoá cũ hết hạn trong lúc tổ chức vô hiệu hay sản phẩm lưu trữ thì vẫn hết (AC-25, AC-48).
- **Sản phẩm đã lưu trữ**: widget ngừng, feedback vẫn xử lý được, cấu hình chỉ xem, gán vẫn đổi được; khôi phục được (BR-15, BR-16, AC-45–48).
- **Trang `http`, địa chỉ IP, tên nội bộ**: domain được phép nhận cả trang `http` và địa chỉ IPv4; tên có dấu chấm như `erp.congty.local` là tên miền; tên không dấu chấm bị từ chối, trừ localhost (BR-05, AC-59–61). Trên trang `http`, widget có thể không nhận ra thành viên đang đăng nhập Hub, vì trình duyệt coi khung `https` nằm trong trang `http` là không an toàn; cách widget nhận diện người gửi: PRD02 (§12).
- **Sửa đồng thời**: lần lưu sau được giữ (BR-20, AC-57).
- **Quyền bị thu hồi khi đang mở trang**: Quản trị mất quyền thì thao tác tiếp theo bị từ chối (AC-55); Phụ trách, Viewer bị bỏ gán thì mất quyền xem feedback ngay (AC-35).
- **Tổ chức bị vô hiệu**: không ai vào phần *Sản phẩm* (AC-54); widget mọi sản phẩm tạm ngừng (PRD-00 BR-31); mốc 24 giờ của khoá cũ vẫn chạy (BR-04).
- **Thành viên được gán bị gỡ khỏi tổ chức**: mất vai trò theo sản phẩm (PRD-00 BR-30); nếu là Phụ trách cuối cùng, sản phẩm thành chưa có Phụ trách (BR-13).
- **Người ngoài phần Sản phẩm**: "không đủ quyền" cho người trong tổ chức không phải Quản trị, "không tìm thấy" cho sản phẩm của tổ chức khác (BR-18, AC-50–53).
- **Điện thoại**: mọi thao tác của module làm được trên màn hình khoảng 400 px (AC-58).

## 9. Dữ liệu (mức nghiệp vụ)

| Trường | Ý nghĩa | Bắt buộc | Giới hạn | Ai được sửa |
|---|---|---|---|---|
| Tên sản phẩm | Tên hiển thị trong Hub; khách cũng thấy (PRD-00 BR-06) | Có | Tối đa 100 ký tự, không trùng trong tổ chức (kể cả đã lưu trữ), không phân biệt hoa thường (BR-02) | Quản trị; chỉ xem khi đã lưu trữ |
| Trạng thái sản phẩm | *Đang hoạt động* hoặc *Đã lưu trữ* | Có | Mặc định đang hoạt động; không có trạng thái đã xoá (BR-17) | Quản trị (lưu trữ, khôi phục) |
| Khoá công khai hiện hành | Mã định danh sản phẩm trong mã nhúng; không phải bí mật | Có, tự cấp | Duy nhất toàn Hub; không ai tự đặt (BR-03) | Không ai sửa; Quản trị tạo khoá mới (BR-04) |
| Khoá cũ còn hạn | Khoá trước đó, còn nhận feedback đến mốc ngừng | Không | Mốc ngừng = lúc bị thay + 24 giờ (BR-04) | Không ai |
| Mã nhúng | Đoạn mã gắn widget vào trang, chứa khoá hiện hành | Tự tạo | — | Không ai |
| Domain được phép | Danh sách nơi widget chạy, ở cả trang `http` và `https` | Không | Tên miền đầy đủ, `*.` + tên miền, hoặc một địa chỉ IPv4; tên không có dấu chấm không nhận, trừ localhost; tối đa 20 mục; không trùng trong sản phẩm (BR-05) | Quản trị |
| Cho phép localhost | Cho mục `localhost:<cổng>`, `127.0.0.1:<cổng>` có hiệu lực | Có | Mặc định tắt (BR-06) | Quản trị |
| Vị trí nút | Góc dưới bên phải hoặc dưới bên trái | Có | Mặc định dưới bên phải (BR-09) | Quản trị |
| Màu nút | Màu nền của nút; màu chữ tự chọn trắng hoặc đen | Có | Mặc định màu chính của Hub (BR-09) | Quản trị |
| Chữ trên nút (vi, en) | Chữ hiện trên nút theo ngôn ngữ widget | Không | Mỗi bản tối đa 24 ký tự; trống thì *Góp ý* / *Feedback* (BR-09) | Quản trị |
| Hiện nút nổi | Có vẽ nút trên trang hay không | Có | Mặc định bật (BR-09) | Quản trị |
| Chế độ khách | Cho người ngoài tổ chức gửi feedback | Có | Mặc định tắt (BR-10); hành vi: PRD02 | Quản trị |
| Vai trò theo sản phẩm | Phụ trách hoặc Viewer của một thành viên ở sản phẩm (PRD-00 §9) | Không | Tối đa một mỗi người mỗi sản phẩm; chỉ thành viên đang hoạt động (BR-11) | Quản trị |
| Nhật ký kiểm toán: sản phẩm | Mục ghi mỗi thay đổi ở BR-19 | Tự ghi | Ai, lúc nào, sản phẩm, thay đổi | Không ai; người có quyền xem nhật ký kiểm toán xem |

## 10. Thông báo và sự kiện

Ai nhận, kênh và nội dung: PRD05. Module này chỉ nêu sự kiện phát sinh.

| Sự kiện | Ai nhận | Kênh | Ghi chú |
|---|---|---|---|
| Thành viên được gán, đổi vai trò, bị bỏ gán ở một sản phẩm | Theo PRD05 (chưa quyết) | Theo PRD05 | Ghi nhật ký kiểm toán (BR-19); §12 PRD05 |
| Sản phẩm thành chưa có Phụ trách, hoặc có Phụ trách trở lại | Quản trị (PRD05) | Theo PRD05 | Feedback mới báo cho Quản trị trong thời gian chưa có Phụ trách (PRD-00 §12 PRD05) |
| Tạo, đổi tên, lưu trữ, khôi phục sản phẩm; đổi cấu hình widget; tạo khoá mới | Không ai được báo (đề xuất) | — | Chỉ ghi nhật ký kiểm toán (BR-19) |
| Khoá cũ sắp ngừng hoặc đã ngừng | Không ai được báo (đề xuất) | — | Mốc ngừng hiện trên trang sản phẩm (FR-07) |

## 11. Yêu cầu phi chức năng

- **Khoá công khai không phải chìa khoá**: khoá nằm công khai trong mã trang; nó chỉ định danh sản phẩm để gửi feedback, không mở được bất kỳ dữ liệu hay thao tác nào khác trong Hub (BR-03, AC-10). Ghi chú cho thiết kế kỹ thuật: `docs/tech/README.md` § *Ràng buộc đã biết*, mục *Khoá công khai của widget*; khoá này qua review bảo mật cùng TECH02.
- **Domain được phép chỉ là lớp chặn phụ**: nó ngăn người khác gắn widget của sản phẩm vào trang lạ một cách thông thường, nhưng nơi gửi do trình duyệt báo lên và có thể bị giả (spec lõi §9). Chống gửi tràn lan và gửi giả: PRD02. Cho phép trang `http` (quyết định 4) nghĩa là ảnh và thông tin gửi đi từ một trang không mã hoá; người dùng chấp nhận vì nhiều ứng dụng nội bộ chạy trên `http`.
- **Kiểm quyền không phụ thuộc giao diện**: phần *Sản phẩm* ẩn với người không phải Quản trị, và mọi yêu cầu gửi trực tiếp vẫn bị kiểm theo BR-18 (PRD-00 FR-12).
- **Tách biệt dữ liệu**: sản phẩm, cấu hình và người được gán của một tổ chức không bao giờ hiện cho người ngoài tổ chức, kể cả admin hệ thống (PRD-00 BR-21); thông báo tên trùng không nêu tổ chức khác (AC-04).
- **Riêng tư (Nghị định 13/2023/NĐ-CP)**: module này chỉ xử lý tên, vai trò và (theo BR-12) email của thành viên; email trong bộ chọn và danh sách người được gán theo đúng PRD-00 BR-74. Bật chế độ khách làm sản phẩm thu dữ liệu cá nhân của người ngoài tổ chức; việc báo cho khách và xin đồng ý: PRD02 (§12).
- **Hiệu năng**: trang danh sách và trang sản phẩm hiện dữ liệu trong 1 giây ở 95% lượt mở với 20 sản phẩm mỗi tổ chức (`docs/process/system-context.md`). Thay đổi cấu hình có hiệu lực với widget chậm nhất 5 phút (BR-08). Cấu hình nút không được làm widget vượt mục tiêu nạp ban đầu của widget (≤ 50 KB sau nén; chi tiết: PRD02).
- **Thiết bị**: danh sách sản phẩm, trang sản phẩm, mã nhúng, domain, nút widget và gán người được tối ưu cho điện thoại, máy tính bảng và máy tính, không chỉ "dùng được" (`docs/process/system-context.md`, AC-58). Theo sở thích đã nêu của người dùng: ít hộp thoại, xác nhận bằng khối ngay dưới dòng (BR-14, BR-15).
- **Ngôn ngữ**: giao diện tiếng Việt và tiếng Anh; tên vai trò Phụ trách, Viewer dịch thống nhất (PRD-00 FR-13). Chữ trên nút có bản riêng cho từng ngôn ngữ (BR-09).
- **Truy vết**: mọi thay đổi sản phẩm, cấu hình và người được gán được ghi nhật ký kiểm toán (BR-19).
- **Quy mô**: ≤ 20 sản phẩm mỗi tổ chức, ≤ 100 toàn Hub; không có hạn mức (BR-21).

## 12. Phụ thuộc

- **PRD-00** (Approved v1.2): thuật ngữ, vai trò, ma trận quyền nhóm *Sản phẩm*, BR-03 (một vai trò mỗi người mỗi sản phẩm), BR-30, BR-80 (gỡ, rời tổ chức bỏ vai trò theo sản phẩm), BR-45 ("không tìm thấy"), BR-67 (quyền đổi có hiệu lực ngay), BR-74 (email thành viên), BR-77 (mẫu ẩn phần quản lý và thứ tự kiểm).
- **Nền tảng có sẵn**: tổ chức, thành viên, vai trò tổ chức, chuyển tổ chức, nhật ký kiểm toán. Nền tảng chưa có sản phẩm, khoá công khai, domain được phép hay vai trò theo sản phẩm; PRD này thêm. Khoá API hiện có là bí mật của tổ chức, không dùng làm khoá công khai (spec lõi §12; `docs/tech/README.md`).
- **PRD02** dùng khoá công khai, domain được phép, *Cho phép localhost*, cấu hình nút và công tắc chế độ khách để quyết widget có chạy, có nhận feedback không.
- **PRD03, PRD05** dùng danh sách Phụ trách và trạng thái *chưa có Phụ trách*.
- **Dịch vụ bên ngoài**: không có.

### Chuyển cho PRD khác

| PRD đích | Điều cần quy định | Phát sinh từ |
|---|---|---|
| PRD02 | **Widget không chạy**: điều người xem trang thấy khi widget được mở trên domain không được phép, khi sản phẩm chưa có domain, khi khoá đã ngừng, khi sản phẩm đã lưu trữ (đề xuất: không hiện nút, không lời nhắn nào nêu tên tổ chức). | BR-04, BR-05, BR-07, BR-15 |
| PRD02 | **Nhận diện thành viên trên trang `http`**: widget có thể không nhận ra thành viên đang đăng nhập Hub khi trang của sản phẩm là `http`; PRD02 quyết widget làm gì khi đó (ví dụ mời đăng nhập, gửi với tư cách khách, hay báo cần trang `https`). | BR-05, quyết định 4 |
| PRD02 | **Cách sản phẩm tự mở widget** khi *Hiện nút nổi* tắt, và nút hiện ra sao trên điện thoại (kích thước, không che nội dung). | FR-10, BR-09 |
| PRD02 | **Chế độ khách và Nghị định 13**: báo cho khách việc thu thông tin cá nhân và ảnh màn hình, xin đồng ý nếu cần, khi sản phẩm bật chế độ khách. | BR-10, §11 |
| PRD02 | **Tắt chế độ khách khi khách đang mở form**: lượt gửi đang dở của khách xử lý ra sao. | BR-10 |
| PRD03 | **Email trong bộ chọn người được giao**: dùng cùng quy tắc với bộ chọn Phụ trách, Viewer của PRD01 (BR-12): email chỉ hiện và chỉ được trả về khi người giao việc có quyền quản lý thành viên (PRD-00 BR-74). Trả lời mục *Email trong bộ chọn người được giao* ở PRD-00 §12. | BR-12, quyết định 3 |
| PRD03 | **Sản phẩm đã lưu trữ trong hộp feedback**: feedback của sản phẩm đã lưu trữ có hiện mặc định không, lọc thế nào, có nhãn *Đã lưu trữ* không. | BR-15 |
| PRD04 | **Link riêng khi tắt chế độ khách hoặc lưu trữ sản phẩm**: link riêng đã có của khách còn dùng được không (đề xuất: còn, như BR-15 giữ việc theo dõi). | BR-10, BR-15 |
| PRD05 | **Báo khi được gán, đổi vai trò, bị bỏ gán** ở một sản phẩm (chưa quyết): có báo người đó không, qua kênh nào. | FR-12 |
| PRD05 | **Sản phẩm đã lưu trữ**: không còn feedback mới nên không có thông báo feedback mới; các thông báo khác của feedback cũ vẫn như thường. | BR-15 |
| PRD06 | **Sản phẩm đã lưu trữ trong Tổng quan và chỉ số**: có tính, có hiện không. | BR-15 |

## 13. Câu hỏi mở

| # | Câu hỏi | Đề xuất trả lời | Vì sao quan trọng | Trạng thái |
|---|---|---|---|---|
| — | Không có. Câu 1–6 đã được trả lời 2026-10-23 (§14 *Quyết định*): câu 4 do người dùng, các câu khác do SA (thay người dùng). | — | — | — |

### Mặc định đề xuất

| # | Mặc định | Mã bị ảnh hưởng | Lý do |
|---|---|---|---|
| D1 | Mức ưu tiên: Must cho US-01, US-02, US-06–US-08, US-10, US-12, FR-01–FR-05, FR-11–FR-13, FR-15, FR-16, FR-18–FR-20; Should cho US-03, US-04, US-05, US-09, US-11, FR-06–FR-08, FR-17; Could cho FR-09, FR-10, FR-14. US-12 và FR-20 (nhật ký kiểm toán) lên Must theo SA (thay người dùng) 2026-10-23 | Toàn bộ US, FR | Must là phần tối thiểu để widget có đường vào và feedback có người nhận; chế độ khách là quyết định cốt lõi của người dùng (spec lõi §9); FR-19 là kiểm quyền; nhật ký kiểm toán là cách duy nhất truy lại ai đổi domain, khoá, người được gán. Should là tiện ích quản lý; Could là phần tiện thêm |
| D2 | Sản phẩm chỉ có một trường người dùng nhập là tên; không logo, không mô tả | FR-01, §2, §9 | Spec lõi không nêu trường nào khác; thêm sau không phá gì |
| D3 | Tên sản phẩm: bắt buộc, khoảng trắng đầu cuối không tính, tối đa 100 ký tự, không trùng trong tổ chức kể cả sản phẩm đã lưu trữ, không phân biệt hoa thường; ô tên nhắc khách thấy tên này | BR-02, AC-02–04 | Cùng cách với tên vai trò tự tạo (PRD-00 BR-79); tính cả sản phẩm đã lưu trữ để khôi phục không gây trùng; khách thấy tên sản phẩm (PRD-00 BR-06) |
| D4 | Khi tạo sản phẩm không tự gán người tạo làm Phụ trách; sản phẩm bắt đầu *chưa có Phụ trách* | FR-01, BR-13, AC-01 | Quản trị tạo sản phẩm thường không phải người xử lý; quy tắc chưa có Phụ trách đã bảo đảm Quản trị nhận feedback mới |
| D5 | Domain: `*.` không gồm tên miền gốc; không nhận `*` trước đuôi tên miền công cộng, không dùng `*` với địa chỉ IP; cổng không tính với tên miền và địa chỉ IPv4; dán URL thì Hub giữ lại tên miền hoặc địa chỉ IP, chữ thường; tối đa 20 mục mỗi sản phẩm; một tên miền nằm được ở nhiều sản phẩm. (Nhận `http`, IPv4 đúng từng địa chỉ, tên có dấu chấm là tên miền: quyết định 4 của người dùng, không phải mặc định) | BR-05, AC-11–16, AC-59–61 | Cách phổ biến (Userback, Zendesk); `*.vn` coi như mở cho mọi trang; 20 mục dư cho một sản phẩm; nhiều ứng dụng có thể chung một máy chủ |
| D6 | Localhost: mục phải ghi cổng; nhận cả `127.0.0.1:<cổng>`, và `127.0.0.1` chỉ nhận theo cách này, không như một địa chỉ IPv4 thường; tắt *Cho phép localhost* thì mục giữ lại nhưng không có hiệu lực | BR-05, BR-06, AC-19–22 | Người dùng ghi `localhost:<cổng>` (spec lõi §12); quyết định 4 mở địa chỉ IPv4 nhưng giữ localhost sau công tắc riêng; giữ mục để bật lại không phải nhập lại |
| D7 | Danh sách domain trống thì widget không chạy ở đâu; domain không bắt buộc khi tạo; dấu *Chưa có domain* | BR-07, AC-17 | An toàn mặc định; Quản trị có thể chưa biết tên miền khi tạo |
| D8 | Thay đổi cấu hình có hiệu lực với widget chậm nhất 5 phút | BR-08, AC-18, AC-26, AC-30, AC-45, AC-48 | Đủ nhanh cho Quản trị kiểm ngay; cho phép thiết kế kỹ thuật dùng bộ nhớ đệm |
| D9 | Nút widget: hai vị trí góc dưới phải (mặc định), dưới trái; màu tự chọn, chữ tự trắng hoặc đen; chữ vi, en tối đa 24 ký tự, trống thì *Góp ý* / *Feedback*; *Hiện nút nổi* mặc định bật | BR-09, AC-26–30 | Spec lõi §7.2 cho vị trí, màu, chữ cấu hình theo sản phẩm; góc trên hay đụng thanh đầu trang; chữ tự đổi màu để luôn đọc được |
| D10 | Chế độ khách tắt khi tạo sản phẩm; công tắc nằm ở trang sản phẩm (PRD01), hành vi ở PRD02 | BR-10, FR-11, AC-31 | Mở cho người ngoài internet là lựa chọn có chủ ý; ma trận PRD-00 xếp cấu hình widget vào PRD01 |
| D11 | Bộ chọn chỉ có thành viên đang hoạt động; người đã có vai trò ở sản phẩm hiện kèm vai trò, không chọn được trong bộ chọn nhiều người; đổi vai trò làm trên dòng của người đó; Quản trị gán được chính mình; gán Viewer cho Quản trị được | BR-11, AC-34, AC-36, AC-39 | Tránh vô tình hạ Phụ trách thành Viewer khi gán hàng loạt; PRD-00 §12 PRD03 đã nói Quản trị tự gán mình làm Phụ trách |
| D12 | Bỏ gán hoặc đổi thành Viewer Phụ trách cuối cùng được phép, kèm khối xác nhận ngay dưới dòng; các bỏ gán khác làm ngay | BR-14, AC-40–42 | Không chặn Quản trị nhưng nói rõ hệ quả; ít hộp thoại theo sở thích của người dùng |
| D13 | Lưu trữ có khối xác nhận chỉ báo widget sẽ ngừng nhận feedback (không nêu số feedback chưa kết thúc, theo SA thay người dùng 2026-10-23, để PRD01 không phụ thuộc PRD03); khôi phục không cần xác nhận | BR-15, BR-16, AC-45, AC-48 | Lưu trữ làm widget ngừng; khôi phục không làm mất gì |
| D14 | Phần *Sản phẩm* ẩn hẳn với người không phải Quản trị; thứ tự kiểm theo mẫu PRD-00 BR-77 (thiếu quyền "không đủ quyền" trước, sản phẩm tổ chức khác "không tìm thấy") | BR-18, AC-50–53 | Thống nhất với các phần quản lý của PRD-00; người không phải Quản trị không dò được sản phẩm nào tồn tại |
| D15 | Ghi nhật ký kiểm toán mọi thay đổi sản phẩm, cấu hình, khoá và người được gán; không báo ai khi đổi cấu hình hay khoá cũ ngừng | BR-19, §10, AC-56 | Domain, khoá, người được gán ảnh hưởng tới widget và quyền xem; nền tảng đã có nhật ký kiểm toán |
| D16 | Sửa đồng thời: lần lưu sau được giữ | BR-20, AC-57 | Mỗi tổ chức có ít Quản trị; nhật ký giữ dấu cả hai lần |
| D17 | Danh sách sắp theo tên; tìm không phân biệt hoa thường và dấu; không giới hạn số sản phẩm | BR-21, AC-06–08 | Spec lõi §2 loại hạn mức; tên tiếng Việt hay được gõ không dấu |

## 14. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| 2026-10-23 | 0.1 | Bản nháp đầu, từ PRD-00 v1.2 (§3.3 nhóm *Sản phẩm*, §12 ba mục PRD01, câu 12, 8, 56, 94), spec lõi §4, §5, §7.2, §9, §11, §12 và `docs/process/system-context.md` | — |
| 2026-10-23 | 0.2 | Áp dụng câu trả lời 1–6 (§14 *Quyết định*). Câu 1: giữ BR-15, BR-16; khôi phục thuộc dòng *Lưu trữ sản phẩm* của PRD-00 §3.3. Câu 2: §3 nêu Phụ trách nhận mã nhúng từ Quản trị. Câu 3: BR-12; §12 PRD03 thêm dòng bộ chọn người được giao dùng cùng quy tắc. Câu 4: BR-05 thêm dạng IPv4, nhận `http` và `https`, tên có dấu chấm là tên miền, tên không dấu chấm bị từ chối trừ localhost; §8, §9, §11 thêm ghi chú trang `http`; §12 PRD02 thêm dòng nhận diện thành viên trên trang `http`; AC-14 thêm `erp`, `192.168.1.*`; thêm AC-59–61; D5, D6 cập nhật. Câu 5: BR-03, BR-04 nêu khoá đã ngừng không bị xoá, mỗi khoá cũ ngừng đúng 24 giờ sau lúc bị thay. Câu 6: dòng *Thiết kế*. D1: US-12, FR-20 lên Must. D13: bỏ số feedback chưa kết thúc khỏi khối xác nhận lưu trữ (BR-15, AC-45). Sửa AC-25 (*Cho trước*: K1 là khoá cũ còn hạn). Hết câu hỏi mở: trạng thái *In review* | Quyết định của người dùng (câu 4); SA (thay người dùng) (câu 1–3, 5, 6, D1, D13) |

### Quyết định

Mỗi câu hỏi đã trả lời và nơi quyết định đó nằm hiện nay. Câu ghi "SA (thay người dùng)" do `solution-architect` trả lời thay người dùng; người dùng duyệt các câu này cùng PRD.

| Câu | Phiên bản | Quyết định | Nguồn | Nơi ghi hiện nay |
|---|---|---|---|---|
| 1 | 0.2 | Lưu trữ: widget ngừng với mọi khoá, kể cả khoá cũ còn hạn; feedback, ảnh, bình luận, người được gán giữ nguyên và vẫn xử lý được; tên và cấu hình chỉ xem; gán, đổi, bỏ gán Phụ trách, Viewer vẫn làm được; Quản trị khôi phục được bất cứ lúc nào; khôi phục thuộc dòng *Lưu trữ sản phẩm* của PRD-00 §3.3, không đổi ma trận | SA (thay người dùng) | BR-15, BR-16, AC-45–48 |
| 2 | 0.2 | Mã nhúng, khoá, domain: chỉ Quản trị xem và sửa; Phụ trách cần mã nhúng thì nhận từ Quản trị | SA (thay người dùng) | §3, BR-18, AC-50 |
| 3 | 0.2 | Email trong bộ chọn thành viên và danh sách Phụ trách, Viewer theo PRD-00 BR-74: chỉ hiện và trả về khi người gán có quyền quản lý thành viên; bộ chọn người được giao ở PRD03 dùng cùng quy tắc | SA (thay người dùng) | BR-12, AC-37, AC-38; §12 PRD03 |
| 4 | 0.2 | Domain được phép nhận trang `http` và `https`, và địa chỉ IPv4 đúng từng địa chỉ; tên có dấu chấm (ví dụ `erp.congty.local`) là tên miền; tên không có dấu chấm bị từ chối, trừ localhost. Trên trang `http` widget có thể không nhận ra thành viên đã đăng nhập (PRD02) | Quyết định của người dùng (thay câu trả lời của SA) | BR-05, D5, D6, §8, §11, AC-14, AC-59–61; §12 PRD02 |
| 5 | 0.2 | Không có thao tác ngừng khoá cũ ngay; mỗi khoá cũ ngừng đúng 24 giờ sau lúc bị thay; khoá đã ngừng không bao giờ nhận feedback lại và không bị xoá | SA (thay người dùng) | BR-03, BR-04, AC-23–25 |
| 6 | 0.2 | Thiết kế: chưa có; thiết kế ở giai đoạn 2 sau khi PRD được duyệt, mỗi màn hình một bảng sáng và một bảng tối | SA (thay người dùng) | Dòng *Thiết kế* đầu tài liệu |

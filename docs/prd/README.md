# Tài liệu yêu cầu sản phẩm (PRD)

Mỗi module một PRD, viết từ `_template.md` bởi agent `product-manager`. Thuật ngữ, vai trò, ma trận quyền và vòng đời trạng thái chỉ định nghĩa ở PRD-00; các PRD khác dẫn chiếu về đó.

## Quy trình

1. **Soạn**: `product-manager` soạn bản nháp đầu từ các quyết định đã có. Mỗi PRD chỉ quyết phần của module mình; điều thuộc module khác ghi vào §12 *Chuyển cho PRD khác*. Chỗ hở nào người dùng phải quyết thì thành câu hỏi mở (tối đa khoảng 8 câu mỗi lượt); chỗ còn lại agent viết theo đề xuất và liệt kê ở bảng *Mặc định đề xuất* (`.claude/agents/product-manager.md` quy tắc 5, 7, 8).
2. **Review sớm**: `business-analyst` review ngay bản nháp đầu, trước khi người dùng nhận lượt câu hỏi đầu tiên, để người dùng nhận một danh sách câu hỏi gộp thay vì nhiều lượt nhỏ. Mỗi báo cáo review được phiên chính đăng thành comment trên PR nháp của PRD (`docs/process/lifecycle.md` § Commit tài liệu) và đưa lại cho BA ở lần review sau.
3. **Hỏi người dùng**: phiên chính luôn chuyển các câu BA đánh dấu là mặc định thành *Mặc định đề xuất* (không hỏi), rồi gộp phần còn lại của `product-manager` và BA thành một lượt, tối đa khoảng 8 câu; nếu vượt mức đó, giữ các câu quan trọng nhất và để phần còn lại cho lượt sau. Câu trả lời nào chạm bảo mật, tách dữ liệu giữa tổ chức, hoặc làm phạm vi phình lớn thì phiên chính trình ngay hệ quả và phương án khác **trước khi** chuyển cho agent viết. Lặp đến khi hết câu hỏi mở.
4. **Review cuối và duyệt**: `business-analyst` review lại; người dùng duyệt PRD cùng bảng *Mặc định đề xuất* (gồm cả mức ưu tiên). Trạng thái thành *Approved*; phiên chính commit bản duyệt lên nhánh của PRD, chuyển PR nháp thành sẵn sàng và merge qua `/git-accept` (`docs/process/lifecycle.md` § Commit tài liệu).
5. **Thiết kế**: `ui-designer` thiết kế theo PRD đã duyệt, ghi mã yêu cầu lên từng bảng. Câu hỏi nghiệp vụ phát sinh khi thiết kế quay lại sửa PRD.
6. **Thiết kế kỹ thuật**: `tech-lead` viết tài liệu trong `docs/tech/` từ PRD và thiết kế đã duyệt. Câu hỏi nghiệp vụ phát sinh quay lại sửa PRD.
7. **Kế hoạch và triển khai**: kế hoạch triển khai dẫn chiếu mã FR/AC của PRD và các mục của tài liệu kỹ thuật; feature-map cập nhật cùng PR; `pr-reviewer` kiểm tra rồi merge vào `dev`.

Mọi thay đổi trên PRD đã duyệt đưa PRD về *In review*.

## Danh mục

| Mã | PRD | Trạng thái | Phiên bản | Phụ thuộc | Thiết kế |
|---|---|---|---|---|---|
| PRD00 | [Tổng quan và nền tảng: thuật ngữ, vai trò và quyền, vòng đời trạng thái, tổ chức và sản phẩm](00-tong-quan-va-nen-tang.md) | Draft | 0.7 | — | — |
| PRD01 | Quản lý sản phẩm: sản phẩm, domain được phép, khoá công khai, nút widget, người phụ trách | Chưa bắt đầu | — | 00 | — |
| PRD02 | Thu thập feedback (widget web): form, chụp và đánh dấu ảnh, thông tin kỹ thuật, định danh thành viên, chế độ khách, chống lạm dụng | Chưa bắt đầu | — | 00, 01 | — |
| PRD03 | Xử lý feedback: hộp feedback (danh sách và bảng), chi tiết, đổi trạng thái, giao việc, bình luận, trùng lặp, nhãn | Chưa bắt đầu | — | 00, 02 | — |
| PRD04 | Cổng người báo: Feedback của tôi, trang link riêng của khách, duyệt In review, mở lại | Chưa bắt đầu | — | 03 | — |
| PRD05 | Thông báo: sự kiện, kênh, cài đặt theo từng người, email cho khách, xuyên tổ chức | Chưa bắt đầu | — | 03, 04 | — |
| PRD06 | Tổng quan và chỉ số: số liệu theo sản phẩm, chỉ số thành công, danh sách cần chú ý | Chưa bắt đầu | — | 03 | — |
| PRD07 | Khảo sát: đợt khảo sát, trả lời, ẩn danh, kết quả | Chưa bắt đầu | — | 00 | — |

Để sau: SDK Flutter, connector (Plane, GitHub, GitLab), AI phân loại.

# Tài liệu yêu cầu sản phẩm (PRD)

Mỗi module một PRD, viết từ `_template.md` bởi agent `product-manager`. Thuật ngữ, vai trò, ma trận quyền và vòng đời trạng thái chỉ định nghĩa ở PRD-00; các PRD khác dẫn chiếu về đó.

## Quy trình

1. **Soạn**: `product-manager` soạn từ các quyết định đã có và trả về câu hỏi mở kèm đề xuất; phiên chính hỏi người dùng và chuyển câu trả lời cho agent đến khi hết câu hỏi.
2. **Review**: `business-analyst` soát quy tắc còn thiếu và tiêu chí nghiệm thu không test được.
3. **Duyệt**: người dùng duyệt; trạng thái thành *Approved*, và phiên chính commit PRD qua PR chỉ chứa tài liệu (`docs/process/lifecycle.md` § Commit tài liệu).
4. **Thiết kế**: `ui-designer` thiết kế theo PRD đã duyệt, ghi mã yêu cầu lên từng bảng. Câu hỏi nghiệp vụ phát sinh khi thiết kế quay lại sửa PRD.
5. **Thiết kế kỹ thuật**: `tech-lead` viết tài liệu trong `docs/tech/` từ PRD và thiết kế đã duyệt. Câu hỏi nghiệp vụ phát sinh quay lại sửa PRD.
6. **Kế hoạch và triển khai**: kế hoạch triển khai dẫn chiếu mã FR/AC của PRD và các mục của tài liệu kỹ thuật; feature-map cập nhật cùng PR; `pr-reviewer` kiểm tra rồi merge vào `dev`.

Mọi thay đổi trên PRD đã duyệt đưa PRD về *In review*.

## Danh mục

| Mã | PRD | Trạng thái | Phiên bản | Phụ thuộc | Thiết kế |
|---|---|---|---|---|---|
| PRD00 | Tổng quan và nền tảng: thuật ngữ, vai trò và quyền, vòng đời trạng thái, tổ chức và sản phẩm | Chưa bắt đầu | — | — | — |
| PRD01 | Quản lý sản phẩm: sản phẩm, domain được phép, khoá công khai, nút widget, người phụ trách | Chưa bắt đầu | — | 00 | — |
| PRD02 | Thu thập feedback (widget web): form, chụp và đánh dấu ảnh, thông tin kỹ thuật, định danh thành viên, chế độ khách, chống lạm dụng | Chưa bắt đầu | — | 00, 01 | — |
| PRD03 | Xử lý feedback: hộp feedback (danh sách và bảng), chi tiết, đổi trạng thái, giao việc, bình luận, trùng lặp, nhãn | Chưa bắt đầu | — | 00, 02 | — |
| PRD04 | Cổng người báo: Feedback của tôi, trang link riêng của khách, duyệt In review, mở lại | Chưa bắt đầu | — | 03 | — |
| PRD05 | Thông báo: sự kiện, kênh, cài đặt theo từng người, email cho khách, xuyên tổ chức | Chưa bắt đầu | — | 03, 04 | — |
| PRD06 | Tổng quan và chỉ số: số liệu theo sản phẩm, chỉ số thành công, danh sách cần chú ý | Chưa bắt đầu | — | 03 | — |
| PRD07 | Khảo sát: đợt khảo sát, trả lời, ẩn danh, kết quả | Chưa bắt đầu | — | 00 | — |

Để sau: SDK Flutter, connector (Plane, GitHub, GitLab), AI phân loại.

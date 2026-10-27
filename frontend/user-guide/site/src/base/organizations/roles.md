---
title: Vai trò
description: 'Xem các vai trò của tổ chức, tạo, sửa hoặc xoá vai trò tự tạo.'
permission: organizations.roles.manage
---

# Vai trò

Vai trò là một nhóm quyền giao cho thành viên. Tổ chức nào cũng có sẵn ba vai trò **Chủ sở hữu**, **Admin** và **Member**; bạn tạo thêm vai trò riêng khi cần.

## Trước khi bắt đầu

- Đã chọn đúng tổ chức (xem [chuyển tổ chức](./switch)).

## Xem danh sách vai trò

Bấm **Thành viên** ở thanh bên, rồi bấm tab **Vai trò**. Mỗi dòng có tên vai trò, các quyền của nó và số người đang giữ ("… người"). Vai trò **Chủ sở hữu** ghi "Mọi quyền"; vai trò chưa có quyền nào ghi "Không có quyền nào".

Dòng ghi **Chỉ xem** là vai trò bạn không sửa được. Vai trò có sẵn không sửa hay xoá được. Vai trò tự tạo mà bạn không có đủ mọi quyền trong đó cũng chỉ xem được: "Bạn không có đủ mọi quyền trong vai trò này nên chỉ xem được."

## Tạo vai trò

1. Bấm **Tạo vai trò** ở góc trên phải (**Tạo** trên điện thoại). Bảng **Tạo vai trò** mở ra.
2. Nhập **Tên vai trò** (tối đa 100 ký tự).
3. Ở mục **Quyền**, tick các quyền vai trò này có. Bạn chỉ chọn được quyền mà bạn có.
4. Bấm **Tạo vai trò**. Bấm **Hủy** để bỏ.

## Sửa vai trò

1. Ở dòng của vai trò, bấm nút ba chấm (**Thao tác với vai trò** tên vai trò) rồi chọn **Sửa vai trò**.
2. Đổi tên hoặc các quyền.
3. Bấm **Lưu**.

## Xoá vai trò

1. Ở dòng của vai trò, bấm nút ba chấm rồi chọn **Xoá vai trò**.
2. Ứng dụng hỏi "Xoá vai trò …?". Nếu có lời mời đang chờ với vai trò này, ứng dụng báo chúng sẽ không dùng được nữa. Bấm **Xoá vai trò** để xác nhận.

## Các quyền

| Quyền                     | Cho phép                                      |
| ------------------------- | --------------------------------------------- |
| **Quản lý tổ chức**       | Đổi tên tổ chức và vô hiệu tổ chức.           |
| **Quản lý thành viên**    | Mời, gỡ thành viên và đổi vai trò của họ.     |
| **Quản lý vai trò**       | Tạo, sửa và xoá các vai trò tự tạo.           |
| **Quản lý khoá API**      | Tạo và thu hồi khoá API của tổ chức.          |
| **Xem nhật ký kiểm toán** | Xem lại ai đã làm gì trong tổ chức.           |
| **Quản lý file**          | Quản lý các file đã tải lên của tổ chức.      |
| **Quản lý cài đặt**       | Đổi các cài đặt chung của tổ chức.            |
| **Quản trị FeedbackHub**  | Quản lý sản phẩm và mọi feedback của tổ chức. |

## Kết quả

Ứng dụng báo "Đã tạo vai trò …", "Đã lưu vai trò …" hoặc "Đã xoá vai trò …", và danh sách cập nhật.

## Sự cố thường gặp

- **Báo "Nhập tên vai trò."**: ô **Tên vai trò** đang trống.
- **Báo "Tên này đã có trong tổ chức."**: chọn tên khác.
- **Báo "Tên này trùng tên một vai trò có sẵn."**: chọn tên khác với **Chủ sở hữu**, **Admin** và **Member**.
- **Mục Xoá vai trò bị tắt và dòng ghi "Vai trò còn … người giữ. Đổi vai trò của họ hoặc gỡ họ khỏi tổ chức trước."**: đổi vai trò của những người đó trong trang [Thành viên](./members), hoặc gỡ họ, rồi xoá lại.
- **Một quyền bị tắt trong danh sách**: bạn không có quyền đó nên không giao được.

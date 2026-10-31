# Error Codes - Umi ClassGuard Lite

| Mã lỗi | Ý nghĩa / cách xử lý |
|---|---|
| `CG-0000` | Chưa ghi nhận lỗi. |
| `CG-1001` | Không ghi được dữ liệu hoặc file báo cáo - kiểm tra quyền ghi thư mục. |
| `CG-1002` | Không đọc được cấu hình - kiểm tra `config.json`, có thể dùng cấu hình mặc định. |
| `CG-1003` | Không lưu được cấu hình - kiểm tra quyền ghi `%LOCALAPPDATA%`. |
| `CG-1004` | Không ghi được nhật ký - kiểm tra quyền ghi hoặc dung lượng đĩa. |
| `CG-1101` | Không truy vấn được danh sách tiến trình - có thể bị Windows hạn chế. |
| `CG-1201` | Không thể đóng ứng dụng - có thể không đủ quyền hoặc không có cửa sổ. |
| `CG-1202` | Tên tiến trình không hợp lệ hoặc thuộc danh sách hệ thống được bảo vệ. |
| `CG-2001` | Chưa xác minh được hiệu lực chặn website - kiểm tra `edge://policy` / `chrome://policy` và thử URL. |
| `CG-2002` | Không ghi được chính sách URLBlocklist (HKCU) - có thể bị chính sách trường chặn. |
| `CG-2003` | Chính sách trình duyệt đã tồn tại hoặc bị thay đổi - ứng dụng không ghi đè. |
| `CG-2004` | Không thể khôi phục chính sách trình duyệt - không tự xóa nếu có thay đổi bên ngoài. |
| `CG-3001` | Không thiết lập/đọc được tự khởi động cùng Windows - dùng chế độ mở thủ công. |
| `CG-9001` | Lỗi ứng dụng chưa xử lý - xuất báo cáo chẩn đoán. |
| `CG-9002` | Windows không đáp ứng điều kiện chạy - kiểm tra hệ điều hành và kiến trúc máy. |
| `CG-9003` | Windows/SmartScreen/App Control chặn EXE trước khi chạy - chụp thông báo Windows, ứng dụng không thể tự sinh mã này. |

**Lấy báo cáo:** `Cài đặt → Chẩn đoán và mã lỗi → Tự kiểm tra máy → Sao chép báo cáo` hoặc `Xuất file báo cáo`.

**Lưu ý:** `CG-2001` không xác nhận đã chặn website. Mã `CG-9003` chỉ dùng để tra cứu thủ công nếu ứng dụng chưa khởi chạy.

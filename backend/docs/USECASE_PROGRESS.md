# Tiến độ API theo use case

Trạng thái dưới đây là **backend API**, chưa phải luồng hoàn chỉnh qua frontend.

| Use case | API hiện có |
| --- | --- |
| 2.1.2.1 — Quản lý tài khoản khách hàng | Đăng ký/login/logout, hồ sơ, đổi email/mật khẩu |
| 2.1.2.2 — Xem/tìm kiếm dịch vụ | Danh sách public, tìm kiếm/phân trang, chi tiết active |
| 2.1.2.3 | Bỏ qua theo yêu cầu người dùng |
| 2.1.2.4 — Quản lý đơn của khách hàng | Tạo đơn một dịch vụ, server tính giá/snapshot, chống gửi trùng; danh sách của mình, chi tiết và lịch sử trạng thái |
| 2.1.2.5 — Hủy dịch vụ | Khách gửi/xem yêu cầu; admin duyệt/từ chối; giải phóng phân công và lượt giữ; chưa hoàn tiền |
| 2.1.2.6 — Thanh toán đơn hàng | Chưa có API; VNPay chưa có tài khoản Sandbox |
| 2.1.2.7 — Đánh giá | Gửi/xem đánh giá từng nhân viên; kiểm tra sở hữu, đơn completed và phân công done; chống đánh giá trùng |
| 2.1.2.8 — Lịch sử dịch vụ | Đơn completed, tìm kiếm, lọc thời gian/dịch vụ, phân trang |
| 2.1.2.9 — Nhân viên đăng nhập | Xác thực email, vai trò, trạng thái và phiên qua auth |
| 2.1.2.10 — Nhân viên cập nhật tài khoản | Hồ sơ cá nhân, email, mật khẩu |
| 2.1.2.13 — Admin quản lý tài khoản | Danh sách/tìm email hoặc ID, chi tiết, tạo, sửa email/hồ sơ, khóa/mở |
| 2.1.2.14 — Admin quản lý dịch vụ | Đủ nhóm API danh sách/tìm, chi tiết, thêm, sửa, bật/ngừng; kèm danh mục |
| 2.1.2.15 — Admin quản lý đơn | Danh sách/lọc/chi tiết, xác nhận, duyệt hủy; tiến độ tự tổng hợp từ phân công |
| 2.1.2.16 — Phân công nhân viên | Nhiều nhân viên/đơn, gỡ phân công, kiểm tra chi nhánh/kỹ năng/lịch; workflow bền vững chống lặp |
| 2.1.2.17 — Quản lý lịch làm việc | Admin quản lý khoảng available/unavailable; nhân viên xem lịch/công việc và cập nhật tiến độ; chống trùng lịch đồng thời |
| 2.1.2.18 — Quản lý thanh toán | Chưa có API |
| 2.1.2.19 — Quản lý đánh giá | Admin xem/lọc/chi tiết, ẩn/hiện có lý do; public chỉ thấy đánh giá được hiển thị và điểm trung bình tương ứng |
| 2.1.2.20 — Thống kê/báo cáo | Chưa có API |

Voucher, gói dịch vụ và quyền sử dụng thuộc phạm vi đã yêu cầu nhưng hiện mới có schema,
chưa có API nghiệp vụ. Chat/AI để sau. Không tự thêm use case vào các mục 11/12 chưa có
trong tài liệu đã nhận.

Tài liệu endpoint: [Auth/User](AUTH_USER_API.md), [Catalog](CATALOG_API.md),
[Đơn và lịch sử khách hàng](CUSTOMER_ORDERS_API.md),
[Đơn, nhân viên, lịch và đánh giá](ORDER_STAFF_REVIEW_API.md).

Đã kiểm thử tích hợp nhóm workflow/đánh giá trên database QA riêng, gồm cạnh tranh đặt lịch,
retry khi staff-service gián đoạn, phân quyền và kiểm duyệt. Migration `002_workflows.sql`
của order/staff đã được áp dụng trên MySQL local. Sự kiện hủy đơn mới được lưu bền vững;
chưa có consumer thanh toán/hoàn tiền. API voucher/gói, thanh toán và báo cáo
vẫn cần triển khai; không coi các luồng này đã hoàn thành chỉ vì đã có schema.

API đặt dịch vụ đã nối frontend: [BOOKING_API.md](BOOKING_API.md). Đã kiểm thử tạo đơn
thành công từ Chrome và admin đọc cùng đơn, cùng các trường hợp HTTP sai quyền/dữ liệu.

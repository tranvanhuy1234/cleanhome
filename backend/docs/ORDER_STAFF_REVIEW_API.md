# API đơn hàng, nhân viên và đánh giá

Luồng hủy nhận việc, tìm người thay thế, admin hủy/dừng, thùng rác và thông báo: [ORDER_LIFECYCLE_API.md](ORDER_LIFECYCLE_API.md).

Các đường dẫn dưới đây đi qua gateway. Gửi `Authorization: Bearer <token>` trừ API đánh giá công khai. Thời gian dùng UTC.

## Đơn hàng

Khách hàng:
- `POST /api/orders/me/{id}/cancellations`: `{ "reason": "...", "expectedVersion": 1 }`.
- `GET /api/orders/me/{id}/cancellations` và `/assignments`: chỉ xem đơn của mình.

Admin, tiền tố `/api/orders/admin/orders`:
- `GET /`, `GET /{id}`: danh sách, lọc, chi tiết và lịch sử.
- `POST /{id}/confirm`: xác nhận đơn pending; body gồm `note`, `expectedVersion`.
- `GET /{id}/cancellations`.
- `POST /{id}/cancellations/{requestId}/reject`: body `note`, `expectedVersion`.
- `POST /{id}/cancellations/{requestId}/approve`: thêm `idempotencyKey`.
- `POST /{id}/assignments`: body `employeeIds`, `requiredSkillIds`, `idempotencyKey`, `expectedVersion`.
- `POST /{id}/unassign`: body `assignmentId`, `idempotencyKey`, `expectedVersion`.

Nhân viên gọi `POST /api/orders/staff/orders/{id}/progress` với `assignmentId`, `nextStatus`, `idempotencyKey`, `expectedVersion`. Chỉ chủ phân công được chuyển `waiting → accepted → in_progress → done`. Đơn chỉ completed khi mọi phân công còn hiệu lực đều done.

Các thao tác xuyên service trả operation. `202` nghĩa là đang chờ xử lý, không phải đã hoàn thành. Đọc `GET /api/orders/operations/{id}` đến trạng thái `succeeded` hoặc `failed`. Khi thử lại cùng nội dung phải giữ nguyên `idempotencyKey`; đổi nội dung với cùng khóa bị từ chối. Chỉ chủ thao tác hoặc admin đọc được operation. Worker tự thử lại khi staff-service mất kết nối; inbox staff chống thực hiện trùng.

Khách chỉ yêu cầu hủy trước khi bắt đầu. Yêu cầu hủy pending chặn phân công và bắt đầu công việc. Duyệt hủy giải phóng phân công, lượt voucher/quyền gói đang giữ, ghi lịch sử và sự kiện bền vững. **Chưa hoàn tiền**: `refundAssessmentPending` chỉ thông báo cần xử lý tài chính tiếp.

## Nhân viên và lịch

Admin dùng tiền tố `/api/staff/admin`:
- `GET/POST branches`, `GET/PUT branches/{id}`.
- `GET/POST employees`, `GET/PUT employees/{id}`.
- `GET/POST employees/{id}/availability`, `PUT/DELETE employees/{id}/availability/{availabilityId}`.
- `GET/POST skills`, `PUT skills/{id}`.
- `GET/PUT employees/{id}/skills`.
- `GET bookings/{id}/assignments`.

Nhân viên đọc `GET /api/staff/me/profile`, `/jobs`, `/availability`.

Nhân viên đọc `GET /api/orders/staff/orders/{id}/destination` để nhận địa chỉ snapshot,
người liên hệ, điện thoại và tọa độ. Chỉ user sở hữu phân công accepted/in_progress
của đơn pending/confirmed/in_progress được xem; pending chỉ có ý nghĩa khi phân công đã được backend nhận thành công và workflow đang đồng bộ đơn. Sai phân công, đã gỡ hoặc đã hoàn thành trả 404.
DTO này không trả tổng tiền hay ID khách. Lỗi staff-service không được bỏ qua để lộ địa chỉ.
Tọa độ thiếu/sai giới hạn trả null, địa chỉ chữ cũ (`address`) được hỗ trợ cùng `addressText`.

Trang nhân viên có nút **Dẫn đường đến nhà khách**: mở HTTPS Google Maps với
`api=1`, `destination` và `dir_action=navigate`, ưu tiên tọa độ; không tự tạo tọa độ.
Không chỉ định origin để Maps dùng vị trí thiết bị, không gửi vị trí nhân viên về backend.
Thiết bị/Maps có thể hiển thị bản xem trước thay vì dẫn đường nếu vị trí hiện tại không sẵn sàng.
Tham khảo [Google Maps URLs](https://developers.google.com/maps/documentation/urls/get-started).

Tài khoản tạo hồ sơ nhân viên phải có role staff và active. Phân công yêu cầu nhân viên active, verified, cùng chi nhánh active, đủ kỹ năng được admin chỉ định, có khoảng available phủ toàn bộ công việc và không đè khoảng unavailable hay công việc khác. Khóa hàng nhân viên ngăn hai yêu cầu đồng thời đặt trùng. Thay lịch/kỹ năng/hồ sơ không được phá vỡ phân công đang có. Chưa tự suy ra kỹ năng từ dịch vụ; admin gửi `requiredSkillIds` khi phân công.

## Đánh giá từng nhân viên

- Khách: `POST /api/reviews/me` với `bookingId`, `employeeId`, `rating` (1–5), `comment`; `GET /api/reviews/me` và `/{id}`.
- Admin: `GET /api/reviews/admin/reviews` và `/{id}`; `PATCH /{id}/visibility` với `isHidden`, `reason` (bắt buộc khi ẩn).
- Công khai: `GET /api/reviews/employees/{id}`; chỉ hiện đánh giá không bị ẩn, kèm phân trang và điểm trung bình trên toàn bộ kết quả lọc.

Chỉ khách sở hữu đơn completed mới được đánh giá nhân viên có phân công done. Mỗi cặp đơn/nhân viên chỉ một đánh giá. Dữ liệu public không chứa ID khách, đơn hoặc thông tin kiểm duyệt.

## Schema và kiểm tra

Áp dụng thêm `databases/order-db/002_workflows.sql` và `databases/staff-db/002_workflows.sql` sau schema ban đầu. Các script bổ sung bảng bằng `CREATE TABLE IF NOT EXISTS`, không reset dữ liệu.

Kiểm thử tích hợp: `dotnet run --project tests/CleanHome.IntegrationTests -- --review-workflows` tại thư mục backend. Bộ này tạo DB QA riêng, kiểm tra hủy/phân công/lịch/progress/đánh giá, cạnh tranh đặt lịch và retry khi staff-service tắt, rồi dọn DB QA.

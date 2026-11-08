# GPS, phân phối đơn và chỉ đường

Frontend Next.js dùng gateway `http://localhost:4000/api`. Backend giữ kiến trúc Controller → Service/Repository → MariaDB, xác thực phiên và phân quyền hiện có. Không cần Google Maps API key.

## Luồng sử dụng

1. Khách chọn địa chỉ liên hệ của mình, sửa địa chỉ vệ sinh nếu cần, chọn ghim Leaflet/OpenStreetMap và xác nhận điểm. Nút lấy vị trí hiện tại chỉ chạy khi khách bấm, không tự coi GPS hiện tại là địa điểm vệ sinh.
2. Frontend tạo đơn với `automaticDispatch: true`, `requiredStaff` từ 1 đến 20 và `location: { addressText, latitude, longitude, confirmed: true }`. Backend kiểm tra quyền sở hữu địa chỉ, tọa độ và xác nhận; lưu snapshot địa điểm vào đơn cùng hàng đợi trong một transaction. Giá và thời lượng vẫn theo dịch vụ/số lượng; số nhân viên không tự nhân giá. Thanh toán vẫn `unpaid`.
3. Worker gửi snapshot qua API nội bộ có service key, có retry. Deadline tìm người tính từ thời điểm tạo đơn và không vượt thời gian bắt đầu. Lỗi mạng không làm mất yêu cầu và không tự tạo đơn khác.
4. Nhân viên đã xác minh, tài khoản/chi nhánh hoạt động, bật sẵn sàng, đủ lịch/kỹ năng và không trùng ca được xét. GPS phải đủ mới và đủ chính xác. Tìm theo bán kính Haversine 5 km, tiếp theo 10 km; giai đoạn cuối tiếp tục tìm người mới sẵn sàng đến hết deadline, có dự phòng cùng chi nhánh nếu admin cho phép. Khoảng cách là đường chim bay.
5. Gửi đồng thời theo nhóm, mỗi lời mời có hạn riêng. Nhận đơn qua workflow bền vững có idempotency, khóa đơn/nhân viên và kiểm tra hạn tại SQL giữ vị trí. Nhận đủ người mới đóng lời mời còn lại. Phân công thủ công cũng chịu giới hạn số chỗ/trùng ca. Chưa đủ người không được bắt đầu đơn tự động.
6. Nhân viên nhận thành công xem bản đồ/liên hệ và mở Google Maps theo tọa độ đích. Mỗi người trong đơn nhiều nhân viên có quyền riêng. Thống kê chia đều giá trị đơn theo cơ chế hiện có, không đổi công thức.

API cũ không gửi `automaticDispatch` vẫn dùng quy trình thủ công. Đơn tự động thiếu ghim đã xác nhận trả lỗi thay vì ngầm dùng GPS thiết bị. Admin có thể hỗ trợ khi khách chưa xác định được điểm.

## API

Tất cả route dưới đây cần bearer token và đúng vai trò. Tiền tố là `/api`.

| Route | Quyền / công dụng |
| --- | --- |
| `POST /orders/booking` | Khách tạo đơn; bổ sung các trường ở trên |
| `GET /orders/dispatch/bookings/{bookingId}` | Chủ đơn/admin xem số đã nhận, số cần, trạng thái và hạn tìm |
| `GET/PUT /staff/me/dispatch/presence` | Nhân viên đọc/cập nhật trạng thái của chính mình |
| `GET /staff/me/dispatch/offers` | Lời mời hợp lệ của chính nhân viên |
| `GET /staff/me/dispatch/offers/{id}` | Lời mời của chính nhân viên, vẫn che địa điểm |
| `POST /staff/me/dispatch/offers/{id}/skip` | Bỏ qua lời mời |
| `POST /orders/dispatch/offers/{id}/accept` | Nhận đơn, body `{ "idempotencyKey": "unique-key" }`; kết quả workflow có thể pending |
| `GET /orders/operations/{id}` | Đồng bộ kết quả workflow theo quyền hiện có |
| `GET /staff/me/dispatch/events` | SSE với Authorization header; thông báo lời mời và thay đổi công việc |
| `POST /staff/me/dispatch/jobs/{assignmentId}/arrive` | Nhân viên đã nhận báo đến nơi |
| `POST /staff/me/dispatch/jobs/{assignmentId}/support` | Gửi `{ "reason": "..." }` cho admin hỗ trợ/xin rút |
| `GET /orders/staff/orders/{bookingId}/summary` | Tóm tắt công việc của chính nhân viên; không chứa liên hệ/tọa độ |
| `GET /staff/admin/dispatch/settings` | Admin đọc cấu hình |
| `PUT /staff/admin/dispatch/settings` | Admin lưu cấu hình |
| `GET /staff/admin/dispatch/jobs` | Danh sách đợt tìm nhân viên |
| `GET /staff/admin/dispatch/ready` | Số sẵn sàng theo chi nhánh |
| `GET /staff/admin/dispatch/jobs/{id}/audit` | Nhật ký điều phối |
| `POST /staff/admin/dispatch/jobs/{id}/pause` | Dừng lời mời cho đơn |
| `POST /staff/admin/dispatch/jobs/{id}/resume` | Mở lại đợt tìm, giữ người đã nhận |
| `GET/PUT /staff/admin/dispatch/services/{serviceId}/skills` | Mảng ID kỹ năng cần cho dịch vụ |
| `GET /staff/admin/dispatch/support` | Yêu cầu hỗ trợ đang chờ |
| `POST /staff/admin/dispatch/support/{id}/resolve` | Đánh dấu đã xử lý |

API lấy điểm đến hiện có chỉ trả địa chỉ/liên hệ/tọa độ khi backend xác nhận assignment của người gọi đã `accepted`/`in_progress`, đơn còn hiệu lực. Assignment đang chờ, người ngoài đơn và nhân viên thua lượt nhận không được lấy điểm đến. Route `/internal/*` bị gateway chặn; chỉ giao tiếp service-to-service với khóa riêng.

Body bật GPS: `{ "ready": true, "latitude": 10.77, "longitude": 106.70, "accuracy": 20, "observedAt": "2026-11-07T03:00:00Z" }`. Backend kiểm tra thời gian thực, không dùng timestamp mẫu này. Tắt bằng `{ "ready": false }` sẽ xóa tọa độ ngay. Bật không GPS chỉ được khi dự phòng chi nhánh được phép và nhân viên chủ động chọn.

Mặc định: `enabled=true`, `offerSeconds=60`, `searchSeconds=180`, `positionTtlSeconds=300`, `maxAccuracyMeters=500`, `firstRadiusKm=5`, `secondRadiusKm=10`, `groupSize=10`, `allowBranchFallback=true`. Hạn toàn đợt phải lớn hơn hoặc bằng hạn lời mời. Không cấu hình kỹ năng cho dịch vụ đồng nghĩa không thêm điều kiện kỹ năng ngoài hồ sơ đã xác minh.

## Dữ liệu, quyền riêng tư và vận hành

- Migration bổ sung: `databases/order-db/004_dispatch.sql` và `databases/staff-db/004_dispatch.sql`, chạy sau các migration hiện có. Chỉ thêm bảng/cấu hình mặc định nếu thiếu, không reset dữ liệu.
- Order lưu `booking_dispatch`; staff lưu cấu hình, GPS mới nhất, đợt tìm, khóa đơn, lời mời, nhật ký, đến nơi và hỗ trợ. Khóa/tombstone hủy đơn ngăn retry đăng ký chậm mở lại đơn đã hủy.
- Lời mời và SSE chỉ chứa khu vực chi nhánh, dịch vụ, thời gian, giá trị/suất ước tính, số chỗ, hạn và khoảng cách làm tròn. Không có tên khách, điện thoại, địa chỉ chi tiết hoặc tọa độ đích. Không có API công khai trả vị trí nhân viên.
- Chỉ lấy GPS khi bấm bật/cập nhật. Không `watchPosition`, không theo dõi nền. GET trạng thái định kỳ không thu GPS. Tắt xóa tọa độ ngay; worker xóa vị trí quá 24 giờ. TTL hết hạn sẽ không dùng điểm cũ để lọc GPS; dự phòng chi nhánh chỉ nếu được cho phép.
- SSE dùng fetch với bearer header, kết nối lại sau gián đoạn, kiểm tra lại phiên định kỳ; không đưa token vào URL. Popup/badge theo lời mời thật; âm báo chỉ bật sau thao tác người dùng. Không bảo đảm thông báo khi đóng trình duyệt.
- Bản đồ dùng tile OpenStreetMap có attribution, không tải hàng loạt/geocoding tự động. Có thể đổi `NEXT_PUBLIC_MAP_TILE_URL` sang nhà cung cấp phù hợp trước khi build. Tile công cộng không có SLA; nhận đơn và lưu tọa độ không phụ thuộc tile tải thành công.
- Google Maps mở bằng URL chỉ đường đến tọa độ đã lưu, không gọi API trả phí. GPS trên thiết bị thật cần HTTPS hoặc localhost và quyền trình duyệt. Sai số/khả năng mở app Maps tùy thiết bị.
- Dự phòng hiện tại theo chi nhánh đã chọn; không tự suy ra địa giới hay tìm toàn quốc. Mỗi lần xét tối đa 1.000 ứng viên/chi nhánh. Quy mô lớn cần tối ưu truy vấn/chia lô.
- Xin hỗ trợ/xin rút tạo yêu cầu admin, không tự giải phóng vị trí. Admin dùng luồng gỡ/phân công hiện có khi được phép; thay người đang thực hiện vẫn cần xử lý nghiệp vụ, không tự động bỏ qua quy trình.

## Kiểm tra

`backend/tests/CleanHome.IntegrationTests` có ca kiểm tra GPS sai/cũ, bán kính/kỹ năng, che tọa độ, tranh chỗ cuối, ba người nhận, trùng ca, lời mời hết hạn, hủy/retry đăng ký chậm, tìm người mới bật sẵn sàng, SSE và quyền điểm đến. Các ca chạy trên database QA riêng và tự dọn, không xóa database ứng dụng.

`frontend/tests/browser-gps.cjs` kiểm tra bản đồ/ghim/xác nhận, từ chối quyền GPS, giao diện di động và trang cấu hình bằng Chrome QA local. Không lưu tọa độ mô phỏng hoặc tạo đơn trong database thật.

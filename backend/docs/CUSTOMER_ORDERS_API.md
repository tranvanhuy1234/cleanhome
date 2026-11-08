# API đơn và lịch sử dịch vụ của khách hàng

Nhóm API backend cho use case **2.1.2.4** và **2.1.2.8**. Base URL gateway:
`http://localhost:4000`. Yêu cầu bearer token vai trò customer. Admin/staff dùng các
API nghiệp vụ riêng sau này, không đọc đơn của người khác qua `/me`.

| Method | Endpoint | Use case |
| --- | --- | --- |
| GET | `/api/orders/me` | Danh sách đơn của khách đang đăng nhập — 2.1.2.4 |
| GET | `/api/orders/me/{id}` | Chi tiết đơn và tiến trình — 2.1.2.4 |
| GET | `/api/orders/me/history` | Lịch sử đơn đã hoàn thành — 2.1.2.8 |

## Danh sách và tìm kiếm

Cả danh sách và lịch sử nhận:

| Query | Quy tắc |
| --- | --- |
| `page` | 1–100000, mặc định 1 |
| `pageSize` | 1–100, mặc định 20 |
| `search` | Tối đa 150 ký tự, tìm theo mã đơn hoặc tên dịch vụ snapshot; trim khoảng trắng |
| `serviceId` | ID dịch vụ nguyên dương, lọc đơn có chứa dịch vụ đó |
| `from` | Thời điểm bắt đầu lịch thực hiện, bao gồm mốc này |
| `to` | Thời điểm kết thúc khoảng lọc, không bao gồm mốc này |

Thời gian lọc là **scheduled_start** của đơn, không phải created_at hoặc ngày thanh toán.
Nên dùng ISO 8601 có offset (UTC `Z` hoặc `+07:00`, URL-encode dấu `+`). Server chuyển
về UTC trước khi truy vấn. Nếu có cả hai mốc thì from phải nhỏ hơn to.
Ví dụ lọc các đơn dự kiến thực hiện trong tháng 1 theo UTC:

```http
GET /api/orders/me/history?from=2026-01-01T00:00:00Z&to=2026-02-01T00:00:00Z&serviceId=11
Authorization: Bearer <customer-token>
```

`/me` nhận thêm `status`: pending, confirmed, in_progress, completed, cancelled; bỏ trống
để lấy mọi trạng thái. `/me/history` luôn chỉ trả completed, không dùng query status
để mở rộng sang đơn đang chờ hoặc đã hủy. Lịch sử được hiểu là dịch vụ đã sử dụng/hoàn thành.

Response có `items`, `page`, `pageSize`, `total`. Mỗi item gồm `id`, `code`, `scheduledStart`,
`scheduledEnd`, `status`, `totalAmount`, `createdAt`. Sắp xếp scheduledStart giảm dần,
sau đó ID giảm dần để ổn định khi trùng lịch. Total là tổng sau lọc; dùng EXISTS khi lọc
booking_items để một đơn nhiều dịch vụ không bị lặp. Không tìm thấy/vượt trang trả 200
với items rỗng. `%`, `_`, `!` được tìm theo nghĩa đen; không phân biệt hoa thường theo DB.

## Chi tiết đơn

`GET /api/orders/me/123` trả các trường của summary cộng:

- `customerId`, `branchId`, `addressSnapshot`, `specialRequest`, `subtotalAmount`,
  `discountAmount`, `version`.
- `items`: `id`, `serviceId`, `serviceName`, `quantity`, `unitPrice`, `unit`, `entitlementId`.
- `history`: `id`, `status`, `note`, `changedBy`, `createdAt`, sắp xếp thời gian tăng dần
  rồi theo ID. Notes trong bảng này được dùng để hiển thị cho khách, không lưu bí mật ở đây.

Địa chỉ, tên dịch vụ, đơn vị và giá lấy từ snapshot của đơn; không ghép dữ liệu catalog
hiện tại để ghi đè thông tin lịch sử. Các số tiền giữ DECIMAL; API chỉ đọc, không tính lại
giá hoặc giảm giá. Tên chi nhánh/nhân viên và trạng thái thanh toán chưa được ghép từ service khác.

Mọi truy vấn đều giới hạn customer_id theo phiên. Query customerId/userId không thể thay
chủ sở hữu. Đơn không tồn tại hoặc không thuộc khách đều trả cùng lỗi 404. Không đọc
items/history trước khi xác nhận quyền trên đơn. Các phần chi tiết được đọc cùng snapshot.
Response cá nhân có Cache-Control: no-store.

## Lỗi, chạy và kiểm thử

- 400: ID không dương, phân trang/trạng thái/thời gian/tiêu chí lọc không hợp lệ.
- 401: chưa đăng nhập hoặc phiên mất hiệu lực.
- 403: vai trò khác customer.
- 404: không có đơn thuộc khách; ID ngoài long không khớp route.
- 503: lỗi truy vấn MariaDB hoặc auth-service không sẵn sàng, không bỏ qua xác thực.

Khởi chạy auth-service, user-service (để đăng ký hồ sơ), order-service cổng 5004 và
gateway. Order-service dùng cấu hình local riêng, không đọc file .env tự động.
Không cần chạy catalog/staff/payment cho các API đọc này.

```powershell
dotnet build CleanHome.slnx --no-restore
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --customer-orders
```

Test dùng ba DB QA auth/user/order ngẫu nhiên, tạo hai khách hàng, kiểm tra quyền sở hữu,
snapshot, lịch sử, tìm/lọc, phân trang, validation và auth ngừng hoạt động; sau đó dọn
đúng dữ liệu QA. Không tạo đơn mẫu trong database ứng dụng.

Mục 2.1.2.3 vẫn được bỏ qua. Chưa có API tạo đơn, gửi/duyệt hủy, xác nhận/phân công,
thanh toán hoặc cập nhật trạng thái ở nhóm này. Frontend chưa nối API.

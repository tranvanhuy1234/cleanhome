# API xem và tìm kiếm dịch vụ — use case 2.1.2.2

```http
GET http://localhost:4000/api/catalog/services?search=sofa&page=1&pageSize=20
```

API public, không yêu cầu token. Khởi chạy gateway và service-service (cổng 5003),
cấu hình kết nối `cleanhome_service` trong file local như [README](../README.md).
Không cần khởi động auth/user để dùng endpoint này.

| Query | Quy tắc |
| --- | --- |
| `search` | Tìm một phần **tên dịch vụ**, tối đa 150 ký tự, trim khoảng trắng |
| `page` | 1–100000, mặc định 1 |
| `pageSize` | 1–100, mặc định 20 |

Bỏ search hoặc chỉ nhập khoảng trắng sẽ lấy toàn bộ danh sách đang cung cấp.
Tìm kiếm theo collation `utf8mb4_unicode_ci` của MariaDB, không phân biệt hoa thường.
`%`, `_`, `!` được tìm theo nghĩa đen. Chưa tìm theo mô tả hoặc lọc danh mục.
Chỉ trả dịch vụ `is_active=1` thuộc danh mục `is_active=1`. Sắp xếp theo ID giảm dần.

Response 200:

```json
{
  "items": [
    {
      "id": 2,
      "categoryId": 1,
      "categoryName": "Vệ sinh nội thất",
      "name": "Vệ sinh sofa",
      "description": "Làm sạch sofa tại nhà",
      "unit": "bộ",
      "basePrice": 300000.00,
      "durationMinutes": 90
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

Giá là giá cơ bản/tham khảo trong schema, không phải báo giá cuối cùng cho đơn.
Danh sách và total được đọc trong cùng transaction snapshot để nhất quán khi phân trang.
Không có dữ liệu, không tìm thấy hoặc vượt trang trả 200 với `items: []`; total vẫn là
tổng số kết quả sau lọc. Query không hợp lệ trả 400 với validation errors; lỗi DB trả
503 Problem Details, không trả SQL/connection string.

## Xem chi tiết dịch vụ

```http
GET http://localhost:4000/api/catalog/services/2
```

API public, không yêu cầu token. Trả 200 với một `ServiceResponse`, không bọc trong
items/page/total:

```json
{
  "id": 2,
  "categoryId": 1,
  "categoryName": "Vệ sinh nội thất",
  "name": "Vệ sinh sofa",
  "description": "Làm sạch sofa tại nhà",
  "unit": "bộ",
  "basePrice": 300000.00,
  "durationMinutes": 90
}
```

Chỉ trả dịch vụ và danh mục đang hoạt động, cùng quy tắc với danh sách. Dịch vụ không
tồn tại, ngừng cung cấp hoặc thuộc danh mục đã ẩn đều trả 404 với cùng thông báo công
khai. ID bằng 0/âm trả 400; ID không biểu diễn được bằng long không khớp route (404).
Lỗi truy vấn MariaDB trả 503 Problem Details. Endpoint chỉ đọc; không thay đổi dữ liệu,
không trả trạng thái nội bộ, không truy vấn sang database khác. Giá là giá tham khảo.
Chưa bổ sung đánh giá, gói hay nhân viên vào thông tin chi tiết.

## Quản lý dịch vụ và danh mục — use case 2.1.2.14

Toàn bộ endpoint dưới đây yêu cầu `Authorization: Bearer <token>` với vai trò admin.
Chưa đăng nhập trả 401; customer/staff trả 403. Response quản trị có `Cache-Control: no-store`.

| Method | Endpoint sau `/api/catalog` | Chức năng |
| --- | --- | --- |
| GET | `/admin/services` | Danh sách/tìm kiếm cả dịch vụ đang hoạt động và đã ngừng |
| GET | `/admin/services/{id}` | Chi tiết quản trị, bao gồm trạng thái |
| POST | `/admin/services` | Tạo dịch vụ, trả 201 và Location |
| PUT | `/admin/services/{id}` | Cập nhật đầy đủ dịch vụ, trả 200 |
| PATCH | `/admin/services/{id}/status` | Bật/ngừng cung cấp, trả 200 |
| GET | `/admin/categories` | Danh sách/tìm kiếm danh mục |
| GET | `/admin/categories/{id}` | Chi tiết danh mục |
| POST | `/admin/categories` | Tạo danh mục, trả 201 và Location |
| PUT | `/admin/categories/{id}` | Cập nhật danh mục, trả 200 |
| PATCH | `/admin/categories/{id}/status` | Bật/ngừng hoạt động danh mục, trả 200 |

Danh sách quản trị nhận `page`, `pageSize`, `search` như API public, thêm `isActive=true|false`
(bỏ trống để lấy cả hai trạng thái). Danh sách dịch vụ thêm `categoryId` nguyên dương.
Các điều kiện lọc được kết hợp, total phản ánh kết quả sau lọc, ID sắp xếp giảm dần;
không tìm thấy trả 200 với items rỗng. Query sai trả 400. Tìm theo tên, không theo mô tả.

Body POST/PUT dịch vụ (tất cả trường bắt buộc):

```json
{
  "categoryId": 1,
  "name": "Vệ sinh sofa",
  "description": "Làm sạch sofa tại nhà",
  "unit": "bộ",
  "basePrice": 300000.00,
  "durationMinutes": 90,
  "isActive": true
}
```

Tên 2–150 ký tự sau trim; mô tả tối đa 10000 ký tự; unit tối đa 20 ký tự, không để trắng.
Giá từ 0 đến 9999999999.99, tối đa hai chữ số thập phân, phù hợp DECIMAL(12,2).
DurationMinutes là số nguyên dương. Request từ chối các trường ngoài hợp đồng, ví dụ role/id.
Response dịch vụ quản trị gồm các trường public cộng `isActive` và `categoryIsActive`.

Body POST/PUT danh mục:

```json
{ "name": "Vệ sinh nội thất", "description": "Các dịch vụ vệ sinh nội thất", "isActive": true }
```

Tên danh mục 2–100 ký tự sau trim; mô tả bắt buộc, tối đa 10000 ký tự.
Response danh mục gồm `id`, `name`, `description`, `isActive`.
Schema hiện không có unique tên, nên API không tự áp đặt tên danh mục/dịch vụ duy nhất.
Body PATCH status cho cả hai loại: `{ "isActive": false }` hoặc true. Không được bỏ trường này.

Quy tắc xử lý:

- ID không dương trả 400, không tồn tại trả 404. ID ngoài kiểu long không khớp route.
- Danh mục phải tồn tại. Không tạo, sửa hay bật dịch vụ active trong danh mục inactive (409).
  Có thể lưu dịch vụ inactive trong danh mục inactive để quản lý bản chưa cung cấp.
- Ngừng danh mục khiến các dịch vụ bên trong biến mất khỏi API public, nhưng giữ nguyên
  cờ active riêng của từng dịch vụ. Bật lại danh mục sẽ hiển thị lại các dịch vụ còn active;
  dịch vụ đã tự ngừng vẫn bị ẩn cho đến khi admin bật lại riêng.
- Không cung cấp DELETE vật lý cho dịch vụ/danh mục. Việc ngừng cung cấp giữ ID, bản ghi
  và liên kết package_items/voucher_services; không thay đổi snapshot lịch sử ở service khác.
- Ghi dữ liệu và kiểm tra danh mục được thực hiện trong transaction có khóa. Request lỗi
  không ghi dở dang; không có JOIN hoặc cập nhật xuyên database.
- Thay giá chỉ cập nhật catalog, không tự đổi đơn đã đặt hoặc giá chốt của gói đã mua.

Code được tách Controller → Service → Repository, có interface, Models, DTOs và
Configuration riêng. Chưa có API gói/voucher ở bước này; chưa nối frontend và không
thêm dữ liệu mẫu vào DB thật. API public bỏ qua bearer header để vẫn dùng được khi
auth-service ngừng, trong khi API quản trị trả 503 khi không xác thực được với auth.

Kiểm thử từ `backend`:

```powershell
dotnet build CleanHome.slnx --no-restore
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --catalog-list-only
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --catalog-detail-only
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --catalog-management
```

Test tạo đúng một database QA có tên ngẫu nhiên trên MariaDB, chỉ chạy catalog/gateway,
kiểm tra HTTP, rồi dừng tiến trình và xóa database QA. Không dùng dữ liệu `cleanhome_service`.
Thông tin kết nối QA lấy từ `CLEANHOME_TEST_ADMIN_CONNECTION` hoặc file local của auth
như bộ test hiện có; cần quyền tạo/xóa database QA.

Chế độ `--catalog-management` dùng ba database QA auth/user/catalog và khởi động bốn
tiến trình tương ứng cùng gateway. Kiểm tra cả 10 route, quyền admin/customer/staff,
validation, lọc/phân trang, quy trình bật/ngừng và giữ liên kết gói. Sau test, dừng đúng
các tiến trình đã tạo và xóa đúng database QA; không chạm dữ liệu ứng dụng.

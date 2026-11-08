# CleanHome backend — ASP.NET Core Web API

## Phạm vi đã tạo

Solution dùng SDK **10.0.101**, target `net10.0`, giữ nguyên các thư mục service đã chia.
Đã triển khai API đăng ký/đăng nhập email, phiên đăng nhập, đổi mật khẩu/email, quản lý
tài khoản theo quyền admin, hồ sơ cá nhân và CRUD địa chỉ. Xem [hợp đồng API](docs/AUTH_USER_API.md).
Gateway, CORS, xử lý lỗi và kiểm tra kết nối MariaDB đã có. Catalog có nhóm API
[danh sách/tìm kiếm, chi tiết và quản trị dịch vụ/danh mục](docs/CATALOG_API.md).
Use case 2.1.2.14 có đầy đủ nhóm API quản lý dịch vụ. Order-service có
[API danh sách/chi tiết đơn và lịch sử của khách](docs/CUSTOMER_ORDERS_API.md) cho 2.1.2.4/2.1.2.8.
Các service staff, payment và review hiện vẫn là khung; API voucher, gói, phân công,
quản trị/cập nhật đơn và VNPay
chưa được triển khai. Frontend chưa nối API, vẫn dùng demo. Các file Node.js TODO cũ
được giữ lại, không tham gia build .NET.

| Service | Cổng local | Prefix gateway | Database | Trách nhiệm |
| --- | --- | --- | --- | --- |
| api-gateway | 4000 | — | — | Định tuyến, CORS cho ba frontend |
| auth-service | 5001 | `/api/auth` | `cleanhome_auth` | Email, mật khẩu, vai trò, phiên |
| user-service | 5002 | `/api/users` | `cleanhome_user` | Hồ sơ, địa chỉ, thông báo |
| service-service | 5003 | `/api/catalog` | `cleanhome_service` | Danh mục, dịch vụ, gói, cấu hình voucher |
| order-service | 5004 | `/api/orders` | `cleanhome_order` | Đơn, hủy, gói đã mua, quyền sử dụng, lượt dùng voucher |
| staff-service | 5005 | `/api/staff` | `cleanhome_staff` | Chi nhánh, nhân viên, kỹ năng, lịch, phân công |
| payment-service | 5006 | `/api/payments` | `cleanhome_payment` | Thanh toán đơn/gói, VNPay, phân chia doanh thu |
| review-service | 5007 | `/api/reviews` | `cleanhome_review` | Đánh giá từng nhân viên trong đơn |

Gateway loại prefix trước khi chuyển tiếp: `/api/auth/health/live` →
auth-service `/health/live`. Catalog hiện có `GET /services` cho danh sách/tìm kiếm và
`GET /services/{id}` cho chi tiết;
`/packages`, `/vouchers` sẽ triển khai sau. Auth và user có endpoint nghiệp vụ; các
route `/admin/services`, `/admin/categories` trong catalog yêu cầu admin.
Order-service có `GET /me`, `/me/{id}`, `/me/history` cho khách; staff/payment/review
hiện chỉ có health và trang thông tin. Xem [tiến độ theo use case](docs/USECASE_PROGRESS.md).

## Cấu trúc

Mỗi service áp dụng cấu trúc sau:

```text
src/
├── Controllers/       # HTTP request/response, authorization
├── DTOs/
│   ├── Requests/      # Input contract và validation
│   └── Responses/     # Output contract, không chứa hash mật khẩu
├── Models/            # Mô hình nội bộ
├── Services/          # Nghiệp vụ, ánh xạ Model ↔ DTO
├── Repositories/      # SQL có tham số, mapping dữ liệu, transaction
├── Interfaces/        # Hợp đồng Service/Repository
├── Configuration/     # Options và đăng ký dependency injection
├── Program.cs
└── appsettings.json
```

Luồng: **Controller → interface Service → Service → interface Repository → Repository → MariaDB**.
Auth có `IAuthTransaction` để giữ transaction cho các bước nghiệp vụ mà không đưa
MySqlConnection/MySqlTransaction vào service. Các thư mục chưa có nghiệp vụ giữ `.gitkeep`,
không tạo lớp rỗng chỉ để đủ layer. Gateway dùng Configuration cho định tuyến/CORS,
không cần repository vì không quản lý database.

`routes`, `middleware`, `app.js`, `package.json` là placeholder Node.js cũ, không được .NET sử dụng.
`shared/CleanHome.ServiceDefaults` chỉ chứa hạ tầng HTTP, xác thực phiên giữa service và
kết nối; không chứa entity hoặc repository nghiệp vụ dùng xuyên database.

## Build và kiểm tra

Từ thư mục `backend`:

```powershell
dotnet --version
dotnet restore CleanHome.slnx
dotnet build CleanHome.slnx --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Smoke-Test.ps1
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj
```

Smoke test chỉ khởi động auth và gateway trên cổng tạm, kiểm tra định tuyến, health,
CORS rồi dừng đúng các tiến trình do test tạo. Không gọi DB hiện có.

Integration test kết nối MariaDB XAMPP nhưng tạo hai database **QA với tên ngẫu nhiên**,
không dùng dữ liệu `cleanhome_auth`/`cleanhome_user`. Test dùng connection string local
của auth hoặc biến `CLEANHOME_TEST_ADMIN_CONNECTION`; tài khoản QA cần quyền CREATE/DROP
database. Khởi động đúng auth, user, gateway trên cổng tạm, kiểm thử HTTP, rồi dừng các
tiến trình và xóa đúng hai database QA vừa tạo. Không in mật khẩu, key hoặc bearer token.
Logs giữ ở TEMP. Kiểm tra gồm validation, lockout, quyền admin, IDOR địa chỉ/hồ sơ,
đổi mật khẩu/email, thu hồi phiên, endpoint nội bộ và retry outbox sau khi restart user.

Từ thư mục gốc repository, kiểm tra SQL với bản MariaDB XAMPP:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File databases/tests/Validate-Schema.ps1
```

Script SQL test tạo data directory riêng trong TEMP và dùng bootstrap không mở cổng,
không kết nối server XAMPP đang chạy. Giữ lại thư mục tạm để kiểm tra khi cần.

## Cấu hình database

Các connection string mặc định để trống; ứng dụng không tự tạo hoặc migrate database.
Trên máy hiện tại đã có `appsettings.Local.json` riêng cho các service, kết nối XAMPP
và key nội bộ được sinh ngẫu nhiên; các file này đã được Git ignore.
Xem [hướng dẫn SQL](../databases/README.md) trước khi tự import vào database mới.
Mỗi service chỉ được cấp quyền trên database tương ứng, không dùng chung tài khoản
root cho các service khi triển khai thật.

Có thể tạo `src/appsettings.Local.json` của service; file này đã được Git ignore.
Ví dụ tại `auth-service/src/appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "Database": "Server=127.0.0.1;Port=3306;Database=cleanhome_auth;User ID=YOUR_SERVICE_USER;Password=YOUR_LOCAL_PASSWORD;Connection Timeout=5;DateTimeKind=Utc"
  },
  "Services": { "AuthUrl": "http://localhost:5001", "UserUrl": "http://localhost:5002" },
  "InternalApi": { "Key": "REPLACE_WITH_RANDOM_KEY_AT_LEAST_32_CHARACTERS" }
}
```

Thay thông tin trên bằng cấu hình local của bạn, không gửi mật khẩu vào chat.
Thay `Database` và tài khoản cho từng service. Các biến môi trường như
`ConnectionStrings__Database` và tham số CLI ghi đè file local. Nếu dùng .NET User Secrets,
khởi chạy với environment `Development`; các project đã có `UserSecretsId` riêng.
Auth và user phải dùng cùng `InternalApi:Key`; không dùng nguyên chuỗi mẫu.
Xác thực API dùng **opaque Bearer token**, không phải JWT và không tự đọc cookie;
frontend khi nối API cần gửi `Authorization: Bearer ...`. Không lưu token thật vào Git.

## Chạy từng service

Từ `backend`, mở ba terminal:

```powershell
dotnet run --project auth-service/src/CleanHome.Auth.Api.csproj
```

```powershell
dotnet run --project user-service/src/CleanHome.User.Api.csproj
```

```powershell
dotnet run --project api-gateway/src/CleanHome.Gateway.Api.csproj
```

Các cổng trong bảng được đặt ở launch profile `http`. Khi chạy DLL/publish trực tiếp,
cần đặt `ASPNETCORE_URLS` hoặc cấu hình deployment tương ứng. Gateway mặc định gọi
các downstream bằng cổng local trong bảng; có thể ghi đè `ReverseProxy` khi deploy.

- `/`: tên service và `stage: foundation`.
- `/health/live`: HTTP 200 nếu tiến trình phục vụ HTTP.
- `/health/ready`: HTTP 200 khi `SELECT 1` thành công; HTTP 503 khi chưa cấu hình/không
  truy cập được database. Không trả chi tiết connection string.
- Readiness của gateway chỉ phản ánh gateway, chưa tổng hợp tình trạng các downstream.
- Health database mới kiểm tra kết nối, chưa kiểm tra phiên bản schema hoặc bảng nghiệp vụ.

CORS chỉ cho `http://localhost:3001`, `3002`, `3003`, có credentials. Khi deploy phải
cấu hình domain và HTTPS tương ứng. CORS không thay thế xác thực/phân quyền. API nội bộ
`/internal/*` cần key giữa service và bị chặn ở gateway. Backend xác thực bearer trên
từng request; khóa tài khoản hoặc đăng xuất làm token mất hiệu lực ngay.

## Quy tắc nghiệp vụ đã thống nhất cho bước triển khai tiếp

- Đăng nhập bằng email; auth quản lý tài khoản, user quản lý hồ sơ cùng `user_id`.
  Email cần được trim/chuẩn hóa tại API; DB chặn email trùng không phân biệt hoa thường.
- Một đơn có nhiều nhân viên. `assignments` duy nhất theo `(booking_id, employee_id)`.
  API phải khóa lịch nhân viên khi kiểm tra/gán để ngăn hai yêu cầu đồng thời trùng lịch.
- Khách gửi yêu cầu hủy khi đơn chưa bắt đầu; admin duyệt hoặc từ chối. Mỗi đơn chỉ
  có một yêu cầu pending. Đổi trạng thái đơn và ghi lịch sử trong cùng transaction.
- Nhân viên cập nhật từng phân công; chỉ hoàn thành đơn khi tất cả phân công còn hiệu lực
  đã hoàn thành. Không cho bắt đầu khi có yêu cầu hủy chờ xử lý.
- Đánh giá theo `(booking_id, employee_id)`, 1–5 sao; API phải kiểm tra khách sở hữu đơn,
  đơn hoàn thành và nhân viên thực sự tham gia. DB riêng không thể tự kiểm tra các điều này.
- Voucher và gói nằm trong phạm vi đợt backend này. Catalog quản lý cấu hình; order
  lưu snapshot, lượt dùng voucher, gói đã mua và sổ sử dụng quyền. API phải chống vượt
  quota voucher và vượt số lượng gói bằng transaction/khóa phù hợp, chưa có trong khung.
- Giá và giảm giá do server tính lại; không lấy số tiền từ frontend làm nguồn tin cậy.
- Chat/AI chưa tạo service hay schema ở bước này.

Không có JOIN hay FK xuyên database. Liên kết bên ngoài giữ ID; API nội bộ phải xác
thực service gọi và kiểm tra dữ liệu. Các cập nhật xuyên service (tạo tài khoản/hồ sơ,
thanh toán/kích hoạt gói, hủy/giải phóng lịch) cần cơ chế retry, idempotency và outbox/inbox
hoặc điều phối có khôi phục; không coi HTTP giữa service là transaction. Luồng tạo tài khoản
đã có outbox bền vững trong auth và provision hồ sơ idempotent ở user. Các luồng xuyên
service về đơn đã có operation bền vững, worker retry và inbox chống lặp tại staff;
thanh toán/gói chưa triển khai. Xem [API đơn, nhân viên và đánh giá](docs/ORDER_STAFF_REVIEW_API.md)
và [tiến độ use case](docs/USECASE_PROGRESS.md). Khi nâng cấp database đang có, áp dụng
thêm `databases/order-db/002_workflows.sql` và `databases/staff-db/002_workflows.sql`;
không chạy script reset để nâng cấp.

## VNPay

`payment-service/src/appsettings.json` có cấu hình `VnPay` để trống và `Enabled=false`.
Đây mới là vị trí cấu hình, chưa có tạo URL, ký/kiểm chữ ký, Return hoặc IPN.
Không có chế độ giả báo thanh toán thành công. Cần tài khoản Sandbox, TmnCode,
HashSecret và URL callback phù hợp để kiểm thử tích hợp sau.

HashSecret chỉ đặt trong cấu hình bí mật local/deployment. Bước nghiệp vụ phải xác thực
chữ ký, số tiền, mã giao dịch, xử lý IPN lặp và đối soát thanh toán trước khi cập nhật
đơn/gói. Chính sách hoàn tiền và hạn mức voucher/gói sẽ được chốt ở bước triển khai
liên quan; không tự quyết định phí hoặc tỷ lệ phân chia doanh thu trong schema nền tảng.

## Tài liệu thư viện

- [MySqlConnector](https://mysqlconnector.net/overview/installing/)
- [YARP configuration](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files)

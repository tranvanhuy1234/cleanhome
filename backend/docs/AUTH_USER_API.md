# API Auth và User

Base URL qua gateway: `http://localhost:4000`. Request/response JSON camelCase.
Khởi chạy gateway, auth-service và user-service theo [README](../README.md).

## Xác thực

Đăng ký hoặc đăng nhập trả `accessToken`. Request tiếp theo gửi:

```http
Authorization: Bearer <accessToken>
```

Token ngẫu nhiên 256 bit, lưu hash SHA-256 trong `auth_sessions`. Mật khẩu dùng
`PasswordHasher` của ASP.NET Core Identity. Đây là opaque token, không phải JWT.
Mặc định phiên hết hạn sau 8 giờ, không có refresh token/sliding expiry.
Đăng nhập sai 5 lần khóa đăng nhập 15 phút; tài khoản bị admin khóa sẽ bị chặn cho đến
khi mở khóa. Login/register/đổi credentials giới hạn mặc định 20 request/phút theo IP
kết nối. Qua gateway local, auth nhìn thấy IP gateway; trước khi mở public cần thiết kế
rate limit tại gateway và trusted proxy theo deployment, không tin X-Forwarded-For tùy ý.

## Auth

| Method | Endpoint | Quyền | Kết quả thành công |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Public | 201, tài khoản customer và token |
| POST | `/api/auth/login` | Public | 200, token và thông tin tài khoản |
| GET | `/api/auth/session` | Đã đăng nhập | 200, id/email/role/expiresAt |
| POST | `/api/auth/logout` | Đã đăng nhập | 204, thu hồi phiên hiện tại |
| POST | `/api/auth/logout-all` | Đã đăng nhập | 204, thu hồi tất cả phiên của mình |
| PUT | `/api/auth/password` | Đã đăng nhập | 204, đổi mật khẩu và thu hồi mọi phiên |
| PUT | `/api/auth/email` | Đã đăng nhập | 204, đổi email và thu hồi mọi phiên |
| GET | `/api/auth/accounts?search=keyword&page=1&pageSize=20` | Admin | 200, tìm kiếm tài khoản có phân trang |
| GET | `/api/auth/accounts/{id}` | Admin | 200, xem chi tiết tài khoản |
| PUT | `/api/auth/accounts/{id}` | Admin | 200, cập nhật email customer/staff |
| POST | `/api/auth/accounts` | Admin | 201, tạo customer/staff, không cấp token cho admin |
| PUT | `/api/auth/accounts/{id}/status` | Admin | 204, khóa/mở tài khoản khác |

Đăng ký:

```json
{
  "email": "your-email@example.com",
  "password": "REPLACE_WITH_YOUR_PASSWORD",
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567"
}
```

Email duy nhất và không phân biệt hoa thường; mật khẩu 8–128 ký tự; họ tên 2–100 ký tự;
điện thoại 9–15 chữ số, có thể có `+` ở đầu. Public register luôn gán customer;
trường role gửi thêm không thể nâng quyền. POST accounts của admin dùng cùng body cộng
`"role": "staff"` hoặc `"customer"`. Không có seed admin/mật khẩu mặc định.

Đăng ký commit tài khoản + phiên + yêu cầu tạo hồ sơ trong cùng transaction của auth.
Response có `profileReady`: false nghĩa tài khoản đã tồn tại nhưng hồ sơ đang chờ
đồng bộ, **không gửi lại register**. Worker thử lại sau khoảng 30 giây khi user-service
lỗi. Có thể đăng nhập, rồi PUT `/api/users/me` để hoàn thiện hồ sơ. Nếu số điện thoại đã
thuộc hồ sơ khác, cần chọn số khác; outbox không ghi đè hồ sơ có sẵn. Chưa có giao dịch
phân tán rollback đồng thời auth và user.

Body login: `{ "email": "...", "password": "..." }`.
Body password: `{ "currentPassword": "...", "newPassword": "..." }`.
Body email: `{ "email": "...", "password": "..." }`.
Body status: `{ "locked": true }` hoặc false; thiếu trường này trả 400.
Sau khi đổi email/mật khẩu phải đăng nhập lại. Admin không được khóa chính mình.
List accounts không trả password hash hoặc session token.

**Tìm kiếm tài khoản (phần use case 2.1.2.13):**

- `search` là số nguyên dương: tìm chính xác `accounts.id`.
- Các từ khóa khác: tìm một phần email, không phân biệt hoa thường theo collation DB.
- Bỏ `search` hoặc chỉ nhập khoảng trắng: lấy danh sách như trước.
- Từ khóa tối đa 254 ký tự; các ký tự `%`, `_`, `!` được hiểu theo nghĩa đen.
- `page` từ 1–100000; `pageSize` từ 1–100, mặc định 20.
- Response gồm `items`, `page`, `pageSize`, `total`; total là tổng số kết quả sau lọc.
  Không tìm thấy hoặc vượt trang trả 200 với `items: []`, không trả 404.
- Kết quả sắp xếp ID giảm dần. Chỉ admin được truy cập: 401 nếu chưa đăng nhập, 403 nếu
  sai vai trò. Chưa tìm theo tên/điện thoại vì hồ sơ thuộc database user-service riêng.

Ví dụ: `GET /api/auth/accounts?search=example.com&page=1&pageSize=10`
hoặc `GET /api/auth/accounts?search=123`.

Kiểm thử riêng API này từ thư mục `backend` sau khi build:

```powershell
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --accounts-search-only
```

Test tạo hai database QA tạm, kiểm tra qua gateway, rồi xóa đúng database QA vừa tạo.

**Xem chi tiết tài khoản (phần use case 2.1.2.13):**

`GET /api/auth/accounts/123`, kèm bearer token admin, trả JSON:

```json
{
  "id": 123,
  "email": "customer@example.com",
  "role": "customer",
  "status": "active",
  "createdAt": "2026-11-06T00:00:00Z"
}
```

Response tái sử dụng `AccountResponse`, chỉ gồm năm trường trên; không trả password hash,
session token hoặc thông tin lockout nội bộ. Header `Cache-Control: no-store`.
Admin có thể xem tài khoản active/locked và tài khoản của chính mình. Endpoint này lấy
thông tin xác thực từ auth-service; họ tên, điện thoại và địa chỉ thuộc user-service,
chưa được ghép vào response này và không truy vấn xuyên database.

- 401 nếu chưa đăng nhập hoặc token không còn hiệu lực.
- 403 nếu đăng nhập bằng customer/staff.
- 400 nếu ID bằng 0 hoặc âm.
- 404 nếu ID dương không tồn tại; ID không hợp lệ với kiểu `long` không khớp route.

Kiểm thử riêng từ thư mục `backend` sau khi build:

```powershell
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --account-detail-only
```

### Admin cập nhật tài khoản — use case 2.1.2.13

`PUT /api/auth/accounts/123`, kèm bearer token admin:

```json
{ "email": "new-email@example.com" }
```

Response 200 dùng `AccountResponse` như API chi tiết. Email được chuẩn hóa chữ thường,
kiểm tra định dạng và duy nhất. Nếu email thay đổi, cập nhật email và thu hồi mọi phiên
của tài khoản trong cùng transaction; người dùng phải đăng nhập lại bằng email mới và
mật khẩu hiện tại. Gửi lại cùng email chuẩn hóa không làm mất phiên đang dùng.

Endpoint chỉ cập nhật email của customer/staff, bao gồm tài khoản đang khóa; không mở
khóa, đổi vai trò hoặc mật khẩu. Không nhận các trường ngoài `email` (trả 400), kể cả
`role`, `status`, `password`, `fullName`, `phone`. Họ tên/điện thoại thuộc user-service,
chưa được cập nhật qua endpoint này. Tài khoản admin trả 409; admin tự đổi email bằng
`PUT /api/auth/email` cùng mật khẩu hiện tại.

Mã lỗi: 400 input hoặc ID không hợp lệ; 401 chưa đăng nhập; 403 sai vai trò; 404 không
có tài khoản; 409 email trùng hoặc target là admin. Email trùng không đổi dữ liệu và
không thu hồi các phiên cũ. Response không chứa hash/token, có `Cache-Control: no-store`.

Kiểm thử riêng sau khi build, từ thư mục `backend`:

```powershell
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --account-update-only
```

## Hồ sơ và địa chỉ

Tất cả endpoint dưới đây yêu cầu đăng nhập. User ID luôn lấy từ phiên, không lấy từ body.

| Method | Endpoint | Kết quả thành công |
| --- | --- | --- |
| GET | `/api/users/me` | 200, hồ sơ hiện tại |
| PUT | `/api/users/me` | 200, tạo/cập nhật hồ sơ của mình |
| GET | `/api/users/me/addresses` | 200, danh sách địa chỉ của mình |
| GET | `/api/users/me/addresses/{id}` | 200, một địa chỉ của mình |
| POST | `/api/users/me/addresses` | 201, tạo địa chỉ |
| PUT | `/api/users/me/addresses/{id}` | 204, cập nhật địa chỉ |
| DELETE | `/api/users/me/addresses/{id}` | 204, xóa địa chỉ |

Body PUT profile:

```json
{
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "avatarUrl": null
}
```

Avatar có thể null hoặc URL HTTP/HTTPS tối đa 2048 ký tự; chưa có API upload ảnh.
Body địa chỉ:

```json
{
  "recipient": "Nguyễn Văn A",
  "phone": "0901234567",
  "addressText": "123 Nguyễn Văn Linh, TP. Hồ Chí Minh",
  "latitude": 10.73,
  "longitude": 106.71
}
```

Tọa độ bắt buộc: latitude -90..90, longitude -180..180. Địa chỉ của người khác trả
404 kể cả tài khoản admin; không dùng endpoint này để sửa dữ liệu người khác.
Profile đang chờ tạo trả 404 khi GET, có thể PUT đầy đủ thông tin để tạo.

### Admin xem chi tiết hồ sơ — use case 2.1.2.13

`GET /api/users/profiles/{userId}`, kèm bearer token admin, trả 200:

```json
{
  "userId": 123,
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "avatarUrl": null,
  "createdAt": "2026-11-06T00:00:00Z",
  "updatedAt": "2026-11-06T00:00:00Z"
}
```

Tái sử dụng `ProfileResponse` và repository đọc hồ sơ hiện có. ID là `profiles.user_id`
(cùng ID tài khoản auth), không phải `employees.id`. Admin xem được hồ sơ chính mình
và người dùng khác, kể cả hồ sơ của tài khoản bị khóa. Avatar chưa có trả null.
Endpoint chỉ đọc, không tạo hồ sơ, sửa thời gian hoặc thay đổi phiên đăng nhập.

Response có `Cache-Control: no-store`, không trả email, mật khẩu, token, vai trò hoặc
địa chỉ. Thông tin xác thực lấy riêng qua `GET /api/auth/accounts/{id}`; API này chỉ
đọc database user-service, không JOIN hoặc gọi sang auth để lấy thông tin target.
Xác thực quyền của người gọi vẫn được kiểm tra qua auth-service trên mỗi request.

Mã lỗi: 401 chưa đăng nhập/phiên không hợp lệ; 403 customer/staff; 400 ID bằng 0/âm;
404 hồ sơ không tồn tại. ID không biểu diễn được bằng kiểu long không khớp route (404).
Tài khoản đã tạo nhưng chưa có hồ sơ cũng trả 404, không tự provision khi GET.

Kiểm thử riêng từ thư mục `backend`, sau khi build:

```powershell
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --admin-profile-detail-only
```

### Admin cập nhật hồ sơ — use case 2.1.2.13

`PUT /api/users/profiles/{userId}`, kèm bearer token admin:

```json
{
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "avatarUrl": "https://example.com/avatar.jpg"
}
```

Trả 200 với `ProfileResponse`: `userId`, `fullName`, `phone`, `avatarUrl`, `createdAt`,
`updatedAt`; có `Cache-Control: no-store`. ID lấy từ route là `accounts.id`/`profiles.user_id`,
không phải `employees.id`. Chỉ cập nhật hồ sơ đã tồn tại; không tạo hồ sơ cho ID bất kỳ.
Giữ nguyên createdAt, email, mật khẩu, vai trò, trạng thái tài khoản, các địa chỉ và phiên.
Admin có thể cập nhật hồ sơ mình hoặc người dùng khác, kể cả hồ sơ tài khoản đang khóa.

FullName/phone bắt buộc, dùng cùng validation với PUT `/me`; fullName được trim, avatar
chỉ nhận HTTP/HTTPS. `avatarUrl: null`, chuỗi trắng hoặc bỏ trường avatarUrl sẽ xóa URL
ảnh hiện tại theo ngữ nghĩa PUT. Đây là cập nhật URL, chưa phải upload ảnh.
Các trường ngoài ba trường trên (ví dụ email, role, password, userId) bị từ chối với 400.

Mã lỗi: 400 dữ liệu/ID không hợp lệ; 401 chưa đăng nhập; 403 customer/staff; 404 hồ sơ
không tồn tại; 409 số điện thoại thuộc hồ sơ khác. Lỗi trùng điện thoại rollback toàn
bộ thay đổi; không ghi đè họ tên hoặc ảnh. Không truy vấn hoặc cập nhật xuyên database.

Kiểm thử riêng sau khi build, từ thư mục `backend`:

```powershell
dotnet run --no-build --project tests/CleanHome.IntegrationTests/CleanHome.IntegrationTests.csproj -- --admin-profile-update-only
```

## API nội bộ

User-service có `POST /internal/profiles` tại cổng nội bộ 5002. Body gồm `userId`,
`fullName`, `phone`; header `X-CleanHome-Service-Key` phải khớp key cấu hình auth/user.
Không mở endpoint này qua gateway. Provision lặp cho cùng user trả 204 và giữ nguyên
hồ sơ đã cập nhật. Kết nối service nội bộ cần mạng riêng/HTTPS khi triển khai thực tế.

## Mã lỗi và phạm vi

- 400: input không hợp lệ; lỗi validation có `errors` theo tên trường.
- 401: chưa đăng nhập, sai mật khẩu, phiên hết hạn/thu hồi hoặc tài khoản không khả dụng.
- 403: sai quyền hoặc key nội bộ.
- 404: không có dữ liệu thuộc tài khoản, hoặc route nội bộ bị gateway chặn.
- 409: email/điện thoại trùng, hoặc thao tác trạng thái không được phép.
- 429: vượt giới hạn request.
- 503: database hoặc dịch vụ xác thực chưa sẵn sàng.

Lỗi nghiệp vụ dùng Problem Details, không trả SQL hoặc connection string.
Response có dữ liệu xác thực/cá nhân được đặt `Cache-Control: no-store`.
Chưa triển khai xác minh email, quên mật khẩu, đăng nhập mạng xã hội, upload ảnh,
frontend adapter, API tạo/quản trị/cập nhật đơn, nhân viên, voucher/gói, đánh giá và thanh toán VNPay.
API public và nhóm API quản trị dịch vụ/danh mục đã có tại [CATALOG_API.md](CATALOG_API.md).
API đọc đơn/lịch sử của khách đã có tại [CUSTOMER_ORDERS_API.md](CUSTOMER_ORDERS_API.md).

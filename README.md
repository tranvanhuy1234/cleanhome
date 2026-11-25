# CleanHome — Dịch vụ tại nhà

Ứng dụng quản lý dịch vụ tại nhà dành cho khách hàng, nhân viên và quản trị viên. Dự án tổ chức frontend thành ba website riêng, sử dụng chung component và logic, kết nối với backend ASP.NET Core qua API Gateway.

## Chức năng chính

- **Khách hàng:** xem dịch vụ, đặt lịch, theo dõi đơn, gửi yêu cầu hủy, quản lý hồ sơ và đánh giá nhân viên.
- **Nhân viên:** xem công việc được phân công, tiếp nhận, cập nhật tiến độ và theo dõi lịch làm việc.
- **Quản trị viên:** quản lý tài khoản, danh mục dịch vụ, đơn hàng, phân công và đánh giá.

## Công nghệ và cấu trúc

- **Frontend:** Next.js, React, TypeScript; npm workspaces để chia sẻ mã nguồn.
- **Backend:** ASP.NET Core Web API, API Gateway và các service theo nghiệp vụ.
- **Database:** MySQL/MariaDB; các script schema và migration nằm trong `databases/`.

```text
frontend/
  apps/customer/   Website khách hàng
  apps/staff/      Website nhân viên
  apps/admin/      Website quản trị
  packages/ui/     Component và màn hình dùng chung
  packages/core/   Kiểu dữ liệu, state và lớp gọi API
backend/           Gateway và các backend service
databases/         Script khởi tạo và migration database
frontend-legacy/   Phiên bản React/Vite cũ để tham khảo
```

Luồng dữ liệu chính:

```text
Component → API client → API Gateway → Controller → Service → Repository → Database
```

Backend trả dữ liệu JSON về frontend; component cập nhật state để hiển thị. Backend chịu trách nhiệm kiểm tra phiên, quyền truy cập và các quy tắc nghiệp vụ.

## Chạy frontend trên máy

Yêu cầu Node.js 22.9 trở lên và npm. Từ thư mục gốc:

```bash
cd frontend
npm ci
```

Chạy mỗi lệnh dưới đây trong một terminal riêng, tại thư mục `frontend`:

```bash
npm run dev:customer
npm run dev:staff
npm run dev:admin
```

| Website | Địa chỉ local |
| --- | --- |
| Khách hàng | http://localhost:3001 |
| Nhân viên | http://localhost:3002 |
| Quản trị | http://localhost:3003 |

Mặc định frontend gọi API tại `http://localhost:4000/api`. Có thể thay đổi bằng `NEXT_PUBLIC_API_URL` trong cấu hình môi trường của từng ứng dụng. Xem [hướng dẫn frontend](frontend/README.md) và [phạm vi kết nối backend](frontend/docs/BACKEND_CONNECTION.md).

Để dùng dữ liệu thật, cần khởi động database và các backend service theo [hướng dẫn backend](backend/README.md). Khách hàng có thể đăng ký trên website; tài khoản nhân viên và quản trị cần được cấp theo quy trình backend. Không sử dụng tài khoản demo của bản legacy để đăng nhập backend.

## Kiểm tra mã nguồn

Chạy trong thư mục `frontend`:

```bash
npm run typecheck
npm run build
```

## Phạm vi và giới hạn

- Frontend mới mặc định dùng API thật. Chế độ dữ liệu mẫu chỉ được bật khi đặt `NEXT_PUBLIC_USE_MOCK=true`.
- `frontend-legacy/` là bản demo phía trình duyệt, lưu dữ liệu cục bộ; không đại diện cho cơ chế xác thực của backend.
- Một số tính năng như hoàn tiền, voucher/gói và báo cáo chưa có API đầy đủ. Thanh toán cần cấu hình môi trường phù hợp; không coi bản chạy local là hệ thống giao dịch production đã hoàn thiện.
- Chi tiết tích hợp và giới hạn được ghi trong [tài liệu kết nối backend](frontend/docs/BACKEND_CONNECTION.md).

Không đưa mật khẩu thật, token, khóa thanh toán hoặc dữ liệu người dùng thật vào mã nguồn và tài liệu. Các biến `NEXT_PUBLIC_*` có thể được trình duyệt đọc, chỉ dùng cho cấu hình công khai.

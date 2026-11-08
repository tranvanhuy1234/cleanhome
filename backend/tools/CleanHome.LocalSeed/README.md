# Dữ liệu mẫu XAMPP local

Chạy từ `backend`:

```powershell
dotnet run --project tools/CleanHome.LocalSeed -- --local-demo
```

Công cụ chỉ kết nối MariaDB tại 127.0.0.1:3306, root không mật khẩu, vào các database
`cleanhome_*` đã khởi tạo. Không reset database và không sửa tài khoản, dịch vụ hay đơn
đã tồn tại. Dùng transaction và advisory lock để tránh hai lần seed đồng thời.

Tạo 4 danh mục, 6 dịch vụ, 1 chi nhánh, 1 kỹ năng, 3 hồ sơ nhân viên verified và lịch
08:00–18:00 UTC+7 trong khoảng từ 3 ngày trước đến 14 ngày sau ngày chạy.
Tạo 4 đơn cho khách mẫu: pending, confirmed (2 nhân viên), completed (2 nhân viên),
cancelled; chi tiết, lịch sử và một đánh giá mẫu cho phân công đã done.
Các ID được tra từ bản ghi thật, không dùng ID cố định hay thay đổi dữ liệu người dùng.

Tạo 5 tài khoản có email:

- `demo.admin@cleanhome.local` — admin tại localhost:3003.
- `demo.customer@cleanhome.local` — khách tại localhost:3001.
- `demo.staff@cleanhome.local`, `demo.staff2@cleanhome.local`, `demo.staff3@cleanhome.local` — staff tại localhost:3002.

Mật khẩu riêng từng tài khoản được sinh ngẫu nhiên, hash bằng PasswordHasher giống auth-service.
Xem thông tin đăng nhập trong `backend/.local/demo-accounts.json`; thư mục này bị Git ignore.
Không đưa file này vào Git hoặc dùng các tài khoản mẫu khi triển khai public.
Chạy lại không đổi mật khẩu/tái tạo đơn đã có; nếu đã tự sửa các đơn, script giữ nguyên.
File tài khoản local cần được giữ lại; script không khôi phục mật khẩu đã mất bằng cách ghi đè DB.

Dữ liệu dùng để minh họa giao diện, không phải khách/đơn thực. Không tạo payment, không
giả lập VNPay thành công. Tài khoản khác do bạn đã tạo không tự nhận các đơn của khách mẫu.

Đã chạy seed hai lần và kiểm tra qua gateway: public có 6 dịch vụ, khách/admin mẫu có
4 đơn, hai staff đầu có 2 công việc/người. Các phiên kiểm tra đã được logout sau khi xác nhận.

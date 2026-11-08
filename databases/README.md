# CleanHome — schema riêng theo service

Bảy file `*-db/001_initial.sql` dành cho **database mới**, theo MariaDB 10.4.32.
Không phải migration dữ liệu từ `dich_vu_tai_nha`; không chạy tự động khi ứng dụng khởi động.
Đã cập nhật bảy database `cleanhome_*` trên XAMPP theo sự cho phép của người dùng
vì không có dữ liệu cần giữ. Các database khác không bị thay đổi.

| File | Database | Nội dung |
| --- | --- | --- |
| `auth-db/001_initial.sql` | `cleanhome_auth` | Tài khoản email, lockout, phiên có token hash, outbox hồ sơ |
| `user-db/001_initial.sql` | `cleanhome_user` | Hồ sơ, địa chỉ, thông báo |
| `service-db/001_initial.sql` | `cleanhome_service` | Dịch vụ, gói, voucher và phạm vi áp dụng |
| `order-db/001_initial.sql` | `cleanhome_order` | Đơn, lịch sử, hủy, gói đã mua, sử dụng gói/voucher |
| `staff-db/001_initial.sql` | `cleanhome_staff` | Chi nhánh, nhân viên, kỹ năng, lịch rảnh, phân công |
| `payment-db/001_initial.sql` | `cleanhome_payment` | Thanh toán đơn hoặc gói, phân chia doanh thu |
| `review-db/001_initial.sql` | `cleanhome_review` | Đánh giá riêng từng nhân viên trong đơn |

Tổng cộng 30 bảng. Các file độc lập, không cần thứ tự import giữa service. Bảng trong
cùng database có khóa ngoại; ID tham chiếu service khác không có khóa ngoại xuyên DB.
`profiles.user_id` là ID tài khoản do auth cấp, không tự tăng. `employees.id` là mã hồ sơ
nhân viên, khác `employees.user_id`; reviews/assignments dùng `employees.id`.

## Cách import khi sẵn sàng

1. Kiểm tra tên database `cleanhome_*` chưa có dữ liệu quan trọng. Script tạo DB nếu
   chưa tồn tại, sau đó tạo bảng một lần; không dùng `DROP`, `TRUNCATE` hay xóa DB cũ.
2. Trong phpMyAdmin, mở tab Import cấp server và chọn từng file `001_initial.sql`.
   File tự chọn database bằng `USE`. Tài khoản import cần quyền tạo database/bảng.
3. Nếu có lỗi, dừng và kiểm tra. DDL MariaDB không rollback toàn bộ file; không import
   lại một cách mù quáng vì một phần bảng có thể đã được tạo.
4. Cấu hình mỗi service với tài khoản chỉ có quyền trên database của chính service.

Không có seed tài khoản admin hoặc mật khẩu mặc định. Không tự chuyển dữ liệu SQL
cũ hoặc dữ liệu localStorage; nếu cần sẽ viết migration riêng sau khi đối chiếu.
`Reset-LocalDatabases.ps1 -ResetEmptyCleanHomeDatabases` là công cụ reset rõ ràng chỉ
dành cho local khi đã được phép xóa dữ liệu của đúng bảy database. Không chạy lại sau
khi bắt đầu có dữ liệu cần giữ; ứng dụng không tự gọi script này.

## Quy ước dữ liệu

- Charset `utf8mb4`, collation `utf8mb4_unicode_ci`.
- Thời điểm `DATETIME(6)` được lưu theo UTC; các cột DATE của gói là ngày hiệu lực,
  cần thống nhất quy tắc ngày kinh doanh tại tầng nghiệp vụ. UI chuyển sang giờ Việt Nam.
- Giá dùng DECIMAL. Trạng thái nghiệp vụ được kiểm tra tại API; bảng giữ phần lớn
  VARCHAR theo SQL gốc. ENUM của voucher và các bảng mới được giữ như khai báo trong SQL.
- Đơn có tổng trước giảm, số tiền giảm và tổng cuối. Quyền sử dụng gói có ledger
  `entitlement_usages` để giữ chỗ/sử dụng/giải phóng, chưa có trigger tự trừ lượt.
- `cancellation_requests.pending_booking_id` là cột sinh, chặn hai yêu cầu pending
  cho cùng đơn; nhiều yêu cầu đã xử lý vẫn được giữ làm lịch sử.
- `assignments` có thời gian snapshot và chỉ mục kiểm tra trùng lịch. Chỉ mục không tự
  ngăn khoảng thời gian chồng nhau; API phải dùng transaction và khóa theo nhân viên.
- Reviews duy nhất theo đơn/nhân viên và rating 1–5; quyền đánh giá kiểm tra qua API.
- Payment có merchant reference và idempotency key; provider reference nullable để
  lưu giao dịch chưa được VNPay cấp mã. Mỗi giao dịch thuộc đúng một đơn hoặc gói.

## Kiểm tra không tác động database hiện có

Từ thư mục gốc repository:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File databases/tests/Validate-Schema.ps1
```

Mặc định dùng `C:\xampp\mysql\bin`; có thể truyền `-MariaDbBin`. Script tạo thư mục
TEMP riêng, khởi tạo MariaDB và chạy bootstrap không mở cổng. Kiểm tra import đủ schema,
đánh giá nhiều nhân viên, chống đánh giá trùng, rating sai, email trùng, đích thanh toán
không hợp lệ, yêu cầu hủy pending, phân công nhiều nhân viên, phần trăm voucher và thời hạn
gói. Thư mục TEMP được giữ để kiểm tra; script không dừng server XAMPP đang chạy.

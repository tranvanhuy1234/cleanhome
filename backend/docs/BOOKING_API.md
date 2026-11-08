# Đặt dịch vụ

Thanh toán mới: frontend gửi `paymentMethod: "VNPAY"` hoặc `"CASH"`; xem [PAYMENTS_API.md](PAYMENTS_API.md). VNPay tạo đơn `pending_payment` và giữ GPS đến sau PAID; tiền mặt tạo đơn `pending`, chưa thu tiền. Không gửi số tiền từ frontend.

Luồng đặt tự động có GPS, xác nhận ghim và số nhân viên: xem [GPS_AUTO_DISPATCH_API.md](GPS_AUTO_DISPATCH_API.md). Frontend hiện gửi `automaticDispatch: true`, `requiredStaff` và `location` đã xác nhận; API cũ không gửi cờ này vẫn dùng phân công thủ công.

Khách hàng đăng nhập, dùng gateway `http://localhost:4000`.

- `GET /api/orders/booking/branches`: chi nhánh active để chọn khi đặt.
- `GET /api/users/me/addresses`: địa chỉ thuộc khách đang đăng nhập.
- `POST /api/orders/booking`: tạo đơn pending, chi tiết snapshot và lịch sử trong một transaction.

```json
{
  "idempotencyKey": "unique-booking-key-001",
  "serviceId": 1,
  "addressId": 1,
  "branchId": 1,
  "quantity": 2,
  "scheduledStart": "2026-12-01T02:00:00Z",
  "specialRequest": "Vui lòng gọi trước khi đến"
}
```

Ngày/ID trong ví dụ cần thay bằng dữ liệu thực tế. Lịch dùng ISO 8601 có offset;
frontend chuyển giờ trên máy người dùng sang UTC trước khi gửi.

HTTP 201 trả `id`, `code`, `status`, `totalAmount`, `scheduledStart`, `scheduledEnd`;
Location trỏ tới `/api/orders/me/{id}`. Khách xem lại đơn tại endpoint đó; admin xem
`/api/orders/admin/orders/{id}`. Xác nhận và phân công là các bước tiếp theo.

Server lấy bảng giá/dịch vụ active từ catalog, địa chỉ qua API user dùng bearer của
khách, chi nhánh qua API nội bộ staff có service key. Không JOIN database xuyên service.
Snapshot giữ tên/giá/đơn vị và thông tin liên hệ/địa chỉ tại thời điểm tạo.
Frontend không gửi tổng giá, customerId, trạng thái hay thời lượng; gửi trường lạ bị 400.

Quy tắc hiện tại:
- Một dịch vụ/đơn, số lượng nguyên 1–100; tổng thời lượng tối đa 10 giờ.
- Lịch trước ít nhất 30 phút, không quá 90 ngày; toàn bộ khoảng thực hiện trong
  08:00–18:00 giờ Việt Nam. Đây là lịch khách đề nghị, chưa giữ nhân viên.
- Địa chỉ phải thuộc khách; chi nhánh phải active. Khách tự chọn chi nhánh;
  chưa tự xác định phạm vi phục vụ theo đa giác/toạ độ.
- Chưa áp voucher/quyền gói hoặc thu tiền. Giá = đơn giá catalog × số lượng.
- Thiếu service phụ thuộc trả 503 và không lưu đơn dở dang.

Giữ nguyên idempotencyKey khi thử lại cùng nội dung. Cùng khách/cùng key chỉ có một
đơn kể cả gửi đồng thời; đổi nội dung với key đã dùng trả 409. Request đã commit
được replay trước khi gọi dependency, kể cả dependency đang gián đoạn. Key khác
là yêu cầu đặt mới. Frontend giữ key khi thử lại trong màn hình hiện tại; tải lại
trang không giữ draft/key, nên cần kiểm tra danh sách đơn sau lỗi mạng trước khi đặt lại.

Áp dụng `databases/order-db/003_booking_creation.sql` trước khi chạy API mới.
Đã áp dụng trên XAMPP local; không reset dữ liệu.

Kiểm thử:
- `dotnet run --project tests/CleanHome.IntegrationTests -- --booking-creation`:
  DB order QA riêng; transaction, 8 request đồng thời, replay/outage, giá, quyền sở hữu,
  reference inactive, lịch và số lượng. Tự dọn đúng DB QA sau khi chạy.
- `node tests/browser-booking.cjs` tại frontend: cần các site/API và Chrome QA ở 9227;
  tạo một đơn thật cho khách mẫu, xác nhận admin đọc cùng đơn. Giữ đơn lại để minh họa.

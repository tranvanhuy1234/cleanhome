# Thanh toán CleanHome: VNPay Sandbox và tiền mặt

## Cấu hình local

Payment-service chạy cổng 5006; gateway dùng `/api/payments/*` trên cổng 4000. Giữ nguyên MariaDB và cấu trúc service hiện có. Không dùng tiền thật.

1. Chạy migration bổ sung `databases/order-db/005_payments.sql` và `databases/payment-db/002_checkout.sql` sau các migration hiện có. Không chạy script reset database. Hai migration đã được áp dụng trên local khi triển khai.
2. Mở `backend/payment-service/.env`, tự điền `VNPAY_HASH_SECRET` được VNPay cung cấp. Không gửi khóa qua frontend, không sửa bản sao trong `bin/Debug`, không commit `.env`. Mã merchant và URL Sandbox đã điền; cấu hình database/service key dùng `src/appsettings.Local.json` đang có.
3. `VNPAY_RETURN_URL` và `VNPAY_IPN_URL` phải là URL HTTPS của tunnel callback. `PAYMENT_FRONTEND_URL=http://localhost:3001` đưa trình duyệt về trang chi tiết đơn sau Return. Cần đăng nhập để xem đơn; không có token trong redirect.
4. Khởi động/restart payment-service sau khi đổi `.env`: `dotnet run --project backend/payment-service/src/CleanHome.Payment.Api.csproj`. Chương trình đọc `.env` riêng của service; biến môi trường thực được ưu tiên.
5. Giữ gateway, order/staff/auth service và frontend chạy. Không cần deploy hosting.

Các biến hỗ trợ:

```dotenv
VNPAY_TMN_CODE=<mã merchant sandbox>
VNPAY_HASH_SECRET=<tự điền khóa sandbox>
VNPAY_PAYMENT_URL=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
VNPAY_RETURN_URL=https://YOUR-TUNNEL.trycloudflare.com/api/payments/vnpay/return
VNPAY_IPN_URL=https://YOUR-TUNNEL.trycloudflare.com/api/payments/vnpay/ipn
PAYMENT_FRONTEND_URL=http://localhost:3001
```

Chỉ cho phép endpoint thanh toán Sandbox chính thức. Thiếu khóa/Return URL hợp lệ trả lỗi cấu hình, không giả lập thành công. Không đưa mật khẩu Merchant Admin vào hệ thống CleanHome.

## Tunnel và đăng ký IPN với VNPay

`backend/tools/Start-VnPayTunnel.ps1` chạy proxy callback local ở 4090 và cloudflared. Công cụ cloudflared nằm trong `backend/.local/tunnel`, được Git bỏ qua. Proxy chỉ nhận GET đúng hai đường dẫn IPN/Return và chuyển về gateway 4000; không công khai admin, đăng nhập hoặc `/internal/*` của backend.

URL hiện tại xem `backend/.local/tunnel/vnpay.err.log`. Ghép URL với hai đường dẫn ở trên rồi cập nhật `.env`. Không chạy nhiều bản script cùng lúc; giữ proxy/tunnel chạy. Nếu cần dừng, xem đúng PID trong `vnpay.pid` và `vnpay-proxy.pid`, kiểm tra tiến trình thuộc tác vụ này rồi dừng. Chạy lại Quick Tunnel có thể đổi hostname; không dùng Quick Tunnel cho production.

**Khai báo `.env` không đăng ký IPN với VNPay.** Đăng nhập Merchant Sandbox bằng tài khoản của bạn, kiểm tra phần cấu hình website/IPN nếu tài khoản được cấp quyền. Nếu không có mục sửa, gửi URL IPN đầy đủ cho bộ phận hỗ trợ VNPay đã gửi email cấp Sandbox và đề nghị cấu hình cho mã merchant của bạn. Không gửi HashSecret trong yêu cầu hỗ trợ. Sau đó dùng tài khoản SIT do VNPay cung cấp để kiểm tra các tình huống IPN. Khi tunnel đổi, cần cập nhật lại cả local và VNPay.

Kiểm tra URL IPN bằng một GET không chữ ký trả `RspCode=97` khi cấu hình merchant đã đủ. Nó không tạo thanh toán và không xác nhận đã đăng ký IPN. Nếu chưa có khóa, endpoint trả `RspCode=99` để yêu cầu thử lại. Chỉ kiểm thử thẻ/OTP Sandbox trong trang chính thức VNPay, không nhập thông tin thẻ vào CleanHome.

## Luồng và trạng thái

Frontend mới luôn gửi đúng một `paymentMethod` là `VNPAY` hoặc `CASH` cùng yêu cầu đặt dịch vụ. Giá lấy từ catalog ở backend, chốt snapshot cùng đơn; số tiền thanh toán không nhận từ client. VND phải nguyên dương. Idempotency đặt đơn bao gồm phương thức, nên không thể tái dùng khóa để đổi phương thức. Request cũ không có trường này vẫn dùng luồng cũ, không tự chuyển các đơn lịch sử thành giao dịch thanh toán.

- **VNPay:** đơn có work status lưu `pending_payment`, payment `PENDING`; hàng đợi GPS là `awaiting_payment`. Backend tạo mã giao dịch ngẫu nhiên, URL ký HMAC-SHA512 v2.1.0 với form encoding và thời gian GMT+7. Giao dịch có hạn tối đa 15 phút, không vượt giờ bắt đầu. Nhấn liên tiếp dùng lại giao dịch đang hiệu lực. Khi thất bại/hết hạn có thể tạo giao dịch mới trên cùng đơn.
- **Return:** kiểm tra chữ ký, merchant, mã giao dịch và số tiền; chỉ redirect tới đơn của khách ở frontend. Trang chi tiết lấy trạng thái thực từ backend và chờ xác minh; không tin tham số URL để đánh dấu PAID.
- **IPN:** kiểm tra chữ ký constant-time, merchant, số tiền, giao dịch, cả ResponseCode/TransactionStatus. Khóa hàng tổng thanh toán rồi giao dịch; ghi tiền, lịch sử và revision trong cùng transaction. `00` ghi nhận, `02` đã xử lý, `01` không có giao dịch, `04` sai số tiền, `97` sai chữ ký, `99` lỗi/chưa sẵn sàng. Database lỗi không trả thành công; VNPay có thể retry.
- **Đồng bộ GPS:** worker gửi trạng thái đã commit sang order-service với revision. Mirror khóa đơn và bỏ qua revision cũ; chỉ PAID mới mở `awaiting_payment → queued`. Callback trùng không mở đợt tìm khác. Thời hạn tìm người bắt đầu khi mở hàng đợi sau thanh toán. Khi order-service gián đoạn, thanh toán vẫn được giữ và đồng bộ lại; không đánh dấu thất bại do mất mạng.
- **CASH:** tạo đơn unpaid và mở GPS ngay. Khi toàn bộ công việc completed, một nhân viên đã nhận được chỉ định thu tiền. Worker chọn người theo thứ tự assignment; admin được đổi sang một người đã nhận trong đơn trước khi báo thu. Chỉ người được chỉ định đã hoàn thành phần việc mới được báo thu đủ số tiền snapshot.
- Báo thu chuyển sang `CASH_AWAITING_CONFIRMATION`, không tự thành PAID. Chủ đơn chọn đã trả → `PAID`, chưa trả → `DISPUTED`. Không có timeout tự xác nhận. Admin đối soát tiền mặt cần lý do/bằng chứng; mọi thay đổi có actor và lịch sử.

Trạng thái thanh toán riêng: UNPAID, PENDING, FAILED, PAID, CASH_AWAITING_CONFIRMATION, DISPUTED, REFUND_PENDING, REFUNDED. Trạng thái công việc cũ `pending/confirmed/in_progress/completed/cancelled` được giữ để tránh phá GPS/review/thống kê; API thanh toán biểu diễn chúng thành SEARCHING_EMPLOYEE/ASSIGNED/IN_PROGRESS/COMPLETED/CANCELLED và thêm PENDING_PAYMENT. Chi tiết tuyển người vẫn hiển thị số đã nhận/số cần để phân biệt đơn đã nhận một phần.

Hoàn thành công việc không đồng nghĩa đã thu tiền. Công thức phân bổ nhân viên vẫn dựa trên giá trị đơn completed chia số nhân viên thực tế; không dùng số lần IPN và không biến thành tiền lương.

## Đối soát, callback muộn và hoàn tiền

Nếu VNPay báo hết hạn ngay sau khi mở URL, đối chiếu giờ máy local với HTTP Date của VNPay và bật đồng bộ ngày giờ Windows (múi giờ Việt Nam UTC+7). Không tự cộng bù giờ vào chữ ký hoặc nới hạn để che lỗi đồng hồ. Trở lại chi tiết đơn để lấy giao dịch mới sau khi URL cũ hết hạn, không đặt trùng đơn.

Tần suất truy vấn được lưu bằng `payment_attempts.last_query_at` dùng UTC riêng, không dựa vào `payments.updated_at` có thể bị ảnh hưởng bởi default timezone của database cũ. Migration `002_checkout.sql` có thể chạy lại để thêm cột này. Giao dịch/đơn mới lưu timestamp UTC tường minh; giữ nguyên trạng thái và dữ liệu giao dịch lịch sử. Giao diện kiểm tra `expiresAt` và đổi khóa yêu cầu khi liên kết replay đã hết hạn.

Worker truy vấn các giao dịch pending sau 2 phút, theo nhóm, chu kỳ 2 phút. Admin có thể truy vấn `querydr`; mỗi giao dịch cách ít nhất 30 giây giữa các lần gọi. Request/response dùng định dạng ký chuỗi phân cách `|` đúng tài liệu VNPay. Không khẳng định failed do khách đóng trang, hết hạn URL, lỗi mạng hoặc truy vấn chưa có kết quả.

Callback thất bại cũ không hạ trạng thái đã PAID/REFUNDED. Thành công xác thực đến muộn sau hủy, hoặc nhiều giao dịch cùng trả tiền cho một đơn, được lưu và tạo hồ sơ hoàn/đối soát, không giao việc lại hoặc cộng doanh thu đơn lần hai. Mã giao dịch nhà cung cấp có unique constraint.

Đơn đã trả tiền bị hủy chuyển REFUND_PENDING. Đây là yêu cầu kiểm tra chính sách và chứng từ, không phải đã hoàn. Giai đoạn này xử lý hoàn thực tế qua VNPay Merchant hoặc quy trình tiền mặt; admin chỉ xác nhận REFUNDED khi có chứng từ, lịch sử lưu người xác nhận. **Chưa tự gọi API hoàn tiền VNPay**, vì cần quyền merchant và chính sách hoàn tiền được duyệt; không giả lập đã hoàn. Hồ sơ thanh toán thừa được đối soát riêng.

## API qua gateway

Tất cả cần bearer token và đúng quyền, trừ callback VNPay được xác minh chữ ký. Prefix `/api/payments`.

| API | Quyền / body |
| --- | --- |
| POST `/vnpay/create`, `/vnpay/retry` | Customer: `{ orderId, idempotencyKey }` |
| GET `/vnpay/return` | Callback có chữ ký, không cập nhật thanh toán |
| GET `/vnpay/ipn` | Callback có chữ ký, trả RspCode/Message đúng casing |
| GET `/orders/{id}/status` | Chủ đơn, admin, hoặc nhân viên có assignment; không trả HashSecret hoặc URL giao dịch người khác |
| POST `/cash/confirm-collection` | Nhân viên thu được chỉ định: `{ orderId }` |
| POST `/cash/customer-confirm` | Chủ đơn: `{ orderId, paid: true/false }` |
| GET `/admin/payments?page=1&status=...` | Admin: số liệu và danh sách phân trang |
| GET `/admin/payments/{id}` | Admin: giao dịch, lịch sử, hồ sơ hoàn tiền |
| POST `/admin/payments/{id}/collector` | Admin: `{ employeeId, evidence }` |
| POST `/admin/payments/{id}/resolve` | Admin: `{ paid, evidence }`, chỉ đối soát tiền mặt |
| POST `/admin/payments/{id}/refund-request` | Admin: `{ evidence }`, mở hồ sơ đánh giá hoàn toàn phần cho đơn đã trả tiền, kể cả tiền mặt sau hoàn thành; chưa chuyển tiền |
| POST `/admin/transactions/{reference}/reconcile` | Admin: truy vấn VNPay |
| POST `/admin/payments/{id}/refunds/{caseId}/confirm` | Admin: `{ evidence }`, xác nhận hoàn tiền thủ công đã có chứng từ |

Màn hình quản trị: `http://localhost:3003/thanh-toan`. Khách/nhân viên xem phần Thanh toán trong chi tiết đơn/công việc, dữ liệu polling từ backend. Tổng đã xác nhận, chưa xác nhận, tiền mặt báo thu và giá trị đơn được tách riêng; tiền mặt báo thu chưa có xác nhận khách không được coi là doanh thu đã xác nhận. Không lưu số thẻ/OTP.

## Kiểm thử

```text
dotnet run --project backend/tests/CleanHome.IntegrationTests -- --payments
dotnet run --project backend/tests/CleanHome.IntegrationTests -- --booking-creation
dotnet run --project backend/tests/CleanHome.IntegrationTests -- --review-workflows
```

Payment QA dùng HTTP thật, database MariaDB QA riêng, auth/order boundary kiểm soát và khóa ký QA; không dùng tài khoản merchant. Có kiểm tra chữ ký/số tiền, Return không cập nhật, IPN đồng thời, retry, hết hạn không tự coi failed, hủy và callback muộn, đối soát querydr có xác minh chữ ký, mất đồng bộ và khôi phục, quyền thu tiền, hai nhân viên, xác nhận/tranh chấp, hoàn thủ công có audit và không cộng thu/GPS hai lần. Hồi quy hiện có kiểm tra nhận việc, tranh chỗ, lịch, review và thống kê.

Thanh toán thử bằng thẻ VNPay thật trên trang Sandbox và SIT cần bạn điền khóa, đăng ký IPN với VNPay và thực hiện thao tác thẻ/OTP. Các bài QA không thay thế bước xác nhận này.

Nguồn: [PAY/IPN/Return](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html), [QueryDr/refund](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html), [Cloudflare Quick Tunnel](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).

# Quản lý đơn, hủy nhận việc và thùng rác

## Luồng chính

Giữ .NET 10, MariaDB, gateway 4000 và frontend Next.js hiện có. VNPay chỉ tìm nhân viên sau PAID; CASH tìm ngay, chỉ thu tiền sau hoàn thành. Không thay đổi giá, cách tính thời lượng hoặc chia doanh thu cho người thực sự hoàn thành.

Hai migration bổ sung, không reset dữ liệu:

- `databases/staff-db/005_order_lifecycle.sql`: chính sách hủy, yêu cầu hủy nhận việc, lịch sử gỡ phân công và đánh dấu người nhận thay thế.
- `databases/order-db/006_order_lifecycle.sql`: trạng thái lưu trữ mềm, audit quản trị và chống lặp thông báo điều phối.

Assignment vẫn dùng `waiting/accepted/in_progress/done/cancelled` để tương thích lịch, thống kê và các API đang hoạt động. Phân loại `cancelled_by_employee/cancelled_by_admin/cancelled_by_customer` lưu trong `assignment_exits`; yêu cầu pending nằm riêng, không biến assignment thành vị trí trống. Lịch sử cũ chưa có phân loại không được tự suy đoán/backfill.

## Nhân viên hủy nhận việc

Trong Công việc → Công việc đã nhận/Chi tiết có nút Hủy nhận việc, hộp xác nhận và lý do. Chỉ nhận assignment đã accepted/in_progress của chính người gọi; không hủy toàn bộ đơn và không sửa tiền.

- Trước giờ bắt đầu ít nhất `earlyMinutes` (mặc định 120), chưa ai bắt đầu công việc: hủy trực tiếp phần việc đó.
- Sát giờ hoặc đã có người bắt đầu: tạo yêu cầu pending; assignment, lịch và quyền tiếp cận điểm đến vẫn còn cho đến khi được xử lý.
- Admin từ chối: giữ nguyên phân công, lưu người duyệt, lý do và thời điểm.
- Admin duyệt: hủy đúng assignment, giữ các nhân viên khác, giải phóng lịch, tắt quyền xem vị trí cho người đã hủy và mở đợt tìm thay thế có hạn mới.
- Hoàn thành/hủy toàn bộ đơn: không cho gửi hoặc duyệt hủy nhận việc nữa.

Nếu nhân viên hoàn thành phần việc trong lúc chờ duyệt, yêu cầu pending của phần việc đó được đóng và giữ lịch sử; admin không thể duyệt gỡ assignment đã done.

Đơn 3 người có một người rút sẽ còn 2 người. Lời mời mới không gửi cho người đang giữ chỗ hoặc người đã tự hủy trong đơn này. Nếu muốn nhận lại, cần admin phân công rõ ràng qua quy trình thủ công khi còn đủ điều kiện. Không xóa assignment, receipt hoặc lịch sử chỉ để tái sử dụng vị trí.

Thay thế trong lúc đang làm giữ nguyên trạng thái IN_PROGRESS. Nếu giờ bắt đầu cũ đã qua, cửa sổ tuyển mới bắt đầu sau thời điểm xử lý 2 phút và kết thúc tại giờ kết thúc cũ; giữ nguyên lịch của những người còn lại. Hết cửa sổ/không đủ người chuyển hỗ trợ, không tự hủy cả đơn. Admin cần xem thực tế phần việc còn lại và lịch phù hợp trước khi duyệt; hệ thống không tự gia hạn lịch hoặc thay giá.

Admin có thể phân công thủ công vào vị trí thay thế đã được duyệt của đơn đang làm; backend kiểm tra phần khung giờ còn lại, kỹ năng, lịch trống và số chỗ. Nhân viên được phân công thủ công vẫn phải tiếp nhận. Không dùng đường này để tăng số người vượt yêu cầu hoặc thêm phân công khi không có vị trí trống.

Các thao tác đi qua workflow order → staff hiện có, khóa đơn rồi khóa nhân viên theo thứ tự, inbox deduplicate và receipt bền vững. Mất mạng không được thông báo hủy thành công trước khi workflow xác nhận. API pending trả operation ID để tiếp tục kiểm tra.

## Hủy, dừng, xóa và khôi phục

Admin có thể hủy trực tiếp đơn pending_payment/pending/confirmed với lý do bắt buộc. Không cần giả tạo một yêu cầu của khách. Dừng điều phối, vô hiệu hóa lời mời, hủy các phân công chưa hoàn thành và ghi nguồn hủy là admin.

Đơn in_progress dùng nút riêng **Dừng công việc và đối soát**, có xác nhận rõ ràng; thao tác hủy thông thường bị từ chối. Dừng giữ lịch sử phần việc done và gỡ các phần còn hoạt động, không tự coi dịch vụ đã hoàn thành. Tiền đã thu cần đối soát/hoàn theo quy trình thanh toán, không tự tính rằng đã hoàn tiền hoặc tự chuyển giao dịch thành REFUNDED.

Khách vẫn dùng yêu cầu hủy hiện có. Khách có thể yêu cầu khi đang làm nhưng cần admin xem xét; duyệt hủy thông thường không áp dụng cho đơn đang làm, admin phải dùng quy trình dừng và đối soát. Trong lúc có yêu cầu hủy khách, các thao tác thay đổi công việc bị giữ lại theo quy tắc cũ.

**Thùng rác chỉ là lưu trữ mềm trong quản trị:**

- Chỉ đơn cancelled/completed, không có workflow đang chạy và nghĩa vụ thanh toán đã được kiểm tra.
- Không cho xóa nếu còn giao dịch chờ xác minh, tiền mặt chưa được xác nhận, tranh chấp hoặc hoàn tiền pending. Dịch vụ thanh toán không truy cập được thì từ chối xóa, không mặc định an toàn.
- Lưu deleted_at/deleted_by/delete_reason trong `booking_archives`, giữ audit từng lần xóa/khôi phục.
- Danh sách admin mặc định ẩn, `trash=true` chỉ hiển thị đơn đã xóa, kể cả khi lọc theo khách. Khách vẫn giữ lịch sử, thống kê và đánh giá vẫn giữ dữ liệu.
- Khôi phục chỉ hiện lại trong admin, không mở lại đơn, không phát lời mời, không đổi phân công/thanh toán.
- Không có API xóa vĩnh viễn trong thay đổi này.

Đơn VNPay bị hủy vẫn giữ giao dịch; callback trả tiền muộn xử lý đối soát/hoàn theo payment-service, không mở lại đơn. Nhân viên rút việc không làm thay đổi trạng thái tiền của cả đơn. Người thu CASH được payment worker xét lại nếu người thu cũ không còn là nhân viên đang đảm nhận.

## API

Qua gateway, cần bearer token. POST workflow dùng `{ idempotencyKey, expectedVersion?, reason, assignmentId? }`; lý do 3–2000 ký tự.

| API | Quyền / hành vi |
| --- | --- |
| POST `/api/orders/staff/orders/{id}/withdraw` | Staff, assignmentId của chính mình |
| GET `/api/staff/me/withdrawals?bookingId=...` | Yêu cầu/lịch sử của chính nhân viên |
| GET `/api/staff/admin/withdrawals?bookingId=...` | Admin đọc yêu cầu và lịch sử |
| POST `/api/orders/admin/orders/{id}/withdrawals/{requestId}/review` | Admin, bổ sung `approve: true/false`, assignmentId và lý do |
| GET/PUT `/api/staff/admin/withdrawal-policy` | Admin; PUT `{ earlyMinutes: 120 }`, 0–10080 phút |
| POST `/api/orders/admin/orders/{id}/cancel` | Admin hủy đơn chưa bắt đầu |
| POST `/api/orders/admin/orders/{id}/stop` | Admin dừng đơn in_progress, chuyển xử lý đối soát |
| GET `/api/orders/admin/orders/{id}/archive` | Admin xem metadata thùng rác và audit |
| POST `/api/orders/admin/orders/{id}/archive` | Admin; `{ reason, expectedVersion? }` |
| POST `/api/orders/admin/orders/{id}/restore` | Admin; body như archive |
| GET `/api/orders/admin/orders?trash=true` | Danh sách thùng rác, giữ phân trang/lọc cũ |
| GET `/api/orders/operations/{id}` | Theo dõi workflow pending, đúng actor hoặc admin |
| GET `/api/orders/notifications` | Customer chỉ dữ liệu của mình; admin toàn bộ |
| GET `/api/orders/notifications/events` | SSE cùng quyền, bearer header, kiểm tra lại phiên/vai trò |

Route nội bộ kiểm tra nghĩa vụ thanh toán bị gateway chặn như các `/internal/*` khác, cần service key khi gọi giữa service.

## Thông báo, lịch và thống kê

Khách/admin có bảng Thông báo và toast theo SSE. Nguồn là history đã commit, ID tăng dần; reconnect không phát lại cùng sự kiện. Không gửi token trong URL. Các trạng thái thanh toán chỉ thông báo khi thực sự đổi; vòng điều phối lưu hash trạng thái/số đã nhận để tránh lặp từng lần worker chạy. Nhân viên dùng SSE công việc hiện có cùng danh sách yêu cầu duyệt tự đồng bộ.

Thông báo gồm tạo đơn, thay đổi thanh toán, đang tìm/đủ người/cần hỗ trợ, nhận việc, yêu cầu hủy, gỡ người tìm thay thế, quyết định duyệt, hủy/dừng và hoàn thành. Nội dung khách về hủy phần việc là thông báo chung, không đưa lý do sức khỏe/cá nhân của nhân viên vào thông báo cho khách. Lý do chi tiết chỉ ở API nhân viên sở hữu hoặc admin.

Lịch trống được giải phóng bằng assignment cancelled; các bộ lọc trùng ca và số chỗ tiếp tục dùng trạng thái tương thích. Stats thêm số tự hủy, admin hủy, khách hủy và nhận thay thế. Không cộng người đã rút vào số người phân bổ của đơn hoàn thành. Thu nhập phân bổ vẫn không phải lương/hoa hồng, không phụ thuộc số lần IPN.

## Kiểm thử

Suite `--review-workflows` mở rộng kiểm tra hủy sớm, sát giờ, duyệt/từ chối, đang làm, đơn ba người còn hai người, thay người, cấm tự nhận lại, từ chối sau hoàn thành, tranh chấp nhận/hủy, admin dừng, thùng rác/khôi phục, quyền thông báo và thống kê phân bổ. Archive checks dùng MariaDB QA và boundary tài chính có thể giả lập từ chối/outage; payment suite kiểm tra các nghĩa vụ bằng repository thật.

Suite `--payments` tiếp tục kiểm tra callback muộn, thanh toán/hoàn và tính đủ điều kiện lưu trữ. `--booking-creation` giữ kiểm tra giá/snapshot/atomic tạo đơn CASH/VNPAY. Tất cả dùng database QA riêng có tên được kiểm tra và tự dọn; không xóa dữ liệu ứng dụng.

VNPay QueryDr/IPN vẫn phụ thuộc cấu hình và khả năng kết nối merchant Sandbox. Thay đổi quản lý đơn không tự xác nhận PAID hoặc bỏ qua xác minh do lỗi 403 trước đó.

# Thống kê cá nhân nhân viên

Trang: `http://localhost:3002/thong-ke-ca-nhan`. Dữ liệu thật, không seed khi mở trang.

API qua gateway, bắt buộc bearer có role staff:

- `GET /api/orders/staff/me/statistics?from=2026-11-01&to=2026-11-30&groupBy=day&page=1&pageSize=10`
- Cùng query: `/api/orders/staff/me/statistics/revenue` trả series biểu đồ.
- Cùng query: `/api/orders/staff/me/statistics/history` trả danh sách hoàn thành có phân trang.

API chính trả summary, revenue, history trong cùng kết quả. Frontend dùng API này để
các thẻ, biểu đồ và bảng cập nhật đồng bộ; hủy request cũ khi thay bộ lọc.
Không nhận employeeId/userId để quyết định người được thống kê. ID luôn lấy từ phiên.
Admin/customer trả 403, thiếu phiên trả 401. Query thêm userId không đổi chủ dữ liệu.

`from` và `to` là ngày Việt Nam, tính cả hai ngày; tối đa 366 ngày. `groupBy`:
day/week/month/year; tuần bắt đầu thứ Hai. PageSize 1–100, mặc định 10.
Ngày thiếu/sai/thứ tự ngược trả 400. Bucket không có doanh thu được trả giá trị 0.

## Nguồn dữ liệu và phép tính

Staff-service chỉ đọc các assignments có employees.user_id khớp user đăng nhập,
trả trạng thái, lịch thực hiện, số người nhận việc và thứ tự chia lẻ. API nội bộ
`/internal/statistics/{userId}` yêu cầu service key, gateway chặn toàn bộ /internal.
Order-service đọc các booking ID đó trong DB của mình, không JOIN database xuyên service.
Không lấy thông tin riêng của khách (địa chỉ, điện thoại) vào API thống kê.

Đơn tạo doanh thu khi booking.status=completed **và** phân công của chính nhân viên
có status=done. Thời điểm hoàn thành là mốc completed cuối cùng trong booking_status_history,
không dùng ngày tạo đơn, scheduled_start hoặc ngày nhân viên đầu tiên check-out.
Nếu booking completed thiếu mốc lịch sử, API báo lỗi dữ liệu thay vì tự đoán ngày.

Mỗi booking chỉ tính một lần/người. Mẫu số là số phân công đang accepted/in_progress/done,
loại waiting/cancelled; trong luồng hợp lệ, khi toàn bộ đơn completed các phân công này
đều done. Tiền phân bổ = total_amount chia đều cho số người đó, không phải toàn bộ tiền
đơn cho từng người. Chia theo đơn vị 0,01đ: phần dư được cộng cố định theo employee_id tăng dần,
để tổng phần của mọi người đúng bằng giá trị đơn. Chênh lệch giữa hai người tối đa 0,01đ.
Đây là doanh thu công việc phân bổ, **không phải lương, hoa hồng hoặc tiền đã thanh toán**.

Trong khoảng lọc:
- Received: đơn đã tiếp nhận (accepted/in_progress/done hoặc có dấu mốc nhận việc).
- Completed: đơn và phân công đều hoàn thành, lọc theo ngày hoàn thành Việt Nam.
- Incomplete: công việc chưa hoàn tất cả đơn, gồm waiting/accepted/in_progress và done
  đang chờ đồng nghiệp; lọc theo ngày thực hiện dự kiến.
- Cancelled: booking hoặc phân công đã hủy, lọc theo ngày thực hiện dự kiến.
- Biểu đồ trạng thái tách waiting, accepted, in_progress, chờ đồng nghiệp, completed, cancelled;
  không cộng Received vào các phần biểu đồ vì Received giao nhau với các trạng thái khác.

Đã nhận rồi hủy vẫn có thể đồng thời thuộc Received và Cancelled; KPI không phải các nhóm
loại trừ lẫn nhau. Biểu đồ trạng thái dùng các nhóm loại trừ để tránh đếm trùng.

## Schema và lịch sử cũ

Áp dụng `databases/staff-db/003_assignment_receipts.sql` (đã áp dụng trên XAMPP local).
Bảng assignment_receipts lưu dấu mốc nhận việc cùng transaction với chuyển waiting→accepted;
không thay quy tắc chuyển trạng thái hay response nghiệp vụ cũ.
Migration ghi nhận các phân công cũ accepted/in_progress/done, để accepted_at=null vì không biết
thời điểm tiếp nhận thực tế. Timestamp này không được dùng để suy đoán ngày doanh thu.
Phân công đã cancelled trước migration không có lịch sử tiếp nhận thì không thể khẳng định
đã từng nhận; vẫn được tính hủy, không tự cộng vào Received. Không bịa dữ liệu quá khứ.

## Kiểm tra

- `dotnet run --project tests/CleanHome.IntegrationTests -- --staff-statistics`: chia lẻ,
  tổng phân bổ, trùng booking, ngày hoàn thành UTC/VN, hủy, gom nhóm, giới hạn khoảng ngày.
- `--order-workflows`: MariaDB QA thật, HTTP phân quyền, chia 2 người, summary/chart/history
  khớp, query không thể thay user; kiểm tra lại nhận việc/tiến độ/hủy/cạnh tranh/retry service.
- Dữ liệu local: hai nhân viên của đơn mẫu 350.000đ có 175.000đ/người; nhân viên còn lại 0.
- Frontend dùng Recharts và component sẵn có; loading, lỗi, dữ liệu rỗng, phân trang,
  lọc preset/tùy chọn, tiền VNĐ và thông tin phân bổ đều bằng tiếng Việt.

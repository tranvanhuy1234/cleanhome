# Kết nối frontend với backend .NET

Thanh toán VNPay Sandbox/tiền mặt dùng payment-service cổng 5006 qua gateway. Đặt đơn chọn một trong hai phương thức; chi tiết đơn/công việc hiển thị trạng thái và xác nhận tiền mặt; admin đối soát tại `/thanh-toan`. Cần tự điền HashSecret và đăng ký IPN: [hướng dẫn thanh toán](../../backend/docs/PAYMENTS_API.md).

GPS và phân phối tự động đã nối API thật: khách chọn/xác nhận ghim khi đặt; nhân viên bật sẵn sàng, nhận lời mời realtime và xem bản đồ/chỉ đường sau khi nhận thành công. Admin cấu hình tại `/phan-phoi-tu-dong`. Xem [tài liệu GPS](../../backend/docs/GPS_AUTO_DISPATCH_API.md) về quyền truy cập, migration, thời hạn và dự phòng.

Mặc định ba ứng dụng dùng API thật tại `http://localhost:4000/api`.
Không cần sửa `frontend-legacy`: các file services ở đó chỉ là TODO của bản Vite cũ.
Đặt `NEXT_PUBLIC_API_URL` trong `.env.local` của từng app nếu đổi gateway, rồi khởi động/build lại Next.js.
Chỉ đặt `NEXT_PUBLIC_USE_MOCK=true` khi chủ động cần xem lại bản demo cũ.

## Các phần đã nối

- Đăng ký khách, đăng nhập đúng vai trò, đăng xuất; kiểm tra phiên với server khi tải lại/focus.
- Hồ sơ, sổ địa chỉ có tọa độ, đổi email/mật khẩu và đăng nhập lại.
- Danh mục dịch vụ public, chi tiết, nhóm dịch vụ và giá từ backend. Ảnh minh họa dùng asset hiện có vì API chưa có ảnh dịch vụ.
- Khách xem đơn/lịch sử/chi tiết, yêu cầu hủy và đánh giá từng nhân viên hoàn thành.
- Khách đặt một dịch vụ, chọn số lượng/chi nhánh/địa chỉ/lịch; server tính giá và tạo đơn
  pending rồi chuyển tới chi tiết. Địa chỉ mới thêm tại hồ sơ, sau đó quay lại đặt.
- Admin xem/xác nhận đơn, duyệt/từ chối hủy, phân công nhiều nhân viên, gỡ phân công; chọn kỹ năng bắt buộc.
- Nhân viên đọc các phân công và thời gian thực hiện, tiếp nhận/bắt đầu/hoàn thành.
  Công việc còn hiệu lực có địa chỉ/liên hệ và nút Dẫn đường đến nhà khách, mở Google Maps
  với tọa độ điểm đến hoặc địa chỉ chữ. API kiểm tra đúng nhân viên được phân công trước
  khi trả địa chỉ; không hiển thị nút cho phân công cancelled/done.
- Admin thêm/xóa khoảng lịch, nhân viên xem lịch available/unavailable.
- Admin tra cứu/tạo tài khoản, sửa email và hồ sơ bằng các thao tác riêng, khóa/mở tài khoản.
- Admin thêm/sửa/bật/ngừng dịch vụ, thêm danh mục; xem và ẩn/hiện đánh giá.
- Nhân viên có `/thong-ke-ca-nhan`: KPI, biểu đồ doanh thu/trạng thái, lịch sử hoàn thành,
  bộ lọc ngày và phân trang. Doanh thu chia đều cho người nhận việc, không coi là lương.
  Hợp đồng và cách tính: `backend/docs/STAFF_STATISTICS_API.md`.

Các màn hình dữ liệu thật dùng lại layout, Panel, Field, Btn, Table và badge hiện có.
Trang tổng quan admin/staff hiện ưu tiên danh sách đơn/công việc thật, chưa dựng thống kê bằng dữ liệu demo.
Backend vẫn là nơi quyết định quyền, giá, trạng thái hợp lệ, trùng lịch và điều kiện đánh giá.

## Phiên, dữ liệu và xử lý lỗi

Opaque token lưu riêng từng tab bằng sessionStorage; nếu không lưu được chỉ giữ trong bộ nhớ.
Không lưu mật khẩu hoặc sao chép tài khoản demo sang MySQL. Ba website đăng nhập riêng và cùng đọc dữ liệu từ backend.
Không xóa/ghi đè localStorage demo. Dữ liệu nghiệp vụ thật không dùng localStorage làm nguồn xác nhận thành công.
HTTP 401 đóng phiên hiện tại; lỗi kết nối/validation được hiển thị cho người dùng, không tự quay về dữ liệu mẫu.

Phân công/duyệt hủy/cập nhật tiến độ gửi idempotency key. HTTP 202 chỉ báo đã nhận;
frontend poll operation đến khi succeeded/failed. Khi lỗi mạng và gửi lại cùng nội dung,
giữ cùng key trong màn hình hiện tại. Worker backend vẫn tiếp tục xử lý nếu người dùng đóng trang.
Sau tải lại trang, cần đọc lại trạng thái đơn trước khi tiếp tục; key chưa lưu bền vững qua tải lại.

## Phần còn thiếu

VNPay/hoàn tiền, voucher/gói và báo cáo chưa có API đầy đủ; frontend hiển thị chờ backend.
Chưa nối biểu mẫu quản trị chi nhánh, xác minh hồ sơ nhân viên, kỹ năng nhân viên, sửa danh mục và sửa khoảng lịch;
các API này đã có nhưng cần bổ sung màn hình phù hợp. Chưa có tải file ảnh hoặc ghi chú công việc từ staff.
Danh sách chọn nhân viên/danh mục tải đủ các trang; danh sách đơn/tài khoản/đánh giá phân trang server.

## Chạy và kiểm tra

Khởi động MySQL XAMPP và gateway/auth/user/catalog/order/staff/review theo README backend.
Tại `frontend`, chạy `npm.cmd run dev:customer`, `dev:staff`, `dev:admin` trong các terminal riêng.
Các cổng lần lượt 3001, 3002, 3003. Backend CORS local đã cho phép ba origin này.
Tài khoản demo cũ không phải tài khoản backend. Tạo khách qua đăng ký; admin phải được provision theo hướng dẫn backend.

- `npm.cmd run typecheck`
- `npm.cmd run build`
- `node tests/api-connection.cjs`: kiểm tra adapter với HTTP được mô phỏng, không ghi DB.
- `node tests/browser-connection.cjs`: cần Chrome headless với profile QA riêng, debug port 9227; kiểm tra ba site gọi gateway thật và từ chối login sai, không tạo dữ liệu.

Kiểm thử thao tác thành công xuyên ba vai trò trên trình duyệt cần tài khoản/fixture QA phù hợp;
không coi smoke test đăng nhập sai là đã kiểm thử toàn bộ nghiệp vụ qua giao diện.

Kết quả kiểm tra trong lượt kết nối: build production ba website đạt; adapter test đạt;
Chrome smoke test ba website gọi gateway local thật đạt; backend `--review-workflows`
đạt trên database QA riêng và đã dọn database QA sau kiểm tra. Chưa kiểm thử toàn bộ
thao tác ghi thành công từ giao diện bằng tài khoản QA ba vai trò.

Bổ sung: luồng đặt dịch vụ đã được kiểm tra thành công bằng Chrome với khách mẫu;
admin đọc được cùng đơn pending và tổng giá do server tính. Đơn kiểm thử được giữ lại.
Xem hợp đồng/ràng buộc tại `backend/docs/BOOKING_API.md`.

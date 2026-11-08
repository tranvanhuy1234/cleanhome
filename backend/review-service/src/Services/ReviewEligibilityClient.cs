using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CleanHome.Review.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Review.Api.Services;

public sealed class ReviewEligibilityClient(IHttpClientFactory clients, IConfiguration configuration) : IReviewEligibilityClient
{
    public async Task<long> ValidateAsync(long bookingId, long employeeId, string token, CancellationToken ct)
    {
        using var orderResponse = await Send("/me/" + bookingId, token, ct);
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (order.GetProperty("status").GetString() != "completed") throw new ApiException(409, "Chỉ đánh giá được đơn đã hoàn thành.");
        using var assignmentResponse = await Send("/me/" + bookingId + "/assignments", token, ct);
        var assignments = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentSnapshot[]>(ct) ?? [];
        var assignment = assignments.FirstOrDefault(a => a.EmployeeId == employeeId && a.Status == "done");
        return assignment?.Id ?? throw new ApiException(409, "Nhân viên không có phân công đã hoàn thành trong đơn này.");
    }
    private async Task<HttpResponseMessage> Send(string path, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, (configuration["Services:OrderUrl"] ?? "http://localhost:5004").TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            var response = await clients.CreateClient("review-order").SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;
            var code = response.StatusCode; response.Dispose();
            if (code == HttpStatusCode.NotFound) throw new ApiException(404, "Không tìm thấy đơn thuộc tài khoản.");
            if (code == HttpStatusCode.Unauthorized) throw new ApiException(401, "Phiên đăng nhập không còn hiệu lực.");
            throw new ApiException(503, "Chưa kiểm tra được điều kiện đánh giá.");
        }
        catch (HttpRequestException) { throw new ApiException(503, "Dịch vụ đơn chưa sẵn sàng."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(503, "Kiểm tra đơn quá thời gian."); }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Services;

public sealed class StaffWorkflowClient(IHttpClientFactory clients, IConfiguration configuration) : IStaffWorkflowClient
{
    public async Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, "/internal/operations"); request.Content = JsonContent.Create(command);
        using var response = await Send(request, ct);
        return await response.Content.ReadFromJsonAsync<StaffWorkflowResult>(ct) ?? throw new ApiException(503, "Phản hồi nhân sự không hợp lệ.");
    }
    public async Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, "/internal/bookings/" + bookingId + "/assignments"); using var response = await Send(request, ct);
        return await response.Content.ReadFromJsonAsync<AssignmentSnapshot[]>(ct) ?? [];
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, (configuration["Services:StaffUrl"] ?? "http://localhost:5005").TrimEnd('/') + path);
        request.Headers.Add("X-CleanHome-Service-Key", configuration["InternalApi:Key"]); return request;
    }
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await clients.CreateClient("staff-workflow").SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;
            using (response)
            {
                var status = (int)response.StatusCode;
                if (status is 400 or 404 or 409)
                {
                    string? title = null;
                    try { var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct); if (body.TryGetProperty("title", out var value)) title = value.GetString(); }
                    catch (JsonException) { }
                    throw new ApiException(status, title ?? "Thao tác nhân sự không được chấp nhận.");
                }
                throw new ApiException(503, "Dịch vụ nhân sự chưa sẵn sàng hoặc chưa được cấu hình đúng.");
            }
        }
        catch (HttpRequestException) { throw new ApiException(503, "Dịch vụ nhân sự chưa sẵn sàng."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(503, "Dịch vụ nhân sự phản hồi quá thời gian."); }
    }
}

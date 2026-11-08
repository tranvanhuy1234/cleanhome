using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MySqlConnector;

namespace CleanHome.ServiceDefaults;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            ApiException error => (error.Status, error.Message),
            MySqlException { Number: 1062 } => (409, "Thông tin đã được sử dụng. Vui lòng kiểm tra lại."),
            MySqlException => (503, "Chưa thể truy cập dữ liệu. Vui lòng thử lại sau."),
            _ => (0, "")
        };
        if (status == 0) return false;
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: message).ExecuteAsync(context);
        return true;
    }
}

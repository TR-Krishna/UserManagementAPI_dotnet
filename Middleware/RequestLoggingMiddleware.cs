using System.Timers;

namespace UserManagementApi.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next,ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path =context.Request.Path;
        var sw=System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await next(context);
            logger.LogInformation("{Method}{Path}-> {StatusCode}({Elapsed ms})",method,path,context.Response.StatusCode,sw.ElapsedMilliseconds);
        }
        catch(Exception ex)
        {
            logger.LogInformation("{Method}{Path}-> {StatusCode}({Elapsed ms})",method,path,StatusCodes.Status500InternalServerError,sw.ElapsedMilliseconds);
           throw;
        }
    }
    
}
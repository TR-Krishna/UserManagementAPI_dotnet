namespace UserManagementApi.Middleware;


public class ErrorhandlingMiddleware(RequestDelegate next,ILogger<ErrorhandlingMiddleware>logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch(BadHttpRequestException ex)
        {
            await WriteErrorAsync(context,ex.StatusCode,"Bad request");
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Unhandled exception for {Method}{Path}",context.Request.Method,context.Request.Path);
            await WriteErrorAsync(context,StatusCodes.Status500InternalServerError,"Internal Server Error");
        }
    }

    private async Task WriteErrorAsync(HttpContext context,int statusCode,string message)
    {
        if(context.Response.HasStarted)return;
        context.Response.Clear();
        context.Response.StatusCode=statusCode;
        await context.Response.WriteAsJsonAsync(new {error=message});
    }

}


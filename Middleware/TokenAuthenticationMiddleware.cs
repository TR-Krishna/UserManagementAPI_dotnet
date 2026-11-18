using System.Security.Cryptography;
using System.Text;

namespace UserManagementApi.Middleware;

public class TokenAuthenticationMiddleware(RequestDelegate next,IConfiguration config)
{
    private readonly string[] _validTokens = config.GetSection("Auth:Tokens").Get<string[]>()??[];

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<TokenAuthenticationMiddleware>>();
logger.LogWarning("Auth header: '{Header}', configured tokens: {Count}",
    context.Request.Headers.Authorization.ToString(), _validTokens.Length);
        if(context.Request.Path.StartsWithSegments("/openapi"))
        {
            await next(context);return;
        }
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && IsValid(header[prefix.Length..].Trim()))
        {
            await next(context);return;
        }

        context.Response.StatusCode=StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate="Bearer";
        await context.Response.WriteAsJsonAsync(new{error="UnAuthorized"});
    }

    private bool IsValid(string token)
    {
        if(token.Length == 0)return false;
        var candidate = Encoding.UTF8.GetBytes(token);
        return _validTokens.Any(t=>CryptographicOperations.FixedTimeEquals(candidate,Encoding.UTF8.GetBytes(t)));
    }

}
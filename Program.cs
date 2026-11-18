using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UserManagementApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();

var app = builder.Build();
app.UseMiddleware<ErrorhandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();   // OpenAPI document at /openapi/v1.json

var users = app.MapGroup("/api/users").WithTags("Users");
// For production, secure it, e.g.: users.RequireAuthorization("HrOrItPolicy");

// GET /api/users
users.MapGet("/", (IUserStore store, Department? department, bool? isActive,
                   int page = 1, int pageSize = 20) =>
{
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var query = store.All().AsEnumerable();
    if (department is not null) query = query.Where(u => u.Department == department);
    if (isActive is not null)   query = query.Where(u => u.IsActive == isActive);

    var items = query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
    return Results.Ok(new
    {
        page,
        pageSize,
        total = items.Count,
        data = items.Skip((page - 1) * pageSize).Take(pageSize)
    });
});

// GET /api/users/{id}
users.MapGet("/{id:guid}", (Guid id, IUserStore store) =>
    store.Get(id) is { } user ? Results.Ok(user) : Results.NotFound());

// POST /api/users
users.MapPost("/", (UserRequest req, IUserStore store) =>
{
    if (!TryValidate(req, out var errors)) return Results.ValidationProblem(errors);
    if (store.EmailExists(req.Email))
        return Results.Conflict(new { error = "A user with this email already exists." });

    var now = DateTime.UtcNow;
    var user = new User(Guid.NewGuid(), req.FirstName, req.LastName, req.Email,
                        req.Department, req.JobTitle, req.IsActive, now, now);
    store.Add(user);
    return Results.Created($"/api/users/{user.Id}", user);
});

// PUT /api/users/{id}
users.MapPut("/{id:guid}", (Guid id, UserRequest req, IUserStore store) =>
{
    if (!TryValidate(req, out var errors)) return Results.ValidationProblem(errors);
    if (store.Get(id) is not { } existing) return Results.NotFound();
    if (store.EmailExists(req.Email, excludeId: id))
        return Results.Conflict(new { error = "A user with this email already exists." });

    var updated = existing with
    {
        FirstName = req.FirstName,
        LastName = req.LastName,
        Email = req.Email,
        Department = req.Department,
        JobTitle = req.JobTitle,
        IsActive = req.IsActive,
        UpdatedAt = DateTime.UtcNow
    };
    store.Update(updated);
    return Results.Ok(updated);
});

// DELETE /api/users/{id}
users.MapDelete("/{id:guid}", (Guid id, IUserStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound());

app.Run();

// ---------- Helpers ----------
static bool TryValidate(object model, out Dictionary<string, string[]> errors)
{
    var results = new List<ValidationResult>();
    var ok = Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    errors = results
        .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
        .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Invalid").ToArray());
    return ok;
}

// ---------- Models ----------
public enum Department { HR, IT, Finance, Operations, Other }

public record User(
    Guid Id, string FirstName, string LastName, string Email,
    Department Department, string JobTitle, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public record UserRequest(
    [property: Required, StringLength(50)] string FirstName,
    [property: Required, StringLength(50)] string LastName,
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required] Department Department,
    [property: Required, StringLength(100)] string JobTitle,
    bool IsActive = true);

// ---------- Store (swap for EF Core / SQL later) ----------
public interface IUserStore
{
    IEnumerable<User> All();
    User? Get(Guid id);
    bool EmailExists(string email, Guid? excludeId = null);
    void Add(User user);
    void Update(User user);
    bool Remove(Guid id);
}

public class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public IEnumerable<User> All() => _users.Values;
    public User? Get(Guid id) => _users.GetValueOrDefault(id);
    public bool EmailExists(string email, Guid? excludeId = null) =>
        _users.Values.Any(u => u.Id != excludeId &&
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    public void Add(User user) => _users[user.Id] = user;
    public void Update(User user) => _users[user.Id] = user;
    public bool Remove(Guid id) => _users.TryRemove(id, out _);
}
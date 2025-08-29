// var builder = WebApplication.CreateBuilder(args);
// var app = builder.Build();

// app.MapGet("/", () => "Hello World!");

// app.Run();

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core with SQLite
builder.Services.AddDbContext<UserDb>(opt =>
    opt.UseSqlite("Data Source=users.db"));

var app = builder.Build();

// Serve wwwroot (for index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDb>();
    db.Database.EnsureCreated();
}

// REST API
app.MapGet("/api/users", async (UserDb db) =>
    await db.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync());

app.MapPost("/api/users", async (UserDb db, UserDto dto) =>
{
    // Basic validation
    if (string.IsNullOrWhiteSpace(dto.FirstName) ||
        string.IsNullOrWhiteSpace(dto.LastName) ||
        string.IsNullOrWhiteSpace(dto.Email))
    {
        return Results.BadRequest("All fields are required.");
    }

    var user = new User
    {
        FirstName = dto.FirstName.Trim(),
        LastName  = dto.LastName.Trim(),
        Email     = dto.Email.Trim()
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

app.Run();

// --- Data layer ---
public class UserDb : DbContext
{
    public UserDb(DbContextOptions<UserDb> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
}

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName  { get; set; } = "";
    public string Email     { get; set; } = "";
}

// DTO used for POST payload
public record UserDto(string FirstName, string LastName, string Email);


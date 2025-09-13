// var builder = WebApplication.CreateBuilder(args);
// var app = builder.Build();

// app.MapGet("/", () => "Hello World!");

// app.Run();

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS
builder.Services.AddCors();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Serve wwwroot (for index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader()
);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed groups if empty
    if (!db.Groups.Any())
    {
        db.Groups.AddRange(
            new Group { Name = "Admin" },
            new Group { Name = "User" }
        );
        db.SaveChanges();
    }

    // Seed default admin user if none exists
    if (!db.Users.Any(u => u.Group.Name == "Admin"))
    {
        var adminGroup = db.Groups.First(g => g.Name == "Admin");

        db.Users.Add(new User
        {
            UserName = "admin",
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@example.com",
            Password = "admin1",
            GroupId = adminGroup.Id
        });

        db.SaveChanges();
    }
}

app.MapGet("/api/users", async (
    [FromServices] AppDbContext db,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10) =>
{
    var users = await db.Users
        .Include(u => u.Group)
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // return minimal info including group name
    var result = users.Select(u => new
    {
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.UserName,
        Group = u.Group != null ? u.Group.Name : null
    });

    return Results.Ok(result);
});

app.MapPost("/api/users", async (
    [FromServices] AppDbContext db,
    [FromBody] UserDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.FirstName) ||
        string.IsNullOrWhiteSpace(dto.LastName) ||
        string.IsNullOrWhiteSpace(dto.Email) ||
        string.IsNullOrWhiteSpace(dto.Password) ||
        string.IsNullOrWhiteSpace(dto.Username))
    {
        return Results.BadRequest("All fields are required.");
    }

    var user = new User
    {
        FirstName = dto.FirstName.Trim(),
        LastName = dto.LastName.Trim(),
        Email = dto.Email.Trim(),
        Password = dto.Password.Trim(),
        UserName = dto.Username.Trim(),
        GroupId = dto.GroupId
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", user);
});

// GET all groups
app.MapGet("/api/groups", async ([FromServices] AppDbContext db) =>
{
    var groups = await db.Groups.OrderBy(g => g.Name).ToListAsync();
    return Results.Ok(groups);
});

// PUT update user
app.MapPut("/api/users/{id:int}", async (
    [FromServices] AppDbContext db,
    [FromRoute] int id,
    [FromBody] UserDto dto) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();

    user.FirstName = dto.FirstName.Trim();
    user.LastName = dto.LastName.Trim();
    user.Email = dto.Email.Trim();
    user.UserName = dto.Username.Trim();
    user.Password = dto.Password.Trim();
    user.GroupId = dto.GroupId;

    await db.SaveChangesAsync();
    return Results.Ok(user);
});

// DELETE user
app.MapDelete("/api/users/{id:int}", async (
    [FromServices] AppDbContext db,
    [FromRoute] int id) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();

    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// LOGIN endpoint
app.MapPost("/api/login", async (
    [FromServices] AppDbContext db,
    [FromBody] LoginRequest login) =>
{
    var user = await db.Users
        .Include(u => u.Group)
        .FirstOrDefaultAsync(u => u.UserName == login.UserName && u.Password == login.Password);

    if (user == null) return Results.Unauthorized();

    // Return minimal info for now
    return Results.Ok(new
    {
        user.Id,
        user.UserName,
        user.FirstName,
        user.LastName,
        user.Email,
        Group = user.Group?.Name
    });
});

app.MapGet("/api/users/check-username", async (
    [FromServices] AppDbContext db,
    [FromQuery] string username) =>
{
    if (string.IsNullOrWhiteSpace(username))
        return Results.BadRequest(new { message = "Username is required" });

    var exists = await db.Users.AnyAsync(u => u.UserName == username.Trim());

    return Results.Ok(new { available = exists });
});


app.Run();

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options){}

    // Tables
    public DbSet<User> Users { get; set; }
    public DbSet<Group> Groups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed Groups
        modelBuilder.Entity<Group>().HasData(
            new Group { Id = 1, Name = "Admin" },
            new Group { Id = 2, Name = "User" }
        );

        // Seed Default Admin User
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                UserName = "admin",
                FirstName = "System",
                LastName = "Administrator",
                Email = "admin@example.com",
                Password = "admin1", // plaintext for now
                GroupId = 1
            }
        );
    }
}

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = "";
    public string LastName  { get; set; } = "";
    public string Email     { get; set; } = "";
    public string Password { get; set; } = ""; // <-- added

    // Foreign key
    public int GroupId { get; set; }
    public Group? Group { get; set; }
}

// DTO used for POST payload
public record UserDto(string FirstName, string LastName, string Password, string Username, string Email,int GroupId);

public class Group
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<User> Users { get; set; } = new();
}

public record LoginRequest(string UserName, string Password);


using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Authentication().AddDevJwtBearer();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// NOTE: These still need to be added manually as we can't affect the app's pipeline from an IStartupFilter
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello, World!");

app.MapGet("/protected", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name ?? "unknown"}")
    .RequireAuthorization();

app.Run();

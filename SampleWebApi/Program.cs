using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// This would actually be like:
// builder.Authentication.AddJwtBearer();
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

// These wouldn't be required if WebApplicationBuilder added them by default
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello, World!");

app.MapGet("/protected", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name ?? "unknown"}")
    .RequireAuthorization();

app.Run();

using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// This would actually be a property, i.e. builder.Authentication.AddJwtBearer();
builder.Authentication().AddJwtBearer();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// The middleware actually wouldn't be required because WebApplicationBuilder would add them by
// by default if any authentication scheme were added via builder.Authentication
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello, World!");

app.MapGet("/protected", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name ?? "unknown"}")
    .RequireAuthorization();

app.Run();

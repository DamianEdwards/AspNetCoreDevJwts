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

app.MapGet("/protected", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name}!")
    .RequireAuthorization();

// This is an extension method allows simple protection of endpoints based on role claims
app.MapGet("/roles", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name}, you have the required role claims.")
    .RequireRole("SampleRole");

// It will be possible in 7.0.0-preview.4 to add authorization rules (policies) directly to endpoints
//app.MapGet("/scopes", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name}, you have the required scope claims.")
//    .RequireAuthorization(p => p.RequireClaim("scope", "protected:read"));

app.Run();

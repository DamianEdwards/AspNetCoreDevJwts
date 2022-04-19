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

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello, World!");

app.MapGet("/protected", (HttpContext context) => $"Hello {context.User.Identity?.Name ?? "unknown"}")
    .RequireAuthorization();

app.Run();

using Microsoft.EntityFrameworkCore;
using Server.Application.Interfaces;
using Server.Application.Services;
using Server.Infrastructure.Data;
using Server.Api.Middleware;
using Server.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with SQL LocalDB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=OlmezServer;Trusted_Connection=true;TrustServerCertificate=true"
    ));

// Add services
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICommandService, CommandService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

// Add Agent Connection Manager (Singleton - tüm uygulama boyunca tek instance)
builder.Services.AddSingleton<AgentConnectionManager>();

// Add Controllers
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Olmez Server API", 
        Version = "v1",
        Description = "Remote management server API for Olmez Agent",
        Contact = new() { 
            Name = "Site Telekom",
            Email = "omer.olmez@sitetelekom.com.tr"
        }
    });
});

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable WebSockets
app.UseWebSockets();

// Add WebSocket middleware for agent connections (MeshCentral compatible)
app.UseMiddleware<AgentWebSocketMiddleware>();

// HTTPS redirection (production'da aktif)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();

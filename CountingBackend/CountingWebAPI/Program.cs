using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CountingWebAPI.Data;
using CountingWebAPI.Services;
using CountingWebAPI.Hubs;
using CountingWebAPI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateBootstrapLogger();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext());

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>();
if (appSettings == null) throw new InvalidOperationException("AppSettings is not configured.");

FFmpegBinariesHelper.RegisterFFmpegBinaries(appSettings.FfmpegPath);

// Use the SQLite implementation of the database helper
builder.Services.AddSingleton<IDatabaseHelper, SqliteDatabaseHelper>();
builder.Services.AddMemoryCache();

// Register application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ActionService>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ZoneService>();

builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<RoiService>();
builder.Services.AddSingleton<VideoProcessingManager>(); 
builder.Services.AddSingleton<ActionExecutionService>();

builder.Services.AddSignalR().AddJsonProtocol(options => {
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddControllers().AddJsonOptions(options => {
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, Scheme = "Bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header, Description = "JWT Authorization header. Example: \"Authorization: Bearer {token}\"" }); options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, new string[] { } } }); });

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.Key)) throw new ArgumentNullException(nameof(jwtSettings), "JWT Key not in appsettings.");
if (jwtSettings.Key.Length < 32) throw new ArgumentException("JWT Key must be at least 32 chars for HS256.");
var key = Encoding.ASCII.GetBytes(jwtSettings.Key);

builder.Services.AddAuthentication(o => { o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; })
    .AddJwtBearer(o => {
        o.RequireHttpsMetadata = builder.Environment.IsProduction(); o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key), ValidateIssuer = true, ValidIssuer = jwtSettings.Issuer, ValidateAudience = true, ValidAudience = jwtSettings.Audience, ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
        o.Events = new JwtBearerEvents { OnMessageReceived = ctx => { var token = ctx.Request.Query["access_token"]; var path = ctx.HttpContext.Request.Path; if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/crowdmonitorhub")) { ctx.Token = token; } return Task.CompletedTask; } };
    });

builder.Services.AddCors(o => {
    o.AddPolicy("ApiCorsPolicy", p => {
        p.WithOrigins("http://localhost:4200")
         .AllowAnyMethod()
         .AllowAnyHeader();
    });

    o.AddPolicy("SignalRCorsPolicy", p => {
        p.WithOrigins("http://localhost:4200", "http://localhost:3000", "http://127.0.0.1:3000")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Serve static files from wwwroot. Must be before UseRouting.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("ApiCorsPolicy");
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CrowdMonitorHub>("/crowdmonitorhub").RequireCors("SignalRCorsPolicy");

// Database Initializer Scope
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitialization");
    try
    {
        var dbHelper = services.GetRequiredService<IDatabaseHelper>();
        await DbInitializer.InitializeAsync(dbHelper, services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DB init error.");
    }
}

var videoManager = app.Services.GetRequiredService<VideoProcessingManager>();
await videoManager.StartAsync(CancellationToken.None);

var actionService = app.Services.GetRequiredService<ActionExecutionService>();
await actionService.StartAsync(CancellationToken.None);


app.Lifetime.ApplicationStopping.Register(() =>
{
    videoManager.StopAsync(CancellationToken.None).Wait();
    actionService.StopAsync(CancellationToken.None).Wait();
});

// This catch-all route is for the SPA. It must be after all other endpoints.
app.MapFallbackToFile("index.html");

app.Run();
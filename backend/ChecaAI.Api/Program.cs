using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Application.Interfaces;
using ChecaAI.Application.Services;
using ChecaAI.Infrastructure.Services;
using ChecaAI.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// CORS — allow frontend (Next.js dev + Expo) and SignalR WebSocket
var extraOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
var allowedOrigins = builder.Environment.IsProduction()
    ? new[] { "https://checa.ai", "https://www.checa.ai" }.Concat(extraOrigins).ToArray()
    : new[] { "http://localhost:3000", "http://localhost:3001", "http://localhost:19006", "https://checa-ai.vercel.app" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // required for SignalR
    });
});

// Database configuration
builder.Services.AddDbContext<ChecaAIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// SignalR for real-time plenary alerts
builder.Services.AddSignalR();

// HTTP Client configuration
builder.Services.AddHttpClient<IChamberOfDeputiesService, ChamberOfDeputiesService>();
builder.Services.AddHttpClient<ISenateService, SenateApiService>();
builder.Services.AddHttpClient<IWebScrapingService, WebScrapingService>();
builder.Services.AddHttpClient<ITseService, TseService>();
builder.Services.AddHttpClient<ICguService, CguService>();
builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>();
builder.Services.AddHttpClient<IClaudeService, ClaudeService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120); // Claude can be slow on complex prompts
});

// Service registration
builder.Services.AddScoped<IChamberOfDeputiesService, ChamberOfDeputiesService>();
builder.Services.AddScoped<ISenateService, SenateApiService>();
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();
builder.Services.AddScoped<ITseService, TseService>();
builder.Services.AddScoped<ICguService, CguService>();
builder.Services.AddScoped<IVotingAlertEngine, VotingAlertEngine>();
builder.Services.AddScoped<IClaudeService, ClaudeService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Ignore circular references (entity navigation properties)
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ChecaAI API", Version = "v1",
        Description = "API de transparência política brasileira — parlamentares, votações, despesas, IA." });
    c.UseInlineDefinitionsForEnums();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only redirect to HTTPS in production — in dev the self-signed cert breaks browser fetches
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// SignalR Hub endpoint
app.MapHub<PlenaryHub>("/hubs/plenary");

app.Run();

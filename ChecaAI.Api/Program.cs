using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Application.Interfaces;
using ChecaAI.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database configuration
builder.Services.AddDbContext<ChecaAIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// HTTP Client configuration
builder.Services.AddHttpClient<IChamberOfDeputiesService, ChamberOfDeputiesService>();
builder.Services.AddHttpClient<IWebScrapingService, WebScrapingService>();

// Service registration
builder.Services.AddScoped<IChamberOfDeputiesService, ChamberOfDeputiesService>();
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

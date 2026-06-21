using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Worker.Configuration;
using ChecaAI.Worker.Services;
using ChecaAI.Worker.Services.StateDeputy;
using ChecaAI.Application.Interfaces;
using ChecaAI.Application.Services;
using ChecaAI.Infrastructure.Services;

// Allow DateTime with Kind=Unspecified to be written as UTC to PostgreSQL
// (Npgsql 9.x rejects Unspecified by default for timestamp with time zone)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(); // For Windows Event Log

// Configure options
builder.Services.Configure<DataSyncOptions>(
    builder.Configuration.GetSection(DataSyncOptions.SectionName));
builder.Services.Configure<SenateApiOptions>(
    builder.Configuration.GetSection(SenateApiOptions.SectionName));
builder.Services.Configure<ChamberApiOptions>(
    builder.Configuration.GetSection(ChamberApiOptions.SectionName));
builder.Services.Configure<PlenaryWatcherOptions>(
    builder.Configuration.GetSection(PlenaryWatcherOptions.SectionName));

// Configure Entity Framework
builder.Services.AddDbContext<ChecaAIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure HTTP clients
builder.Services.AddHttpClient<ISenateScrapperService, SenateScrapperService>(client =>
{
    var baseUrl = builder.Configuration["SenateApi:BaseUrl"] ?? "https://legis.senado.leg.br/dadosabertos";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");

    if (TimeSpan.TryParse(builder.Configuration["SenateApi:RequestTimeout"], out var timeout))
    {
        client.Timeout = timeout;
    }
});

builder.Services.AddHttpClient<IChamberScrapperService, ChamberScrapperService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
});

builder.Services.AddHttpClient("StateDeputy", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
    client.Timeout = TimeSpan.FromMinutes(3);
});

// Named client for Câmara dos Deputados dadosabertos API
builder.Services.AddHttpClient("Chamber", client =>
{
    client.BaseAddress = new Uri("https://dadosabertos.camara.leg.br");
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// SSL-bypass client for government APIs with invalid/self-signed certificates (ES, CE)
builder.Services.AddHttpClient("StateDeputyInsecure", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
    client.Timeout = TimeSpan.FromMinutes(3);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
});

// HTTP client for PlenaryWatcher (polling Câmara + Senado APIs)
builder.Services.AddHttpClient("PlenaryWatcher", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HTTP client for TseSeed (CDN ZIP downloads — large files, long timeout)
builder.Services.AddHttpClient("TseSeed", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
    client.Timeout = TimeSpan.FromMinutes(15);
});

// HTTP client for CGU Portal da Transparência (requires chave-api-dados header)
// To bypass AWS WAF: get aws-waf-token cookie from portaldatransparencia.gov.br in Chrome
// (F12 → Application → Cookies) and paste the value into appsettings.json "CguWafToken".
builder.Services.AddHttpClient("Cgu", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://api.portaldatransparencia.gov.br");
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
    client.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
    client.DefaultRequestHeaders.Add("Referer", "https://portaldatransparencia.gov.br/");
    client.Timeout = TimeSpan.FromSeconds(30);
    var apiKey = cfg["CguApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.TryAddWithoutValidation("chave-api-dados", apiKey);
    var wafToken = cfg["CguWafToken"];
    if (!string.IsNullOrWhiteSpace(wafToken))
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"aws-waf-token={wafToken}");
});

// HTTP client for push notifications (Expo Push API)
builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ChecaAI-Worker/1.0");
});

// Configure application services
builder.Services.AddScoped<ISenateScrapperService, SenateScrapperService>();
builder.Services.AddScoped<IDataPersistenceService, DataPersistenceService>();
builder.Services.AddScoped<IChamberScrapperService, ChamberScrapperService>();
builder.Services.AddScoped<IChamberPersistenceService, ChamberDataPersistenceService>();
builder.Services.AddScoped<IStateDeputyPersistenceService, StateDeputyPersistenceService>();
builder.Services.AddScoped<IVotingAlertEngine, VotingAlertEngine>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// Tier 1 — dedicated REST scrapers (SP XML, MG party-iteration, PE/ES/CE JSON, DF REST, PR SSL-bypass)
builder.Services.AddScoped<IStateDeputyScrapperService, AlespScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlmgScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlepeScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlesScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlceScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AldfScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlepScrapperService>();

// Tier 2 — old SAPL installations (MA and any future legacy assemblies)
var legacySaplAssemblies = builder.Configuration.GetSection("SaplLegacyAssemblies").Get<List<SaplAssemblyConfig>>() ?? [];
foreach (var assembly in legacySaplAssemblies)
{
    var config = assembly;
    builder.Services.AddScoped<IStateDeputyScrapperService>(sp =>
        new OldSaplScrapperService(
            config,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<OldSaplScrapperService>>()
        ));
}

// Tier 3 — HTML scrapers for states without public APIs (RJ, GO, RS, SC, PA, AP, BA)
builder.Services.AddScoped<IStateDeputyScrapperService, AlerjScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlegoScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlrsScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlescScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlepaScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AleapScrapperService>();
builder.Services.AddScoped<IStateDeputyScrapperService, AlbaScrapperService>();

// Register one SAPL scrapper per configured state assembly (modern SAPL REST API, includes RN and SE)
var saplAssemblies = builder.Configuration.GetSection("SaplAssemblies").Get<List<SaplAssemblyConfig>>() ?? [];
foreach (var assembly in saplAssemblies)
{
    var config = assembly;
    builder.Services.AddScoped<IStateDeputyScrapperService>(sp =>
        new SaplScrapperService(
            config,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<SaplScrapperService>>()
        ));
}

// Configure hosted services
builder.Services.AddHostedService<SenateDataSyncService>();
builder.Services.AddHostedService<ChamberDataSyncService>();
builder.Services.AddHostedService<StateDeputySyncService>();
builder.Services.AddHostedService<TsePoliticianSeedService>();
builder.Services.AddHostedService<PlenaryWatcherService>();
builder.Services.AddHostedService<PartyDataSyncService>();
builder.Services.AddHostedService<ChamberExpenseSyncService>();
builder.Services.AddHostedService<SenateExpenseSyncService>();
builder.Services.AddHostedService<CguSalarySyncService>();
builder.Services.AddHostedService<CguCabinetStaffSyncService>();
builder.Services.AddHostedService<CguAllowanceSyncService>();
builder.Services.AddHostedService<TseTransparencySyncService>();
builder.Services.AddHostedService<TseElectionResultSyncService>();
builder.Services.AddHostedService<AttendanceSyncService>();
builder.Services.AddHostedService<CommitteeSyncService>();
builder.Services.AddHostedService<VoteProposalSyncService>();
builder.Services.AddHostedService<CpfBackfillService>();

// Build and configure host
var host = builder.Build();

// Ensure database is created and up to date
using (var scope = host.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Checking database connectivity...");

        if (await context.Database.CanConnectAsync())
        {
            logger.LogInformation("Database connectivity confirmed");

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying pending migrations: {Migrations}",
                    string.Join(", ", pendingMigrations));
                await context.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("Database is up to date");
            }
        }
        else
        {
            logger.LogError("Cannot connect to database. Please check connection string and ensure PostgreSQL is running");
            throw new InvalidOperationException("Database connectivity failed");
        }
    }
    catch (Exception ex)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database");
        throw;
    }
}

// Log application startup
var appLogger = host.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("ChecaAI Data Worker started successfully (Senate + Federal Deputies + State Deputies + TSE Seed + Plenary Watcher)");

// Run the application
try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    appLogger.LogCritical(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    appLogger.LogInformation("ChecaAI Data Worker stopped");
}

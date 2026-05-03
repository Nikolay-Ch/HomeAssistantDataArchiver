using HomeAssistantDataArchiver;
using HomeAssistantDataArchiver.DB;
using HomeAssistantDataArchiver.HomeAssistant;
using HomeAssistantDataArchiver.Properties;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    //.AddJsonFile("appsettings.json", true, false) // .net behaviour by default 
    .AddJsonFile("/config/appsettings.json", true, false);
    //.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, false) // .net behaviour by default 
    //.AddEnvironmentVariables(); // .net behaviour by default 

builder.Services
    .Configure<CommonOptions>(builder.Configuration.GetSection(CommonOptions.SectionName))
    .Configure<HomeAssistantOptions>(builder.Configuration.GetSection(HomeAssistantOptions.SectionName))
    .Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.SectionName))
    .Configure<ClickHouseOptions>(builder.Configuration.GetSection(ClickHouseOptions.SectionName));

builder.Services
    .AddSingleton<PostgreDataReader>()
    .AddSingleton<ClickHouseDataManipulator>()
    .AddSingleton<DbMethods>();

builder.Services.AddTransient<HomeAssistantWebSocketClient>();


builder.Services.AddHostedService<WorkerImportEntitiesMapping>();
builder.Services.AddHostedService<WorkerSensorDataMigration>();

var host = builder.Build();
host.Run();

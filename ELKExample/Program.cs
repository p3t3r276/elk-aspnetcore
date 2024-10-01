
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.File;
using System.Reflection;
using static Serilog.Events.LogEventLevel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

ConfigureLogging();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseSerilog();
// builder.Host.UseSerilog((t, f) => f.Enrich.FromLogContext().ReadFrom.Configuration(t.Configuration));
// builder.Host.UseSerilog((context, services, configuration) => {
//     configuration.Enrich.FromLogContext()
//         .ReadFrom.Configuration(context.Configuration)
//         .Enrich.WithExceptionDetails()
//         .WriteTo.Debug()
//         .WriteTo.Console()
//         .WriteTo.Elasticsearch(ConfigureEslasticSink(context.Configuration, context.HostingEnvironment.EnvironmentName))
//         .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
//         .ReadFrom.Configuration(context.Configuration);
// });

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

void ConfigureLogging()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .Build();

    Log.Logger = new LoggerConfiguration()
    #if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
        .MinimumLevel.Override("Microsoft", Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Warning)
        .Enrich.FromLogContext()
        .Enrich.WithExceptionDetails()
        .WriteTo.Debug()
        .WriteTo.Console()
        .WriteTo.Elasticsearch(ConfigureEslasticSink(configuration, environment))
        .Enrich.WithProperty("Environment", environment)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

static  ElasticsearchSinkOptions ConfigureEslasticSink(IConfigurationRoot configuration, string environment)
{
    Console.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name?.ToLower().Replace(".", "-")}");
    return new ElasticsearchSinkOptions(new Uri(configuration["ElasticConfiguration:Uri"]))
    {
        IndexFormat = $"{Assembly.GetExecutingAssembly().GetName().Name?.ToLower().Replace(".", "-")} - {environment.ToLower()} - {DateTime.UtcNow:yyyy-MM}",
        TemplateName="serilog-events-template",
        AutoRegisterTemplate = true,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
        BatchAction = ElasticOpType.Create, 
        TypeName = null,
        FailureCallback = e => Console.WriteLine("Unable to submit event " + e.MessageTemplate),
        EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                        EmitEventFailureHandling.WriteToFailureSink |
                                        EmitEventFailureHandling.RaiseCallback,
        FailureSink = new FileSink("./failures.json", new JsonFormatter(), null)
    };
}
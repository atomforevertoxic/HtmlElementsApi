using System.Text.Json;
using Dapper;
using FluentValidation;
using HtmlElementsApi.Models;
using HtmlElementsApi.Services;
using HtmlElementsApi.Validators;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.WriteIndented = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("ElementsDb")
    ?? throw new InvalidOperationException("Connection string 'ElementsDb' is not configured");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IElementsService, ElementsService>();
builder.Services.AddScoped<IValidator<ElementExtractRequest>, ElementExtractRequestValidator>();

var app = builder.Build();

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/swagger";
});

app.MapControllers();

await InitDatabaseAsync(app.Services);

app.Run();

static async Task InitDatabaseAsync(IServiceProvider services)
{
    const string createTableSql = """
        CREATE TABLE IF NOT EXISTS elements (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            attribute_value TEXT,
            element_html TEXT
        );
        """;

    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInit");

    for (var attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            var dataSource = services.GetRequiredService<NpgsqlDataSource>();
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync(createTableSql);
            return;
        }
        catch (Exception ex)
        {
            if (attempt == 15)
            {
                logger.LogWarning(
                    "Database schema init failed after {Attempts} attempts: {Message}. The app will start; requests will return DB_ERROR until the database is reachable.",
                    attempt,
                    ex.Message);
                return;
            }

            logger.LogWarning(
                "Database is not ready (attempt {Attempt}/15): {Message}. Retrying in 2s...",
                attempt,
                ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

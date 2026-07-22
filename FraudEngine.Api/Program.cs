using FraudEngine.Api.Domain;
using FraudEngine.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.Configure<FraudScoringOptions>(builder.Configuration.GetSection(FraudScoringOptions.SectionName));

// Connection string is resolved from IConfiguration at DbContext creation time (not captured
// eagerly here) so that test hosts overriding configuration after this point take effect.
builder.Services.AddDbContext<FraudDbContext>((serviceProvider, options) =>
    options.UseSqlite(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("FraudDb")
        ?? "Data Source=fraudengine.db"));

builder.Services.AddScoped<FraudScoringService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();
    db.Database.Migrate();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.MapPost("/api/fraud/assess", async (TransactionInput input, FraudScoringService service, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.AssessAsync(input, cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/fraud/assess/batch", async (List<TransactionInput> inputs, FraudScoringService service, CancellationToken cancellationToken) =>
{
    if (inputs is null || inputs.Count == 0)
        return Results.BadRequest(new { error = "Batch cannot be empty." });
    if (inputs.Count > service.MaxBatchSize)
        return Results.BadRequest(new { error = $"Batch cannot contain more than {service.MaxBatchSize} transactions." });

    try
    {
        var results = new List<FraudAssessment>(inputs.Count);
        foreach (var input in inputs)
            results.Add(await service.AssessAsync(input, cancellationToken));
        return Results.Ok(results);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/fraud/audit", async (FraudScoringService service, CancellationToken cancellationToken, int skip = 0, int take = 50) =>
    Results.Ok(await service.GetAuditAsync(skip, take, cancellationToken)));

app.MapGet("/api/fraud/audit/transaction/{transactionId}", async (string transactionId, FraudScoringService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAuditByTransactionAsync(transactionId, cancellationToken)));

app.MapGet("/api/fraud/audit/customer/{customerId}", async (string customerId, FraudScoringService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAuditByCustomerAsync(customerId, cancellationToken)));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "TransactionFraudEngine" }));

app.Run();

public partial class Program;

using FraudEngine.Api.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<FraudScoringService>();

var app = builder.Build();

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

app.MapPost("/api/fraud/assess", (TransactionInput input, FraudScoringService service) =>
{
    try
    {
        return Results.Ok(service.Assess(input));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/fraud/assess/batch", (List<TransactionInput> inputs, FraudScoringService service) =>
{
    if (inputs is null || inputs.Count == 0)
        return Results.BadRequest(new { error = "Batch cannot be empty." });
    if (inputs.Count > 100)
        return Results.BadRequest(new { error = "Batch cannot contain more than 100 transactions." });

    try
    {
        var results = inputs.Select(service.Assess).ToList();
        return Results.Ok(results);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/fraud/audit", (FraudScoringService service) => Results.Ok(service.GetAudit()));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "TransactionFraudEngine" }));

app.Run();

public partial class Program;

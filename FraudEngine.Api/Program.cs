using FraudEngine.Api.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<FraudScoringService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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
    if (inputs.Count == 0)
        return Results.BadRequest(new { error = "Batch cannot be empty." });

    var results = inputs.Select(service.Assess).ToList();
    return Results.Ok(results);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "transaction-fraud-engine" }));

app.Run();

public partial class Program;

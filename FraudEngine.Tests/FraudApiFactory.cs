using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FraudEngine.Tests;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> pointed at a caller-provided SQLite file.
/// Creating a second factory for the same path simulates a process restart against the same
/// durable database, without touching the real appsettings connection string.
/// </summary>
public sealed class FraudApiFactory(string sqliteDbPath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FraudDb"] = $"Data Source={sqliteDbPath}"
            });
        });
    }
}

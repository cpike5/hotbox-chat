using HotBox.Mcp.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (stdout is reserved for MCP protocol)
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register HotBoxApiClient as a typed HTTP client
builder.Services.AddHttpClient<HotBoxApiClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["HotBox:BaseUrl"] ?? "http://localhost:5000";

    client.BaseAddress = new Uri(baseUrl);
});

// Make the API key available for admin-only requests
builder.Services.AddSingleton<ApiKeyProvider>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new ApiKeyProvider(configuration["HotBox:ApiKey"] ?? "");
});

// Add MCP server with stdio transport and auto-discover tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

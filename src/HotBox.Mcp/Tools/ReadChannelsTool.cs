using System.ComponentModel;
using System.Text.Json;
using HotBox.Mcp.Clients;
using ModelContextProtocol.Server;

namespace HotBox.Mcp.Tools;

[McpServerToolType]
public static class ReadChannelsTool
{
    [McpServerTool]
    [Description("Lists all channels in HotBox. Optionally filter by name. Requires the agent's bearer token.")]
    public static async Task<string> ReadChannels(
        [Description("Optional channel name filter. Case-insensitive partial match.")] string nameFilter = "",
        [Description("The agent's JWT bearer token for authentication")] string bearerToken = "",
        HotBoxApiClient? client = null)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return "Error: bearerToken cannot be empty";
        }

        if (client == null)
        {
            return "Error: client cannot be null";
        }

        try
        {
            var result = await client.ListChannelsAsync(bearerToken);

            // Apply client-side name filter if provided
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                var channels = result.EnumerateArray()
                    .Where(c => c.TryGetProperty("name", out var name) &&
                                name.GetString()?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                return JsonSerializer.Serialize(channels, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode != null ? $"HTTP {(int)ex.StatusCode}" : "HTTP error";
            return $"Error: {statusCode} - {ex.Message}";
        }
    }
}

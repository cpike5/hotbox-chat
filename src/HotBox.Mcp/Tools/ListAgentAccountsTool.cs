using System.ComponentModel;
using System.Text.Json;
using HotBox.Mcp.Clients;
using ModelContextProtocol.Server;

namespace HotBox.Mcp.Tools;

[McpServerToolType]
public static class ListAgentAccountsTool
{
    [McpServerTool]
    [Description("Lists all agent accounts created by this API key.")]
    public static async Task<string> ListAgentAccounts(HotBoxApiClient? client = null)
    {
        // Input validation
        if (client == null)
        {
            return "Error: client cannot be null";
        }

        try
        {
            var result = await client.ListAgentAccountsAsync();
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode != null ? $"HTTP {(int)ex.StatusCode}" : "HTTP error";
            return $"Error: {statusCode} - Request failed";
        }
    }
}

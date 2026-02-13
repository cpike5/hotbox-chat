using System.ComponentModel;
using System.Text.Json;
using HotBox.Mcp.Clients;
using ModelContextProtocol.Server;

namespace HotBox.Mcp.Tools;

[McpServerToolType]
public static class CreateAgentAccountTool
{
    [McpServerTool]
    [Description("Creates a new agent account in HotBox. Returns the agent's ID and JWT token for authentication.")]
    public static async Task<string> CreateAgentAccount(
        [Description("Email address for the agent account")] string email,
        [Description("Display name for the agent")] string displayName,
        HotBoxApiClient? client = null)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Error: email cannot be empty";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Error: displayName cannot be empty";
        }

        if (client == null)
        {
            return "Error: client cannot be null";
        }

        try
        {
            var result = await client.CreateAgentAccountAsync(email, displayName);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode != null ? $"HTTP {(int)ex.StatusCode}" : "HTTP error";
            return $"Error: {statusCode} - {ex.Message}";
        }
    }
}

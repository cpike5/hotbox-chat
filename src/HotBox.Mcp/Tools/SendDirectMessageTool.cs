using System.ComponentModel;
using System.Text.Json;
using HotBox.Mcp.Clients;
using ModelContextProtocol.Server;

namespace HotBox.Mcp.Tools;

[McpServerToolType]
public static class SendDirectMessageTool
{
    [McpServerTool]
    [Description("Sends a direct message to a user in HotBox. Requires the agent's bearer token.")]
    public static async Task<string> SendDirectMessage(
        [Description("The GUID of the user to send the direct message to")] Guid userId,
        [Description("The message content to send")] string content,
        [Description("The agent's JWT bearer token for authentication")] string bearerToken,
        HotBoxApiClient? client = null)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: content cannot be empty";
        }

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
            var result = await client.SendDirectMessageAsync(userId, content, bearerToken);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode != null ? $"HTTP {(int)ex.StatusCode}" : "HTTP error";
            return $"Error: {statusCode} - {ex.Message}";
        }
    }
}

using System.ComponentModel;
using System.Text.Json;
using HotBox.Mcp.Clients;
using ModelContextProtocol.Server;

namespace HotBox.Mcp.Tools;

[McpServerToolType]
public static class ReadDirectMessagesTool
{
    [McpServerTool]
    [Description("Reads direct messages with a user in HotBox. Requires the agent's bearer token.")]
    public static async Task<string> ReadDirectMessages(
        [Description("The GUID of the user to read direct messages with")] Guid userId,
        [Description("Maximum number of messages to retrieve (default: 20)")] int limit = 20,
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
            var result = await client.ReadDirectMessagesAsync(userId, limit, bearerToken);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode != null ? $"HTTP {(int)ex.StatusCode}" : "HTTP error";
            return $"Error: {statusCode} - Request failed";
        }
    }
}

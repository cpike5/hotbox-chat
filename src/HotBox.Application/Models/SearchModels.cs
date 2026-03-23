namespace HotBox.Application.Models;

public record SearchResultResponse(
    IReadOnlyList<SearchResultItemResponse> Items,
    string? Cursor,
    int TotalEstimate);

public record SearchResultItemResponse(
    Guid MessageId,
    string Snippet,
    Guid ChannelId,
    string ChannelName,
    Guid AuthorId,
    string AuthorDisplayName,
    DateTime CreatedAt,
    double RelevanceScore,
    bool IsDirectMessage,
    Guid? OtherParticipantId,
    string? OtherParticipantDisplayName);

public record SearchStatusResponse(
    bool IsFullTextSearchAvailable,
    string ProviderName);

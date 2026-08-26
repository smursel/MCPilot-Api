namespace MCPilot.Application.Models;

public sealed class ChatResponse
{
    public string ConversationId { get; set; }
    public string MessageId { get; set; }
    public string Answer { get; set; }

    public IReadOnlyList<ToolCallTrace> ToolCalls { get; set; } = Array.Empty<ToolCallTrace>();

    public required UsageInfo UsageInfo { get; set; }

    public bool truncated { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string AnswerToMessageId { get; set; }

    ChatResponse(string conversationId, string messageId, string answer, string answerToMessageId, IReadOnlyList<ToolCallTrace> toolCalls, UsageInfo usageInfo)
    {
        ConversationId = conversationId;
        MessageId = messageId;
        Answer = answer;
        AnswerToMessageId = answerToMessageId;
        ToolCalls = toolCalls;
        UsageInfo = usageInfo;

    }
}


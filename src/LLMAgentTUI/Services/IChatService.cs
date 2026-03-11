// Copyright (c) RazorConsole. All rights reserved.

namespace LLMAgentTUI.Services;

public interface IChatService
{
    IAsyncEnumerable<StreamingChatUpdate> SendMessageStreamingAsync(string message, CancellationToken cancellationToken = default);
}

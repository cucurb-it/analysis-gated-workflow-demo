// Copyright (c) RazorConsole. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace LLMAgentTUI.Services;

public class ChatService : IChatService
{
    private readonly IChatClient _chatClient;
    private readonly List<ChatMessage> _conversationHistory = new();

    public ChatService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async IAsyncEnumerable<StreamingChatUpdate> SendMessageStreamingAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _conversationHistory.Add(new ChatMessage(ChatRole.User, message));

        var thinkingBuilder = new StringBuilder();
        var responseBuilder = new StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(_conversationHistory, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextReasoningContent reasoningContent && !string.IsNullOrEmpty(reasoningContent.Text))
                {
                    thinkingBuilder.Append(reasoningContent.Text);
                    yield return new StreamingChatUpdate(reasoningContent.Text, true);
                }
                else if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                {
                    responseBuilder.Append(textContent.Text);
                    yield return new StreamingChatUpdate(textContent.Text, false);
                }
            }
        }

        _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, responseBuilder.ToString()));
    }
}

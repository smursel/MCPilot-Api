using Anthropic;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Chat;
using MCPilot.Application.Options;
using MCPilot.Infrastructure.Conversations;
using MCPilot.Infrastructure.Llm;
using MCPilot.Infrastructure.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MCPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMCPilot(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChatOptions>(configuration.GetSection(ChatOptions.SectionName));
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.Configure<McpOptions>(configuration.GetSection(McpOptions.SectionName));

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AnthropicOptions>>().Value;

            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = options.ApiKey };
        });

        services.AddSingleton<ILlmClient, AnthropicLlmClient>();
        services.AddSingleton<McpToolCatalog>();
        services.AddSingleton<IToolCatalog>(sp => sp.GetRequiredService<McpToolCatalog>());
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}

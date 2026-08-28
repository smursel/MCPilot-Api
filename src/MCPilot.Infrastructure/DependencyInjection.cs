using MCPilot.Application.Abstractions;
using MCPilot.Application.Chat;
using MCPilot.Application.Options;
using MCPilot.Infrastructure.Conversations;
using MCPilot.Infrastructure.Llm;
using MCPilot.Infrastructure.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MCPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMCPilot(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChatOptions>(configuration.GetSection(ChatOptions.SectionName));

        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddSingleton<IToolCatalog, EmptyToolCatalog>();
        services.AddSingleton<ILlmClient, NotConfiguredLlmClient>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}

using Anthropic;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Chat;
using MCPilot.Application.Llm;
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
        services.Configure<DeepSeekOptions>(configuration.GetSection(DeepSeekOptions.SectionName));
        services.Configure<McpOptions>(configuration.GetSection(McpOptions.SectionName));

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AnthropicOptions>>().Value;

            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = options.ApiKey };
        });

        // Baslangic secimi yapilandirmadan gelir; POST /api/model ile calisma aninda degistirilebilir.
        var provider = configuration["Llm:Provider"] ?? LlmCatalog.Anthropic;
        var startupModel = string.Equals(provider, LlmCatalog.DeepSeek, StringComparison.OrdinalIgnoreCase)
            ? configuration["DeepSeek:Model"] ?? "deepseek-chat"
            : configuration["Anthropic:Model"] ?? "claude-opus-5";

        services.AddSingleton(new LlmRuntimeState(provider, startupModel));

        services.AddScoped<AnthropicLlmClient>();
        services.AddHttpClient<DeepSeekLlmClient>((sp, client) =>
        {
            var deepSeek = sp.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
            client.BaseAddress = new Uri(deepSeek.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(deepSeek.TimeoutSeconds);
        });

        services.AddScoped<ILlmClient, RoutingLlmClient>();

        services.AddSingleton<McpToolCatalog>();
        services.AddSingleton<IToolCatalog>(sp => sp.GetRequiredService<McpToolCatalog>());
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}

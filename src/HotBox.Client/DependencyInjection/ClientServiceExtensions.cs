using HotBox.Client.Services;
using HotBox.Client.State;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Client.DependencyInjection;

public static class ClientServiceExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddScoped<AuthState>();
        services.AddScoped<ChannelState>();
        services.AddScoped<DirectMessageState>();
        services.AddScoped<VoiceState>();
        services.AddScoped<AppState>();
        services.AddScoped<ChatHubService>();
        services.AddScoped<VoiceHubService>();
        services.AddScoped<WebRtcService>();
        services.AddScoped<VoiceConnectionManager>();

        return services;
    }

    public static IServiceCollection AddApiClient(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = baseAddress;
        });

        return services;
    }
}

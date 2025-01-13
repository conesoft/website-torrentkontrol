using Conesoft.Blazor.Components.Interfaces;
using Conesoft.Hosting;
using Conesoft.PwaGenerator;
using Conesoft.Website.TorrentKontrol.Components;
using Conesoft.Website.TorrentKontrol.Configuration;
using Conesoft.Website.TorrentKontrol.Helpers;
using Conesoft.Website.TorrentKontrol.Services;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddHostConfigurationFiles(configurator =>
    {
        configurator.Add<Config>();
    })
    .AddUsersWithStorage()
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddNotificationService()
    ;

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("noredirect").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AllowAutoRedirect = false
});

builder.Services
    .AddCompiledHashCacheBuster()

    .AddSingleton(new FileHostingPaths(@"D:\Public", @"E:\Public"))

    .AddSingleton<Torrents>().AsHostedService<Torrents>()

    .AddSingleton<TorrentNamingHelper>()
    .AddTransient<ITagListGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    .AddTransient<ICleanNameGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    
    .AddHttpClient()
    .AddAntiforgery()
    .AddCascadingAuthenticationState()
    .AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app
    .UseCompiledHashCacheBuster()
    .UseDeveloperExceptionPage()
    .UseRouting()
    .UseStaticFiles()
    .UseAntiforgery();

app.MapStaticAssets();

app.MapUsersWithStorage();
app.MapPwaInformationFromAppSettings();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
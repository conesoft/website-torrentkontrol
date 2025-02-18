using Conesoft.Blazor.Components.Interfaces;
using Conesoft.Hosting;
using Conesoft.PwaGenerator;
using Conesoft.Website.TorrentKontrol.Components;
using Conesoft.Website.TorrentKontrol.Configuration;
using Conesoft.Website.TorrentKontrol.Helpers;
using Conesoft.Website.TorrentKontrol.Services;
using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddHostConfigurationFiles(configurator =>
    {
        configurator.Add<Config>();
    })
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddUsersWithStorage()
    .AddNotificationService()
    ;

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("noredirect").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AllowAutoRedirect = false
});

builder.Services
    .AddCompiledHashCacheBuster()
    .AddViewTransition()
    .AddSingleton(new FileHostingPaths(@"D:\Public", @"E:\Public"))
    .AddSingleton<Torrents>().AsHostedService<Torrents>()
    .AddSingleton<TorrentNamingHelper>()
    .AddTransient<ITagListGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    .AddTransient<ICleanNameGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    .AddHostedService<MagnetWatcher>()
    .AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.MapPwaInformationFromAppSettings();
app.MapUsersWithStorage();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
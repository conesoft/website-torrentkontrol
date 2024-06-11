using Conesoft.Blazor.Components.Interfaces;
using Conesoft.Hosting;
using Conesoft.Users;
using Conesoft.Website.TorrentKontrol.Components;
using Conesoft.Website.TorrentKontrol.Helpers;
using Conesoft.Website.TorrentKontrol.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddHostConfiguration();

builder.Services
    .AddLoggingToHost()
    .AddPeriodicGarbageCollection(TimeSpan.FromMinutes(5))

    .AddSingleton(new FileHostingPaths(@"D:\Public", @"E:\Public"))
    .AddSingleton<Notification>()

    .AddSingleton<Torrents>()
    .AddHostedService(s => s.GetRequiredService<Torrents>())

    .AddSingleton<TorrentNamingHelper>()
    .AddTransient<ITagListGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    .AddTransient<ICleanNameGenerator>(s => s.GetRequiredService<TorrentNamingHelper>())
    
    .AddHttpClient()
    .AddAntiforgery()
    .AddCascadingAuthenticationState()
    .AddUsersWithStorage()
    .AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app
    .UseDeveloperExceptionPage()
    .UseHostingDefaults(useDefaultFiles: true, useStaticFiles: true)
    .UseRouting()
    .UseStaticFiles()
    .UseAntiforgery();

app.MapUsers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
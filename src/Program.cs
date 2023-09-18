using Conesoft.Hosting;
using Conesoft.Users;
using Conesoft.Website.TorrentKontrol.Components;
using Conesoft.Website.TorrentKontrol.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddHostConfiguration();

builder.Services.AddSingleton<Notification>();
builder.Services.AddHttpClient();

builder.Services.AddUsers("Conesoft.Host.User", (Conesoft.Hosting.Host.GlobalStorage / "Users").Path);
builder.Services.AddRazorComponents()
    .AddServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapUsers();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddServerRenderMode();

app.Run();
﻿using Conesoft.Hosting;
using Conesoft.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddHostConfiguration();

builder.Services.AddSingleton<Conesoft.Website.TorrentKontrol.Helpers.Notification>();

builder.Services.AddUsers("Conesoft.Host.User", (Conesoft.Hosting.Host.GlobalStorage / "Users").Path);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapUsers();
app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
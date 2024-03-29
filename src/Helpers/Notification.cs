using Microsoft.AspNetCore.Http.Extensions;

namespace Conesoft.Website.TorrentKontrol.Helpers;

public class Notification(IConfiguration configuration)
{
    readonly string conesoftSecret = configuration["conesoft:secret"];

    public async Task Notify(string title, string message, string url)
    {
        var query = new QueryBuilder
{
    { "token", conesoftSecret },
    { "title", title },
    { "message", message },
    { "url", url }
};

        await new HttpClient().GetAsync($@"https://conesoft.net/notify" + query.ToQueryString());
    }
}

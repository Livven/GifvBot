using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GifvBot
{
    class Reddit : IDisposable
    {
        const int limit = 100;

        string lastProcessedWikiSubreddit, lastProcessedWikiPage;

        HttpClient client = new HttpClient();

        public Reddit(string lastProcessedWikiSubreddit, string lastProcessedWikiPage)
        {
            this.lastProcessedWikiSubreddit = lastProcessedWikiSubreddit;
            this.lastProcessedWikiPage = lastProcessedWikiPage;
            client.BaseAddress = new Uri("https://oauth.reddit.com");
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GifvBot", "0.1"));
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task AuthenticateAsync(string clientId, string secret, string refreshToken)
        {
            client.DefaultRequestHeaders.SetBasicAuthentication(clientId, secret);
            var response = await client.PostAsync(new Uri("https://www.reddit.com/api/v1/access_token"), new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
            }));
            var json = await response.Content.ReadAsJsonAsync();
            client.DefaultRequestHeaders.SetBearerAuthentication((string)json["access_token"]);
        }

        public async Task<IReadOnlyList<Item>> GetNewItemsAsync()
        {
            var lastProcessed = await GetLastProcessedAsync();
            var items = await GetFullListingAsync(lastProcessed);
            await UpdateLastProcessedAsync(items.FirstOrDefault()?.Name);
            return items.Reverse().ToList();
        }

        public async Task PostCommentAsync(string parent, Uri link)
        {
            var response = await client.PostAsync("api/comment", new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "api_type", "json" },
                { "thing_id", parent },
                // TODO read template from environment variable
                { "text", $"[GIFV link]({link}) // [FAQ](https://www.reddit.com/r/livven/wiki/gifv-bot)" },
            }));
            Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }

        async Task<string> GetLastProcessedAsync()
        {
            var json = await client.GetJsonAsync($"r/{lastProcessedWikiSubreddit}/wiki/{lastProcessedWikiPage}");
            return (string)json["data"]["content_md"];
        }

        async Task UpdateLastProcessedAsync(string lastProcessed)
        {
            if (string.IsNullOrEmpty(lastProcessed))
            {
                return;
            }
            await client.PostAsync($"r/{lastProcessedWikiSubreddit}/api/wiki/edit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "page", lastProcessedWikiPage },
                { "content", lastProcessed },
            }));
        }

        async Task<IReadOnlyList<Item>> GetFullListingAsync(string before)
        {
            var items = await GetListingPageAsync(before);
            if (string.IsNullOrEmpty(before) || items.Count < limit)
            {
                return items;
            }
            var previousEntries = await GetFullListingAsync(items[0].Name);
            return previousEntries.Concat(items).ToList();
        }

        async Task<IReadOnlyList<Item>> GetListingPageAsync(string before)
        {
            var json = await client.GetJsonAsync($"domain/imgur.com/new?limit={limit}&before={before}");
            return json["data"]["children"].Select(item => ParseItem(item["data"])).ToList();
        }

        Item ParseItem(JToken json)
        {
            return new Item()
            {
                Name = (string)json["name"],
                Link = (Uri)json["url"],
            };
        }

        public class Item
        {
            public string Name { get; set; }

            public Uri Link { get; set; }
        }
    }
}

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

        static readonly TimeSpan recentThreshold = TimeSpan.FromMinutes(8);

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

        public async Task<IReadOnlyList<Item>> GetNewItemsAsync(Item lastProcessed, bool optimizeLoading)
        {
            if (lastProcessed == null)
            {
                lastProcessed = await GetLastProcessedAsync();
            }

            // lastProcessed will still be null if the account is new
            var lastProcessedId = lastProcessed?.Name;

            // sometimes using the "before" parameter can return an empty list even if new items are available
            // avoid that by disabling optimized loading and filtering the full list manually
            var items = await GetFullListingAsync(optimizeLoading ? lastProcessedId : null);
            var recent = DateTimeOffset.UtcNow - recentThreshold;
            return items
                .TakeWhile(item => item.Name != lastProcessedId)
                .Where(item => item.Created > recent)
                .Reverse()
                .ToList();
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

        async Task<Item> GetLastProcessedAsync()
        {
            var json = await client.GetJsonAsync("user/gifv-bot/comments/?sort=new&limit=1");
            var items = ParseJsonListing(json, true);
            return items.FirstOrDefault();
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
            return ParseJsonListing(json, false);
        }

        IReadOnlyList<Item> ParseJsonListing(JToken json, bool isCommentListing)
        {
            return json["data"]["children"].Select(item => ParseItem(item["data"], isCommentListing)).ToList();
        }

        Item ParseItem(JToken json, bool isComment)
        {
            return new Item()
            {
                Name = (string)json[isComment ? "parent_id" : "name"],
                Created = DateTimeOffset.FromUnixTimeSeconds((long)json["created_utc"]),
                Link = (Uri)json[isComment ? "link_url" : "url"],
            };
        }

        public class Item
        {
            public string Name { get; set; }

            public DateTimeOffset Created { get; set; }

            public Uri Link { get; set; }
        }
    }
}

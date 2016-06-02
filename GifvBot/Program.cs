using System;
using System.Linq;
using System.Threading.Tasks;

namespace GifvBot
{
    class Program
    {
        static readonly string ClientId;
        static readonly string Secret;
        static readonly string RefreshToken;

        static Program()
        {
            ClientId = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_CLIENT_ID");
            Secret = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_SECRET");
            RefreshToken = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_REFRESH_TOKEN");
        }

        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            while (true)
            {
                try
                {
                    await RunAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        static async Task RunAsync()
        {
            using (var reddit = new Reddit("gifvbot", "lastprocessed"))
            using (var imgur = new Imgur())
            {
                await reddit.AuthenticateAsync(ClientId, Secret, RefreshToken);
                var items = await reddit.GetNewItemsAsync();
                var filtered = items.Where(item => (item.Link.Host == "imgur.com" || item.Link.Host.EndsWith(".imgur.com")) && !item.Link.AbsolutePath.EndsWith(".gifv")).ToList();
                Console.WriteLine($"{items.Count} new items, {filtered.Count} to process, {items.Count - filtered.Count} filtered out");
                var convertedCount = 0;
                foreach (var item in filtered)
                {
                    try
                    {
                        var result = await imgur.GetGifvUriAsync(item.Link);
                        if (result != null)
                        {
                            convertedCount++;
                            await reddit.PostCommentAsync(item.Name, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"error processing item {item.Name} with link {item.Link}");
                        Console.WriteLine(ex.Message);
                    }
                }
                Console.WriteLine($"{convertedCount} links converted");
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;

namespace GifvBot
{
    class Program
    {
        const int ConsecutiveRunDelay = 10;

        readonly string ClientId;
        readonly string Secret;
        readonly string RefreshToken;

        Reddit.Item lastProcessed;

        Program()
        {
            ClientId = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_CLIENT_ID");
            Secret = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_SECRET");
            RefreshToken = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_REFRESH_TOKEN");
        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.KeepRunningAsync().Wait();
        }

        async Task KeepRunningAsync()
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
                await Task.Delay(TimeSpan.FromSeconds(ConsecutiveRunDelay));
            }
        }

        async Task RunAsync()
        {
            using (var reddit = new Reddit("gifvbot", "lastprocessed"))
            using (var imgur = new Imgur())
            {
                await reddit.AuthenticateAsync(ClientId, Secret, RefreshToken);
                var items = await reddit.GetNewItemsAsync(lastProcessed);
                var filtered = items.Where(item => (item.Link.Host == "imgur.com" || item.Link.Host.EndsWith(".imgur.com")) && !item.Link.AbsolutePath.EndsWith(".gifv")).ToList();
                Console.WriteLine($"{items.Count} new items, {filtered.Count} to process, {items.Count - filtered.Count} filtered out");
                var convertedCount = 0;
                foreach (var item in filtered)
                {
                    lastProcessed = item;
                    try
                    {
                        var result = await imgur.GetGifvUriAsync(item.Link);
                        if (result != null)
                        {
                            convertedCount++;
#if !DEBUG
                            await reddit.PostCommentAsync(item.Name, result);
#endif
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

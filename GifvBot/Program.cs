using System;
using System.Linq;
using System.Threading.Tasks;

namespace GifvBot
{
    class Program
    {
        const int ConsecutiveRunDelay = 10;

        const int EmptyListingFailsafeThreshold = 6;

        readonly string clientId, secret, refreshToken;

        readonly bool isCommentingEnabled;

        Reddit.Item lastProcessed;

        int emptyListingCount;

        Program()
        {
            clientId = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_CLIENT_ID");
            secret = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_SECRET");
            refreshToken = Environment.GetEnvironmentVariable("GIFVBOT_REDDIT_REFRESH_TOKEN");
            isCommentingEnabled = "true".Equals(Environment.GetEnvironmentVariable("GIFVBOT_IS_COMMENTING_ENABLED"), StringComparison.OrdinalIgnoreCase);
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
            using (var reddit = new Reddit())
            using (var imgur = new Imgur())
            {
                await reddit.AuthenticateAsync(clientId, secret, refreshToken);

                // if no items are returned several times in a row it could be because of the ID of the last processed item
                // this could happen when the corresponding post has become too old, or maybe if it has been deleted
                // in that case disable optimized loading as a fail-safe to avoid getting stuck in a state where no new items can ever be loaded again
                var items = await reddit.GetNewItemsAsync(lastProcessed, emptyListingCount < EmptyListingFailsafeThreshold);
                emptyListingCount = items.Count > 0 ? 0 : emptyListingCount + 1;

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
                            if (isCommentingEnabled)
                            {
                                await reddit.PostCommentAsync(item.Name, result);
                            }
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

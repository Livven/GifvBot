using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;

namespace GifvBot
{
    class Imgur : IDisposable
    {
        HttpClient client = new HttpClient();

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<Uri> GetGifvLinkAsync(Uri url)
        {
            try
            {
                var response = await GetImgurPageAsync(url);
                if (response == null)
                {
                    return null;
                }
                var html = await response.Content.ReadAsHtmlAsync();
                var twitterStreams = html.QuerySelectorAll(@"meta[name=""twitter:player:stream""]");
                return GetGifvLink(twitterStreams);
            }
            catch
            {
                return null;
            }
        }

        async Task<HttpResponseMessage> GetImgurPageAsync(Uri url)
        {
            url = GetImgurPageLink(url) ?? url;
            while (true)
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                var redirectedUri = response.RequestMessage.RequestUri;
                if (redirectedUri == url)
                {
                    return response;
                }
                url = GetImgurPageLink(redirectedUri);
                if (url == null)
                {
                    return response;
                }
            }
        }

        static void Analyze(IHtmlDocument html)
        {
            var ogType = html.QuerySelector(@"meta[property=""og:type""]")?.GetAttribute("content");
            var twitterCard = html.QuerySelector(@"meta[name=""twitter:card""]")?.GetAttribute("content");
            var ogVideos = GetGifvLink(html.QuerySelectorAll(@"meta[property=""og:video:secure_url""]"));
            var twitterStreams = GetGifvLink(html.QuerySelectorAll(@"meta[name=""twitter:player:stream""]"));
        }

        static Uri GetGifvLink(IEnumerable<IElement> list)
        {
            foreach (var element in list)
            {
                var url = new Uri(element.GetAttribute("content"));
                if (url.Host == "i.imgur.com")
                {
                    var result = new UriBuilder(url);
                    result.Path = result.Path.Split('.')[0] + ".gifv";
                    return result.Uri;
                }
            }
            return null;
        }

        static Uri GetImgurPageLink(Uri url)
        {
            if (url.Host != "i.imgur.com")
            {
                return null;
            }
            var result = new UriBuilder(url);
            result.Host = "imgur.com";
            result.Path = result.Path.Split('.')[0];
            result.Query = "";
            return result.Uri;
        }
    }
}

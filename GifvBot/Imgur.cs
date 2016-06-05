using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;

namespace GifvBot
{
    class Imgur : IDisposable
    {
        HttpClient client = new HttpClient();

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<Uri> GetGifvUriAsync(Uri uri)
        {
            var response = await GetPageAsync(uri);
            var html = await response.Content.ReadAsHtmlAsync();
            return FindGifvUri(html.QuerySelectorAll(@"meta[name=""twitter:player:stream""]"));
        }

        async Task<HttpResponseMessage> GetPageAsync(Uri uri)
        {
            uri = GetPageUri(uri);
            while (true)
            {
                var response = await client.GetAsync(uri);

                // if the link is on the main imgur.com domain but has a valid file ending, it will be redirected to i.imgur.com
                // so make sure the redirected link is on the main imgur.com domain
                var redirectedUri = response.RequestMessage.RequestUri;
                uri = GetPageUri(redirectedUri);
                if (redirectedUri == uri)
                {
                    return response;
                }
            }
        }

        static Uri FindGifvUri(IEnumerable<IElement> list)
        {
            foreach (var element in list)
            {
                var uri = new Uri(element.GetAttribute("content"));
                if (uri.Host == "i.imgur.com")
                {
                    var result = new UriBuilder(uri);
                    result.Path = result.Path.Split('.')[0] + ".gifv";
                    return result.Uri;
                }
            }
            return null;
        }

        static Uri GetPageUri(Uri uri)
        {
            if (uri.Host == "i.imgur.com")
            {
                var result = new UriBuilder(uri);
                result.Host = "imgur.com";
                result.Path = result.Path.Split('.')[0];
                result.Query = "";
                return result.Uri;
            }
            return uri;
        }
    }
}

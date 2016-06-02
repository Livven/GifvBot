using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GifvBot
{
    static class Extensions
    {
        public static async Task<JToken> GetJsonAsync(this HttpClient client, Uri uri)
        {
            var response = await client.GetAsync(uri);
            return await response.Content.ReadAsJsonAsync();
        }

        public static async Task<JToken> GetJsonAsync(this HttpClient client, string uri)
        {
            var response = await client.GetAsync(uri);
            return await response.Content.ReadAsJsonAsync();
        }

        public static async Task<JToken> ReadAsJsonAsync(this HttpContent content)
        {
            using (var stream = await content.ReadAsStreamAsync())
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return JToken.ReadFrom(reader);
            }
        }

        public static async Task<IHtmlDocument> ReadAsHtmlAsync(this HttpContent content)
        {
            using (var stream = await content.ReadAsStreamAsync())
            {
                var parser = new HtmlParser();
                return parser.Parse(stream);
            }
        }

        public static void SetBasicAuthentication(this HttpRequestHeaders headers, string username, string password)
        {
            var bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }

        public static void SetBearerAuthentication(this HttpRequestHeaders headers, string token)
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}

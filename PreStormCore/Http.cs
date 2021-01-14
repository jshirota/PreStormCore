using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PreStormCore
{
    internal static class Http
    {
        public static async Task<T> Get<T>(string url)
        {
            using var http = GetHttpClient();
            var response = await http.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            return json.Deserialize<T>()!;
        }

        public static async Task<T> Post<T>(string url, string data)
        {
            using var http = GetHttpClient();
            var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await http.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();
            return json.Deserialize<T>()!;
        }

        private static HttpClient GetHttpClient()
        {
            var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip });
            http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            return http;
        }
    }
}

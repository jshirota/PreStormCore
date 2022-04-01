using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PreStormCore;

internal static class Http
{
    private static readonly HttpClient HttpClient;
    private static readonly int MaxRetryAttempts = 4;

    static Http()
    {
        if (string.IsNullOrEmpty(typeof(Http).Assembly.Location))
        {
            HttpClient = new();
        }
        else
        {
            HttpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip });
            HttpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            HttpClient.Timeout = TimeSpan.FromMinutes(4);
        }
    }

    public static async Task<T> GetAsync<T>(string url, int attempt = 0)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            return json.Deserialize<T>()!;
        }
        catch
        {
            if (attempt < MaxRetryAttempts)
            {
                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 1000));
                return await GetAsync<T>(url, attempt + 1);
            }

            throw;
        }
    }

    public static async Task<T> PostAsync<T>(string url, string data)
    {
        var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await HttpClient.PostAsync(url, content);
        var json = await response.Content.ReadAsStringAsync();
        return json.Deserialize<T>()!;
    }
}

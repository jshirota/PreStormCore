using System.Net;
using System.Text;

namespace PreStormCore;

internal static class Http
{
    private static readonly int MaxRetryAttempts = 4;

    public static async Task<T> Get<T>(string url, int attempt = 0)
    {
        try
        {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

            return await request.GetResponseAsAsync<T>();
        }
        catch
        {
            if (attempt < MaxRetryAttempts)
            {
                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 1000));
                return await Get<T>(url, attempt + 1);
            }

            throw;
        }
    }

    public static async Task<T> Post<T>(string url, string data)
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";

        var bytes = Encoding.UTF8.GetBytes(data);

        using var stream = request.GetRequestStream();
        stream.Write(bytes, 0, bytes.Length);

        return await request.GetResponseAsAsync<T>();
    }

    private static async Task<T> GetResponseAsAsync<T>(this WebRequest request)
    {
        ((HttpWebRequest)request).AutomaticDecompression = DecompressionMethods.GZip;

        using var stream = request.GetResponse().GetResponseStream();
        using StreamReader reader = new(stream, Encoding.UTF8);

        var json = await reader.ReadToEndAsync();

        return json.Deserialize<T>()!;
    }
}

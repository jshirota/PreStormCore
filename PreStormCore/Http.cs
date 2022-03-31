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
            return request.GetResponseAs<T>();
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static async Task<T> Post<T>(string url, string data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";

        var bytes = Encoding.UTF8.GetBytes(data);

        using var stream = request.GetRequestStream();
        stream.Write(bytes, 0, bytes.Length);

        return request.GetResponseAs<T>();
    }

    private static T GetResponseAs<T>(this WebRequest request)
    {
        using var stream = request.GetResponse().GetResponseStream();
        using StreamReader reader = new(stream, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return json.Deserialize<T>()!;
    }
}

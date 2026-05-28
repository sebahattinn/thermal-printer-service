using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThermalPrinterService.IntegrationTests;

internal static class TestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Verilen predicate gerceklesinceye kadar kisa araliklarla yoklar.
    /// Worker/Reconnect arka plan servisleri async oldugundan, testler
    /// "POST sonrasi belli bir durum olusana kadar bekle" diyebilmeli.
    /// </summary>
    public static async Task<T> WaitForAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var step = interval ?? TimeSpan.FromMilliseconds(50);

        T last = default!;
        while (DateTime.UtcNow < deadline)
        {
            last = await probe();
            if (predicate(last)) return last;
            await Task.Delay(step);
        }
        throw new TimeoutException(
            $"Beklenen kosul {timeout?.TotalSeconds ?? 5} sn icinde gerceklesmedi. " +
            $"Son deger: {JsonSerializer.Serialize(last, Json)}");
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient http, string path)
    {
        var json = await http.GetStringAsync(path);
        return JsonSerializer.Deserialize<T>(json, Json)
            ?? throw new InvalidOperationException($"Null deserialized for {path}");
    }

    public static async Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient http, string path, T body)
        => await http.PostAsJsonAsync(path, body, Json);
}

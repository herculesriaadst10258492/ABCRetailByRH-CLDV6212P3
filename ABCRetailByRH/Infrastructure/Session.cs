using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace ABCRetailByRH
{
    public static class SessionExtensions
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static void SetObject<T>(this ISession session, string key, T value)
            => session.SetString(key, JsonSerializer.Serialize(value, _json));

        public static T? GetObject<T>(this ISession session, string key)
        {
            var s = session.GetString(key);
            return string.IsNullOrEmpty(s) ? default : JsonSerializer.Deserialize<T>(s, _json);
        }
    }
}

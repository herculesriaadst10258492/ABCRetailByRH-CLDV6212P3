// ABCRetailByRH/Infrastructure/SessionExtensions.cs
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ABCRetailByRH.Infrastructure
{
    public static class SessionExtensions
    {
        private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static void SetObject<T>(this ISession session, string key, T value)
            => session.SetString(key, JsonSerializer.Serialize(value, _json));

        public static T? GetObject<T>(this ISession session, string key)
        {
            var s = session.GetString(key);
            return string.IsNullOrWhiteSpace(s) ? default : JsonSerializer.Deserialize<T>(s, _json);
        }
    }
}

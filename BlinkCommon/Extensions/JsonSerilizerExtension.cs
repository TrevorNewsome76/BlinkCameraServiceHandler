using System.Text.Json;
using Shadow.Quack;
using Shadow.Quack.Json;

namespace BlinkCommon.Extensions
{
    public static class JsonSerializerExtension
    {
        public static string Serialize<T>(this T parameters) => parameters == null ? string.Empty : JsonSerializer.Serialize<object>(parameters);

        public static T Deserialize<T>(this string parameters)
        {
            T result;

            try
            {
                result = !string.IsNullOrEmpty(parameters)
                    ? Duck.Implement<T>((JsonProxy)parameters)
                    : Duck.Implement<T>(new { });
            }
            catch
            {
                return Duck.Implement<T>(new { });
            }

            return result;
        }
    }
}
using System.Text.Json;
using Shadow.Quack;
using Shadow.Quack.Json;

namespace BlinkCameraCall.Extensions;

internal static class JsonSerializerExtension
{
    internal static string Serialize<T>(this T parameters) => parameters == null ? string.Empty : JsonSerializer.Serialize<object>(parameters);

    internal static T Deserialize<T>(this string parameters)
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

    internal static IDictionary<string, int> DeserializeEnumStringInt(this string jsonString) =>
        jsonString
            .RemoveBrackets()
            .ToLowerCase()
            .RemoveCarriageReturnAndNewLines()
            .RemoveCurlyBrackets()
            .RemoveOuterSpeechMarks()
            .ExtractKeyValuePairs()
            .ExtractKeyPairsStringInt();

    private static IDictionary<string, int> ExtractKeyPairsStringInt(this string[] jsonKeyValuePairs)
    {
        var newKeyPairs = Duck.Implement<IDictionary<string, int>>(new());
        foreach (var keyValue in jsonKeyValuePairs)
        {
            var keyVal = keyValue.Split(":");
            if (keyVal.Length < 2)
                throw new ArgumentException(
                    "Cannot find a valid key pair value in data set. The Json string may have corruption.");
            newKeyPairs.Add(keyVal[0].Trim(), Int32.Parse(keyVal[1]));
        }

        return newKeyPairs;
    }

    private static string[] ExtractKeyValuePairs(this string jsonString) =>
        jsonString.Split(",");

    private static string RemoveOuterSpeechMarks(this string jsonString) =>
        jsonString.Replace("\"", "");

    private static string RemoveCurlyBrackets(this string jsonString)
    {
        if (jsonString.Substring(0, 1) != "{"
            || jsonString.Substring(jsonString.Length - 1, 1) != "}")
            throw new ArgumentException("Json Enum string is not of correct format. Missing outer curly bracket(s)");

        var removedBrackets = jsonString.Substring(1, jsonString.Length - 2).Trim();
        return removedBrackets;
    }

    private static string RemoveCarriageReturnAndNewLines(this string jsonString) =>
        jsonString.Replace("\r\n", "").Trim();

    private static string ToLowerCase(this string jsonString) =>
        jsonString.ToLower();

    private static string RemoveBrackets(this string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            throw new ArgumentException("The string is either null or empty.");

        var trimmedString = jsonString.Trim();

        if (trimmedString.Substring(0, 1) != "["
            || trimmedString.Substring(trimmedString.Length - 1, 1) != "]")
            throw new ArgumentException("Json Enum string is not of correct format. Missing outer bracket(s)");

        var removedBrackets = trimmedString.Substring(1, jsonString.Length - 2).Trim();
        return removedBrackets;
    }
}
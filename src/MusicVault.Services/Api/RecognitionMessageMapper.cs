using System.Text.Json;
using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>识曲提供方错误 → 语义化消息（禁止把后端/异常原文塞进 UI）。</summary>
public static class RecognitionMessageMapper
{
    public static string MapProviderMessage(string? apiMsg)
    {
        if (string.IsNullOrWhiteSpace(apiMsg))
            return ServiceMessageCodec.Key("Recognition.Failed");

        if (ServiceMessageCodec.IsEncoded(apiMsg))
            return apiMsg;

        if (ContainsAny(apiMsg, "no match", "not found", "未识别", "未匹配", "无结果", "null result"))
            return ServiceMessageCodec.Key("Recognition.NoMatch");

        if (ContainsAny(apiMsg, "token", "api_token", "unauthorized", "401", "403", "鉴权", "密钥", "invalid key"))
            return ServiceMessageCodec.Key("Recognition.AuthFailed");

        if (ContainsAny(apiMsg, "timeout", "超时", "timed out"))
            return ServiceMessageCodec.Key("Recognition.Timeout");

        return ServiceMessageCodec.Key("Recognition.Failed");
    }

    public static string MapException(Exception ex)
    {
        if (ex is TaskCanceledException or OperationCanceledException)
            return ServiceMessageCodec.Key("Recognition.Timeout");

        if (ex is HttpRequestException)
            return ServiceMessageCodec.Key("Recognition.NetworkError");

        if (ex is JsonException)
            return ServiceMessageCodec.Key("Recognition.ParseFailedGeneric");

        return ServiceMessageCodec.Key("Recognition.Failed");
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

using System.Text.Json;
using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiAccountClientTests
{
    [Fact]
    public void ParsePollResponse_success_returns_encoded_success_message()
    {
        using var doc = JsonDocument.Parse(
            """{"code":200,"msg":"success","data":{"status":"success","message":"登录成功","cookie":"uin=1"}}""");

        var result = GoMusicApiAccountClient.ParsePollResponse(doc.RootElement, "qq_wx");

        Assert.Equal(QrLoginPollState.Succeeded, result.State);
        Assert.Equal(ServiceMessageCodec.Key("Login.Qr.Success"), result.Message);
    }

    [Fact]
    public void ParsePollResponse_waiting_preserves_api_message_when_scanned()
    {
        using var doc = JsonDocument.Parse(
            """{"code":200,"msg":"success","data":{"status":"scanned","message":"已扫码，请在微信中确认"}}""");

        var result = GoMusicApiAccountClient.ParsePollResponse(doc.RootElement, "qq_wx");

        Assert.Equal(QrLoginPollState.Waiting, result.State);
        Assert.Equal("已扫码，请在微信中确认", result.Message);
    }

    [Fact]
    public void ParsePollResponse_waiting_uses_encoded_hint_when_scanned_message_empty()
    {
        using var doc = JsonDocument.Parse(
            """{"code":200,"msg":"success","data":{"status":"scanned","message":""}}""");

        var result = GoMusicApiAccountClient.ParsePollResponse(doc.RootElement, "qq_wx");

        Assert.Equal(QrLoginPollState.Waiting, result.State);
        Assert.Equal(ServiceMessageCodec.Key("Login.Qr.ScannedConfirm"), result.Message);
    }

    [Fact]
    public void ParsePollResponse_waiting_preserves_english_scanned_message()
    {
        using var doc = JsonDocument.Parse(
            """{"code":200,"msg":"success","data":{"status":"scanned","message":"Scanned; confirm in WeChat"}}""");

        var result = GoMusicApiAccountClient.ParsePollResponse(doc.RootElement, "qq_wx");

        Assert.Equal(QrLoginPollState.Waiting, result.State);
        Assert.Equal("Scanned; confirm in WeChat", result.Message);
    }

    [Fact]
    public void ParsePollResponse_failed_when_status_failed()
    {
        using var doc = JsonDocument.Parse(
            """{"code":200,"msg":"success","data":{"status":"failed","message":"wechat auth code missing"}}""");

        var result = GoMusicApiAccountClient.ParsePollResponse(doc.RootElement, "qq_wx");

        Assert.Equal(QrLoginPollState.Failed, result.State);
        Assert.Equal("wechat auth code missing", result.Message);
    }

    [Fact]
    public void RequiresLongPollQr_only_qq_wx()
    {
        Assert.True(GoMusicApiAccountClient.RequiresLongPollQr("qq_wx"));
        Assert.False(GoMusicApiAccountClient.RequiresLongPollQr("qq"));
    }

    [Fact]
    public void IsDirectQrImageUrl_wechat_qrcode_without_extension()
    {
        const string url = "https://open.weixin.qq.com/connect/qrcode/0719RIxO1Bk70w3N";
        Assert.True(GoMusicApiAccountClient.IsDirectQrImageUrl(url));
        Assert.True(GoMusicApiAccountClient.IsWeChatOAuthQrImageUrl(url));
    }

    [Fact]
    public void IsDirectQrImageUrl_html_login_page_is_false()
    {
        const string url = "https://example.com/loginQRCode/foo/index.html";
        Assert.False(GoMusicApiAccountClient.IsDirectQrImageUrl(url));
    }
}

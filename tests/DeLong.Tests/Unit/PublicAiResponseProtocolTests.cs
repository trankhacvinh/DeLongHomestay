using DeLong.Web.Features.PublicAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicAiResponseProtocolTests
{
    [Fact]
    public void Parse_reads_bounded_booking_draft()
    {
        var result = PublicAiResponseProtocol.Parse("""
            {"message":"Tôi đã chuẩn bị bản nháp.","suggestedRoomCode":"COCO-01","draft":{"requested":true,"roomCode":"COCO-01","stayDate":"2026-09-30","rateNames":["Khung 1","Khung 2"],"guestCount":2,"voucherCode":"KHACHMOI"}}
            """);

        Assert.NotNull(result);
        Assert.True(result!.Draft!.Requested);
        Assert.Equal("COCO-01", result.Draft.RoomCode);
        Assert.Equal(2, result.Draft.RateNames.Count);
        Assert.Equal(2, result.Draft.GuestCount);
        Assert.Equal("COCO-01", result.SuggestedRoomCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"draft\":null}")]
    public void Parse_rejects_invalid_or_message_less_output(string value)
    {
        Assert.Null(PublicAiResponseProtocol.Parse(value));
    }


    [Fact]
    public void Schema_requires_server_validated_room_suggestion_field()
    {
        var required = PublicAiResponseProtocol.Schema.GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Contains("suggestedRoomCode", required);
        Assert.Equal("string", PublicAiResponseProtocol.Schema.GetProperty("properties")
            .GetProperty("suggestedRoomCode").GetProperty("type").GetString());
    }
}

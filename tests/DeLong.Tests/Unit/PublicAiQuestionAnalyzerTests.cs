using DeLong.Web.Features.PublicAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicAiQuestionAnalyzerTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);

    [Theory]
    [InlineData("Ngày mai còn phòng không?", PublicAiIntent.Availability, "2026-09-12")]
    [InlineData("Giá phòng ngày 30/09/2026 bao nhiêu?", PublicAiIntent.Pricing, "2026-09-30")]
    [InlineData("Còn phòng 2026-10-01 không?", PublicAiIntent.Availability, "2026-10-01")]
    public void Analyze_detects_live_intent_and_date(string message, PublicAiIntent intent, string expectedDate)
    {
        var result = PublicAiQuestionAnalyzer.Analyze(message, Today);

        Assert.Equal(intent, result.Intent);
        Assert.Equal(DateOnly.Parse(expectedDate), result.Date);
    }

    [Fact]
    public void Analyze_requires_a_date_for_unspecified_price_question()
    {
        var result = PublicAiQuestionAnalyzer.Analyze("Phòng Coco Blue giá bao nhiêu?", Today);

        Assert.Equal(PublicAiIntent.Pricing, result.Intent);
        Assert.Null(result.Date);
    }

    [Fact]
    public void Analyze_keeps_policy_question_general()
    {
        var result = PublicAiQuestionAnalyzer.Analyze("Chính sách nhận phòng là gì?", Today);

        Assert.Equal(PublicAiIntent.General, result.Intent);
        Assert.Null(result.Date);
    }

    [Fact]
    public void Analyze_extracts_booking_code_and_phone_for_secure_lookup()
    {
        var result = PublicAiQuestionAnalyzer.Analyze(
            "Tra cứu booking BK-260823-A5A7700355 số 0979 745 945", Today);

        Assert.Equal(PublicAiIntent.BookingLookup, result.Intent);
        Assert.Equal("BK-260823-A5A7700355", result.BookingCode);
        Assert.Equal("0979 745 945", result.Phone);
    }

    [Theory]
    [InlineData("Tôi muốn hủy booking")]
    [InlineData("Tôi cần đổi ngày")]
    public void Analyze_never_treats_change_or_cancel_as_a_mutation(string message)
    {
        Assert.Equal(PublicAiIntent.ChangeOrCancel, PublicAiQuestionAnalyzer.Analyze(message, Today).Intent);
    }
}

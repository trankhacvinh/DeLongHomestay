using DeLong.Web.Features.PublicAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicAiQuestionAnalyzerTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);

    [Theory]
    [InlineData("Ngày mai còn phòng không?", PublicAiIntent.Availability, "2026-09-12")]
    [InlineData("Đặt phòng Coco Blue cho 2 người vào tối nay", PublicAiIntent.BookingDraft, "2026-09-11")]
    [InlineData("Tôi muốn đặt phòng vào đêm nay", PublicAiIntent.BookingDraft, "2026-09-11")]
    [InlineData("Còn phòng chiều nay không?", PublicAiIntent.Availability, "2026-09-11")]
    [InlineData("Đặt phòng Coco Blue tối mai", PublicAiIntent.BookingDraft, "2026-09-12")]
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
    public void Analyze_recognizes_policy_questions()
    {
        var result = PublicAiQuestionAnalyzer.Analyze("Chính sách nhận phòng là gì?", Today);

        Assert.Equal(PublicAiIntent.Policy, result.Intent);
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

    [Theory]
    [InlineData("Tra cứu bằng email Guest+booking@example.com", "Guest+booking@example.com")]
    [InlineData("guest@example.com", "guest@example.com")]
    public void Analyze_extracts_email_for_lookup(string message, string expectedEmail)
    {
        var result = PublicAiQuestionAnalyzer.Analyze(message, Today);
        Assert.Equal(PublicAiIntent.BookingLookup, result.Intent);
        Assert.Equal(expectedEmail, result.Email);
        Assert.True(result.EmailLookupRequested);
    }

    [Theory]
    [InlineData("Chính sách hủy phòng thế nào?")]
    [InlineData("Nội quy ở đây là gì?")]
    [InlineData("FAQ có gì?")]
    public void Policy_questions_are_not_booking_changes(string message)
    {
        Assert.Equal(PublicAiIntent.Policy, PublicAiQuestionAnalyzer.Analyze(message, Today).Intent);
    }

    [Fact]
    public void Email_lookup_without_address_requests_clarification()
    {
        var result = PublicAiQuestionAnalyzer.Analyze("Tra cứu bằng email", Today);
        Assert.Equal(PublicAiIntent.BookingLookup, result.Intent);
        Assert.True(result.EmailLookupRequested);
        Assert.Null(result.Email);
    }

    [Fact]
    public void Invalid_email_is_not_replaced_with_a_valid_suffix()
    {
        var result = PublicAiQuestionAnalyzer.Analyze("Tra cứu bằng email bad@@guest@example.com", Today);
        Assert.Equal(PublicAiIntent.BookingLookup, result.Intent);
        Assert.Null(result.Email);
    }
}

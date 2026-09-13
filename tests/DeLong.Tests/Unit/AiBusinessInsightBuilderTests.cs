using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.Reports;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class AiBusinessInsightBuilderTests
{
    [Fact]
    public void Insufficient_sample_returns_facts_without_change_recommendation()
    {
        var result = AiBusinessInsightBuilder.Build(Report(3, 1_000_000, 25), Report(2, 2_000_000, 30), []);

        Assert.Equal("Thấp", result.Confidence);
        Assert.Empty(result.Recommendations);
        Assert.Contains(result.Interpretations, x => x.Contains("Chưa đủ dữ liệu", StringComparison.Ordinal));
    }

    [Fact]
    public void Material_revenue_decline_and_low_occupancy_create_preview_only_recommendations()
    {
        var rates = new[] { new AiBusinessBreakdownDto("Khung 1", 2, 500_000), new AiBusinessBreakdownDto("Khung 2", 4, 2_000_000) };
        var result = AiBusinessInsightBuilder.Build(Report(10, 7_000_000, 30), Report(10, 10_000_000, 55), rates);

        Assert.Equal([
            "Kỳ hiện tại có 10 booking, thực thu 7,000,000 đ và công suất 30.0%.",
            "Kỳ trước có 10 booking, thực thu 10,000,000 đ và công suất 55.0%.",
            "Kỳ hiện tại hoàn 0 đ, chi phí 0 đ và dòng tiền ròng 7,000,000 đ."
        ], result.Facts);
        Assert.Equal([
            "Thực thu giảm 30.0% so với kỳ trước.",
            "Công suất 30.0% đang dưới ngưỡng tham khảo 40%."
        ], result.Interpretations);
        Assert.Contains(result.Interpretations, x => x.Contains("giảm 30", StringComparison.Ordinal));
        Assert.Contains(result.Recommendations, x => x.Title.Contains("Khung 1", StringComparison.Ordinal));
        Assert.All(result.Recommendations, x => Assert.Contains("Không áp dụng ngay", x.ProposalPrompt));
    }

    [Fact]
    public void Stable_period_returns_only_report_facts_and_a_non_mutating_monitoring_recommendation()
    {
        var result = AiBusinessInsightBuilder.Build(Report(15, 10_500_000, 65), Report(15, 10_000_000, 62), []);

        Assert.Equal("Cao", result.Confidence);
        Assert.Single(result.Interpretations);
        Assert.Equal("Thực thu không biến động quá 10% so với kỳ trước.", result.Interpretations[0]);
        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal("Tiếp tục theo dõi", recommendation.Title);
        Assert.DoesNotContain("áp dụng", recommendation.ProposalPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tạo voucher", recommendation.ProposalPrompt, StringComparison.OrdinalIgnoreCase);
    }

    private static ReportSnapshotDto Report(int bookings, decimal receipts, double occupancy) => new(
        bookings, 0, receipts, bookings == 0 ? 0 : receipts / bookings, bookings * 3, 5, occupancy, 0,
        receipts, 0, receipts, 0, receipts, 0, new ReportPeriodReceiptsDto(0, 0, receipts, 0, null),
        [], [], [], [], [], [], [], [], [], []);
}

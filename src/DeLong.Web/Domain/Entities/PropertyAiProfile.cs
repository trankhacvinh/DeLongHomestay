using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class PropertyAiProfile : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public AiProviderKind Provider { get; set; } = AiProviderKind.OpenAi;
    public string Model { get; set; } = "gpt-5-mini";
    public string ProtectedApiKey { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; } = 2000;
    public int MonthlyTokenLimit { get; set; }
    public decimal MonthlyBudgetUsd { get; set; }
    public decimal InputCostPerMillionTokensUsd { get; set; }
    public decimal OutputCostPerMillionTokensUsd { get; set; }
    public int BudgetWarningPercent { get; set; } = 80;
    public bool IsPublicAiEnabled { get; set; }
    public int AdminBudgetReservePercent { get; set; } = 30;
    public int PublicRequestsPerMinute { get; set; } = 10;
    public int PublicRequestsPerDay { get; set; } = 100;
    public Guid? UpdatedByUserId { get; set; }
}

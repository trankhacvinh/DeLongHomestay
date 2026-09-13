namespace DeLong.Web.Domain.Enums;

public enum AiProviderKind { OpenAi = 0, Gemini = 1, DeepSeek = 2 }
public enum AiAudience { Admin, Staff, Customer }
public enum AiProposalStatus { Pending, Applied, Rejected, Expired, Failed }
public enum AiProposalType { CreateRoomWithRates, CreateVoucher, CreateSpecialPricingDay, UpdatePricingSettings, UpdateRoomRate, Batch, ConfigureRoomRates, UpdateRoom, CreateRoomRate, UpdateVoucher, UpdateSpecialPricingDay, UpdateRoomContent, UpdateSiteSettings }

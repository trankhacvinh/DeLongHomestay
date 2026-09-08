namespace DeLong.Web.Domain.Enums;

public enum AiProviderKind { OpenAi, Gemini }
public enum AiProposalStatus { Pending, Applied, Rejected, Expired, Failed }
public enum AiProposalType { CreateRoomWithRates, CreateVoucher, CreateSpecialPricingDay, UpdatePricingSettings, UpdateRoomRate, Batch, ConfigureRoomRates, UpdateRoom, CreateRoomRate, UpdateVoucher, UpdateSpecialPricingDay }

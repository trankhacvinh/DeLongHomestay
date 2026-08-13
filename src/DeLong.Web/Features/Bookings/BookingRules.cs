using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Bookings;

public static class BookingRules
{
    public static bool LocksRoom(BookingStatus status) => status is
        BookingStatus.Held or BookingStatus.Confirmed or BookingStatus.CheckedIn;

    public static bool CanTransition(BookingStatus current, BookingStatus next)
    {
        if (current == next) return true;
        return current switch
        {
            BookingStatus.Requested => next is BookingStatus.Held or BookingStatus.Confirmed or BookingStatus.Cancelled,
            BookingStatus.Held => next is BookingStatus.Confirmed or BookingStatus.Cancelled,
            BookingStatus.Confirmed => next is BookingStatus.CheckedIn or BookingStatus.Cancelled or BookingStatus.NoShow,
            BookingStatus.CheckedIn => next is BookingStatus.Completed,

            // Hai trạng thái kết thúc do thao tác của nhân viên được phép khôi phục khi bấm nhầm.
            // Hủy quay về Yêu cầu (không khóa phòng) để nhân viên chủ động xác nhận lại.
            // Không đến chỉ có thể phát sinh từ Đã xác nhận nên khôi phục về Đã xác nhận;
            // ChangeStatusAsync vẫn chạy conflict guard trước khi khóa lại phòng.
            BookingStatus.Cancelled => next is BookingStatus.Requested,
            BookingStatus.NoShow => next is BookingStatus.Confirmed,

            // Hoàn tất có tác động sang dọn phòng nên vẫn là trạng thái một chiều.
            BookingStatus.Completed => false,
            _ => false
        };
    }
}

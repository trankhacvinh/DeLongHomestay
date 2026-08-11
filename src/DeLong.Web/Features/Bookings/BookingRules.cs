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
            BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.NoShow => false,
            _ => false
        };
    }
}

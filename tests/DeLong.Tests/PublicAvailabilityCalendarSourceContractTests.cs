using Xunit;

namespace DeLong.Tests;

public sealed class PublicAvailabilityCalendarSourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Cms_exposes_the_public_availability_calendar_block()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/Site/SiteContentService.cs");
        var editor = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-visual-editor-v2.js");
        var home = ReadRepositoryFile("src/DeLong.Web/Pages/Index.cshtml");

        Assert.Contains("\"AvailabilityCalendar\"", service, StringComparison.Ordinal);
        Assert.Contains("['AvailabilityCalendar', 'Lịch phòng trống V2']", editor, StringComparison.Ordinal);
        Assert.Contains("case \"AvailabilityCalendar\"", home, StringComparison.Ordinal);
        Assert.Contains("data-public-availability-calendar", home, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Consecutive_slots_open_the_existing_booking_flow_inside_a_static_modal()
    {
        var source = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/public-availability-calendar.css");

        Assert.Contains("/api/public/room-availability", source, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.set('slots'", source, StringComparison.Ordinal);
        Assert.Contains("function nextCandidate()", source, StringComparison.Ordinal);
        Assert.Contains("data-selection-book", source, StringComparison.Ordinal);
        Assert.Contains("selectSlot(day, slot, button)", source, StringComparison.Ordinal);
        Assert.Contains("trigger?.classList.toggle('is-selected', selected)", source, StringComparison.Ordinal);
        Assert.Contains("existing >= 0 && existing === state.selected.length - 1", source, StringComparison.Ordinal);
        Assert.Contains("button.setAttribute('aria-pressed', String(selected))", source, StringComparison.Ordinal);
        Assert.Contains(".public-v2-selection{position:fixed", styles, StringComparison.Ordinal);
        Assert.Contains(".public-v2-slot-bar.state-available.is-selected::after{content:'✓'", styles, StringComparison.Ordinal);
        Assert.Contains("public-v2-viewport-shell", source, StringComparison.Ordinal);
        Assert.Contains("remainingIndicatorTime", source, StringComparison.Ordinal);
        Assert.Contains("Vuốt thêm để tải tiếp", source, StringComparison.Ordinal);
        Assert.Contains("public-v2-loader-spin", styles, StringComparison.Ordinal);
        Assert.Contains(".public-v2-load-sentinel.ready", styles, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.set('embed', '1')", source, StringComparison.Ordinal);
        Assert.Contains("bookingModal.open", source, StringComparison.Ordinal);
        Assert.Contains("<iframe", source, StringComparison.Ordinal);
        Assert.DoesNotContain("backdrop.addEventListener('click'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("event.key === 'Escape'", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Embedded_booking_form_has_a_mobile_specific_layout()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/booking-embed.css");

        Assert.Contains("~/css/booking-embed.css", layout, StringComparison.Ordinal);
        Assert.Contains(".public-embedded-page .public-form-grid", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1fr", styles, StringComparison.Ordinal);
        Assert.Contains(".public-embedded-page .public-rate-choice-grid", styles, StringComparison.Ordinal);
        Assert.Contains("repeat(2, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden", styles, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Public_calendar_loads_forward_in_batches_and_virtualizes_date_rows()
    {
        var source = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js");

        Assert.Contains("const batchSize = 14", source, StringComparison.Ordinal);
        Assert.Contains("data-calendar-top", source, StringComparison.Ordinal);
        Assert.Contains("data-calendar-bottom", source, StringComparison.Ordinal);
        Assert.Contains("viewport.scrollTop / rowHeight", source, StringComparison.Ordinal);
        Assert.Contains("window.requestAnimationFrame(renderVirtualRows)", source, StringComparison.Ordinal);
        Assert.Contains("void loadNextBatch()", source, StringComparison.Ordinal);
        Assert.Contains("batchRequestedInGesture", source, StringComparison.Ordinal);
        Assert.Contains("function beginScrollGesture(forceNew = false)", source, StringComparison.Ordinal);
        Assert.Contains("viewport.addEventListener('scroll', handleViewportScroll", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (viewport.scrollTop + viewport.clientHeight >= viewport.scrollHeight - (rowHeight * 5)) void loadNextBatch();", source, StringComparison.Ordinal);
        Assert.Contains("repeat(${Math.max(1, count)}, minmax(0, 1fr))", source, StringComparison.Ordinal);
        Assert.DoesNotContain("data-calendar-date", source, StringComparison.Ordinal);
        Assert.DoesNotContain("data-calendar-prev", source, StringComparison.Ordinal);
        Assert.DoesNotContain("data-calendar-next", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Guest_guide_editor_has_its_own_expand_control()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Rooms/Content.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-room-content.js");

        Assert.Contains("toggleGuestGuideExpanded", page, StringComparison.Ordinal);
        Assert.Contains("guestGuideExpanded", script, StringComparison.Ordinal);
        Assert.Contains("guestGuideQuill?.focus()", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Booking_conflict_is_visible_and_disables_the_rejected_slot()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Booking/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-booking.js");

        Assert.Contains("id=\"booking-conflict-notice\"", page, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", page, StringComparison.Ordinal);
        Assert.Contains("handleBookingConflict(attemptedRoomId, attemptedRateId, message)", script, StringComparison.Ordinal);
        Assert.Contains("if (rate) rate.available = false", script, StringComparison.Ordinal);
        Assert.Contains("this.selectedRateId = null", script, StringComparison.Ordinal);
        Assert.Contains("this.bookingConflictMessage = message", script, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}

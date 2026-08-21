using DeLong.Web.Common.Persistence;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Data;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomRate> RoomRates => Set<RoomRate>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
    public DbSet<AmenityPreset> AmenityPresets => Set<AmenityPreset>();
    public DbSet<AmenityPresetItem> AmenityPresetItems => Set<AmenityPresetItem>();
    public DbSet<RoomTag> RoomTags => Set<RoomTag>();
    public DbSet<RoomTagAssignment> RoomTagAssignments => Set<RoomTagAssignment>();
    public DbSet<RoomHighlight> RoomHighlights => Set<RoomHighlight>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserPropertyAccess> UserPropertyAccesses => Set<UserPropertyAccess>();
    public DbSet<PropertyGalleryItem> PropertyGalleryItems => Set<PropertyGalleryItem>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<GlobalEditorialShowcase> GlobalEditorialShowcases => Set<GlobalEditorialShowcase>();
    public DbSet<PropertyNotification> PropertyNotifications => Set<PropertyNotification>();
    public DbSet<PropertyNotificationRead> PropertyNotificationReads => Set<PropertyNotificationRead>();
    public DbSet<PropertyNotificationSettings> PropertyNotificationSettings => Set<PropertyNotificationSettings>();
    public DbSet<NotificationEmailOutbox> NotificationEmailOutbox => Set<NotificationEmailOutbox>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<CustomerAccountLink> CustomerAccountLinks => Set<CustomerAccountLink>();
    public DbSet<CustomerAccountSettings> CustomerAccountSettings => Set<CustomerAccountSettings>();
    public DbSet<CustomerAccountTermsAcceptance> CustomerAccountTermsAcceptances => Set<CustomerAccountTermsAcceptance>();
    public DbSet<LoyaltyLedgerEntry> LoyaltyLedgerEntries => Set<LoyaltyLedgerEntry>();
    public DbSet<RoomConditionReport> RoomConditionReports => Set<RoomConditionReport>();
    public DbSet<RoomConditionReportImage> RoomConditionReportImages => Set<RoomConditionReportImage>();
    public DbSet<RoomConditionTag> RoomConditionTags => Set<RoomConditionTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.HousekeepingBeforeCheckInMinutes).HasDefaultValue(0).IsRequired();
            entity.Property(x => x.HousekeepingAfterCheckOutMinutes).HasDefaultValue(0).IsRequired();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "ck_properties_housekeeping_before_check_in_minutes",
                    "housekeeping_before_check_in_minutes BETWEEN 0 AND 1440");
                table.HasCheckConstraint(
                    "ck_properties_housekeeping_after_check_out_minutes",
                    "housekeeping_after_check_out_minutes BETWEEN 0 AND 1440");
            });
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HousekeepingStatus).HasConversion<string>().HasMaxLength(20).HasDefaultValue(HousekeepingStatus.Clean).IsRequired();
            entity.HasOne(x => x.Property).WithMany(x => x.Rooms).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomConditionReport>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.RoomId, x.CreatedAtUtc });
            entity.Property(x => x.InspectionType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.TagsJson).HasColumnType("jsonb").IsRequired();
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomConditionReportImage>(entity =>
        {
            entity.HasIndex(x => new { x.ReportId, x.SortOrder });
            entity.Property(x => x.OriginalFileName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.OriginalStoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.LargePath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.CardPath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ThumbnailPath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Report).WithMany(x => x.Images).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomConditionTag>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.NormalizedName }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomRate>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RoomRateType.TimeSlot).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasOne(x => x.Room).WithMany(x => x.Rates).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.NormalizedPhone }).IsUnique();
            entity.HasIndex(x => new { x.PropertyId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.NormalizedPhone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.IdentityNumber).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerAccountLink>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.PropertyId, x.CustomerId });
            entity.HasIndex(x => new { x.UserId, x.PropertyId }).IsUnique();
            entity.HasIndex(x => x.CustomerId).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerAccountSettings>(entity =>
        {
            entity.HasIndex(x => x.PropertyId).IsUnique();
            entity.Property(x => x.LoyaltySpendPerPoint).HasDefaultValue(10_000).IsRequired();
            entity.Property(x => x.BenefitText).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.TermsTitle).HasMaxLength(240).IsRequired();
            entity.Property(x => x.TermsHtml).HasColumnType("text").IsRequired();
            entity.HasOne(x => x.Property).WithOne().HasForeignKey<CustomerAccountSettings>(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_customer_account_settings_spend_per_point", "loyalty_spend_per_point BETWEEN 1 AND 1000000000"));
        });

        modelBuilder.Entity<CustomerAccountTermsAcceptance>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.PropertyId, x.TermsVersion }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoyaltyLedgerEntry>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.PropertyId, x.CreatedAtUtc });
            entity.HasIndex(x => x.BookingId).IsUnique().HasFilter("\"booking_id\" IS NOT NULL");
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.PropertyId, x.Status });
            entity.HasIndex(x => new { x.RoomId, x.CheckInUtc, x.CheckOutUtc });
            entity.HasIndex(x => new { x.PropertyId, x.PublicRequestKey }).IsUnique().HasFilter("\"public_request_key\" IS NOT NULL");
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).HasDefaultValue(BookingType.TimeSlot).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.RateName).HasMaxLength(100);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.RoomAmount).HasPrecision(18, 2);
            entity.Property(x => x.ExtraAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.Source).HasMaxLength(100);
            entity.Property(x => x.PublicRequestKey).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RoomRate).WithMany().HasForeignKey(x => x.RoomRateId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(x => new { x.BookingId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.PropertyId, x.OccurredAtUtc });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Reference).HasMaxLength(200);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.Property(x => x.VoidReason).HasMaxLength(1000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.OccurredAtUtc });
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.Reference).HasMaxLength(200);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.Property(x => x.VoidReason).HasMaxLength(1000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.BeforeJson).HasColumnType("jsonb");
            entity.Property(x => x.AfterJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPropertyAccess>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.PropertyId });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertySiteSettings>(entity =>
        {
            entity.Property(x => x.GalleryLayout).HasMaxLength(30).HasDefaultValue("mosaic").IsRequired();
        });

        modelBuilder.Entity<PropertyGalleryItem>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.SortOrder });
            entity.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.AltText).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Caption).HasMaxLength(500);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.Slug }).IsUnique();
            entity.HasIndex(x => new { x.PropertyId, x.IsPublished, x.PublishedAtUtc });
            entity.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Excerpt).HasMaxLength(800).IsRequired();
            entity.Property(x => x.CoverImageUrl).HasMaxLength(1000);
            entity.Property(x => x.BodyHtml).HasColumnType("text").IsRequired();
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GlobalEditorialShowcase>(entity =>
        {
            entity.Property(x => x.GalleryMode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.GalleryPropertyIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.GalleryItemIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.GalleryTitle).HasMaxLength(240).IsRequired();
            entity.Property(x => x.GalleryLayout).HasMaxLength(30).HasDefaultValue("mosaic").IsRequired();
            entity.Property(x => x.BlogMode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.BlogPropertyIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.BlogPostIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.BlogTitle).HasMaxLength(240).IsRequired();
        });

        modelBuilder.Entity<PropertyNotification>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PropertyId, x.Type, x.BookingId }).IsUnique().HasFilter("\"booking_id\" IS NOT NULL");
            entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.ActionUrl).HasMaxLength(1000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertyNotificationRead>(entity =>
        {
            entity.HasKey(x => new { x.NotificationId, x.UserId });
            entity.HasIndex(x => new { x.UserId, x.ReadAtUtc });
            entity.HasOne(x => x.Notification).WithMany(x => x.Reads).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertyNotificationSettings>(entity =>
        {
            entity.HasIndex(x => x.PropertyId).IsUnique();
            entity.Property(x => x.EmailRecipients).HasMaxLength(2000);
            entity.Property(x => x.SmtpHost).HasMaxLength(300);
            entity.Property(x => x.SmtpUsername).HasMaxLength(300);
            entity.Property(x => x.SmtpFromEmail).HasMaxLength(320);
            entity.Property(x => x.SmtpFromName).HasMaxLength(240);
            entity.Property(x => x.LastEmailError).HasMaxLength(2000);
            entity.HasOne(x => x.Property).WithOne().HasForeignKey<PropertyNotificationSettings>(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationEmailOutbox>(entity =>
        {
            entity.HasIndex(x => x.NotificationId).IsUnique();
            entity.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc });
            entity.Property(x => x.ToRecipients).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.BodyText).HasColumnType("text").IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PropertyId, x.Sha256 });
            entity.HasIndex(x => x.StorageKey).IsUnique();
            entity.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(600).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.AltText).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationUser>(entity => entity.Property(x => x.DisplayName).HasMaxLength(200));
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplySnakeCaseNames();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HousekeepingStatus).HasConversion<string>().HasMaxLength(20).HasDefaultValue(HousekeepingStatus.Clean).IsRequired();
            entity.HasOne(x => x.Property).WithMany(x => x.Rooms).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.PropertyId, x.Status });
            entity.HasIndex(x => new { x.RoomId, x.CheckInUtc, x.CheckOutUtc });
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).HasDefaultValue(BookingType.TimeSlot).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.RateName).HasMaxLength(100);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.RoomAmount).HasPrecision(18, 2);
            entity.Property(x => x.ExtraAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.Source).HasMaxLength(100);
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

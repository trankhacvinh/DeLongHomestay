using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class PayrollConfiguration : IEntityTypeConfiguration<Employee>, IEntityTypeConfiguration<EmployeeSalaryRate>,
    IEntityTypeConfiguration<AttendanceEntry>, IEntityTypeConfiguration<PayrollAdjustment>, IEntityTypeConfiguration<PayrollHoliday>,
    IEntityTypeConfiguration<PayrollPolicy>, IEntityTypeConfiguration<PayrollPeriod>, IEntityTypeConfiguration<PayrollTransaction>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.PropertyId, x.IsActive, x.SortOrder });
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Position).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_employee_shifts_per_day", "shifts_per_day BETWEEN 1 AND 6"));
    }

    public void Configure(EntityTypeBuilder<EmployeeSalaryRate> b)
    {
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.Property(x => x.MonthlySalary).HasPrecision(18, 2);
        b.HasOne(x => x.Employee).WithMany(x => x.SalaryRates).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_employee_salary_rate_amount", "monthly_salary >= 0"));
    }

    public void Configure(EntityTypeBuilder<AttendanceEntry> b)
    {
        b.HasIndex(x => new { x.EmployeeId, x.WorkDate, x.Slot }).IsUnique();
        b.HasIndex(x => new { x.PropertyId, x.WorkDate });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollAdjustment> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.Month });
        b.Property(x => x.Type).HasMaxLength(50).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollHoliday> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.Date }).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollPolicy> b)
    {
        b.HasIndex(x => x.PropertyId).IsUnique();
        b.Property(x => x.AttendanceBonus).HasPrecision(18, 2);
        b.Property(x => x.HolidayMultiplier).HasPrecision(6, 2);
        b.Property(x => x.TetMultiplier).HasPrecision(6, 2);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.Month }).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollTransaction> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.RequestKey }).IsUnique();
        b.HasIndex(x => new { x.PropertyId, x.Month });
        b.HasIndex(x => x.ExpenseId).IsUnique();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Expense>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_payroll_transaction_amount", "amount > 0"));
    }
}

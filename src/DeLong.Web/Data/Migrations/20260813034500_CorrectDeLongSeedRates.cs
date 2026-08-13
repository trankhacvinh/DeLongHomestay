using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260813034500_CorrectDeLongSeedRates")]
public partial class CorrectDeLongSeedRates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE room_rates AS rr
            SET start_time = v.new_start,
                end_time = v.new_end,
                updated_at_utc = NOW()
            FROM rooms AS r,
            (VALUES
                ('ABAUS-02', 1, TIME '10:30', TIME '13:30', TIME '11:00', TIME '14:00'),
                ('ABAUS-02', 2, TIME '14:00', TIME '17:00', TIME '14:30', TIME '17:30'),
                ('ABAUS-02', 3, TIME '17:30', TIME '20:30', TIME '18:00', TIME '21:00'),
                ('ABAUS-02', 4, TIME '21:00', TIME '09:30', TIME '21:30', TIME '10:00'),
                ('HONGKONG-03', 1, TIME '10:30', TIME '13:30', TIME '11:00', TIME '14:00'),
                ('HONGKONG-03', 2, TIME '14:00', TIME '17:00', TIME '14:30', TIME '17:30'),
                ('HONGKONG-03', 3, TIME '17:30', TIME '20:30', TIME '18:00', TIME '21:00'),
                ('HONGKONG-03', 4, TIME '21:00', TIME '09:30', TIME '21:30', TIME '10:00'),
                ('MOON-04', 1, TIME '10:30', TIME '13:30', TIME '11:30', TIME '14:30'),
                ('MOON-04', 2, TIME '14:00', TIME '17:00', TIME '15:00', TIME '18:00'),
                ('MOON-04', 3, TIME '17:30', TIME '20:30', TIME '18:30', TIME '21:30'),
                ('MOON-04', 4, TIME '21:00', TIME '09:30', TIME '22:00', TIME '10:30'),
                ('AMBER-05', 1, TIME '10:30', TIME '13:30', TIME '12:00', TIME '15:00'),
                ('AMBER-05', 2, TIME '14:00', TIME '17:00', TIME '15:30', TIME '18:30'),
                ('AMBER-05', 3, TIME '17:30', TIME '20:30', TIME '19:00', TIME '22:00'),
                ('AMBER-05', 4, TIME '21:00', TIME '09:30', TIME '22:30', TIME '11:00'),
                ('ROMAN-06', 1, TIME '10:30', TIME '13:30', TIME '12:00', TIME '15:00'),
                ('ROMAN-06', 2, TIME '14:00', TIME '17:00', TIME '15:30', TIME '18:30'),
                ('ROMAN-06', 3, TIME '17:30', TIME '20:30', TIME '19:00', TIME '22:00'),
                ('ROMAN-06', 4, TIME '21:00', TIME '09:30', TIME '22:30', TIME '11:00')
            ) AS v(code, sort_order, old_start, old_end, new_start, new_end)
            WHERE rr.room_id = r.id
              AND r.property_id = '0198a5a0-1000-7000-8000-000000000001'::uuid
              AND r.code = v.code
              AND rr.sort_order = v.sort_order
              AND rr.start_time = v.old_start
              AND rr.end_time = v.old_end;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE room_rates AS rr
            SET start_time = v.old_start,
                end_time = v.old_end,
                updated_at_utc = NOW()
            FROM rooms AS r,
            (VALUES
                ('ABAUS-02', 1, TIME '10:30', TIME '13:30', TIME '11:00', TIME '14:00'),
                ('ABAUS-02', 2, TIME '14:00', TIME '17:00', TIME '14:30', TIME '17:30'),
                ('ABAUS-02', 3, TIME '17:30', TIME '20:30', TIME '18:00', TIME '21:00'),
                ('ABAUS-02', 4, TIME '21:00', TIME '09:30', TIME '21:30', TIME '10:00'),
                ('HONGKONG-03', 1, TIME '10:30', TIME '13:30', TIME '11:00', TIME '14:00'),
                ('HONGKONG-03', 2, TIME '14:00', TIME '17:00', TIME '14:30', TIME '17:30'),
                ('HONGKONG-03', 3, TIME '17:30', TIME '20:30', TIME '18:00', TIME '21:00'),
                ('HONGKONG-03', 4, TIME '21:00', TIME '09:30', TIME '21:30', TIME '10:00'),
                ('MOON-04', 1, TIME '10:30', TIME '13:30', TIME '11:30', TIME '14:30'),
                ('MOON-04', 2, TIME '14:00', TIME '17:00', TIME '15:00', TIME '18:00'),
                ('MOON-04', 3, TIME '17:30', TIME '20:30', TIME '18:30', TIME '21:30'),
                ('MOON-04', 4, TIME '21:00', TIME '09:30', TIME '22:00', TIME '10:30'),
                ('AMBER-05', 1, TIME '10:30', TIME '13:30', TIME '12:00', TIME '15:00'),
                ('AMBER-05', 2, TIME '14:00', TIME '17:00', TIME '15:30', TIME '18:30'),
                ('AMBER-05', 3, TIME '17:30', TIME '20:30', TIME '19:00', TIME '22:00'),
                ('AMBER-05', 4, TIME '21:00', TIME '09:30', TIME '22:30', TIME '11:00'),
                ('ROMAN-06', 1, TIME '10:30', TIME '13:30', TIME '12:00', TIME '15:00'),
                ('ROMAN-06', 2, TIME '14:00', TIME '17:00', TIME '15:30', TIME '18:30'),
                ('ROMAN-06', 3, TIME '17:30', TIME '20:30', TIME '19:00', TIME '22:00'),
                ('ROMAN-06', 4, TIME '21:00', TIME '09:30', TIME '22:30', TIME '11:00')
            ) AS v(code, sort_order, old_start, old_end, new_start, new_end)
            WHERE rr.room_id = r.id
              AND r.property_id = '0198a5a0-1000-7000-8000-000000000001'::uuid
              AND r.code = v.code
              AND rr.sort_order = v.sort_order
              AND rr.start_time = v.new_start
              AND rr.end_time = v.new_end;
            """);
    }
}

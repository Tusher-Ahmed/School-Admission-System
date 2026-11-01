using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Maps the entities onto the tables created by schema.sql.
/// There are no EF migrations: schema.sql is the single source of truth.
/// </summary>
public class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.TotalSeats).HasColumnName("total_seats");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClassId).HasColumnName("class_id");
            e.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("char(17)");
            e.Property(x => x.Status).HasColumnName("status").HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<HoldStatus>(v, true));
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        });
    }
}

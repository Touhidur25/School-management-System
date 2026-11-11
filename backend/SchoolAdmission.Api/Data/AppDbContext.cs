using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Stored as "HELD", "CONFIRMED", ... so DB values match the API values.
        var statusConverter = new ValueConverter<HoldStatus, string>(
            v => v.ToString().ToUpperInvariant(),
            s => Enum.Parse<HoldStatus>(s, true));

        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes", t => t.HasCheckConstraint("ck_classes_total_seats", "total_seats > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(32);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.TotalSeats).HasColumnName("total_seats");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(32);
            e.Property(x => x.ClassId).HasColumnName("class_id").HasMaxLength(32).IsRequired();
            e.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16)
                .HasConversion(statusConverter).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");

            e.HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // Safety net: one student can never have two active (HELD/CONFIRMED)
            // rows for the same class, even if application logic has a bug.
            e.HasIndex(x => new { x.ClassId, x.StudentId })
                .IsUnique()
                .HasDatabaseName("ux_holds_active_student_class")
                .HasFilter("status IN ('HELD','CONFIRMED')");

            // Seat counting per class.
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt })
                .HasDatabaseName("ix_holds_class_status_expires");

            e.HasIndex(x => x.StudentId).HasDatabaseName("ix_holds_student");
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(32);
            e.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            e.Property(x => x.HoldId).HasColumnName("hold_id").HasMaxLength(32).IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(40).IsRequired();
            e.Property(x => x.PreviousStatus).HasColumnName("previous_status").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ReadAt).HasColumnName("read_at");

            e.HasOne(x => x.Hold)
                .WithMany()
                .HasForeignKey(x => x.HoldId)
                .OnDelete(DeleteBehavior.Restrict);

            // Keep this name in sync with DbInitializer.UpgradeSchemaAsync.
            e.HasIndex(x => new { x.StudentId, x.CreatedAt }).HasDatabaseName("ix_notifications_student_created");
        });
    }
}

using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerMember> CustomerMembers { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Sitting> Sittings { get; set; }
    public DbSet<Area> Areas { get; set; }
    public DbSet<Table> Tables { get; set; }
    public DbSet<ReservationTable> ReservationTables { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Identity tables before the Bean Scene relationships.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CustomerMember>()
            .HasBaseType((Type?)null);

        modelBuilder.Entity<CustomerMember>()
            .HasKey(c => c.CustomerID);

        // One login account can have one customer. A customer can exist without a login.
        modelBuilder.Entity<CustomerMember>()
            .HasOne(c => c.ApplicationUser)
            .WithOne(u => u.CustomerMember)
            .HasForeignKey<CustomerMember>(c => c.UserID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerMember>()
            .HasIndex(c => c.UserID)
            .IsUnique();

        modelBuilder.Entity<Reservation>()
            .HasKey(r => r.ReservationID);

        modelBuilder.Entity<Sitting>()
            .HasKey(s => s.SittingID);

        modelBuilder.Entity<Area>()
            .HasKey(a => a.AreaID);

        modelBuilder.Entity<Table>()
            .HasKey(t => t.TableID);

        // ReservationTable links reservations and restaurant tables.
        modelBuilder.Entity<ReservationTable>()
            .HasKey(rt => new { rt.ReservationID, rt.TableID });

        // A customer or sitting cannot be deleted while reservations still use it.
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.CustomerMember)
            .WithMany()
            .HasForeignKey(r => r.CustomerID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Sitting)
            .WithMany()
            .HasForeignKey(r => r.SittingID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Table>()
            .HasOne<Area>()
            .WithMany()
            .HasForeignKey(t => t.AreaID)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting a reservation also removes its table links.
        modelBuilder.Entity<ReservationTable>()
            .HasOne(rt => rt.Reservation)
            .WithMany(r => r.ReservationTables)
            .HasForeignKey(rt => rt.ReservationID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReservationTable>()
            .HasOne(rt => rt.Table)
            .WithMany(t => t.ReservationTables)
            .HasForeignKey(rt => rt.TableID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using VenueBookingSystem.Models;

namespace VenueBookingSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Venue> Venues { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingViewModel> vw_EnhancedBookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Venue configuration
            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(e => e.VenueID);
                entity.Property(e => e.VenueName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Capacity).IsRequired();
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.ToTable("Venues", "dbo");
            });

            // Event configuration
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.EventID);
                entity.Property(e => e.EventName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.EventDate).IsRequired();
                entity.Property(e => e.DurationHours).IsRequired();
                entity.Property(e => e.OrganizerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

                entity.ToTable("Events", "dbo");
            });

            // Booking configuration
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingID);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
                entity.Property(e => e.SpecialRequests).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => new { e.VenueID, e.BookingDate })
                      .IsUnique()
                      .HasDatabaseName("UQ_Booking_DateTime");

                entity.HasOne(e => e.Venue)
                      .WithMany(e => e.Bookings)
                      .HasForeignKey(e => e.VenueID)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Event)
                      .WithMany(e => e.Bookings)
                      .HasForeignKey(e => e.EventID)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.ToTable("Bookings", "dbo");
            });

            // View configuration
            modelBuilder.Entity<BookingViewModel>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_EnhancedBookings", "dbo");
            });
        }
    }
}
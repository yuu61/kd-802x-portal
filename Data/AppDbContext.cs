using kd_802x_portal.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace kd_802x_portal.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<EncryptedPassword> EncryptedPasswords => Set<EncryptedPassword>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Username).IsUnique();
            b.HasIndex(u => u.Email).IsUnique();
            b.Property(u => u.CreatedAt).HasColumnType("datetime(6)");
            b.Property(u => u.UpdatedAt).HasColumnType("datetime(6)");
        });

        modelBuilder.Entity<EncryptedPassword>(b =>
        {
            b.HasIndex(p => p.UserId).IsUnique();
            b.Property(p => p.Ciphertext).HasColumnType("TEXT");
            b.Property(p => p.CreatedAt).HasColumnType("datetime(6)");
            b.Property(p => p.UpdatedAt).HasColumnType("datetime(6)");
            b.HasOne(p => p.User)
                .WithOne(u => u.Password)
                .HasForeignKey<EncryptedPassword>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

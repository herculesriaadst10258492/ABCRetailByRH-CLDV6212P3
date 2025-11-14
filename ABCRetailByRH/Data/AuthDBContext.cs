using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ABCRetailByRH.Models;

namespace ABCRetailByRH.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var conn = config.GetConnectionString("AuthDatabase");
                optionsBuilder.UseSqlServer(conn);
            }
        }

        // SQL TABLES
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<CartItem> Cart => Set<CartItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // USERS TABLE
            modelBuilder.Entity<AppUser>(e =>
            {
                e.ToTable("Users");
                e.HasKey(u => u.Id);

                e.HasIndex(u => u.Username).IsUnique();

                e.Property(u => u.Username).HasMaxLength(64);
                e.Property(u => u.PasswordHash).HasMaxLength(128);
                e.Property(u => u.Email).HasMaxLength(128);
                e.Property(u => u.Phone).HasMaxLength(32);
                e.Property(u => u.Role).HasMaxLength(16);
            });

            // CART TABLE
            modelBuilder.Entity<CartItem>(e =>
            {
                e.ToTable("Cart");
                e.HasKey(c => c.Id);

                e.Property(c => c.CustomerUsername).HasMaxLength(100);
                e.Property(c => c.ProductId).HasMaxLength(100);
                e.Property(c => c.ProductName).HasMaxLength(200);
                e.Property(c => c.ImageUrl).HasMaxLength(500);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}

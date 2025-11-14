using ABCRetailByRH.Models;
using Microsoft.EntityFrameworkCore;

namespace ABCRetailByRH.Data
{
    public class CartDbContext : DbContext
    {
        public CartDbContext(DbContextOptions<CartDbContext> options)
            : base(options) { }

        public DbSet<CartItem> CartItems => Set<CartItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CartItem>(e =>
            {
                e.ToTable("Cart");
                e.HasKey(c => c.Id);

                e.Property(c => c.Id)
                    .ValueGeneratedOnAdd();

                e.Property(c => c.CustomerUsername)
                    .HasMaxLength(100)
                    .IsRequired();

                e.Property(c => c.ProductId)
                    .HasMaxLength(100)
                    .IsRequired();

                e.Property(c => c.Quantity)
                    .IsRequired();

                e.Property(c => c.ProductName)
                    .HasMaxLength(200)
                    .IsRequired();

                e.Property(c => c.UnitPrice)
                    .IsRequired();

                e.Property(c => c.ImageUrl)
                    .HasMaxLength(500);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}

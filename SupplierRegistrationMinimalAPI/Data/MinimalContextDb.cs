using Microsoft.EntityFrameworkCore;
using SupplierRegistrationMinimalAPI.Models;

namespace SupplierRegistrationMinimalAPI.Data
{
    public class MinimalContextDb : DbContext
    {
        public MinimalContextDb(DbContextOptions<MinimalContextDb> options) : base(options) { }

        public DbSet<SupplierModel> Suppliers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SupplierModel>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<SupplierModel>()
                .Property(p => p.Name)
                .IsRequired()
                .HasColumnType("varchar(200)");

            modelBuilder.Entity<SupplierModel>()
                .Property(p => p.Document)
                .IsRequired()
                .HasColumnType("varchar(14)");

            modelBuilder.Entity<SupplierModel>()
                .ToTable("Suppliers");

            base.OnModelCreating(modelBuilder);
        }
    }
}

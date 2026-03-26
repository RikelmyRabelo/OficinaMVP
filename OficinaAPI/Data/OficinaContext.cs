using Microsoft.EntityFrameworkCore;
using OficinaAPI.Models;

namespace OficinaAPI.Data
{
    public class OficinaContext : DbContext
    {
        public OficinaContext(DbContextOptions<OficinaContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<ServiceOrder> ServiceOrders { get; set; }
        public DbSet<ServiceItem> ServiceItems { get; set; }
        public DbSet<LaborService> LaborServices { get; set; }
        public DbSet<CashTransaction> CashTransactions { get; set; }
        public DbSet<RevenueAdjustment> RevenueAdjustments { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<ServiceOrderPayment> ServiceOrderPayments { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Isso resolve os avisos de precisão decimal (18,2) em todas as tabelas
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }
        }
    }
}
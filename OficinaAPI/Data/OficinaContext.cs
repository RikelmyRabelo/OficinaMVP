using Microsoft.AspNetCore.Mvc;
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

        public DbSet<Employee> Employees { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }

        public DbSet<QuickSale> QuickSales { get; set; }

        public DbSet<LaborService> LaborServices { get; set; }

        public DbSet<CashTransaction> CashTransactions { get; set; }
    }
}
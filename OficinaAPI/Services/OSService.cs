using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using OficinaAPI.DTOs;

namespace OficinaAPI.Services
{
    public interface IOSService
    {
        Task<ServiceOrder> CreateOSAsync(CreateOSDTO request, int month, int year);
        Task<ServiceItem> AddProductItemAsync(int osId, AddItemDTO dto);
        Task<ServiceItem> AddLaborAsync(int osId, AddLaborDTO dto);
        Task<ServiceItem> AddCustomItemAsync(int osId, AddCustomItemDTO dto);
        Task<bool> CompleteOrderAsync(int osId, DateTime completionDate);
    }

    public class OSService : IOSService
    {
        private readonly OficinaContext _context;

        public OSService(OficinaContext context) => _context = context;

        public async Task<ServiceOrder> CreateOSAsync(CreateOSDTO request, int month, int year)
        {
            var vehicle = new Vehicle { CustomerName = request.ClientName, Model = request.VehicleModel, CustomerAddress = request.CustomerAddress, CustomerPhone = request.CustomerPhone };
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var os = new ServiceOrder { VehicleId = vehicle.Id, EntryDate = DateTime.Now, Status = "Pending", AccountingMonth = month, AccountingYear = year };
            _context.ServiceOrders.Add(os);
            await _context.SaveChangesAsync();
            os.Vehicle = vehicle;
            return os;
        }

        public async Task<ServiceItem> AddProductItemAsync(int osId, AddItemDTO dto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == osId);
            var prod = await _context.Products.FindAsync(dto.ProductId);
            var item = new ServiceItem { ServiceOrderId = osId, ProductId = prod.Id, Description = $"{prod.Code} - {prod.Name}", Price = (dto.Price ?? prod.SalePrice) * dto.Quantity, CostPrice = (dto.CostPrice ?? prod.CostPrice) * dto.Quantity, Quantity = dto.Quantity, ItemType = "Product" };
            os.TotalAmount += item.Price;
            if (os.Status == "Completed") prod.StockQuantity -= dto.Quantity;
            _context.ServiceItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<ServiceItem> AddLaborAsync(int osId, AddLaborDTO dto)
        {
            var os = await _context.ServiceOrders.FindAsync(osId);
            var item = new ServiceItem { ServiceOrderId = osId, Description = dto.Description, Price = dto.Price, Quantity = 1, ItemType = "Service" };
            os.TotalAmount += item.Price;
            _context.ServiceItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<ServiceItem> AddCustomItemAsync(int osId, AddCustomItemDTO dto)
        {
            var os = await _context.ServiceOrders.FindAsync(osId);
            var item = new ServiceItem { ServiceOrderId = osId, Description = dto.Description, Price = dto.Price * dto.Quantity, CostPrice = dto.CostPrice * dto.Quantity, Quantity = dto.Quantity, ItemType = "Custom" };
            os.TotalAmount += item.Price;
            _context.ServiceItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> CompleteOrderAsync(int osId, DateTime completionDate)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == osId);
            if (os == null || os.Status == "Completed") return false;

            os.Status = "Completed";
            os.CompletionDate = completionDate;
            foreach (var item in os.Items.Where(i => i.ProductId != null))
            {
                var p = await _context.Products.FindAsync(item.ProductId);
                if (p != null) p.StockQuantity -= item.Quantity;
            }
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
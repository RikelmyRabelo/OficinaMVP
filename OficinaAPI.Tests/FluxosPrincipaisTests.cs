using Microsoft.EntityFrameworkCore;
using OficinaAPI.Controllers;
using OficinaAPI.Data;
using OficinaAPI.Models;
using OficinaAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;
using Microsoft.Extensions.Caching.Memory;

namespace OficinaAPI.Tests
{
    public class FluxosPrincipaisTests
    {
        private readonly Mock<IStorageService> _mockStorage;
        private readonly IMemoryCache _cache;

        public FluxosPrincipaisTests()
        {
            _mockStorage = new Mock<IStorageService>();
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        private OficinaContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<OficinaContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new OficinaContext(options);
            databaseContext.Database.EnsureCreated();
            return databaseContext;
        }

        [Fact]
        public async Task Settings_DeveCriarConfiguracoesIniciais_ComDataDeFechamento()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            var result = await controller.GetSettings();
            var settings = result.Value!;

            Assert.NotNull(settings);
            Assert.Equal(DateTime.Now.Month, settings.ActiveMonth);
            Assert.True(settings.LastClosingDate <= DateTime.Now);
        }

        [Fact]
        public async Task Settings_FecharCiclo_DeveAtualizarDataLastClosing()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);
            var dataAntiga = DateTime.Now.AddDays(-10);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 5, ActiveYear = 2026, LastClosingDate = dataAntiga });
            await context.SaveChangesAsync();

            await controller.CloseAvulsosCycle();

            var settings = await context.SystemSettings.FirstAsync();
            Assert.True(settings.LastClosingDate > dataAntiga);
        }

        [Fact]
        public async Task OS_AdicionarItem_DevePersistirPrecoDeCustoSnapshot()
        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            var p = new Product { Code = "OLEO", Name = "Oleo", SalePrice = 50, CostPrice = 20, StockQuantity = 10 };
            context.Products.Add(p);
            await context.SaveChangesAsync();

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "Rikelmy", VehicleModel = "Carro" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.AddItem(os!.Id, new AddItemDTO { ProductId = p.Id, Quantity = 1 });

            var itemNoBanco = await context.ServiceItems.FirstAsync(i => i.ServiceOrderId == os.Id);
            Assert.Equal(20, itemNoBanco.CostPrice);
            Assert.Equal(50, itemNoBanco.Price);
        }

        [Fact]
        public async Task OS_ItemAvulso_DeveCalcularLucroCorretamente()
        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "Test", VehicleModel = "Car" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.AddCustomItem(os!.Id, new AddCustomItemDTO
            {
                Description = "Peca Externa",
                Price = 100,
                CostPrice = 60,
                Quantity = 1
            });

            var item = await context.ServiceItems.FirstAsync();
            Assert.Equal(40, item.Price - item.CostPrice);
        }

        [Fact]
        public async Task ProfitSummary_DeveDividirLucroProporcionalmente()
        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 5, ActiveYear = 2026 });
            await context.SaveChangesAsync();

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "User", VehicleModel = "V" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.AddCustomItem(os!.Id, new AddCustomItemDTO { Description = "Item", Price = 100, CostPrice = 50, Quantity = 1 });

            var updatePayment = new UpdatePaymentDTO
            {
                AmountPaid = 100,
                Payments = new List<PaymentSplitDTO>
                {
                    new PaymentSplitDTO { PaymentMethod = "PIX", Amount = 100 }
                }
            };
            await osController.UpdateAmountPaid(os.Id, updatePayment);
            await osController.CompleteOrder(os.Id, new CompletionDTO { CompletionDate = DateTime.Now });

            var res = await osController.GetProfitSummary();
            var summary = (res.Result as OkObjectResult)!.Value as ProfitSummaryDTO;

            Assert.Equal(100, summary!.TotalRevenue);
            Assert.Equal(50, summary.TotalProfit);
            Assert.Equal(50, summary.ByPaymentMethod.First(p => p.PaymentMethod == "PIX").Profit);
        }

        [Fact]
        public async Task ResumoFinanceiro_DeveFiltrarPorMetodo_MesmoComPagamentoUnicoLegado()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 5, ActiveYear = 2026 });

            var os = new ServiceOrder
            {
                Status = "Completed",
                TotalAmount = 200,
                AmountPaid = 200,
                PaymentMethod = "PIX",
                AccountingMonth = 5,
                AccountingYear = 2026,
                CompletionDate = DateTime.Now
            };
            context.ServiceOrders.Add(os);
            await context.SaveChangesAsync();

            var res = await controller.GetFinancialSummary();
            var summary = (res.Result as OkObjectResult)!.Value as FinancialSummaryDTO;

            Assert.Equal(200, summary!.TotalPix);
        }

        [Fact]
        public async Task OS_GetTrash_DeveRetornarApenasDeletadosRecentes()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            var resOs1 = await controller.PostServiceOrder(new CreateOSDTO { ClientName = "Deletada", VehicleModel = "Carro A" });
            var os1 = (resOs1.Result as OkObjectResult)!.Value as ServiceOrder;

            var resOs2 = await controller.PostServiceOrder(new CreateOSDTO { ClientName = "Ativa", VehicleModel = "Carro B" });
            var os2 = (resOs2.Result as OkObjectResult)!.Value as ServiceOrder;

            await controller.SoftDeleteServiceOrder(os1!.Id);

            var result = await controller.GetTrash();
            var lista = result.Value!;

            Assert.Single(lista);
            Assert.Equal(os1.Id, lista.First().Id);
        }

        [Fact]
        public async Task OS_Restore_DeveRetornarOrdemParaEstadoAtivo()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _cache, _mockStorage.Object);

            var resOs = await controller.PostServiceOrder(new CreateOSDTO { ClientName = "Restaurada", VehicleModel = "Carro C" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await controller.SoftDeleteServiceOrder(os!.Id);
            await controller.RestoreServiceOrder(os.Id);

            var osRestaurada = await context.ServiceOrders.FindAsync(os.Id);
            Assert.False(osRestaurada!.IsDeleted);
            Assert.Null(osRestaurada.DeletionDate);
        }

        [Fact]
        public async Task Produtos_GetLowStock_DeveRespeitarMinimoConfigurado()
        {
            var context = GetDatabaseContext();
            var controller = new ProductsController(context);

            context.Products.AddRange(
                new Product { Code = "A", Name = "P1", StockQuantity = 10, MinimumStock = 12 },
                new Product { Code = "B", Name = "P2", StockQuantity = 5, MinimumStock = 3 }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetLowStock();
            var lista = result.Value!;

            Assert.Single(lista);
            Assert.Equal("A", lista.First().Code);
        }
    }
}
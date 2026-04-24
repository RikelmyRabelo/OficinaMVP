using Microsoft.EntityFrameworkCore;
using OficinaAPI.Controllers;
using OficinaAPI.Data;
using OficinaAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace OficinaAPI.Tests
{
    public class FluxosPrincipaisTests
    {
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly IMemoryCache _cache;

        public FluxosPrincipaisTests()
        {
            _mockEnv = new Mock<IWebHostEnvironment>();
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
        public async Task Settings_DeveCriarConfiguracoesIniciais_AoConsultarPelaPrimeiraVez()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            var result = await controller.GetSettings();
            var settings = result.Value!;

            Assert.NotNull(settings);
            Assert.Equal(DateTime.Now.Month, settings.ActiveMonth);
            Assert.Equal(DateTime.Now.Year, settings.ActiveYear);
        }

        [Fact]
        public async Task Settings_FecharPeriodo_DeveIncrementarMes()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 3, ActiveYear = 2026 });
            await context.SaveChangesAsync();

            await controller.ClosePeriod();

            var settings = await context.SystemSettings.FirstAsync();
            Assert.Equal(4, settings.ActiveMonth);
            Assert.Equal(2026, settings.ActiveYear);
        }

        [Fact]
        public async Task Settings_FecharDezembro_DeveVirarOAno()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 12, ActiveYear = 2026 });
            await context.SaveChangesAsync();

            await controller.ClosePeriod();

            var settings = await context.SystemSettings.FirstAsync();
            Assert.Equal(1, settings.ActiveMonth);
            Assert.Equal(2027, settings.ActiveYear);
        }

        [Fact]
        public async Task OS_AdicionarItem_NaoDeveBaixarEstoqueImediatamente()
        {
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var produtoSalvo = await context.Products.FirstAsync();

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "Rikelmy", VehicleModel = "Moto" });
            var osCriada = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.AddItem(osCriada!.Id, new AddItemDTO { ProductId = produtoSalvo.Id, Quantity = 2 });

            var produtoNoBanco = await context.Products.FindAsync(produtoSalvo.Id);
            Assert.Equal(10, produtoNoBanco!.StockQuantity);

            var osNoBanco = await context.ServiceOrders.FindAsync(osCriada.Id);
            Assert.Equal(800, osNoBanco!.TotalAmount);
        }

        [Fact]
        public async Task OS_Finalizar_DeveBaixarEstoqueAgora()
        {
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var p = await context.Products.FirstAsync();

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "Test", VehicleModel = "Car" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.AddItem(os!.Id, new AddItemDTO { ProductId = p.Id, Quantity = 2 });

            await osController.CompleteOrder(os.Id, new CompletionDTO { CompletionDate = DateTime.Now });

            var produtoFinal = await context.Products.FindAsync(p.Id);
            Assert.Equal(8, produtoFinal!.StockQuantity);
        }

        [Fact]
        public async Task PagamentoMisto_DeveSalvarMultiplasFormasDePagamento()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            var resOs = await controller.PostServiceOrder(new CreateOSDTO { ClientName = "Cliente Pagador", VehicleModel = "Civic" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            var updatePaymentDto = new UpdatePaymentDTO
            {
                AmountPaid = 500,
                PaymentMethod = "Misto",
                Payments = new List<PaymentSplitDTO>
                {
                    new PaymentSplitDTO { PaymentMethod = "PIX", Amount = 200 },
                    new PaymentSplitDTO { PaymentMethod = "Dinheiro", Amount = 300 }
                }
            };

            await controller.UpdateAmountPaid(os!.Id, updatePaymentDto);

            var osAtualizada = await context.ServiceOrders.Include(o => o.Payments).FirstAsync(o => o.Id == os.Id);
            Assert.Equal(500, osAtualizada.AmountPaid);
            Assert.Equal(2, osAtualizada.Payments.Count);
        }


        [Fact]
        public async Task ResumoFinanceiro_DeveCalcularApenasDoMesContabilAtivo()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 3, ActiveYear = 2026 });

            var os1 = new ServiceOrder { Status = "Completed", TotalAmount = 500, AmountPaid = 500, AccountingMonth = 1, AccountingYear = 2026 };
            var os2 = new ServiceOrder { Status = "Completed", TotalAmount = 1000, AmountPaid = 600, AccountingMonth = 3, AccountingYear = 2026 };

            context.ServiceOrders.AddRange(os1, os2);
            await context.SaveChangesAsync();

            var res = await controller.GetFinancialSummary();
            var summary = (res.Result as OkObjectResult)!.Value as FinancialSummaryDTO;

            Assert.Equal(600, summary!.FaturamentoTotal);
            Assert.Equal(400, summary.Inadimplencia);
        }

        [Fact]
        public async Task OS_GetActive_NaoDeveTrazerOrdensFinalizadasOuDeletadas()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            context.Vehicles.Add(new Vehicle { Id = 1, CustomerName = "João", Model = "Carro" });

            context.ServiceOrders.AddRange(
                new ServiceOrder { Id = 1, VehicleId = 1, Status = "Pending", IsDeleted = false },
                new ServiceOrder { Id = 2, VehicleId = 1, Status = "Completed", IsDeleted = false },
                new ServiceOrder { Id = 3, VehicleId = 1, Status = "Pending", IsDeleted = true }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetActiveServiceOrders();
            var listaAtiva = result.Value!;

            Assert.Single(listaAtiva);
            Assert.Equal(1, listaAtiva.First().Id);
        }

        [Fact]
        public async Task OS_Alertas_DeveTrazerApenasInadimplentesAtrasados()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            context.Vehicles.Add(new Vehicle { Id = 1, CustomerName = "Maria", Model = "Moto" });

            context.ServiceOrders.AddRange(
                new ServiceOrder { Id = 1, VehicleId = 1, Status = "Completed", TotalAmount = 100, AmountPaid = 100 },
                new ServiceOrder { Id = 2, VehicleId = 1, Status = "Completed", TotalAmount = 100, AmountPaid = 50, PromisedPaymentDate = DateTime.Today.AddDays(1) },
                new ServiceOrder { Id = 3, VehicleId = 1, Status = "Completed", TotalAmount = 100, AmountPaid = 50, PromisedPaymentDate = DateTime.Today.AddDays(-1) }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetCollectionAlerts();
            var alertas = result.Value!;

            Assert.Single(alertas);
            Assert.Equal(3, alertas.First().Id);
        }

        [Fact]
        public async Task Lixeira_SoftDelete_DeveRemoverDaListaAtiva()
        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context, _mockEnv.Object, _cache);

            var resOs = await osController.PostServiceOrder(new CreateOSDTO { ClientName = "Cliente Lixeira", VehicleModel = "Moto" });
            var os = (resOs.Result as OkObjectResult)!.Value as ServiceOrder;

            await osController.SoftDeleteServiceOrder(os!.Id);

            var resultAtiva = await osController.GetServiceOrders();
            var listaAtiva = resultAtiva.Value!;

            var resultLixeira = await osController.GetTrash();
            var listaLixeira = resultLixeira.Value!;

            Assert.DoesNotContain(listaAtiva, o => o.Id == os.Id);
            Assert.Contains(listaLixeira, o => o.Id == os.Id);
        }

        [Fact]
        public async Task Produtos_GetLowStock_DeveRetornarEstoqueMenorOuIgualA3()
        {
            var context = GetDatabaseContext();
            var controller = new ProductsController(context);

            context.Products.AddRange(
                new Product { Code = "A", Name = "Prod A", StockQuantity = 10, IsDeleted = false },
                new Product { Code = "B", Name = "Prod B", StockQuantity = 3, IsDeleted = false },
                new Product { Code = "C", Name = "Prod C", StockQuantity = 1, IsDeleted = false },
                new Product { Code = "D", Name = "Prod D", StockQuantity = 0, IsDeleted = true }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetLowStock();
            var produtosBaixoEstoque = result.Value!;

            Assert.Equal(2, produtosBaixoEstoque.Count());
            Assert.Contains(produtosBaixoEstoque, p => p.Code == "B");
            Assert.Contains(produtosBaixoEstoque, p => p.Code == "C");
        }
    }
}
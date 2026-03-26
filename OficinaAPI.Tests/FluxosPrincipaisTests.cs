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

namespace OficinaAPI.Tests
{
    public partial class FluxosPrincipaisTests
    {
        private OficinaContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<OficinaContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new OficinaContext(options);
            databaseContext.Database.EnsureCreated();
            return databaseContext;
        }

        // --- NOVOS TESTES: GESTÃO DE PERÍODO (MÊS ATIVO) ---

        [Fact]
        public async Task Settings_DeveCriarConfiguracoesIniciais_AoConsultarPelaPrimeiraVez()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            var result = await controller.GetSettings();
            var settings = result.Value;

            Assert.NotNull(settings);
            Assert.Equal(DateTime.Now.Month, settings.ActiveMonth);
            Assert.Equal(DateTime.Now.Year, settings.ActiveYear);
        }

        [Fact]
        public async Task Settings_FecharPeriodo_DeveIncrementarMes()
        {
            var context = GetDatabaseContext();
            var controller = new SettingsController(context);

            // Setup inicial: Março/2026
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

            // Setup: Dezembro/2026
            context.SystemSettings.Add(new SystemSettings { ActiveMonth = 12, ActiveYear = 2026 });
            await context.SaveChangesAsync();

            await controller.ClosePeriod();

            var settings = await context.SystemSettings.FirstAsync();
            Assert.Equal(1, settings.ActiveMonth);
            Assert.Equal(2027, settings.ActiveYear);
        }

        // --- TESTES DE FLUXO DE O.S. E ESTOQUE ---

        [Fact]
        public async Task OS_AdicionarItem_NaoDeveBaixarEstoqueImediatamente()
        {
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var produtoSalvo = await context.Products.FirstAsync();

            var osDto = new ServiceOrdersController.CreateOSDTO { ClientName = "Rikelmy", VehicleModel = "Moto" };
            var resOs = await osController.PostServiceOrder(osDto);
            var osCriada = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            var itemDto = new ServiceOrdersController.AddItemDTO { ProductId = produtoSalvo.Id, Quantity = 2 };
            await osController.AddItem(osCriada.Id, itemDto);

            var produtoNoBanco = await context.Products.FindAsync(produtoSalvo.Id);
            Assert.Equal(10, produtoNoBanco.StockQuantity);

            var osNoBanco = await context.ServiceOrders.FindAsync(osCriada.Id);
            Assert.Equal(800, osNoBanco.TotalAmount);
        }

        [Fact]
        public async Task OS_Finalizar_DeveBaixarEstoqueAgora()
        {
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var p = await context.Products.FirstAsync();

            var resOs = await osController.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Test", VehicleModel = "Car" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            await osController.AddItem(os.Id, new ServiceOrdersController.AddItemDTO { ProductId = p.Id, Quantity = 2 });

            var completionDto = new ServiceOrdersController.CompletionDTO { CompletionDate = DateTime.Now };
            await osController.CompleteOrder(os.Id, completionDto);

            var produtoFinal = await context.Products.FindAsync(p.Id);
            Assert.Equal(8, produtoFinal.StockQuantity);
        }

        [Fact]
        public async Task PagamentoMisto_DeveSalvarMultiplasFormasDePagamento()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            var resOs = await controller.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Cliente Pagador", VehicleModel = "Civic" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            var updatePaymentDto = new ServiceOrdersController.UpdatePaymentDTO
            {
                AmountPaid = 500,
                PaymentMethod = "Misto",
                Payments = new List<ServiceOrdersController.PaymentSplitDTO>
                {
                    new ServiceOrdersController.PaymentSplitDTO { PaymentMethod = "PIX", Amount = 200 },
                    new ServiceOrdersController.PaymentSplitDTO { PaymentMethod = "Dinheiro", Amount = 300 }
                }
            };

            await controller.UpdateAmountPaid(os.Id, updatePaymentDto);

            var osAtualizada = await context.ServiceOrders.Include(o => o.Payments).FirstAsync(o => o.Id == os.Id);
            Assert.Equal(500, osAtualizada.AmountPaid);
            Assert.Equal(2, osAtualizada.Payments.Count);
        }

        [Fact]
        public async Task ResumoFinanceiro_DeveCalcularTotaisAcumulados_SemFiltroDeMesNaHome()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            // O.S. de Meses Diferentes
            var os1 = new ServiceOrder { VehicleId = 1, Status = "Completed", TotalAmount = 500, AmountPaid = 500, CompletionDate = new DateTime(2026, 01, 15) };
            var os2 = new ServiceOrder { VehicleId = 2, Status = "Completed", TotalAmount = 1000, AmountPaid = 600, CompletionDate = new DateTime(2026, 03, 10) };

            context.ServiceOrders.AddRange(os1, os2);
            await context.SaveChangesAsync();

            var res = await controller.GetFinancialSummary();
            var summary = (res.Result as OkObjectResult).Value as ServiceOrdersController.FinancialSummaryDTO;

            // Faturamento Total deve ser a soma de tudo (500 + 600 = 1100)
            Assert.Equal(1100, summary.FaturamentoTotal);
            // Inadimplência deve refletir a diferença da os2 (1000 - 600 = 400)
            Assert.Equal(400, summary.Inadimplencia);
        }

        [Fact]
        public async Task Lixeira_SoftDelete_DeveRemoverDaListaAtiva()





        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context);

            var resOs = await osController.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Rikelmy", VehicleModel = "Moto" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            await osController.SoftDeleteServiceOrder(os.Id);

            var listaAtiva = (await osController.GetServiceOrders()).Value;
            var listaLixeira = (await osController.GetTrash()).Value;

            Assert.DoesNotContain(listaAtiva, o => o.Id == os.Id);
            Assert.Contains(listaLixeira, o => o.Id == os.Id);
        }
    }
}
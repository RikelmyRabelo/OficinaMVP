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
    public class FluxosPrincipaisTests
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
        public async Task MaoDeObra_DeveCalcularValorSemAfetarEstoque()
        {
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context);

            var resOs = await osController.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Mecanico", VehicleModel = "Gol" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            var laborDto = new ServiceOrdersController.AddLaborDTO { Description = "Troca de Óleo", Price = 150 };
            await osController.AddLabor(os.Id, laborDto);

            var osAtualizada = await context.ServiceOrders.Include(o => o.Items).FirstAsync(o => o.Id == os.Id);
            Assert.Equal(150, osAtualizada.TotalAmount);
            Assert.Null(osAtualizada.Items.First().ProductId);
        }

        [Fact]
        public async Task Estoque_DeveImpedirProdutoDuplicado()
        {
            var context = GetDatabaseContext();
            var controller = new ProductsController(context);
            var produto = new Product { Code = "OLEO123", Name = "Óleo 5w30", SalePrice = 50, StockQuantity = 10 };

            await controller.PostProduct(produto);
            var resultado = await controller.PostProduct(new Product { Code = "OLEO123", Name = "Outro Óleo", SalePrice = 60 });

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }


        [Fact]
        public async Task OS_Edicao_DeveAtualizarDadosDoCliente()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            var dto = new ServiceOrdersController.CreateOSDTO { ClientName = "Antigo", VehicleModel = "Fusca" };
            var res = await controller.PostServiceOrder(dto);
            var os = (res.Result as CreatedAtActionResult).Value as ServiceOrder;

            var updateDto = new ServiceOrdersController.UpdateVehicleDTO
            {
                CustomerName = "Novo Nome",
                VehicleModel = "Ferrari",
                CustomerPhone = "1199999",
                CustomerAddress = "Rua Teste"
            };
            await controller.UpdateVehicleData(os.Id, updateDto);

            var osAtualizada = await context.ServiceOrders.Include(o => o.Vehicle).FirstAsync(o => o.Id == os.Id);
            Assert.Equal("Novo Nome", osAtualizada.Vehicle.CustomerName);
            Assert.Equal("Ferrari", osAtualizada.Vehicle.Model);
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
        public async Task ResumoFinanceiro_DeveCalcularTotaisEInadimplenciaCorretamente()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            var os1 = new ServiceOrder { VehicleId = 1, EntryDate = DateTime.Now, Status = "Completed", TotalAmount = 500, AmountPaid = 500, PaymentMethod = "Dinheiro", CompletionDate = DateTime.Now };
            context.ServiceOrders.Add(os1);

            var os2 = new ServiceOrder { VehicleId = 2, EntryDate = DateTime.Now, Status = "Completed", TotalAmount = 1000, AmountPaid = 600, PaymentMethod = "Misto", CompletionDate = DateTime.Now };
            context.ServiceOrders.Add(os2);
            context.ServiceOrderPayments.Add(new ServiceOrderPayment { ServiceOrder = os2, PaymentMethod = "PIX", Amount = 600 });

            await context.SaveChangesAsync();

            var res = await controller.GetFinancialSummary();
            var summary = (res.Result as OkObjectResult).Value as ServiceOrdersController.FinancialSummaryDTO;

            Assert.NotNull(summary);
            Assert.Equal(1100, summary.FaturamentoTotal);
            Assert.Equal(400, summary.Inadimplencia);
        }

        [Fact]
        public async Task AjusteFinanceiro_DeveSerSalvoERecuperadoDaSemana()
        {
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);
            var ajusteDto = new ServiceOrdersController.CashAdjustmentDTO { Amount = 150.50m, Description = "Pagamento Extra" };

            await controller.PostRevenueAdjustment(ajusteDto);
            var resList = await controller.GetRevenueAdjustments();
            var lista = resList.Value.ToList();

            Assert.Single(lista);
            Assert.Equal(150.50m, lista[0].Amount);
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


        [Fact]
        public async Task Estoque_ItemExterno_NaoDeveSomarNoValorFinanceiro()
        {
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);

            await prodController.PostProduct(new Product { Code = "LOJA", Name = "Peça Loja", SalePrice = 100, StockQuantity = 1, IsExternal = false });
            await prodController.PostProduct(new Product { Code = "EXT", Name = "Peça Fora", SalePrice = 500, StockQuantity = 1, IsExternal = true });

            var res = await prodController.GetHistoricalStockValue();
            var values = (res.Result as OkObjectResult).Value;

            var currentInventoryValue = (decimal)values.GetType().GetProperty("currentInventoryValue").GetValue(values);

            Assert.Equal(100, currentInventoryValue);
        }
    }
}
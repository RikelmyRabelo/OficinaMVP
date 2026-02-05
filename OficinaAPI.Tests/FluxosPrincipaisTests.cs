using Microsoft.EntityFrameworkCore;
using OficinaAPI.Controllers;
using OficinaAPI.Data;
using OficinaAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace OficinaAPI.Tests
{
    public class FluxosPrincipaisTests
    {
        private OficinaContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<OficinaContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // BD único por teste
                .Options;
            var databaseContext = new OficinaContext(options);
            databaseContext.Database.EnsureCreated();
            return databaseContext;
        }

        [Fact]
        public async Task Estoque_DeveImpedirProdutoDuplicado()
        {
            // Arrange
            var context = GetDatabaseContext();
            var controller = new ProductsController(context);
            var produto = new Product { Code = "OLEO123", Name = "Óleo 5w30", SalePrice = 50, StockQuantity = 10 };

            // Act - Primeira inserção (Sucesso)
            await controller.PostProduct(produto);

            // Act - Segunda inserção com mesmo código (Deve falhar)
            var resultado = await controller.PostProduct(new Product { Code = "OLEO123", Name = "Outro Óleo", SalePrice = 60 });

            // Assert
            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task OS_AdicionarItem_DeveBaixarEstoque()
        {
            // Arrange
            var context = GetDatabaseContext();

            // 1. Criar Produto
            var prodController = new ProductsController(context);
            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 4 });
            var produtoSalvo = await context.Products.FirstAsync();

            // 2. Criar OS
            var osController = new ServiceOrdersController(context);
            var osDto = new ServiceOrdersController.CreateOSDTO { ClientName = "João", VehicleModel = "Fiat Uno" };
            var resultadoOs = await osController.PostServiceOrder(osDto);
            var osCriada = (resultadoOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            // Act - Adicionar 2 Pneus na OS
            var itemDto = new ServiceOrdersController.AddItemDTO { ProductId = produtoSalvo.Id, Quantity = 2 };
            await osController.AddItem(osCriada.Id, itemDto);

            // Assert
            var produtoAtualizado = await context.Products.FindAsync(produtoSalvo.Id);
            var osAtualizada = await context.ServiceOrders.FindAsync(osCriada.Id);

            Assert.Equal(2, produtoAtualizado.StockQuantity); // Estoque era 4, usou 2, sobrou 2
            Assert.Equal(800, osAtualizada.TotalAmount); // 2 * 400 = 800
        }

        [Fact]
        public async Task RH_CicloDeVida_ContratarEPagar()
        {
            // Arrange
            var context = GetDatabaseContext();
            var rhController = new EmployeesController(context);

            // 1. Contratar
            var novoFunc = new Employee { Name = "Carlos", Role = "Mecânico", BaseSalary = 2500 };
            var resContratar = await rhController.PostEmployee(novoFunc);
            var funcCriado = (resContratar.Result as CreatedAtActionResult).Value as Employee;

            // 2. Lançar Pagamento
            var pagamento = new PaymentRecord { Amount = 1250 }; // Adiantamento
            await rhController.AddPayment(funcCriado.Id, pagamento);

            // Act - Confirmar Pagamento (Dar Baixa)
            var pagamentoSalvo = await context.PaymentRecords.FirstAsync();
            await rhController.ConfirmPayment(pagamentoSalvo.Id);

            // Assert
            var pagamentoFinal = await context.PaymentRecords.FirstAsync();
            Assert.True(pagamentoFinal.IsPaid);
            Assert.NotNull(pagamentoFinal.PaymentDate);
            Assert.Contains("Confirmado via Web", pagamentoFinal.AdminNotes);
        }

        [Fact]
        public async Task OS_Edicao_DeveAtualizarDadosDoCliente()
        {
            // Arrange
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            // 1. Criar OS Inicial
            var dto = new ServiceOrdersController.CreateOSDTO { ClientName = "Antigo", VehicleModel = "Fusca" };
            var res = await controller.PostServiceOrder(dto);
            var os = (res.Result as CreatedAtActionResult).Value as ServiceOrder;

            // Act - Editar dados
            var updateDto = new ServiceOrdersController.UpdateVehicleDTO
            {
                CustomerName = "Novo Nome",
                VehicleModel = "Ferrari",
                CustomerPhone = "1199999",
                CustomerAddress = "Rua Teste"
            };
            await controller.UpdateVehicleData(os.Id, updateDto);

            // Assert
            var osAtualizada = await context.ServiceOrders.Include(o => o.Vehicle).FirstAsync(o => o.Id == os.Id);
            Assert.Equal("Novo Nome", osAtualizada.Vehicle.CustomerName);
            Assert.Equal("Ferrari", osAtualizada.Vehicle.Model);
        }
    }
}
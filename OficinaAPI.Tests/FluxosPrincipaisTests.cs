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
            // ARRANGE
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var produtoSalvo = await context.Products.FirstAsync();

            var osDto = new ServiceOrdersController.CreateOSDTO { ClientName = "Rikelmy", VehicleModel = "Moto" };
            var resOs = await osController.PostServiceOrder(osDto);
            var osCriada = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            // ACT
            var itemDto = new ServiceOrdersController.AddItemDTO { ProductId = produtoSalvo.Id, Quantity = 2 };
            await osController.AddItem(osCriada.Id, itemDto);

            // ASSERT
            var produtoNoBanco = await context.Products.FindAsync(produtoSalvo.Id);
            Assert.Equal(10, produtoNoBanco.StockQuantity);

            var osNoBanco = await context.ServiceOrders.FindAsync(osCriada.Id);
            Assert.Equal(800, osNoBanco.TotalAmount);
        }

        [Fact]
        public async Task OS_Finalizar_DeveBaixarEstoqueAgora()
        {
            // ARRANGE
            var context = GetDatabaseContext();
            var prodController = new ProductsController(context);
            var osController = new ServiceOrdersController(context);

            await prodController.PostProduct(new Product { Code = "PNEU01", Name = "Pneu", SalePrice = 400, StockQuantity = 10 });
            var p = await context.Products.FirstAsync();

            var resOs = await osController.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Test", VehicleModel = "Car" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            await osController.AddItem(os.Id, new ServiceOrdersController.AddItemDTO { ProductId = p.Id, Quantity = 2 });

            // ACT
            var completionDto = new ServiceOrdersController.CompletionDTO { CompletionDate = DateTime.Now };
            await osController.CompleteOrder(os.Id, completionDto);

            // ASSERT
            var produtoFinal = await context.Products.FindAsync(p.Id);
            var osFinal = await context.ServiceOrders.FindAsync(os.Id);

            Assert.Equal(8, produtoFinal.StockQuantity);
            Assert.Equal("Completed", osFinal.Status);
        }

        [Fact]
        public async Task MaoDeObra_DeveCalcularValorSemAfetarEstoque()
        {
            // ARRANGE
            var context = GetDatabaseContext();
            var osController = new ServiceOrdersController(context);

            var resOs = await osController.PostServiceOrder(new ServiceOrdersController.CreateOSDTO { ClientName = "Mecanico", VehicleModel = "Gol" });
            var os = (resOs.Result as CreatedAtActionResult).Value as ServiceOrder;

            // ACT
            var laborDto = new ServiceOrdersController.AddLaborDTO { Description = "Troca de Óleo", Price = 150 };
            await osController.AddLabor(os.Id, laborDto);

            // ASSERT
            var osAtualizada = await context.ServiceOrders.Include(o => o.Items).FirstAsync(o => o.Id == os.Id);
            Assert.Equal(150, osAtualizada.TotalAmount);
            Assert.Null(osAtualizada.Items.First().ProductId);
        }

        [Fact]
        public async Task OS_Edicao_DeveAtualizarDadosDoCliente()
        {
            // Arrange
            var context = GetDatabaseContext();
            var controller = new ServiceOrdersController(context);

            var dto = new ServiceOrdersController.CreateOSDTO { ClientName = "Antigo", VehicleModel = "Fusca" };
            var res = await controller.PostServiceOrder(dto);
            var os = (res.Result as CreatedAtActionResult).Value as ServiceOrder;

            // Act
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

        [Fact]
        public async Task Estoque_DeveImpedirProdutoDuplicado()
        {
            // Arrange
            var context = GetDatabaseContext();
            var controller = new ProductsController(context);
            var produto = new Product { Code = "OLEO123", Name = "Óleo 5w30", SalePrice = 50, StockQuantity = 10 };

            // Act
            await controller.PostProduct(produto);
            var resultado = await controller.PostProduct(new Product { Code = "OLEO123", Name = "Outro Óleo", SalePrice = 60 });

            // Assert
            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }
    }
}
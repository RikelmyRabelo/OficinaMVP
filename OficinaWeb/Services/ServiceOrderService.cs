using System.Net.Http.Json;
using OficinaWeb.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace OficinaWeb.Services
{
    public class ServiceOrderService
    {
        private readonly HttpClient _http;
        private readonly string _apiUrl;

        public ServiceOrderService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiUrl = config["ApiBaseUrl"] ?? "https://localhost:7082";
        }

        public async Task<List<ServiceOrderDTO>> GetActiveOrdersAsync()
        {
            return await _http.GetFromJsonAsync<List<ServiceOrderDTO>>($"{_apiUrl}/api/serviceorders/active") ?? new();
        }

        public async Task<List<ServiceOrderDTO>> GetCompletedOrdersAsync(int take = 50)
        {
            return await _http.GetFromJsonAsync<List<ServiceOrderDTO>>($"{_apiUrl}/api/serviceorders/completed?take={take}") ?? new();
        }

        public async Task<List<ServiceOrderDTO>> GetAlertsAsync()
        {
            return await _http.GetFromJsonAsync<List<ServiceOrderDTO>>($"{_apiUrl}/api/serviceorders/alerts") ?? new();
        }


        public async Task<List<LaborServiceDTO>> GetLaborServicesAsync()
        {
            return await _http.GetFromJsonAsync<List<LaborServiceDTO>>($"{_apiUrl}/api/laborservices") ?? new();
        }

        public async Task<List<ProductListDTO>> GetProductsAsync()
        {
            return await _http.GetFromJsonAsync<List<ProductListDTO>>($"{_apiUrl}/api/products") ?? new();
        }

        public async Task<List<AttachmentDTO>> GetAttachmentsAsync(int id)
        {
            return await _http.GetFromJsonAsync<List<AttachmentDTO>>($"{_apiUrl}/api/serviceorders/{id}/attachments") ?? new();
        }

        public async Task<bool> UploadAttachmentAsync(int id, MultipartFormDataContent content)
        {
            var response = await _http.PostAsync($"{_apiUrl}/api/serviceorders/{id}/attachments", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAttachmentAsync(int id, int attachmentId)
        {
            var response = await _http.DeleteAsync($"{_apiUrl}/api/serviceorders/{id}/attachments/{attachmentId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateOrderAsync(CreateOsInput request)
        {
            var response = await _http.PostAsJsonAsync($"{_apiUrl}/api/serviceorders", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CompleteOrderAsync(int id, DateTime completionDate)
        {
            var response = await _http.PutAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/complete", new { CompletionDate = completionDate });
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteOrderAsync(int id)
        {
            var response = await _http.DeleteAsync($"{_apiUrl}/api/serviceorders/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateVehicleAsync(int id, UpdateVehicleDTO request)
        {
            var response = await _http.PutAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/vehicle", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateTotalAsync(int id, decimal totalAmount)
        {
            var response = await _http.PutAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/total", new { TotalAmount = totalAmount });
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdatePaymentAsync(int id, UpdatePaymentDTO request)
        {
            var response = await _http.PutAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/payment", request);
            return response.IsSuccessStatusCode;
        }

        // --- Gestão de Itens da O.S. ---

        public async Task<bool> AddItemAsync(int id, AddItemInput request)
        {
            var response = await _http.PostAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/items", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddCustomItemAsync(int id, AddCustomItemInput request)
        {
            var response = await _http.PostAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/custom-items", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddLaborAsync(int id, AddLaborInput request)
        {
            var response = await _http.PostAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/labor", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateItemAsync(int id, int itemId, UpdateServiceItemDTO request)
        {
            var response = await _http.PutAsJsonAsync($"{_apiUrl}/api/serviceorders/{id}/items/{itemId}", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteItemAsync(int id, int itemId)
        {
            var response = await _http.DeleteAsync($"{_apiUrl}/api/serviceorders/{id}/items/{itemId}");
            return response.IsSuccessStatusCode;
        }
    }
}
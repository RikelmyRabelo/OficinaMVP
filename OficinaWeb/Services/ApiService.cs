using System.Net.Http.Json;

namespace OficinaWeb.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public ApiService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }
        private string GetBaseUrl()
        {
            var url = _config["ApiBaseUrl"] ?? "https://localhost:7082/";
            return url.EndsWith("/") ? url : url + "/";
        }
        private string GetCleanEndpoint(string endpoint)
        {
            return endpoint.StartsWith("/") ? endpoint.Substring(1) : endpoint;
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            return await _http.GetFromJsonAsync<T>(GetBaseUrl() + GetCleanEndpoint(endpoint));
        }

        public async Task<HttpResponseMessage> PostAsync(string endpoint)
        {
            return await _http.PostAsync(GetBaseUrl() + GetCleanEndpoint(endpoint), null);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
        {
            return await _http.PostAsJsonAsync(GetBaseUrl() + GetCleanEndpoint(endpoint), data);
        }

        public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
        {
            return await _http.PutAsJsonAsync(GetBaseUrl() + GetCleanEndpoint(endpoint), data);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string endpoint)
        {
            return await _http.DeleteAsync(GetBaseUrl() + GetCleanEndpoint(endpoint));
        }
    }
}
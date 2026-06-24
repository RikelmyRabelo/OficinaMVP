using Microsoft.AspNetCore.Http;

namespace OficinaAPI.Services
{
    public interface IStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string folderName);
        void DeleteFile(string fileUrl);
    }
}
using System.Net.Http.Json;

public class BackupService
{
    private readonly HttpClient _http;

    public BackupService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Sucesso, string Mensagem)> DispararBackup()
    {
        try
        {
            // Substitua pela porta correta da sua API que aparece no Visual Studio
            var response = await _http.PostAsync("api/Backup/gerar", null);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Backup concluído e pronto para sincronizar com o Drive!");
            }
            return (false, "Erro ao gerar backup no servidor.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro de conexão: {ex.Message}");
        }
    }
}
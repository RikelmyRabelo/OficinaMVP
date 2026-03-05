namespace OficinaAPI.Models
{
    public class CashTransaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string Description { get; set; } = ""; // Ex: "Retirada para os donos"
        public decimal Amount { get; set; } // Negativo para saída, positivo para ajuste
    }
}   
namespace OficinaAPI.Models
{
    public class RevenueAdjustment
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
        public DateTime Date { get; set; }
    }
}
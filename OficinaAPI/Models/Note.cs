using System;
using System.ComponentModel.DataAnnotations;

namespace OficinaAPI.Models
{
    public class Note
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Guncho.Shared.Models
{
    public class PlayerDto
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public bool IsAdmin { get; set; }
        
        public bool IsGuest { get; set; }
        
        public Dictionary<string, string> Attributes { get; set; } = new();
    }
    
    public class PlayerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}

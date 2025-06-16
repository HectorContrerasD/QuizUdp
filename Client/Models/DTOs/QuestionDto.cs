using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Models.DTOs
{
    public class QuestionDto
    {
        public string? Question { get; set; } 
        public string[] Options { get; set; } 
    }
}

using Server.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Models
{
    public class QuestionModel
    {
        public string Question { get; set; } 
        public string[] Options { get; set; } 
        public string? CorrectAnswer { get; set; } 
    }
}

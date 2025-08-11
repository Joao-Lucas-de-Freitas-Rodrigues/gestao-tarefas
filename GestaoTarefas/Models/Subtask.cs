using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestaoTarefas.Models
{
    public class Subtask
    {
        public int Id { get; set; }

        public int TaskItemId { get; set; } 
        [ValidateNever]                      
        public TaskItem? TaskItem { get; set; } 

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }
}

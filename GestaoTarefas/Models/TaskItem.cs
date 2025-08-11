using System.ComponentModel.DataAnnotations;
using GestaoTarefas.Models.Enums;

namespace GestaoTarefas.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        public TodoStatus Status { get; set; } = TodoStatus.Aberto;
        public TodoPriority Priority { get; set; } = TodoPriority.Normal;

        [DataType(DataType.Date)]
        public DateTime? DueDateUtc { get; set; }

        public bool IsDeleted { get; set; } = false; 
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
    }
}

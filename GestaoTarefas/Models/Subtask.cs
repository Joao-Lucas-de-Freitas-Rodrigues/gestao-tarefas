using System.ComponentModel.DataAnnotations;

namespace GestaoTarefas.Models
{
    public class Subtask
    {
        public int Id { get; set; }

        public int TaskItemId { get; set; }
        public TaskItem TaskItem { get; set; } = null!;

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }
}

using GestaoTarefas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoTarefas.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Subtask> Subtasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("Tasks");
                entity.HasQueryFilter(t => !t.IsDeleted);
                entity.Property(t => t.Title).IsRequired().HasMaxLength(120);
                entity.HasOne(t => t.Category)
                      .WithMany(c => c.Tasks)
                      .HasForeignKey(t => t.CategoryId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(80);
                entity.HasIndex(c => c.Name).IsUnique();
            });

            modelBuilder.Entity<Subtask>(entity =>
            {
                entity.Property(s => s.Title).IsRequired().HasMaxLength(120);
                entity.HasOne(s => s.TaskItem)
                      .WithMany(t => t.Subtasks)
                      .HasForeignKey(s => s.TaskItemId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(s => !s.TaskItem.IsDeleted);
            });
        }
    }
}

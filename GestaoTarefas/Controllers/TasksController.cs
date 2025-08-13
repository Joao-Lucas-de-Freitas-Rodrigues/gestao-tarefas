using GestaoTarefas.Data;
using GestaoTarefas.Models;
using GestaoTarefas.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GestaoTarefas.Controllers
{
    public class TasksController : Controller
    {
        private readonly AppDbContext _context;

        public TasksController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Tasks
        public async Task<IActionResult> Index(int? categoryId, TodoStatus? status, TodoPriority? label, string sort = "recent")
        {
            var q = _context.Tasks
                .Where(t => !t.IsDeleted)
                .Include(t => t.Category)
                .Include(t => t.Subtasks)
                .OrderByDescending(t => t.CreatedAtUtc)
                .AsQueryable();

            if (categoryId.HasValue) q = q.Where(t => t.CategoryId == categoryId);
            if (status.HasValue) q = q.Where(t => t.Status == status);
            if (label.HasValue) q = q.Where(t => t.Priority == label);

            q = sort switch
            {
                "activity" => q.OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc),
                "due" => q.OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue),
                _ => q.OrderByDescending(t => t.CreatedAtUtc),
            };

            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
            ViewBag.Status = status; ViewBag.Label = label; ViewBag.Sort = sort;

            return View(await q.ToListAsync());
        }

        // GET: /Tasks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var taskItem = await _context.Tasks
                .Include(t => t.Category)
                .Include(t => t.Subtasks.OrderBy(s => s.SortOrder))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (taskItem is null) return NotFound();
            return View(taskItem);
        }

        // GET: /Tasks/Create
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            var model = new TaskItem { DueDateUtc = DateTime.UtcNow.Date.AddDays(1) };
            return View(model);
        }

        // POST: /Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryId,Title,Description,Status,Priority,DueDateUtc,Subtasks")] TaskItem taskItem)
        {
            // regra: vencimento não pode ser no passado
            if (taskItem.DueDateUtc is DateTime d && d.Date < DateTime.UtcNow.Date)
                ModelState.AddModelError(nameof(taskItem.DueDateUtc), "A data de vencimento não pode ser no passado.");

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", taskItem.CategoryId);
                return View(taskItem);
            }

            taskItem.Subtasks = (taskItem.Subtasks ?? new List<Subtask>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                .Select((s, i) => { s.SortOrder = i; return s; })
                .ToList();

            taskItem.CreatedAtUtc = DateTime.UtcNow;
            taskItem.UpdatedAtUtc = DateTime.UtcNow;

            _context.Add(taskItem);
            await _context.SaveChangesAsync();

            TempData["Ok"] = "Tarefa criada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Tasks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();

            var taskItem = await _context.Tasks
                .Include(t => t.Subtasks.OrderBy(s => s.SortOrder))
                .FirstOrDefaultAsync(t => t.Id == id);

            if (taskItem is null) return NotFound();

            if (taskItem.Status == TodoStatus.Feito)
                return BadRequest("Tarefa concluída não pode ser editada.");

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", taskItem.CategoryId);
            return View(taskItem);
        }

        // POST: /Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,CategoryId,Title,Description,Status,Priority,DueDateUtc,Subtasks")]
            TaskItem form)
        {
            if (id != form.Id) return NotFound();

            if (form.DueDateUtc is DateTime d && d.Date < DateTime.UtcNow.Date)
                ModelState.AddModelError(nameof(form.DueDateUtc), "A data de vencimento não pode ser no passado.");
            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", form.CategoryId);
                return View(form);
            }

            var db = await _context.Tasks
                .Include(t => t.Subtasks)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (db is null) return NotFound();

            // bloqueio de edição se concluída
            if (db.Status == TodoStatus.Feito)
                return BadRequest("Tarefa concluída não pode ser editada.");

            db.Title = form.Title;
            db.Description = form.Description;
            db.Status = form.Status;
            db.Priority = form.Priority;
            db.CategoryId = form.CategoryId;
            db.DueDateUtc = form.DueDateUtc;
            db.UpdatedAtUtc = DateTime.UtcNow;

            var incoming = (form.Subtasks ?? new System.Collections.Generic.List<Subtask>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                .Select((s, i) => { s.SortOrder = i; return s; })
                .ToList();

            var existingById = db.Subtasks.ToDictionary(s => s.Id);
            var touched = new System.Collections.Generic.HashSet<int>();

            foreach (var s in incoming.Where(s => s.Id > 0))
            {
                if (existingById.TryGetValue(s.Id, out var hit))
                {
                    hit.Title = s.Title;
                    hit.IsCompleted = s.IsCompleted;
                    hit.SortOrder = s.SortOrder;
                    touched.Add(s.Id);
                }
            }

            var toRemove = db.Subtasks.Where(s => !touched.Contains(s.Id)).ToList();
            foreach (var rem in toRemove)
                _context.Subtasks.Remove(rem);

            foreach (var s in incoming.Where(s => s.Id == 0))
            {
                db.Subtasks.Add(new Subtask
                {
                    Title = s.Title,
                    IsCompleted = s.IsCompleted,
                    SortOrder = s.SortOrder,
                    TaskItemId = db.Id
                });
            }

            await _context.SaveChangesAsync();
            TempData["Ok"] = "Tarefa atualizada.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Tasks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var taskItem = await _context.Tasks
                .Include(t => t.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (taskItem is null) return NotFound();

            return View(taskItem);
        }

        // POST: /Tasks/Delete/5  (soft delete)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var taskItem = await _context.Tasks.FindAsync(id);
            if (taskItem is not null)
            {
                taskItem.IsDeleted = true;
                taskItem.DeletedAtUtc = DateTime.UtcNow;
                _context.Update(taskItem);
                await _context.SaveChangesAsync();
                TempData["Ok"] = "Tarefa removida.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSub(int id)
        {
            var sub = await _context.Subtasks.FindAsync(id);
            if (sub is null || sub.TaskItem is null || sub.TaskItem.IsDeleted)
            {
                return NotFound();
            }

            sub.IsCompleted = !sub.IsCompleted;
            sub.TaskItem.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = sub.TaskItemId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task is null || task.IsDeleted) return NotFound();
            if (task.Status != TodoStatus.Feito)
            {
                task.Status = TodoStatus.Andamento;
                task.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetOpen(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task is null || task.IsDeleted) return NotFound();
            if (task.Status != TodoStatus.Feito)
            {
                task.Status = TodoStatus.Aberto;
                task.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TaskItemExists(int id) =>
            _context.Tasks.Any(e => e.Id == id);
    }
}

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
        public async Task<IActionResult> Index()
        {
            var query = _context.Tasks
                                .Include(t => t.Category)
                                .OrderByDescending(t => t.CreatedAtUtc);

            return View(await query.ToListAsync());
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
        TaskItem taskItem)
        {
            if (id != taskItem.Id) return NotFound();

            if (taskItem.DueDateUtc is DateTime d && d.Date < DateTime.UtcNow.Date)
                ModelState.AddModelError(nameof(taskItem.DueDateUtc), "A data de vencimento não pode ser no passado.");
            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", taskItem.CategoryId);
                return View(taskItem);
            }

            // carrega do banco com subtarefas
            var db = await _context.Tasks
                .Include(t => t.Subtasks)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (db is null) return NotFound();

            if (db.Status == TodoStatus.Feito)
                return BadRequest("Tarefa concluída não pode ser editada.");

            db.Title = taskItem.Title;
            db.Description = taskItem.Description;
            db.Status = taskItem.Status;
            db.Priority = taskItem.Priority;
            db.CategoryId = taskItem.CategoryId;
            db.DueDateUtc = taskItem.DueDateUtc;
            db.UpdatedAtUtc = DateTime.UtcNow;

            var incoming = (taskItem.Subtasks ?? new System.Collections.Generic.List<Subtask>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                .Select((s, i) => { s.SortOrder = i; return s; })
                .ToList();

            // atualizar/criar
            foreach (var s in incoming)
            {
                if (s.Id == 0)
                {
                    db.Subtasks.Add(new Subtask
                    {
                        Title = s.Title,
                        IsCompleted = s.IsCompleted,
                        SortOrder = s.SortOrder
                    });
                }
                else
                {
                    var hit = db.Subtasks.FirstOrDefault(x => x.Id == s.Id);
                    if (hit != null)
                    {
                        hit.Title = s.Title;
                        hit.IsCompleted = s.IsCompleted;
                        hit.SortOrder = s.SortOrder;
                    }
                }
            }

            // remover as que saíram do form
            var keepIds = incoming.Where(s => s.Id != 0).Select(s => s.Id).ToHashSet();
            var toRemove = db.Subtasks.Where(s => !keepIds.Contains(s.Id)).ToList();
            foreach (var rem in toRemove) _context.Subtasks.Remove(rem);

            await _context.SaveChangesAsync();
            TempData["Ok"] = "Tarefa atualizada.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSub(int id)
        {
            var sub = await _context.Subtasks.FindAsync(id);
            if (sub is null) return NotFound();
            sub.IsCompleted = !sub.IsCompleted;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = sub.TaskItemId });
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


        private bool TaskItemExists(int id) =>
            _context.Tasks.Any(e => e.Id == id);
    }
}

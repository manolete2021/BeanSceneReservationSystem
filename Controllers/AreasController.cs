using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Controllers;

[Authorize(Roles = "Manager")]
public class AreasController : Controller
{
    private readonly ApplicationDbContext _context;

    public AreasController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Areas.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var area = await _context.Areas
            .FirstOrDefaultAsync(a => a.AreaID == id);

        if (area == null)
        {
            return NotFound();
        }

        return View(area);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("AreaName")] Area area)
    {
        ValidateAreaName(area);

        if (ModelState.IsValid)
        {
            _context.Add(area);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(area);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var area = await _context.Areas.FindAsync(id);

        if (area == null)
        {
            return NotFound();
        }

        return View(area);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("AreaID,AreaName")] Area area)
    {
        if (id != area.AreaID)
        {
            return NotFound();
        }

        ValidateAreaName(area);

        if (ModelState.IsValid)
        {
            // Reattach the detached entity before saving the update.
            _context.Attach(area);
            _context.Entry(area).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(area);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var area = await _context.Areas
            .FirstOrDefaultAsync(a => a.AreaID == id);

        if (area == null)
        {
            return NotFound();
        }

        return View(area);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var area = await _context.Areas.FindAsync(id);

        if (area != null)
        {
            _context.Areas.Remove(area);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private void ValidateAreaName(Area area)
    {
        if (string.IsNullOrWhiteSpace(area.AreaName))
        {
            ModelState.AddModelError(nameof(area.AreaName), "Area Name is required.");
        }
    }
}

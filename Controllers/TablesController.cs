using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Controllers;

[Authorize(Roles = "Manager")]
public class TablesController : Controller
{
    private readonly ApplicationDbContext _context;

    public TablesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var tables = await _context.Tables.ToListAsync();
        var areas = await _context.Areas.ToListAsync();
        var areaNames = areas.ToDictionary(a => a.AreaID, a => a.AreaName);

        ViewBag.AreaNames = areaNames;

        // Display order is Main, Outside, then Balcony.
        // Table numbers are sorted numerically so M10 follows M9.
        var areaOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Main"] = 1,
            ["Outside"] = 2,
            ["Balcony"] = 3
        };

        tables = tables
            .OrderBy(table => GetAreaOrder(table.AreaID))
            .ThenBy(table => GetTableSequence(table.TableNumber))
            .ToList();

        return View(tables);

        int GetAreaOrder(int areaId)
        {
            if (areaNames.TryGetValue(areaId, out var areaName) &&
                areaOrder.TryGetValue(areaName, out var order))
            {
                return order;
            }

            return int.MaxValue;
        }

        static int GetTableSequence(string tableNumber)
        {
            var digits = new string(tableNumber.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var number) ? number : int.MaxValue;
        }
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.TableID == id);

        if (table == null)
        {
            return NotFound();
        }

        ViewBag.AreaName = await GetAreaNameAsync(table.AreaID);

        return View(table);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAreasDropDownList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("AreaID,Capacity,IsAvailable")] Table table)
    {
        await ValidateTableAsync(table);

        if (ModelState.IsValid)
        {
            var area = await _context.Areas.FirstAsync(a => a.AreaID == table.AreaID);
            // Use the correct table prefix for the selected area.
            var prefix = GetTablePrefix(area.AreaName);

            if (prefix == null)
            {
                ModelState.AddModelError(nameof(table.AreaID), "Please select Main, Outside, or Balcony.");
            }
            else
            {
                var nextTableNumber = await GenerateNextTableNumberAsync(table.AreaID, prefix);
                if (nextTableNumber == null)
                {
                    ModelState.AddModelError(string.Empty, "This area already has the maximum of 10 tables.");
                }
                else
                {
                    table.TableNumber = nextTableNumber;
                    if (await _context.Tables.AnyAsync(t => t.TableNumber == table.TableNumber))
                    {
                        ModelState.AddModelError(string.Empty, "A table with this Table Number already exists.");
                    }
                }
            }
        }

        if (ModelState.IsValid)
        {
            _context.Add(table);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await PopulateAreasDropDownList(table.AreaID);
        return View(table);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var table = await _context.Tables.FindAsync(id);

        if (table == null)
        {
            return NotFound();
        }

        await PopulateAreasDropDownList(table.AreaID);
        return View(table);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("TableID,AreaID,Capacity,IsAvailable")] Table table)
    {
        if (id != table.TableID)
        {
            return NotFound();
        }

        var existingTable = await _context.Tables.FindAsync(id);

        if (existingTable == null)
        {
            return NotFound();
        }

        await ValidateTableAsync(table);

        if (ModelState.IsValid)
        {
            existingTable.AreaID = table.AreaID;
            existingTable.Capacity = table.Capacity;
            existingTable.IsAvailable = table.IsAvailable;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        table.TableNumber = existingTable.TableNumber;
        await PopulateAreasDropDownList(table.AreaID);
        return View(table);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.TableID == id);

        if (table == null)
        {
            return NotFound();
        }

        ViewBag.AreaName = await GetAreaNameAsync(table.AreaID);

        return View(table);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var table = await _context.Tables.FindAsync(id);

        if (table != null)
        {
            _context.Tables.Remove(table);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAreasDropDownList(int? selectedAreaId = null)
    {
        var areas = await _context.Areas
            .OrderBy(a => a.AreaName)
            .ToListAsync();

        ViewBag.Areas = new SelectList(areas, "AreaID", "AreaName", selectedAreaId);
    }

    private async Task<string> GetAreaNameAsync(int areaId)
    {
        var area = await _context.Areas.FirstOrDefaultAsync(a => a.AreaID == areaId);
        return area?.AreaName ?? "";
    }

    private async Task ValidateTableAsync(Table table)
    {
        if (table.AreaID <= 0 || !await _context.Areas.AnyAsync(a => a.AreaID == table.AreaID))
        {
            ModelState.AddModelError(nameof(table.AreaID), "Please select an Area.");
        }

        if (table.Capacity <= 0)
        {
            ModelState.AddModelError(nameof(table.Capacity), "Capacity must be greater than 0.");
        }
    }

    // Main uses M, Outside uses O, and Balcony uses B.
    private static string? GetTablePrefix(string areaName)
    {
        return areaName switch
        {
            "Main" => "M",
            "Outside" => "O",
            "Balcony" => "B",
            _ => null
        };
    }

    private async Task<string?> GenerateNextTableNumberAsync(int areaId, string prefix)
    {
        var tableNumbers = await _context.Tables
            .Where(t => t.AreaID == areaId)
            .Select(t => t.TableNumber)
            .ToListAsync();

        // Each restaurant area can contain a maximum of 10 tables.
        if (tableNumbers.Count >= 10)
        {
            return null;
        }

        var usedNumbers = new HashSet<int>();
        foreach (var tableNumber in tableNumbers)
        {
            if (tableNumber.Length > prefix.Length &&
                tableNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(tableNumber[prefix.Length..], out var number))
            {
                usedNumbers.Add(number);
            }
        }

        // Use the first free number for this area, such as M1 or O3.
        for (var number = 1; number <= 10; number++)
        {
            if (!usedNumbers.Contains(number))
            {
                return prefix + number;
            }
        }

        return null;
    }
}

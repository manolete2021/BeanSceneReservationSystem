using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Models;
using BeanSceneReservationSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Controllers;

[Authorize(Roles = "Manager")]
public class SittingsController : Controller
{
    private const string SittingHasReservationsMessage =
        "This sitting cannot be deleted because it has existing reservations.";

    // Breakfast, Lunch, and Dinner stay the standard sitting types.
    private static readonly string[] ValidTypes = { "Breakfast", "Lunch", "Dinner" };
    private const string OtherSittingTypeOption = "Other / Special Event...";

    private readonly ApplicationDbContext _context;

    public SittingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Sittings.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var sitting = await _context.Sittings
            .FirstOrDefaultAsync(s => s.SittingID == id);

        if (sitting == null)
        {
            return NotFound();
        }

        return View(sitting);
    }

    public IActionResult Create()
    {
        PopulateTimeOptions();
        return View(new SittingViewModel
        {
            Date = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SittingViewModel model)
    {
        var startDateTime = CombineDateAndTime(model.Date, model.StartTime);
        var endDateTime = CombineDateAndTime(model.Date, model.EndTime);

        if (model.SittingType == OtherSittingTypeOption)
        {
            model.CustomSittingType = model.CustomSittingType?.Trim() ?? string.Empty;
        }

        await ValidateSittingViewModelAsync(
            model,
            startDateTime,
            endDateTime,
            existingSittingId: null,
            allowCustomSittingType: true);

        if (ModelState.IsValid)
        {
            var sittingType = model.SittingType == OtherSittingTypeOption
                ? model.CustomSittingType?.Trim() ?? string.Empty
                : model.SittingType;

            var sitting = new Sitting
            {
                SittingType = sittingType,
                StartTime = startDateTime,
                EndTime = endDateTime,
                Capacity = model.Capacity,
                IsClosed = model.IsClosed
            };

            _context.Add(sitting);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        PopulateTimeOptions();
        return View(model);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var sitting = await _context.Sittings.FindAsync(id);

        if (sitting == null)
        {
            return NotFound();
        }

        PopulateTimeOptions();
        return View(ToViewModel(sitting));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SittingViewModel model)
    {
        if (id != model.SittingID)
        {
            return NotFound();
        }

        var sitting = await _context.Sittings.FindAsync(id);

        if (sitting == null)
        {
            return NotFound();
        }

        var startDateTime = CombineDateAndTime(model.Date, model.StartTime);
        var endDateTime = CombineDateAndTime(model.Date, model.EndTime);

        await ValidateSittingViewModelAsync(model, startDateTime, endDateTime, existingSittingId: id);

        if (ModelState.IsValid)
        {
            sitting.SittingType = model.SittingType;
            sitting.StartTime = startDateTime;
            sitting.EndTime = endDateTime;
            sitting.Capacity = model.Capacity;
            sitting.IsClosed = model.IsClosed;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        PopulateTimeOptions();
        return View(model);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var sitting = await _context.Sittings
            .FirstOrDefaultAsync(s => s.SittingID == id);

        if (sitting == null)
        {
            return NotFound();
        }

        return await DeleteViewAsync(sitting);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var sitting = await _context.Sittings.FindAsync(id);

        if (sitting == null)
        {
            return NotFound();
        }

        // Do not delete a sitting that already has reservations.
        if (await SittingHasReservationsAsync(id))
        {
            return await DeleteViewAsync(sitting);
        }

        _context.Sittings.Remove(sitting);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private Task<bool> SittingHasReservationsAsync(int sittingId)
    {
        return _context.Reservations.AnyAsync(r => r.SittingID == sittingId);
    }

    private async Task<IActionResult> DeleteViewAsync(Sitting sitting)
    {
        if (await SittingHasReservationsAsync(sitting.SittingID))
        {
            ViewBag.CannotDelete = true;
            ViewBag.DeleteError = SittingHasReservationsMessage;
        }

        return View("Delete", sitting);
    }

    private static SittingViewModel ToViewModel(Sitting sitting)
    {
        return new SittingViewModel
        {
            SittingID = sitting.SittingID,
            SittingType = sitting.SittingType,
            Date = sitting.StartTime.Date,
            StartTime = sitting.StartTime.TimeOfDay,
            EndTime = sitting.EndTime.TimeOfDay,
            Capacity = sitting.Capacity,
            IsClosed = sitting.IsClosed
        };
    }

    private static DateTime CombineDateAndTime(DateTime date, TimeSpan time)
    {
        return date.Date.Add(time);
    }

    private static bool IsHalfHourInterval(TimeSpan time)
    {
        return time.TotalMinutes >= 0 &&
               time.TotalMinutes < 24 * 60 &&
               time.TotalMinutes % 30 == 0 &&
               time.Seconds == 0 &&
               time.Milliseconds == 0;
    }

    private static List<SelectListItem> GetTimeOptions()
    {
        var times = new List<SelectListItem>();
        var current = DateTime.Today;

        while (current < DateTime.Today.AddDays(1))
        {
            times.Add(new SelectListItem
            {
                Value = current.ToString("HH:mm"),
                Text = current.ToString("h:mm tt")
            });

            current = current.AddMinutes(30);
        }

        return times;
    }

    private void PopulateTimeOptions()
    {
        ViewBag.TimeOptions = GetTimeOptions();
    }

    private async Task ValidateSittingViewModelAsync(
        SittingViewModel model,
        DateTime startDateTime,
        DateTime endDateTime,
        int? existingSittingId,
        bool allowCustomSittingType = false)
    {
        ModelState.Remove(nameof(model.SittingType));

        var usingCustomOption = allowCustomSittingType && model.SittingType == OtherSittingTypeOption;
        var sittingType = usingCustomOption ? model.CustomSittingType : model.SittingType;
        var hasSittingType = usingCustomOption
            ? !string.IsNullOrWhiteSpace(sittingType)
            : ValidTypes.Contains(model.SittingType);

        if (usingCustomOption && !hasSittingType)
        {
            ModelState.Remove(nameof(model.CustomSittingType));
            ModelState.AddModelError(nameof(model.CustomSittingType), "Please enter a custom sitting type.");
        }
        else if (!usingCustomOption && !hasSittingType)
        {
            ModelState.AddModelError(nameof(model.SittingType), "Please select a Sitting Type.");
        }

        if (model.Date == default || model.Date == DateTime.MinValue || model.Date.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.Date), "Please select a valid date.");
        }

        // The sitting must end after it starts.
        if (endDateTime <= startDateTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "End Time must be later than Start Time.");
        }

        // Start and end times must use 30-minute intervals.
        if (!IsHalfHourInterval(model.StartTime))
        {
            ModelState.AddModelError(nameof(model.StartTime), "Please select a valid start time.");
        }

        if (!IsHalfHourInterval(model.EndTime))
        {
            ModelState.AddModelError(nameof(model.EndTime), "Please select a valid end time.");
        }

        // A sitting needs a guest capacity greater than zero.
        if (model.Capacity <= 0)
        {
            ModelState.AddModelError(nameof(model.Capacity), "Capacity must be greater than 0.");
        }

        if (hasSittingType &&
            model.Date.Date >= DateTime.Today &&
            endDateTime > startDateTime)
        {
            var dayStart = model.Date.Date;
            var dayEnd = dayStart.AddDays(1);

            var conflictExists = await _context.Sittings.AnyAsync(s =>
                s.SittingType == sittingType &&
                s.StartTime >= dayStart &&
                s.StartTime < dayEnd &&
                s.StartTime < endDateTime &&
                startDateTime < s.EndTime &&
                (!existingSittingId.HasValue || s.SittingID != existingSittingId.Value));

            if (conflictExists)
            {
                ModelState.AddModelError(string.Empty, "A similar Sitting already exists for this date and time.");
            }
        }
    }
}

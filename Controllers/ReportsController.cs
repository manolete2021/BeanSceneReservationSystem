using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Controllers;

// Reports are available only to Manager users.
[Authorize(Roles = "Manager")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        var model = new ReservationReportViewModel
        {
            FromDate = fromDate?.Date,
            ToDate = toDate?.Date
        };

        if (model.FromDate.HasValue && model.ToDate.HasValue && model.FromDate.Value > model.ToDate.Value)
        {
            ModelState.AddModelError(string.Empty, "From Date must not be after To Date.");
            return View(model);
        }

        var query = _context.Reservations.AsNoTracking();

        // Optional dates limit the report. The end date includes the whole day.
        if (model.FromDate.HasValue)
        {
            var from = model.FromDate.Value;
            query = query.Where(r => r.StartTime >= from);
        }

        if (model.ToDate.HasValue)
        {
            var endExclusive = model.ToDate.Value.AddDays(1);
            query = query.Where(r => r.StartTime < endExclusive);
        }

        var statusCounts = await query
            .GroupBy(r => r.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Guests = g.Sum(r => r.NumberOfGuests)
            })
            .ToListAsync();

        // Count reservations and guests, then split the counts by status.
        model.TotalReservations = statusCounts.Sum(s => s.Count);
        model.TotalGuests = statusCounts.Sum(s => s.Guests);
        model.PendingCount = statusCounts.FirstOrDefault(s => s.Status == "Pending")?.Count ?? 0;
        model.ConfirmedCount = statusCounts.FirstOrDefault(s => s.Status == "Confirmed")?.Count ?? 0;
        model.SeatedCount = statusCounts.FirstOrDefault(s => s.Status == "Seated")?.Count ?? 0;
        model.CompletedCount = statusCounts.FirstOrDefault(s => s.Status == "Completed")?.Count ?? 0;
        model.CancelledCount = statusCounts.FirstOrDefault(s => s.Status == "Cancelled")?.Count ?? 0;

        var reservations = await query
            .Include(r => r.CustomerMember)
            .OrderBy(r => r.StartTime)
            .ThenBy(r => r.ReservationID)
            .ToListAsync();

        model.Reservations = reservations.Select(r => new ReservationReportRow
        {
            ReservationID = r.ReservationID,
            Customer = r.CustomerMember == null
                ? string.Empty
                : $"{r.CustomerMember.FirstName} {r.CustomerMember.LastName}",
            StartTime = r.StartTime,
            NumberOfGuests = r.NumberOfGuests,
            Status = r.Status
        }).ToList();

        return View(model);
    }
}

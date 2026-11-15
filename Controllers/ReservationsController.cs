using System.Security.Claims;
using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Interfaces;
using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Controllers;

// Signed-in users can open reservations. Extra role checks protect each action.
[Authorize]
public class ReservationsController : Controller
{
    private static readonly string[] ValidStatuses =
    {
        "Pending",
        "Confirmed",
        "Seated",
        "Completed",
        "Cancelled"
    };

    // Only these statuses use sitting capacity. Cancelled and Completed do not.
    private static readonly string[] ActiveCapacityStatuses =
    {
        "Pending",
        "Confirmed",
        "Seated"
    };

    private static readonly string[] ValidSources =
    {
        "Online",
        "Mobile",
        "In-person",
        "Email",
        "Phone"
    };

    // Allowed status changes. A reservation cannot skip a step.
    private static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["Pending"] = ["Confirmed", "Cancelled"],
        ["Confirmed"] = ["Seated", "Cancelled"],
        ["Seated"] = ["Completed"],
        ["Completed"] = [],
        ["Cancelled"] = []
    };

    private readonly ApplicationDbContext _context;
    private readonly IReservationService _reservationService;

    public ReservationsController(ApplicationDbContext context, IReservationService reservationService)
    {
        _context = context;
        _reservationService = reservationService;
    }

    public async Task<IActionResult> Index(string? customerName, DateTime? reservationDate, string? status)
    {
        customerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        if (string.IsNullOrWhiteSpace(status) || !ValidStatuses.Contains(status))
        {
            status = null;
        }

        ViewBag.CustomerName = customerName;
        ViewBag.ReservationDate = reservationDate?.ToString("yyyy-MM-dd");
        ViewBag.SelectedStatus = status;

        // Members can search only their own reservations.
        int? ownerCustomerId = null;
        if (!CanManageReservations())
        {
            var customer = await GetLinkedCustomerAsync();
            if (customer == null)
            {
                return View(new List<Reservation>());
            }

            ownerCustomerId = customer.CustomerID;
        }

        var reservations = await _reservationService.SearchAsync(
            customerName,
            reservationDate,
            status,
            ownerCustomerId);

        return View(reservations);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationID == id);

        if (reservation == null)
        {
            return NotFound();
        }

        // A Member cannot open another customer's reservation.
        if (!await MemberOwnsReservationAsync(reservation))
        {
            return Forbid();
        }

        await PopulateReservationDetailsAsync(reservation);
        return View(reservation);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTables(int id, int[]? tableIds)
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationID == id);

        if (reservation == null)
        {
            return NotFound();
        }

        var selectedTableIds = (tableIds ?? Array.Empty<int>())
            .Where(tableId => tableId > 0)
            .Distinct()
            .ToList();

        if (selectedTableIds.Count > 0)
        {
            var existingTableIds = await _context.Tables
                .Where(t => selectedTableIds.Contains(t.TableID))
                .Select(t => t.TableID)
                .ToListAsync();

            if (existingTableIds.Count != selectedTableIds.Count)
            {
                ModelState.AddModelError(string.Empty, "One or more selected tables do not exist.");
                return await ReservationDetailsViewAsync(reservation);
            }

            // Stop if a selected table is already booked at this time.
            var conflictMessage = await FindTableConflictMessageAsync(reservation, selectedTableIds);
            if (conflictMessage != null)
            {
                ModelState.AddModelError(string.Empty, conflictMessage);
                return await ReservationDetailsViewAsync(reservation);
            }
        }

        // Start a transaction so all table changes succeed or fail together.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var existingLinks = await _context.ReservationTables
                .Where(rt => rt.ReservationID == reservation.ReservationID)
                .ToListAsync();

            _context.ReservationTables.RemoveRange(existingLinks);
            await _context.SaveChangesAsync();

            foreach (var tableId in selectedTableIds)
            {
                _context.ReservationTables.Add(new ReservationTable
                {
                    ReservationID = reservation.ReservationID,
                    TableID = tableId
                });
            }

            // Save all table allocation changes.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            // Roll back the transaction if an error occurs.
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "The tables could not be saved. Please try again.");
            return await ReservationDetailsViewAsync(reservation);
        }

        return RedirectToAction(nameof(Details), new { id = reservation.ReservationID });
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
        {
            return NotFound();
        }

        // Only the next allowed status can be saved.
        if (!IsValidTransition(reservation.Status, newStatus))
        {
            ModelState.AddModelError(string.Empty, "This status change is not allowed.");
            await PopulateReservationDetailsAsync(reservation);
            return View("Details", reservation);
        }

        reservation.Status = newStatus;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Create()
    {
        int? customerId = null;

        if (!CanManageReservations())
        {
            var customer = await GetLinkedCustomerAsync();
            if (customer == null)
            {
                return Forbid();
            }

            customerId = customer.CustomerID;
            ShowLockedCustomer(customer);
        }

        await PopulateDropDowns(customerId, isCreate: true);

        return View(new ReservationViewModel
        {
            Status = "Pending",
            NumberOfGuests = 1,
            CustomerID = customerId ?? 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationViewModel model)
    {
        if (!CanManageReservations())
        {
            var customer = await GetLinkedCustomerAsync();
            if (customer == null)
            {
                return Forbid();
            }

            // A Member cannot choose another customer.
            model.CustomerID = customer.CustomerID;
            ShowLockedCustomer(customer);
        }

        // A new reservation always starts as Pending.
        model.Status = "Pending";
        model.Notes = model.Notes ?? string.Empty;
        model.Source = model.Source?.Trim() ?? string.Empty;

        var sitting = await LoadSittingAsync(model.SittingID);
        var startTime = BuildStartTime(sitting, model.ReservationTime);

        await ValidateReservationViewModelAsync(model, sitting, startTime, isCreate: true);

        if (ModelState.IsValid && startTime.HasValue)
        {
            var reservation = new Reservation
            {
                CustomerID = model.CustomerID,
                SittingID = model.SittingID,
                StartTime = startTime.Value,
                Duration = model.Duration,
                NumberOfGuests = model.NumberOfGuests,
                Source = model.Source,
                Notes = model.Notes,
                Status = "Pending"
            };

            _context.Add(reservation);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropDowns(model.CustomerID, model.SittingID, model.Status, model.ReservationTime, model.Duration, isCreate: true);
        return View(model);
    }

    [Authorize(Roles = "Manager,Staff")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
        {
            return NotFound();
        }

        var model = ToViewModel(reservation);
        await PopulateDropDowns(model.CustomerID, model.SittingID, model.Status, model.ReservationTime, model.Duration, isCreate: false);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ReservationViewModel model)
    {
        if (id != model.ReservationID)
        {
            return NotFound();
        }

        model.Notes = model.Notes ?? string.Empty;
        model.Source = model.Source?.Trim() ?? string.Empty;

        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
        {
            return NotFound();
        }

        model.Status = reservation.Status;

        var sitting = await LoadSittingAsync(model.SittingID);
        var startTime = BuildStartTime(sitting, model.ReservationTime);

        await ValidateReservationViewModelAsync(model, sitting, startTime, isCreate: false);

        if (ModelState.IsValid && startTime.HasValue)
        {
            reservation.CustomerID = model.CustomerID;
            reservation.SittingID = model.SittingID;
            reservation.StartTime = startTime.Value;
            reservation.Duration = model.Duration;
            reservation.NumberOfGuests = model.NumberOfGuests;
            reservation.Source = model.Source;
            reservation.Notes = model.Notes;
            // Detect if another user changed the reservation first.
            _context.Entry(reservation).Property(r => r.RowVersion).OriginalValue = model.RowVersion;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                var databaseReservation = await _context.Reservations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReservationID == id);

                if (databaseReservation == null)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, "The reservation was changed by another user. Please reload and try again.");
                var current = ToViewModel(databaseReservation);
                await PopulateDropDowns(current.CustomerID, current.SittingID, current.Status, current.ReservationTime, current.Duration, isCreate: false);
                return View(current);
            }

            return RedirectToAction(nameof(Index));
        }

        await PopulateDropDowns(model.CustomerID, model.SittingID, model.Status, model.ReservationTime, model.Duration, isCreate: false);
        return View(model);
    }

    // Only a Manager can physically delete a reservation.
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationID == id);

        if (reservation == null)
        {
            return NotFound();
        }

        await PopulateReservationDetailsAsync(reservation);
        return View(reservation);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation != null)
        {
            _context.Reservations.Remove(reservation);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // Physical delete by HTTP DELETE. Only a Manager can use this.
    [HttpDelete("/Reservations/DeleteApi/{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteApi(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
        {
            return NotFound();
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static bool IsValidTransition(string currentStatus, string newStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowedStatuses) &&
               allowedStatuses.Contains(newStatus);
    }

    private static ReservationViewModel ToViewModel(Reservation reservation)
    {
        return new ReservationViewModel
        {
            ReservationID = reservation.ReservationID,
            CustomerID = reservation.CustomerID,
            SittingID = reservation.SittingID,
            ReservationTime = reservation.StartTime.ToString("HH:mm"),
            Duration = reservation.Duration,
            NumberOfGuests = reservation.NumberOfGuests,
            Source = reservation.Source,
            Status = reservation.Status,
            Notes = reservation.Notes,
            RowVersion = reservation.RowVersion
        };
    }

    private async Task<Sitting?> LoadSittingAsync(int sittingId)
    {
        if (sittingId <= 0)
        {
            return null;
        }

        return await _context.Sittings.FirstOrDefaultAsync(s => s.SittingID == sittingId);
    }

    private static DateTime? BuildStartTime(Sitting? sitting, string? reservationTime)
    {
        if (sitting == null || !TimeSpan.TryParse(reservationTime, out var time))
        {
            return null;
        }

        return sitting.StartTime.Date.Add(time);
    }

    private static List<string> GetValidTimes(Sitting sitting)
    {
        var times = new List<string>();
        var current = sitting.StartTime;

        while (current < sitting.EndTime)
        {
            times.Add(current.ToString("HH:mm"));
            current = current.AddMinutes(30);
        }

        return times;
    }

    private static List<int> GetValidDurations(DateTime startTime, DateTime sittingEndTime)
    {
        var durations = new List<int>();
        var minutes = 30;

        while (startTime.AddMinutes(minutes) <= sittingEndTime)
        {
            durations.Add(minutes);
            minutes += 30;
        }

        return durations;
    }

    private async Task PopulateDropDowns(
        int? selectedCustomerId = null,
        int? selectedSittingId = null,
        string? selectedStatus = null,
        string? selectedTime = null,
        int? selectedDuration = null,
        bool isCreate = false)
    {
        var customers = await _context.CustomerMembers
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();

        ViewBag.Customers = new SelectList(
            customers.Select(c => new
            {
                c.CustomerID,
                FullName = $"{c.FirstName} {c.LastName}"
            }),
            "CustomerID",
            "FullName",
            selectedCustomerId);

        var now = DateTime.Now;

        var sittings = await _context.Sittings
            .Where(s => !s.IsClosed)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        sittings = sittings
            .Where(s => s.EndTime > now)
            .ToList();

        if (!isCreate && selectedSittingId.HasValue && selectedSittingId.Value > 0 &&
            sittings.All(s => s.SittingID != selectedSittingId.Value))
        {
            var existingSitting = await _context.Sittings
                .FirstOrDefaultAsync(s => s.SittingID == selectedSittingId.Value);

            if (existingSitting != null)
            {
                sittings.Add(existingSitting);
                sittings = sittings.OrderBy(s => s.StartTime).ToList();
            }
        }

        ViewBag.SittingOptions = sittings;
        ViewBag.ReservationTimes = new SelectList(Enumerable.Empty<string>());
        ViewBag.Durations = new SelectList(Enumerable.Empty<int>());

        if (selectedSittingId.HasValue && selectedSittingId.Value > 0)
        {
            var selectedSitting = sittings.FirstOrDefault(s => s.SittingID == selectedSittingId.Value);

            if (selectedSitting != null)
            {
                ViewBag.ReservationTimes = new SelectList(GetValidTimes(selectedSitting), selectedTime);

                var startTime = BuildStartTime(selectedSitting, selectedTime);
                if (startTime.HasValue)
                {
                    var durations = GetValidDurations(startTime.Value, selectedSitting.EndTime);
                    ViewBag.Durations = new SelectList(
                        durations.Select(d => new { Value = d, Text = d + " minutes" }),
                        "Value",
                        "Text",
                        selectedDuration);
                }
            }
        }

        ViewBag.Statuses = new SelectList(ValidStatuses, selectedStatus);
    }

    private async Task PopulateReservationDetailsAsync(Reservation reservation)
    {
        var customer = await _context.CustomerMembers
            .FirstOrDefaultAsync(c => c.CustomerID == reservation.CustomerID);
        var sitting = await _context.Sittings
            .FirstOrDefaultAsync(s => s.SittingID == reservation.SittingID);

        ViewBag.CustomerName = customer == null ? "" : $"{customer.FirstName} {customer.LastName}";
        ViewBag.SittingLabel = sitting == null
            ? ""
            : $"{sitting.SittingType} ({sitting.StartTime:dd/MM/yyyy HH:mm} - {sitting.EndTime:HH:mm})";

        var assignedLinks = await _context.ReservationTables
            .Include(rt => rt.Table)
            .Where(rt => rt.ReservationID == reservation.ReservationID)
            .ToListAsync();

        ViewBag.AssignedTables = assignedLinks
            .Where(rt => rt.Table != null)
            .Select(rt => rt.Table!)
            .OrderBy(t => t.TableNumber)
            .ToList();

        ViewBag.AssignedTableIds = assignedLinks
            .Select(rt => rt.TableID)
            .ToList();

        ViewBag.RestaurantTables = await _context.Tables
            .OrderBy(t => t.TableNumber)
            .ToListAsync();
    }

    private async Task<IActionResult> ReservationDetailsViewAsync(Reservation reservation)
    {
        await PopulateReservationDetailsAsync(reservation);
        return View("Details", reservation);
    }

    private async Task<string?> FindTableConflictMessageAsync(Reservation current, List<int> selectedTableIds)
    {
        var currentEnd = current.StartTime.AddMinutes(current.Duration);

        var otherLinks = await _context.ReservationTables
            .Include(rt => rt.Reservation)
            .Include(rt => rt.Table)
            .Where(rt => selectedTableIds.Contains(rt.TableID) && rt.ReservationID != current.ReservationID)
            .ToListAsync();

        foreach (var link in otherLinks)
        {
            var other = link.Reservation;
            // A cancelled reservation does not keep the table.
            if (other == null || other.Status == "Cancelled")
            {
                continue;
            }

            var otherEnd = other.StartTime.AddMinutes(other.Duration);
            if (other.StartTime < currentEnd && current.StartTime < otherEnd)
            {
                var tableNumber = link.Table == null ? link.TableID.ToString() : link.Table.TableNumber;
                return $"Table {tableNumber} is already assigned to another reservation at this time.";
            }
        }

        return null;
    }

    private async Task ValidateReservationViewModelAsync(
        ReservationViewModel model,
        Sitting? sitting,
        DateTime? startTime,
        bool isCreate)
    {
        ModelState.Remove(nameof(model.CustomerID));
        ModelState.Remove(nameof(model.SittingID));
        ModelState.Remove(nameof(model.ReservationTime));

        if (model.CustomerID <= 0 ||
            !await _context.CustomerMembers.AnyAsync(c => c.CustomerID == model.CustomerID))
        {
            ModelState.AddModelError(nameof(model.CustomerID), "Please select a customer.");
        }

        // Check that a real, open sitting was selected.
        if (model.SittingID <= 0 || sitting == null)
        {
            ModelState.AddModelError(nameof(model.SittingID), "Please select a sitting.");
        }
        else
        {
            // A closed sitting cannot take a new reservation.
            if (sitting.IsClosed)
            {
                ModelState.AddModelError(nameof(model.SittingID), "This sitting is closed.");
            }

            if (isCreate && sitting.EndTime <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.SittingID), "This sitting has already finished.");
            }
        }

        // Check that the reservation fits inside the selected sitting.
        if (string.IsNullOrWhiteSpace(model.ReservationTime) || !startTime.HasValue)
        {
            ModelState.AddModelError(nameof(model.ReservationTime), "Please select a start time.");
        }
        else if (sitting != null)
        {
            var validTimes = GetValidTimes(sitting);

            if (!validTimes.Contains(model.ReservationTime) ||
                startTime.Value < sitting.StartTime ||
                startTime.Value >= sitting.EndTime)
            {
                ModelState.AddModelError(nameof(model.ReservationTime), "Start Time must be within the selected sitting.");
            }
        }

        // The reservation must finish before the sitting ends.
        if (model.Duration <= 0)
        {
            ModelState.AddModelError(nameof(model.Duration), "Duration must be greater than 0.");
        }
        else if (sitting != null && startTime.HasValue)
        {
            var validDurations = GetValidDurations(startTime.Value, sitting.EndTime);

            if (!validDurations.Contains(model.Duration) ||
                startTime.Value.AddMinutes(model.Duration) > sitting.EndTime)
            {
                ModelState.AddModelError(nameof(model.Duration), "Reservation duration must finish within the selected Sitting.");
            }
        }

        if (model.NumberOfGuests <= 0)
        {
            ModelState.AddModelError(nameof(model.NumberOfGuests), "Number of Guests must be greater than 0.");
        }
        else if (sitting != null && model.NumberOfGuests > sitting.Capacity)
        {
            ModelState.AddModelError(nameof(model.NumberOfGuests), $"Number of Guests cannot exceed the sitting capacity of {sitting.Capacity}.");
        }
        else if (sitting != null &&
                 model.NumberOfGuests > 0 &&
                 ActiveCapacityStatuses.Contains(model.Status))
        {
            // Check the remaining sitting capacity before saving.
            var existingGuests = await GetActiveGuestCountAsync(
                sitting.SittingID,
                isCreate ? null : model.ReservationID);

            if (existingGuests + model.NumberOfGuests > sitting.Capacity)
            {
                ModelState.AddModelError(nameof(model.NumberOfGuests), "The sitting does not have enough remaining capacity.");
            }
        }

        if (!ValidSources.Contains(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Please select a valid reservation source.");
        }

        if (!isCreate && !ValidStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "Please select a valid Status.");
        }
    }

    private async Task<int> GetActiveGuestCountAsync(int sittingId, int? excludedReservationId)
    {
        var query = _context.Reservations.Where(r =>
            r.SittingID == sittingId &&
            ActiveCapacityStatuses.Contains(r.Status));

        // Do not count the reservation that is being edited.
        if (excludedReservationId.HasValue)
        {
            var reservationId = excludedReservationId.Value;
            query = query.Where(r => r.ReservationID != reservationId);
        }

        return await query.SumAsync(r => (int?)r.NumberOfGuests) ?? 0;
    }

    // Manager and Staff can manage every reservation. A Member cannot.
    private bool CanManageReservations()
    {
        return User.IsInRole("Manager") || User.IsInRole("Staff");
    }

    private async Task<CustomerMember?> GetLinkedCustomerAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await _context.CustomerMembers.FirstOrDefaultAsync(c => c.UserID == userId);
    }

    private async Task<bool> MemberOwnsReservationAsync(Reservation reservation)
    {
        if (CanManageReservations())
        {
            return true;
        }

        var customer = await GetLinkedCustomerAsync();
        return customer != null && reservation.CustomerID == customer.CustomerID;
    }

    private void ShowLockedCustomer(CustomerMember customer)
    {
        ViewBag.LockCustomer = true;
        ViewBag.MemberCustomerName = $"{customer.FirstName} {customer.LastName}";
    }
}

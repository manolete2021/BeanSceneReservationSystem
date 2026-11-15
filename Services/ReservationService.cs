using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Interfaces;
using BeanSceneReservationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BeanSceneReservationSystem.Services;

public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;

    public ReservationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Reservation>> SearchAsync(
        string? customerName,
        DateTime? reservationDate,
        string? status,
        int? customerId)
    {
        // Load the related customer and sitting with the reservation.
        IQueryable<Reservation> query = _context.Reservations
            .Include(r => r.CustomerMember)
            .Include(r => r.Sitting);

        // A Member search is limited to that member's customer record.
        if (customerId.HasValue)
        {
            var linkedCustomerId = customerId.Value;
            query = query.Where(r => r.CustomerID == linkedCustomerId);
        }

        // Match part of the customer first name or last name.
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var term = customerName.Trim();
            query = query.Where(r =>
                r.CustomerMember != null &&
                (r.CustomerMember.FirstName.Contains(term) ||
                 r.CustomerMember.LastName.Contains(term) ||
                 (r.CustomerMember.FirstName + " " + r.CustomerMember.LastName).Contains(term)));
        }

        // Match the reservation date and ignore the time.
        if (reservationDate.HasValue)
        {
            var date = reservationDate.Value.Date;
            query = query.Where(r => r.StartTime.Date == date);
        }

        // Match one reservation status when a status was selected.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var selectedStatus = status.Trim();
            query = query.Where(r => r.Status == selectedStatus);
        }

        return await query.ToListAsync();
    }
}

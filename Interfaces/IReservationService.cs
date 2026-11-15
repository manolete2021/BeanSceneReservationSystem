using BeanSceneReservationSystem.Models;

namespace BeanSceneReservationSystem.Interfaces;

public interface IReservationService
{
    Task<List<Reservation>> SearchAsync(
        string? customerName,
        DateTime? reservationDate,
        string? status,
        int? customerId);
}

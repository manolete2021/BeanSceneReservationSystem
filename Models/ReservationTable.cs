namespace BeanSceneReservationSystem.Models;

public class ReservationTable
{
    public int ReservationID { get; set; }
    public virtual Reservation? Reservation { get; set; }
    public int TableID { get; set; }
    public virtual Table? Table { get; set; }
}

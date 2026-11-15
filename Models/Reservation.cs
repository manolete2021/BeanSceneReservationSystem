using System.ComponentModel.DataAnnotations;

namespace BeanSceneReservationSystem.Models;

public class Reservation
{
    public int ReservationID { get; set; }
    public int CustomerID { get; set; }
    // Virtual so related records can be loaded when they are needed.
    public virtual CustomerMember? CustomerMember { get; set; }
    public int SittingID { get; set; }
    public virtual Sitting? Sitting { get; set; }
    public DateTime StartTime { get; set; }
    public int Duration { get; set; }
    public int NumberOfGuests { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // Used to detect concurrent updates.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}

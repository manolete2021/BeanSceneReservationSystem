namespace BeanSceneReservationSystem.Models;

public class ReservationViewModel
{
    public int ReservationID { get; set; }
    public int CustomerID { get; set; }
    public int SittingID { get; set; }
    public string ReservationTime { get; set; } = string.Empty;
    public int Duration { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Notes { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

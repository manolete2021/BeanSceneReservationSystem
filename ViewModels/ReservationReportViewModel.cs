namespace BeanSceneReservationSystem.ViewModels;

public class ReservationReportViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalReservations { get; set; }
    public int TotalGuests { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int SeatedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public List<ReservationReportRow> Reservations { get; set; } = new();
}

public class ReservationReportRow
{
    public int ReservationID { get; set; }
    public string Customer { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int NumberOfGuests { get; set; }
    public string Status { get; set; } = string.Empty;
}

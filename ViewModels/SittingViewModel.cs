namespace BeanSceneReservationSystem.ViewModels;

public class SittingViewModel
{
    public int SittingID { get; set; }
    public string SittingType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int Capacity { get; set; }
    public bool IsClosed { get; set; }
}

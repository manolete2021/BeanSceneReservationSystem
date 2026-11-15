namespace BeanSceneReservationSystem.Models;

public class Sitting
{
    public int SittingID { get; set; }
    public string SittingType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public bool IsClosed { get; set; }
}

namespace BeanSceneReservationSystem.Models;

public class Table
{
    public int TableID { get; set; }
    public int AreaID { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsAvailable { get; set; }
    public virtual ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}

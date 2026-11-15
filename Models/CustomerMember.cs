namespace BeanSceneReservationSystem.Models;

// CustomerMember inherits common personal details from Person.
public class CustomerMember : Person
{
    public int CustomerID { get; set; }
    public int? UserID { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }
}

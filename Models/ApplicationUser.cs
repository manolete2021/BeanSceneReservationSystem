using Microsoft.AspNetCore.Identity;

namespace BeanSceneReservationSystem.Models;

public class ApplicationUser : IdentityUser<int>
{
    public virtual CustomerMember? CustomerMember { get; set; }
}

namespace BeanSceneReservationSystem.Interfaces;

public interface IEmailService
{
    Task SendRegistrationConfirmationAsync(string toEmail, string firstName);
}

namespace HRSystem.DAL.Interfaces;

// Implemented in the Web layer (reads the logged-in user from HttpContext)
public interface ICurrentUserService
{
    string? UserId { get; }
}

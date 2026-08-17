namespace MyAssistant.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    string? UserName { get; }
    string? OrganizationId { get; }
}

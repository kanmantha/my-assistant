using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

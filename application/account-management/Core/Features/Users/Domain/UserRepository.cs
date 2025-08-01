using Microsoft.EntityFrameworkCore;
using PlatformPlatform.AccountManagement.Database;
using PlatformPlatform.SharedKernel.Domain;
using PlatformPlatform.SharedKernel.ExecutionContext;
using PlatformPlatform.SharedKernel.Persistence;

namespace PlatformPlatform.AccountManagement.Features.Users.Domain;

public interface IUserRepository : ICrudRepository<User, UserId>, IBulkRemoveRepository<User>
{
    Task<User?> GetByIdUnfilteredAsync(UserId id, CancellationToken cancellationToken);

    Task<User> GetLoggedInUserAsync(CancellationToken cancellationToken);

    Task<User?> GetUserByEmailUnfilteredAsync(string email, CancellationToken cancellationToken);

    Task<bool> IsEmailFreeAsync(string email, CancellationToken cancellationToken);

    Task<int> CountTenantUsersAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<(int TotalUsers, int ActiveUsers, int PendingUsers)> GetUserSummaryAsync(CancellationToken cancellationToken);

    Task<User[]> GetByIdsAsync(UserId[] ids, CancellationToken cancellationToken);

    Task<(User[] Users, int TotalItems, int TotalPages)> Search(
        string? search,
        UserRole? userRole,
        UserStatus? userStatus,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        SortableUserProperties? orderBy,
        SortOrder? sortOrder,
        int? pageOffset,
        int? pageSize,
        CancellationToken cancellationToken
    );

    Task<User[]> GetTenantUsers(CancellationToken cancellationToken);
}

internal sealed class UserRepository(AccountManagementDbContext accountManagementDbContext, IExecutionContext executionContext)
    : RepositoryBase<User, UserId>(accountManagementDbContext), IUserRepository
{
    /// <summary>
    ///     Retrieves a user by ID without applying tenant query filters.
    ///     This method should only be used during authentication processes where tenant context is not yet established.
    /// </summary>
    public async Task<User?> GetByIdUnfilteredAsync(UserId id, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User> GetLoggedInUserAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionContext.UserInfo.Id);
        return await GetByIdAsync(executionContext.UserInfo.Id, cancellationToken) ??
               throw new InvalidOperationException("Logged in user not found.");
    }

    /// <summary>
    ///     Retrieves a user by email without applying tenant query filters.
    ///     This method should only be used during the login processes where tenant context is not yet established.
    /// </summary>
    public async Task<User?> GetUserByEmailUnfilteredAsync(string email, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> IsEmailFreeAsync(string email, CancellationToken cancellationToken)
    {
        return !await DbSet.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public Task<int> CountTenantUsersAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.CountAsync(u => u.TenantId == tenantId, cancellationToken);
    }

    public async Task<User[]> GetByIdsAsync(UserId[] ids, CancellationToken cancellationToken)
    {
        return await DbSet.Where(u => ids.Contains(u.Id)).ToArrayAsync(cancellationToken);
    }

    public async Task<(int TotalUsers, int ActiveUsers, int PendingUsers)> GetUserSummaryAsync(CancellationToken cancellationToken)
    {
        var thirtyDaysAgo = TimeProvider.System.GetUtcNow().AddDays(-30);

        var summary = await DbSet
            .GroupBy(_ => 1) // Group all records into a single group to calculate multiple COUNT aggregates in one query
            .Select(g => new
                {
                    TotalUsers = g.Count(),
                    ActiveUsers = g.Count(u => u.EmailConfirmed && u.ModifiedAt >= thirtyDaysAgo),
                    PendingUsers = g.Count(u => !u.EmailConfirmed)
                }
            )
            .SingleAsync(cancellationToken);

        return (summary.TotalUsers, summary.ActiveUsers, summary.PendingUsers);
    }

    public async Task<(User[] Users, int TotalItems, int TotalPages)> Search(
        string? search,
        UserRole? userRole,
        UserStatus? userStatus,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        SortableUserProperties? orderBy,
        SortOrder? sortOrder,
        int? pageOffset,
        int? pageSize,
        CancellationToken cancellationToken
    )
    {
        IQueryable<User> users = DbSet;

        if (search is not null)
        {
            // Concatenate first and last name to enable searching by full name
            users = users.Where(u =>
                u.Email.Contains(search) ||
                (u.FirstName + " " + u.LastName).Contains(search) ||
                (u.Title ?? "").Contains(search)
            );
        }

        if (userRole is not null)
        {
            users = users.Where(u => u.Role == userRole);
        }

        if (userStatus is not null)
        {
            var active = userStatus == UserStatus.Active;
            users = users.Where(u => u.EmailConfirmed == active);
        }

        if (startDate is not null)
        {
            users = users.Where(u => u.ModifiedAt >= startDate);
        }

        if (endDate is not null)
        {
            users = users.Where(u => u.ModifiedAt < endDate.Value.AddDays(1));
        }

        users = orderBy switch
        {
            SortableUserProperties.CreatedAt => sortOrder == SortOrder.Ascending
                ? users.OrderBy(u => u.CreatedAt)
                : users.OrderByDescending(u => u.CreatedAt),
            SortableUserProperties.ModifiedAt => sortOrder == SortOrder.Ascending
                ? users.OrderBy(u => u.ModifiedAt)
                : users.OrderByDescending(u => u.ModifiedAt),
            SortableUserProperties.Name => sortOrder == SortOrder.Ascending
                ? users.OrderBy(u => u.FirstName == null ? 1 : 0)
                    .ThenBy(u => u.FirstName)
                    .ThenBy(u => u.LastName == null ? 1 : 0)
                    .ThenBy(u => u.LastName)
                    .ThenBy(u => u.Email)
                : users.OrderBy(u => u.FirstName == null ? 0 : 1)
                    .ThenByDescending(u => u.FirstName)
                    .ThenBy(u => u.LastName == null ? 0 : 1)
                    .ThenByDescending(u => u.LastName)
                    .ThenBy(u => u.Email),
            SortableUserProperties.Email => sortOrder == SortOrder.Ascending
                ? users.OrderBy(u => u.Email)
                : users.OrderByDescending(u => u.Email),
            SortableUserProperties.Role => sortOrder == SortOrder.Ascending
                ? users.OrderBy(u => u.Role)
                : users.OrderByDescending(u => u.Role),
            _ => users
                .OrderBy(u => u.FirstName == null ? 1 : 0)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.LastName == null ? 1 : 0)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Email)
        };

        pageSize ??= 50;
        var itemOffset = (pageOffset ?? 0) * pageSize.Value;
        var result = await users.Skip(itemOffset).Take(pageSize.Value).ToArrayAsync(cancellationToken);

        var totalItems = pageOffset == 0 && result.Length < pageSize
            ? result.Length // If the first page returns fewer items than page size, skip querying the total count
            : await users.CountAsync(cancellationToken);

        var totalPages = (totalItems - 1) / pageSize.Value + 1;
        return (result, totalItems, totalPages);
    }

    public async Task<User[]> GetTenantUsers(CancellationToken cancellationToken)
    {
        return await DbSet.ToArrayAsync(cancellationToken);
    }
}

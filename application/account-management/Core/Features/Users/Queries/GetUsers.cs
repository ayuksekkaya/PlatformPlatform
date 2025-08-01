using FluentValidation;
using JetBrains.Annotations;
using Mapster;
using PlatformPlatform.AccountManagement.Features.Users.Domain;
using PlatformPlatform.SharedKernel.Cqrs;
using PlatformPlatform.SharedKernel.Domain;
using PlatformPlatform.SharedKernel.Persistence;

namespace PlatformPlatform.AccountManagement.Features.Users.Queries;

[PublicAPI]
public sealed record GetUsersQuery(
    string? Search = null,
    UserRole? UserRole = null,
    UserStatus? UserStatus = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    SortableUserProperties OrderBy = SortableUserProperties.Name,
    SortOrder SortOrder = SortOrder.Ascending,
    int? PageOffset = null,
    int PageSize = 25
) : IRequest<Result<UsersResponse>>;

[PublicAPI]
public sealed record UsersResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, UserDetails[] Users);

[PublicAPI]
public sealed record UserDetails(
    UserId Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string Email,
    UserRole Role,
    string FirstName,
    string LastName,
    string Title,
    bool EmailConfirmed,
    string? AvatarUrl
);

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("Search must be no longer than 100 characters.");
        RuleFor(x => x.PageSize).InclusiveBetween(0, 1000).WithMessage("Page size must be between 0 and 1000.");
        RuleFor(x => x.PageOffset).GreaterThanOrEqualTo(0).WithMessage("Page offset must be greater than or equal to 0.");
    }
}

public sealed class GetUsersHandler(IUserRepository userRepository)
    : IRequestHandler<GetUsersQuery, Result<UsersResponse>>
{
    public async Task<Result<UsersResponse>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var (users, count, totalPages) = await userRepository.Search(
            query.Search,
            query.UserRole,
            query.UserStatus,
            query.StartDate,
            query.EndDate,
            query.OrderBy,
            query.SortOrder,
            query.PageOffset,
            query.PageSize,
            cancellationToken
        );

        if (query.PageOffset.HasValue && query.PageOffset.Value >= totalPages)
        {
            return Result<UsersResponse>.BadRequest($"The page offset {query.PageOffset.Value} is greater than the total number of pages.");
        }

        var userResponses = users.Adapt<UserDetails[]>();
        return new UsersResponse(count, query.PageSize, totalPages, query.PageOffset ?? 0, userResponses);
    }
}

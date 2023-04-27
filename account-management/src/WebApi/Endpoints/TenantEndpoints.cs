using MediatR;
using PlatformPlatform.AccountManagement.Application.Tenants.Commands;
using PlatformPlatform.AccountManagement.Application.Tenants.Queries;
using PlatformPlatform.AccountManagement.Domain.Tenants;

namespace PlatformPlatform.AccountManagement.WebApi.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/tenants");
        group.MapGet("/{id}", GetTenant);
        group.MapPost("/", CreateTenant);
    }

    private static async Task<IResult> GetTenant(string id, ISender sender)
    {
        var getTenantByIdQuery = new GetTenantByIdQuery(TenantId.FromString(id));
        var tenantDto = await sender.Send(getTenantByIdQuery);
        return tenantDto is null ? Results.NotFound() : Results.Ok(tenantDto);
    }

    private static async Task<IResult> CreateTenant(CreateTenantCommand createTenantCommand, ISender sender)
    {
        var tenantDto = await sender.Send(createTenantCommand);
        return Results.Created($"/tenants/{tenantDto.Id}", tenantDto);
    }
}
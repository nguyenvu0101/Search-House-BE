namespace TD.KCN.WebApi.Application.Identity.Roles;

public class RoleListFilter : PaginationFilter
{
    public bool? IsActive { get; set; }
}
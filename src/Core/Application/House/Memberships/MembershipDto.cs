using TD.KCN.WebApi.Application.House.ImageHouses;
using TD.KCN.WebApi.Application.Identity.Users;

namespace TD.KCN.WebApi.Application.House.Memberships;

public class MembershipDto : IDto
{
    public DefaultIdType Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int? CountPost { get; set; }
    public bool? IsVip { get; set; }
}
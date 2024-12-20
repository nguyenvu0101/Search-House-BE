namespace TD.KCN.WebApi.Application.Catalog.Areas;

public class SearchAreasRequest : PaginationFilter, IRequest<PaginationResponse<AreaDto>>
{
    public Guid? ParentId { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? DistrictId { get; set; }
    public Guid? CommuneId { get; set; }
    public string? ParentCode { get; set; }
    public string? Type { get; set; }
    public int? Level { get; set; }
}

public class AreasBySearchRequestSpec : EntitiesByPaginationFilterSpec<Area, AreaDto>
{
    public AreasBySearchRequestSpec(SearchAreasRequest request)
        : base(request) =>
        Query
            .OrderBy(c => c.Name, !request.HasOrderBy())
            .Where(c => c.Id == request.ProvinceId, request.ProvinceId.HasValue)
            .Where(c => c.Id == request.DistrictId, request.DistrictId.HasValue)
            .Where(c => c.Id == request.CommuneId, request.CommuneId.HasValue)
            .Where(p => p.ParentCode == request.ParentCode, !string.IsNullOrWhiteSpace(request.ParentCode))
            .Where(p => p.Type == request.Type, !string.IsNullOrWhiteSpace(request.Type))
            .Where(p => p.Level == request.Level, request.Level.HasValue && request.Level.Value > 0);
}

public class SearchCategoriesRequestHandler : IRequestHandler<SearchAreasRequest, PaginationResponse<AreaDto>>
{
    private readonly IReadRepository<Area> _repository;

    public SearchCategoriesRequestHandler(IReadRepository<Area> repository) => _repository = repository;

    public async Task<PaginationResponse<AreaDto>> Handle(SearchAreasRequest request, CancellationToken cancellationToken)
    {
        if (request.ParentId.HasValue)
        {
            var area = await _repository.GetByIdAsync(request.ParentId, cancellationToken);
            if (area != null) request.ParentCode = area.Code;
        }

        var spec = new AreasBySearchRequestSpec(request);

        var list = await _repository.ListAsync(spec, cancellationToken);
        foreach (var area in list)
        {
            var lstChildren = await _repository.ListAsync(
                (ISpecification<Area, AreaDto>)new AreasByParentCodeSpec(area.Code),
                cancellationToken
            );
            area.Children = lstChildren;
        }
        int count = await _repository.CountAsync(spec, cancellationToken);

        return new PaginationResponse<AreaDto>(list, count, request.PageNumber, request.PageSize);
    }
}
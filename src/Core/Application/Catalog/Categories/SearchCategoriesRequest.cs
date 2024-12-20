

using TD.KCN.WebApi.Application.House.ImageHouses;

namespace TD.KCN.WebApi.Application.Catalog.Categories;

public class SearchCategoriesRequest : PaginationFilter, IRequest<PaginationResponse<CategoryDto>>
{

}

public class CategoriesBySearchRequestSpec : EntitiesByPaginationFilterSpec<Category, CategoryDto>
{
    public CategoriesBySearchRequestSpec(SearchCategoriesRequest request)
        : base(request) =>
        Query
            .OrderBy(c => c.Name, !request.HasOrderBy());
}

public class SearchCategoriesRequestHandler : IRequestHandler<SearchCategoriesRequest, PaginationResponse<CategoryDto>>
{
    private readonly IRepository<Category> _repository;

    public SearchCategoriesRequestHandler(IRepository<Category> repository) => _repository = repository;

    public async Task<PaginationResponse<CategoryDto>> Handle(SearchCategoriesRequest request, CancellationToken cancellationToken)
    {
        var spec = new CategoriesBySearchRequestSpec(request);

        var list = await _repository.ListAsync(spec, cancellationToken);
        int count = await _repository.CountAsync(spec, cancellationToken);

        return new PaginationResponse<CategoryDto>(list, count, request.PageNumber, request.PageSize);
    }
}
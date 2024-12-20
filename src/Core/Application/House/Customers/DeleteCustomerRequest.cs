namespace TD.KCN.WebApi.Application.House.Customers;

public class DeleteCustomerRequest : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }

    public DeleteCustomerRequest(Guid id) => Id = id;
}

public class DeleteCustomerRequestHandler : IRequestHandler<DeleteCustomerRequest, Result<Guid>>
{
    // Add Domain Events automatically by using IRepositoryWithEvents
    private readonly IRepositoryWithEvents<Customer> _repo;
    private readonly IRepositoryWithEvents<Motel> _repositoryMotel;

    private readonly IStringLocalizer<DeleteCustomerRequestHandler> _localizer;

    public DeleteCustomerRequestHandler(IRepositoryWithEvents<Customer> repo, IRepositoryWithEvents<Motel> repositoryMotel, IStringLocalizer<DeleteCustomerRequestHandler> localizer) =>
        (_repo, _repositoryMotel, _localizer) = (repo, repositoryMotel, localizer);

    public async Task<Result<Guid>> Handle(DeleteCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await _repo.GetByIdAsync(request.Id, cancellationToken);

        _ = customer ?? throw new NotFoundException(_localizer["Customer.notfound"]);

        await _repo.DeleteAsync(customer, cancellationToken);
        var motel = await _repositoryMotel.GetByIdAsync(customer.MotelId);
        if (motel != null)
        {
            motel.Status = "Chưa thuê";
            await _repositoryMotel.UpdateAsync(motel, cancellationToken);
        }
        return Result<Guid>.Success(request.Id);
    }
}
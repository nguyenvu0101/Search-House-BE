﻿using TD.KCN.WebApi.Application.Auditing;

namespace TD.KCN.WebApi.Host.Controllers.Catalog;

public class AuditsController : VersionedApiController
{
    private readonly IAuditService _auditService;
    public AuditsController(IAuditService auditService) => _auditService = auditService;

    [HttpPost("search")]
    [OpenApiOperation("Danh sách nhật ký hệ thống.", "")]
    public Task<PaginationResponse<AuditDto>> SearchAsync(AuditListFilter request, CancellationToken cancellationToken)
    {
        return _auditService.SearchAsync(request, cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    [OpenApiOperation("Xóa nhật ký hệ thống.", "")]
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return _auditService.DeleteAsync(id, cancellationToken);
    }
}
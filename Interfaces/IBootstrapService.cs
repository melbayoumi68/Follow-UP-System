using InvoicesErp.DTOs;

namespace InvoicesErp.Interfaces;

public interface IBootstrapService
{
    Task<BootstrapDto> GetAsync(CancellationToken ct = default);
    Task ReplaceAsync(BootstrapDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<string>> UpsertAsync(BootstrapDto dto, CancellationToken ct = default);
}

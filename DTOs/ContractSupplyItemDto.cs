namespace InvoicesErp.DTOs;

public record ContractSupplyItemDto(
    string? Id,
    string? ContractId,
    string? ProjectId,
    string Description,
    string Unit,
    decimal Qty,
    decimal UnitPrice,
    decimal Total,
    string SupplierName,
    DateOnly SupplyDate,
    string? Note,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public record ContractSupplyItemWriteDto(
    string Description,
    string Unit,
    decimal Qty,
    decimal UnitPrice,
    string SupplierName,
    DateOnly SupplyDate,
    string? Note);

public record ContractSupplySummaryDto(
    decimal TotalSupplies,
    decimal ContractAmount,
    decimal ModifiedContractAmount,
    decimal PercentageOfOriginal,
    decimal PercentageOfModified);

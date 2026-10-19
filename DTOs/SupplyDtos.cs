namespace InvoicesErp.DTOs;

public record ProjectSupplyWriteDto(string Description, string Unit, decimal Qty, decimal UnitPrice, DateOnly SupplyDate, string? Note);
public record ProjectSupplyItemDto(string Id, string ProjectId, string Description, string Unit, decimal Qty, decimal UnitPrice, decimal Total, decimal AllocatedQty, decimal AvailableQty, DateOnly SupplyDate, string? Note, DateTime CreatedAt, DateTime UpdatedAt);
public record ContractSupplyAllocationWriteDto(string ProjectSupplyItemId, decimal Qty);
public record ContractSupplyAllocationDto(string Id, string ProjectSupplyItemId, string Description, string Unit, decimal SourceQty, decimal AvailableQty, decimal Qty, decimal UnitPrice, decimal Total, DateTime CreatedAt);

public record InvoiceSupplyAllocationWriteDto(string ContractSupplyAllocationId, decimal Qty);
public record InvoiceSupplyAllocationDto(
    string Id,
    string InvoiceId,
    string ContractSupplyAllocationId,
    string ProjectSupplyItemId,
    string Description,
    string Unit,
    decimal ContractAllocationQty,
    decimal AvailableQty,
    decimal Qty,
    decimal UnitPrice,
    decimal Total,
    DateTime CreatedAt);

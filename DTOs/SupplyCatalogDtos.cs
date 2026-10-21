namespace InvoicesErp.DTOs;

public record SupplyCatalogWriteDto(string Type, string Unit);
public record SupplyCatalogStatusUpdateDto(string Status);
public record SupplyCatalogItemDto(string Id, string Type, string Unit, string Status, DateTime CreatedAt, string[] ProjectNames);
public record SupplyCatalogAssignmentWriteDto(string ProjectId, decimal Qty, decimal UnitPrice, DateOnly SupplyDate);
public record SupplyCatalogAssignmentDto(string Id, string SupplyCatalogItemId, string ProjectId, string ProjectName, string ProjectSupplyItemId, decimal Qty, DateOnly SupplyDate, DateTime CreatedAt);

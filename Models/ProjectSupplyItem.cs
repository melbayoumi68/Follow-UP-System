namespace InvoicesErp.Models;

public class ProjectSupplyItem
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateOnly SupplyDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Project Project { get; set; } = null!;
}

public class ContractSupplyAllocation
{
    public string Id { get; set; } = "";
    public string ProjectSupplyItemId { get; set; } = "";
    public string ContractId { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ProjectSupplyItem ProjectSupplyItem { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
}


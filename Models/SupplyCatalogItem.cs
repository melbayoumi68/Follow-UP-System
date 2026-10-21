namespace InvoicesErp.Models;

public class SupplyCatalogItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Material { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateOnly SupplyDate { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<SupplyCatalogAssignment> Assignments { get; set; } = new List<SupplyCatalogAssignment>();
}

public class SupplyCatalogAssignment
{
    public string Id { get; set; } = "";
    public string SupplyCatalogItemId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ProjectSupplyItemId { get; set; } = "";
    public decimal Qty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SupplyCatalogItem SupplyCatalogItem { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectSupplyItem ProjectSupplyItem { get; set; } = null!;
}

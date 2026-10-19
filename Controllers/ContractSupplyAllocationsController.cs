using InvoicesErp.Auth;
using InvoicesErp.Data;
using InvoicesErp.DTOs;
using InvoicesErp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/contracts/{contractId}/supply-allocations")]
public class ContractSupplyAllocationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.View)]
    public async Task<IActionResult> List(string contractId, CancellationToken ct)
    {
        if (!await db.Contracts.AnyAsync(c => c.Id == contractId, ct)) return NotFound();
        var rows = await db.ContractSupplyAllocations.AsNoTracking().Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.CreatedAt).Select(a => new ContractSupplyAllocationDto(
                a.Id, a.ProjectSupplyItemId, a.ProjectSupplyItem.Description, a.ProjectSupplyItem.Unit,
                a.ProjectSupplyItem.Qty,
                a.ProjectSupplyItem.Qty - (db.ContractSupplyAllocations.Where(x => x.ProjectSupplyItemId == a.ProjectSupplyItemId).Sum(x => (decimal?)x.Qty) ?? 0m),
                a.Qty, a.UnitPrice, a.Total, a.CreatedAt)).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.Edit)]
    public async Task<IActionResult> Assign(string contractId, ContractSupplyAllocationWriteDto dto, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null) return NotFound(new { message = "العقد غير موجود" });
        if (dto.Qty <= 0) return BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر" });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var supply = await db.ProjectSupplyItems.FirstOrDefaultAsync(s => s.Id == dto.ProjectSupplyItemId && s.ProjectId == contract.ProjectId, ct);
        if (supply is null) return BadRequest(new { message = "التوريدة لا تتبع مشروع هذا العقد" });
        var allocated = await db.ContractSupplyAllocations.Where(a => a.ProjectSupplyItemId == supply.Id).SumAsync(a => (decimal?)a.Qty, ct) ?? 0m;
        var available = supply.Qty - allocated;
        if (dto.Qty > available) return BadRequest(new { message = $"الكمية المتاحة هي {available:0.##} {supply.Unit}" });
        var item = new ContractSupplyAllocation { Id = Guid.NewGuid().ToString("N"), ProjectSupplyItemId = supply.Id, ContractId = contractId, Qty = dto.Qty, UnitPrice = supply.UnitPrice, Total = Math.Round(dto.Qty * supply.UnitPrice, 2), CreatedAt = DateTime.UtcNow };
        db.ContractSupplyAllocations.Add(item);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { item.Id, item.ProjectSupplyItemId, item.Qty, item.UnitPrice, item.Total });
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.Edit)]
    public async Task<IActionResult> Release(string contractId, string id, CancellationToken ct)
    {
        var item = await db.ContractSupplyAllocations.FirstOrDefaultAsync(a => a.Id == id && a.ContractId == contractId, ct);
        if (item is null) return NotFound();
        db.ContractSupplyAllocations.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

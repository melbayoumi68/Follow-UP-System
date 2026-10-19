using InvoicesErp.Auth;
using InvoicesErp.Data;
using InvoicesErp.DTOs;
using InvoicesErp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/invoices/{invoiceId}/supply-allocations")]
public class InvoiceSupplyAllocationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.View)]
    public async Task<IActionResult> List(string invoiceId, CancellationToken ct)
    {
        if (!await db.Invoices.AnyAsync(i => i.Id == invoiceId, ct)) return NotFound();
        var rows = await db.InvoiceSupplyAllocations.AsNoTracking().Where(a => a.InvoiceId == invoiceId)
            .OrderByDescending(a => a.CreatedAt).Select(a => new InvoiceSupplyAllocationDto(
                a.Id,
                a.InvoiceId,
                a.ContractSupplyAllocationId,
                a.ContractSupplyAllocation.ProjectSupplyItemId,
                a.ContractSupplyAllocation.ProjectSupplyItem.Description,
                a.ContractSupplyAllocation.ProjectSupplyItem.Unit,
                a.ContractSupplyAllocation.Qty,
                a.ContractSupplyAllocation.Qty - (db.InvoiceSupplyAllocations
                    .Where(x => x.ContractSupplyAllocationId == a.ContractSupplyAllocationId)
                    .Sum(x => (decimal?)x.Qty) ?? 0m),
                a.Qty,
                a.UnitPrice,
                a.Total,
                a.CreatedAt)).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Edit)]
    public async Task<IActionResult> Create(string invoiceId, InvoiceSupplyAllocationWriteDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest(new { message = "بيانات التخصيص مطلوبة" });
        if (string.IsNullOrWhiteSpace(dto.ContractSupplyAllocationId)) return BadRequest(new { message = "تخصيص التوريدة مطلوب" });
        if (dto.Qty <= 0) return BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر" });

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return NotFound(new { message = "المستخلص غير موجود" });

        var contractAlloc = await db.ContractSupplyAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == dto.ContractSupplyAllocationId, ct);
        if (contractAlloc is null) return BadRequest(new { message = "تخصيص التوريدة غير موجود" });

        if (contractAlloc.ContractId != invoice.ContractId)
            return BadRequest(new { message = "تخصيص التوريدة لا يتبع عقد هذا المستخلص" });

        var allocated = await db.InvoiceSupplyAllocations
            .Where(a => a.ContractSupplyAllocationId == contractAlloc.Id)
            .SumAsync(a => (decimal?)a.Qty, ct) ?? 0m;
        var available = contractAlloc.Qty - allocated;
        if (dto.Qty > available)
            return BadRequest(new { message = $"الكمية المتاحة من حصة العقد هي {available:0.##}" });

        var item = new InvoiceSupplyAllocation
        {
            Id = Guid.NewGuid().ToString("N"),
            InvoiceId = invoiceId,
            ContractSupplyAllocationId = contractAlloc.Id,
            Qty = dto.Qty,
            UnitPrice = contractAlloc.UnitPrice,
            Total = Math.Round(dto.Qty * contractAlloc.UnitPrice, 2),
            CreatedAt = DateTime.UtcNow
        };
        db.InvoiceSupplyAllocations.Add(item);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var result = await db.InvoiceSupplyAllocations.AsNoTracking()
            .Where(a => a.Id == item.Id)
            .Select(a => new InvoiceSupplyAllocationDto(
                a.Id,
                a.InvoiceId,
                a.ContractSupplyAllocationId,
                a.ContractSupplyAllocation.ProjectSupplyItemId,
                a.ContractSupplyAllocation.ProjectSupplyItem.Description,
                a.ContractSupplyAllocation.ProjectSupplyItem.Unit,
                a.ContractSupplyAllocation.Qty,
                a.ContractSupplyAllocation.Qty - (db.InvoiceSupplyAllocations
                    .Where(x => x.ContractSupplyAllocationId == a.ContractSupplyAllocationId)
                    .Sum(x => (decimal?)x.Qty) ?? 0m),
                a.Qty,
                a.UnitPrice,
                a.Total,
                a.CreatedAt))
            .FirstAsync(ct);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Edit)]
    public async Task<IActionResult> Delete(string invoiceId, string id, CancellationToken ct)
    {
        var item = await db.InvoiceSupplyAllocations
            .FirstOrDefaultAsync(a => a.Id == id && a.InvoiceId == invoiceId, ct);
        if (item is not null)
        {
            db.InvoiceSupplyAllocations.Remove(item);
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}

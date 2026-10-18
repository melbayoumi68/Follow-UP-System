using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesErp.Data;
using InvoicesErp.Models;
using InvoicesErp.DTOs;
using InvoicesErp.Auth;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/contracts/{contractId}/supply-items")]
public class ContractSupplyItemsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.View)]
    public async Task<IActionResult> GetAll(string contractId, CancellationToken ct)
    {
        if (!await db.Contracts.AnyAsync(c => c.Id == contractId, ct))
            return NotFound(new { success = false, message = "العقد غير موجود" });

        var items = await db.ContractSupplyItems
            .AsNoTracking()
            .Where(x => x.ContractId == contractId)
            .OrderByDescending(x => x.SupplyDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ContractSupplyItemDto(
                x.Id, x.ContractId, x.ProjectId, x.Description, x.Unit,
                x.Qty, x.UnitPrice, x.Total, x.SupplierName, x.SupplyDate,
                x.Note, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("summary")]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.View)]
    public async Task<IActionResult> GetSummary(string contractId, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking()
            .Where(c => c.Id == contractId)
            .Select(c => new { c.Amount, c.ModifiedAmount, c.VoAmount, c.ClaimsAmount, c.VatAmount })
            .FirstOrDefaultAsync(ct);

        if (contract is null)
            return NotFound(new { success = false, message = "العقد غير موجود" });

        var totalSupplies = await db.ContractSupplyItems
            .Where(x => x.ContractId == contractId)
            .SumAsync(x => (decimal?)x.Total, ct) ?? 0m;

        var modified = contract.ModifiedAmount + contract.VoAmount + contract.ClaimsAmount + contract.VatAmount;
        var pctOriginal = contract.Amount > 0 ? Math.Round(totalSupplies / contract.Amount * 100m, 2) : 0m;
        var pctModified = modified > 0 ? Math.Round(totalSupplies / modified * 100m, 2) : 0m;

        return Ok(new ContractSupplySummaryDto(
            totalSupplies,
            contract.Amount,
            modified,
            pctOriginal,
            pctModified));
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.Create)]
    public async Task<IActionResult> Create(string contractId, ContractSupplyItemWriteDto dto, CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([contractId], ct);
        if (contract is null)
            return NotFound(new { success = false, message = "العقد غير موجود" });

        if (string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(new { success = false, message = "وصف الخامة مطلوب" });
        if (string.IsNullOrWhiteSpace(dto.Unit))
            return BadRequest(new { success = false, message = "الوحدة مطلوبة" });
        if (string.IsNullOrWhiteSpace(dto.SupplierName))
            return BadRequest(new { success = false, message = "اسم المورد مطلوب" });
        if (dto.Qty <= 0)
            return BadRequest(new { success = false, message = "الكمية يجب أن تكون أكبر من صفر" });
        if (dto.UnitPrice < 0)
            return BadRequest(new { success = false, message = "سعر الوحدة لا يمكن أن يكون سالباً" });

        var item = new ContractSupplyItem
        {
            Id = Guid.NewGuid().ToString("N"),
            ContractId = contractId,
            ProjectId = contract.ProjectId,
            Description = dto.Description.Trim(),
            Unit = dto.Unit.Trim(),
            Qty = dto.Qty,
            UnitPrice = dto.UnitPrice,
            Total = Math.Round(dto.Qty * dto.UnitPrice, 2),
            SupplierName = dto.SupplierName.Trim(),
            SupplyDate = dto.SupplyDate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ContractSupplyItems.Add(item);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new { contractId },
            new ContractSupplyItemDto(
                item.Id, item.ContractId, item.ProjectId, item.Description, item.Unit,
                item.Qty, item.UnitPrice, item.Total, item.SupplierName, item.SupplyDate,
                item.Note, item.CreatedAt, item.UpdatedAt));
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.Edit)]
    public async Task<IActionResult> Update(string contractId, string id, ContractSupplyItemWriteDto dto, CancellationToken ct)
    {
        var item = await db.ContractSupplyItems
            .FirstOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct);
        if (item is null)
            return NotFound(new { success = false, message = "بند التوريد غير موجود" });

        if (string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(new { success = false, message = "وصف الخامة مطلوب" });
        if (string.IsNullOrWhiteSpace(dto.Unit))
            return BadRequest(new { success = false, message = "الوحدة مطلوبة" });
        if (string.IsNullOrWhiteSpace(dto.SupplierName))
            return BadRequest(new { success = false, message = "اسم المورد مطلوب" });
        if (dto.Qty <= 0)
            return BadRequest(new { success = false, message = "الكمية يجب أن تكون أكبر من صفر" });
        if (dto.UnitPrice < 0)
            return BadRequest(new { success = false, message = "سعر الوحدة لا يمكن أن يكون سالباً" });

        item.Description = dto.Description.Trim();
        item.Unit = dto.Unit.Trim();
        item.Qty = dto.Qty;
        item.UnitPrice = dto.UnitPrice;
        item.Total = Math.Round(dto.Qty * dto.UnitPrice, 2);
        item.SupplierName = dto.SupplierName.Trim();
        item.SupplyDate = dto.SupplyDate;
        item.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionModules.Contracts, PermissionActions.Delete)]
    public async Task<IActionResult> Delete(string contractId, string id, CancellationToken ct)
    {
        var item = await db.ContractSupplyItems
            .FirstOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct);
        if (item is null)
            return NotFound(new { success = false, message = "بند التوريد غير موجود" });

        db.ContractSupplyItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

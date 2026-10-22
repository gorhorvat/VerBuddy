using System.Security.Claims;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Storage for Savepoint, the owner's game-library app. Savepoint's server
/// logs in through /api/auth/login and calls these with the SuperAdmin's
/// token. Every save carries the rev it is based on; a save from an
/// out-of-date copy (another device saved since) gets 409 instead of
/// overwriting newer data.
/// </summary>
[ApiController]
[Route("api/savepoint")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class SavepointController(AppDbContext db) : ControllerBase
{
    private string CallerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("state")]
    public async Task<IActionResult> Get()
    {
        var row = await db.SavepointData.AsNoTracking().SingleOrDefaultAsync(d => d.UserId == CallerId);
        if (row is null)
            return NotFound(new SavepointRevResponse(0, null));

        // The state goes out as stored, without being parsed and re-serialized.
        var savedAt = JsonSerializer.Serialize(DateTime.SpecifyKind(row.SavedAtUtc, DateTimeKind.Utc));
        return Content(
            $"{{\"rev\":{row.Rev},\"savedAt\":{savedAt},\"version\":{row.Version},\"state\":{row.StateJson}}}",
            "application/json");
    }

    /// <summary>Cheap check for "has another device saved since I loaded?".</summary>
    [HttpGet("rev")]
    public async Task<SavepointRevResponse> Rev()
    {
        var row = await db.SavepointData.AsNoTracking()
            .Where(d => d.UserId == CallerId)
            .Select(d => new { d.Rev, d.SavedAtUtc })
            .SingleOrDefaultAsync();
        return row is null
            ? new SavepointRevResponse(0, null)
            : new SavepointRevResponse(row.Rev, DateTime.SpecifyKind(row.SavedAtUtc, DateTimeKind.Utc));
    }

    [HttpPut("state")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<SavepointRevResponse>> Save(SavepointSaveRequest request)
    {
        if (request.State.ValueKind != JsonValueKind.Object
            || !request.State.TryGetProperty("entries", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
            return BadRequest(new { message = "Expected { baseRev, version, state }." });

        var json = request.State.GetRawText();
        var now = DateTime.UtcNow;
        var next = request.BaseRev + 1;

        if (request.BaseRev == 0)
        {
            // First save: insert. Two racing first saves can't both win the primary key.
            db.SavepointData.Add(new SavepointData
            {
                UserId = CallerId, Rev = next, Version = request.Version, SavedAtUtc = now, StateJson = json
            });
            try
            {
                await db.SaveChangesAsync();
                return new SavepointRevResponse(next, now);
            }
            catch (DbUpdateException)
            {
                return Conflict(await Rev());
            }
        }

        // Update only if nobody saved since this copy loaded (atomic in one statement).
        var updated = await db.SavepointData
            .Where(d => d.UserId == CallerId && d.Rev == request.BaseRev)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Rev, next)
                .SetProperty(d => d.Version, request.Version)
                .SetProperty(d => d.SavedAtUtc, now)
                .SetProperty(d => d.StateJson, json));

        if (updated != 1)
            return Conflict(await Rev());
        return new SavepointRevResponse(next, now);
    }
}

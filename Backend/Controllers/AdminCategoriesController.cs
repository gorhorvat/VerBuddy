using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Teacher-defined folders/classes for games and students. Deleting a category
/// never deletes its contents — games move back to "General" and students
/// become unassigned (SetNull FKs).
/// </summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminCategoriesController(AppDbContext db) : ControllerBase
{
    private string TeacherId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> List()
    {
        return await db.Categories
            .Where(c => c.TeacherId == TeacherId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Games.Count, c.Students.Count))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.TeacherId == TeacherId && c.Name == name))
            return Conflict(new { message = $"A category named '{name}' already exists." });

        var category = new Category { Name = name, TeacherId = TeacherId };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new CategoryDto(category.Id, category.Name, 0, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Rename(int id, CategoryRequest request)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == TeacherId);
        if (category is null)
            return NotFound();

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.TeacherId == TeacherId && c.Name == name && c.Id != id))
            return Conflict(new { message = $"A category named '{name}' already exists." });

        category.Name = name;
        await db.SaveChangesAsync();

        return new CategoryDto(category.Id, category.Name,
            await db.GameInstances.CountAsync(g => g.CategoryId == id),
            await db.Users.CountAsync(u => u.CategoryId == id));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == TeacherId);
        if (category is null)
            return NotFound();

        db.Categories.Remove(category); // Games/students are SetNull'd, not deleted.
        await db.SaveChangesAsync();
        return NoContent();
    }
}

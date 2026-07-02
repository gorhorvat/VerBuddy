using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public sealed record CategoryDto(int Id, string Name, int GameCount, int StudentCount);

public sealed record CategoryRequest([Required, MaxLength(100)] string Name);

/// <summary>Moves a game to another category; null files it under "General".</summary>
public sealed record ChangeCategoryRequest(int? CategoryId);

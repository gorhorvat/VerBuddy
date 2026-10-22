using System.Text.Json;

namespace Backend.DTOs;

/// <summary>A save from Savepoint: the rev its copy is based on, and the whole state.</summary>
public record SavepointSaveRequest(int BaseRev, int Version, JsonElement State);

public record SavepointRevResponse(int Rev, DateTime? SavedAt);

namespace MlsmoonSkillManager.Core.Models;

public sealed class MlsmoonSkillFile
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "skill";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Repo { get; set; }
    public string? ParentId { get; set; }
    public List<MlsmoonSkillRoute>? Routes { get; set; }

    public bool IsRouting =>
        Kind.Equals("routing", StringComparison.OrdinalIgnoreCase);

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public sealed class MlsmoonSkillRoute
{
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
}

namespace MlsmoonSkillManager.Core.Models;

public enum CommitRelation
{
    Same = 0,
    LocalBehind,
    LocalAhead,
    Diverged,
    Unknown
}

public sealed record CommitCompare(CommitRelation Relation, int AheadBy, int BehindBy)
{
    public static CommitCompare Same { get; } = new(CommitRelation.Same, 0, 0);

    public static CommitCompare Unknown { get; } = new(CommitRelation.Unknown, 0, 0);

    public static CommitCompare Behind(int behindBy = 0) =>
        new(CommitRelation.LocalBehind, 0, behindBy);

    public static CommitCompare Ahead(int aheadBy = 0) =>
        new(CommitRelation.LocalAhead, aheadBy, 0);

    public static CommitCompare Fork(int aheadBy = 0, int behindBy = 0) =>
        new(CommitRelation.Diverged, aheadBy, behindBy);
}

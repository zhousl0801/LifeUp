using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed record EntryCategoryDto(
    Guid Id,
    LedgerEntryKind Kind,
    string Name,
    int SortOrder,
    bool IsActive,
    string? SystemKey,
    Guid? ParentId = null,
    bool IsGroup = false);

public sealed record CreateCategoryRequest(LedgerEntryKind Kind, string Name, Guid? ParentId = null, bool IsGroup = false);

public sealed record UpdateCategoryRequest(string Name, bool IsActive, Guid? ParentId = null);

public sealed record ReorderCategoriesRequest(LedgerEntryKind Kind, IReadOnlyList<Guid> Ids, Guid? ParentId = null, bool IsGroup = false);

public enum CategoryOperationStatus
{
    Success,
    Created,
    NotFound,
    Invalid,
    Conflict
}

public sealed record CategoryOperationResult(
    CategoryOperationStatus Status,
    EntryCategoryDto? Category,
    IReadOnlyList<string> Errors)
{
    public static CategoryOperationResult Success(EntryCategoryDto category) =>
        new(CategoryOperationStatus.Success, category, []);

    public static CategoryOperationResult Success() =>
        new(CategoryOperationStatus.Success, null, []);

    public static CategoryOperationResult Created(EntryCategoryDto category) =>
        new(CategoryOperationStatus.Created, category, []);

    public static CategoryOperationResult NotFound(string message) =>
        new(CategoryOperationStatus.NotFound, null, [message]);

    public static CategoryOperationResult Invalid(IReadOnlyList<string> errors) =>
        new(CategoryOperationStatus.Invalid, null, errors);

    public static CategoryOperationResult Conflict(string message) =>
        new(CategoryOperationStatus.Conflict, null, [message]);
}

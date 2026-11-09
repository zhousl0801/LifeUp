using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed class CategoryService
{
    private const string SalaryCategoryKey = "salary";

    private readonly ILedgerRepository _ledgerRepository;
    private readonly BusinessDateProvider _dates;

    public CategoryService(ILedgerRepository ledgerRepository, BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _dates = dates;
    }

    public async Task<IReadOnlyList<EntryCategoryDto>?> GetAsync(CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        var categories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);

        return categories.Select(Map).ToList();
    }

    public async Task<CategoryOperationResult> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return CategoryOperationResult.NotFound("账本尚未初始化。");
        }

        string name;

        try
        {
            name = EntryCategory.NormalizeName(request.Name);
        }
        catch (ArgumentException exception)
        {
            return CategoryOperationResult.Invalid([exception.Message]);
        }

        var categories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);

        var parent = request.IsGroup ? null : categories.FirstOrDefault(category =>
            category.IsGroup && category.Kind == request.Kind && category.IsActive
            && (request.ParentId is null || request.ParentId == category.Id));
        if ((request.IsGroup && request.ParentId is not null) || (!request.IsGroup && parent is null))
        {
            return CategoryOperationResult.Invalid(["请选择同类型且启用的一级分类。"]);
        }

        if (categories.Any(category => category.Kind == request.Kind && category.IsGroup == request.IsGroup
            && string.Equals(category.Name, name, StringComparison.Ordinal)))
        {
            return CategoryOperationResult.Conflict("同类型下已存在同名分类。");
        }

        var sortOrder = categories
            .Where(category => category.Kind == request.Kind && category.IsGroup == request.IsGroup && category.ParentId == parent?.Id)
            .Select(category => category.SortOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var now = _dates.UtcNow;
        if (parent is not null && !await TouchParentAsync(ledger.Id, parent.Id, now, cancellationToken))
        {
            return CategoryOperationResult.Conflict("一级分类已变化，请刷新后重试。");
        }

        var created = EntryCategory.Create(ledger.Id, request.Kind, name, sortOrder, null, now, parent?.Id, request.IsGroup);
        await _ledgerRepository.AddCategoryAsync(created, cancellationToken);

        try
        {
            await _ledgerRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CategoryOperationResult.Conflict("分类已变化，请刷新后重试。");
        }

        return CategoryOperationResult.Created(Map(created));
    }

    public async Task<CategoryOperationResult> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return CategoryOperationResult.NotFound("账本尚未初始化。");
        }

        var category = await _ledgerRepository.GetCategoryForUpdateAsync(ledger.Id, categoryId, cancellationToken);

        if (category is null)
        {
            return CategoryOperationResult.NotFound("分类不存在。");
        }

        string name;

        try
        {
            name = EntryCategory.NormalizeName(request.Name);
        }
        catch (ArgumentException exception)
        {
            return CategoryOperationResult.Invalid([exception.Message]);
        }

        if (!request.IsActive && category.SystemKey == SalaryCategoryKey)
        {
            return CategoryOperationResult.Invalid(["工资分类由自动记账使用，不能停用。"]);
        }

        var categories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);

        if (categories.Any(other => other.Id != category.Id
            && other.Kind == category.Kind && other.IsGroup == category.IsGroup
            && string.Equals(other.Name, name, StringComparison.Ordinal)))
        {
            return CategoryOperationResult.Conflict("同类型下已存在同名分类。");
        }

        if (category.IsGroup && (request.ParentId is not null
            || (!request.IsActive && categories.Any(child => child.ParentId == category.Id && child.IsActive))))
        {
            return CategoryOperationResult.Invalid(["请先移动或停用该分组下仍启用的二级分类。"]);
        }

        var parentId = request.ParentId ?? category.ParentId;
        if (!category.IsGroup && (request.IsActive || request.ParentId is not null)
            && !categories.Any(parent => parent.Id == parentId && parent.IsGroup && parent.IsActive && parent.Kind == category.Kind))
        {
            return CategoryOperationResult.Invalid(["请选择同类型且启用的一级分类。"]);
        }

        var previousName = category.Name;
        var now = _dates.UtcNow;
        if (!category.IsGroup && request.IsActive && parentId is Guid activeParent
            && !await TouchParentAsync(ledger.Id, activeParent, now, cancellationToken))
        {
            return CategoryOperationResult.Conflict("一级分类已变化，请刷新后重试。");
        }

        if (!category.IsGroup && parentId is Guid target && target != category.ParentId)
        {
            category.MoveTo(target, now);
            var order = categories.Where(child => child.ParentId == target).Select(child => child.SortOrder).DefaultIfEmpty(-1).Max();
            category.SetSortOrder(order + 1, now);
        }

        category.SetActive(request.IsActive, now);

        if (!string.Equals(previousName, name, StringComparison.Ordinal))
        {
            category.Rename(name, now);
        }

        if (!category.IsGroup && previousName != name)
        {
            await _ledgerRepository.RenameEntryCategoryAsync(
                ledger.Id,
                category.Kind,
                previousName,
                name,
                cancellationToken);
        }

        try
        {
            await _ledgerRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CategoryOperationResult.Conflict("分类已变化，请刷新后重试。");
        }

        return CategoryOperationResult.Success(Map(category));
    }

    public async Task<CategoryOperationResult> DeleteAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return CategoryOperationResult.NotFound("账本尚未初始化。");
        }

        var category = await _ledgerRepository.GetCategoryForUpdateAsync(ledger.Id, categoryId, cancellationToken);

        if (category is null)
        {
            return CategoryOperationResult.NotFound("分类不存在。");
        }

        if (category.SystemKey == SalaryCategoryKey)
        {
            return CategoryOperationResult.Invalid(["工资分类由自动记账使用，不能删除。"]);
        }

        return await UpdateAsync(categoryId, new UpdateCategoryRequest(category.Name, false), cancellationToken);
    }

    public async Task<CategoryOperationResult> ReorderAsync(
        ReorderCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return CategoryOperationResult.NotFound("账本尚未初始化。");
        }

        var allCategories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);
        if (request.ParentId is Guid parentId && (request.IsGroup || !allCategories.Any(category =>
            category.Id == parentId && category.IsGroup && category.Kind == request.Kind)))
        {
            return CategoryOperationResult.Invalid(["排序范围不是有效的一级分类。"]);
        }

        var categories = allCategories
            .Where(category => category.Kind == request.Kind && category.IsGroup == request.IsGroup
                && (request.ParentId is null || category.ParentId == request.ParentId))
            .ToList();

        if (request.Ids.Count != categories.Count
            || request.Ids.Distinct().Count() != request.Ids.Count
            || request.Ids.Any(id => categories.All(category => category.Id != id)))
        {
            return CategoryOperationResult.Invalid(["排序必须包含该类型的全部分类且不能重复。"]);
        }

        var now = _dates.UtcNow;
        var byId = categories.ToDictionary(category => category.Id);

        for (var index = 0; index < request.Ids.Count; index++)
        {
            var category = byId[request.Ids[index]];
            var tracked = await _ledgerRepository.GetCategoryForUpdateAsync(ledger.Id, category.Id, cancellationToken);

            tracked?.SetSortOrder(index, now);
        }

        await _ledgerRepository.SaveChangesAsync(cancellationToken);

        return CategoryOperationResult.Success();
    }

    private async Task<bool> TouchParentAsync(Guid ledgerId, Guid parentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var parent = await _ledgerRepository.GetCategoryForUpdateAsync(ledgerId, parentId, cancellationToken);
        if (parent is not { IsGroup: true, IsActive: true })
        {
            return false;
        }

        parent.SetActive(true, now);
        return true;
    }

    private static EntryCategoryDto Map(EntryCategory category)
    {
        return new EntryCategoryDto(
            category.Id,
            category.Kind,
            category.Name,
            category.SortOrder,
            category.IsActive,
            category.SystemKey,
            category.ParentId,
            category.IsGroup);
    }
}

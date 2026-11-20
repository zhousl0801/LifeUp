using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed class CategoryService
{
    private const string SalaryCategoryKey = "salary";

    // 对应前端随应用发布的 Noto Emoji 资源。
    private static readonly HashSet<string> s_iconKeys = new(StringComparer.Ordinal)
    {
        "1f37d", "1f381", "1f3ae", "1f3e0", "1f3f7", "1f43e",
        "1f455", "1f45f", "1f48a", "1f4b0", "1f4b5", "1f4bc",
        "1f4c1", "1f4da", "1f4e6", "1f4f6", "1f5d3", "1f68c",
        "1f6cb", "1f6cd", "1f6d2", "1f9f3", "1f9f4"
    };

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

        if (request.IconKey is not null && !s_iconKeys.Contains(request.IconKey))
        {
            return CategoryOperationResult.Invalid(["请选择有效的分类图标。"]);
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
        if (request.IconKey is not null)
        {
            created.SetIcon(request.IconKey, now);
        }

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

        if (request.IconKey is not null && !s_iconKeys.Contains(request.IconKey))
        {
            return CategoryOperationResult.Invalid(["请选择有效的分类图标。"]);
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
        if (request.IconKey is not null)
        {
            category.SetIcon(request.IconKey, now);
        }

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
        var parent = allCategories.FirstOrDefault(category => category.Id == request.ParentId);
        if (request.ParentId is not null && (request.IsGroup || parent is not { IsGroup: true } || parent.Kind != request.Kind))
        {
            return CategoryOperationResult.Invalid(["排序范围不是有效的一级分类。"]);
        }

        var categories = allCategories
            .Where(category => category.Kind == request.Kind && category.IsGroup == request.IsGroup
                && (request.ParentId is null || category.ParentId == request.ParentId))
            .ToDictionary(category => category.Id);

        if (request.MovingId is Guid movingId)
        {
            var moving = allCategories.FirstOrDefault(category => category.Id == movingId);
            if (parent is not { IsActive: true } || moving is null || moving.IsGroup || moving.Kind != request.Kind
                || request.SourceParentId is null)
            {
                return CategoryOperationResult.Invalid(["请选择同类型且启用的一级分类。"]);
            }

            if (moving.ParentId != request.SourceParentId && moving.ParentId != parent.Id)
            {
                return CategoryOperationResult.Conflict("分类归属已变化，请刷新后重试。");
            }

            categories.TryAdd(moving.Id, moving);
        }

        if (request.Ids.Count != categories.Count
            || request.Ids.Distinct().Count() != categories.Count
            || request.Ids.Any(id => !categories.ContainsKey(id)))
        {
            return CategoryOperationResult.Invalid(["排序必须包含目标范围内的全部分类且不能重复。"]);
        }

        var now = _dates.UtcNow;
        if (parent is not null)
        {
            // 与同组新增、移动和停用共享一级分类的并发版本。
            var trackedParent = await _ledgerRepository.GetCategoryForUpdateAsync(ledger.Id, parent.Id, cancellationToken);
            if (trackedParent is null || trackedParent.Version != parent.Version)
            {
                return CategoryOperationResult.Conflict("一级分类已变化，请刷新后重试。");
            }

            trackedParent.SetActive(parent.IsActive, now);
        }

        for (var index = 0; index < request.Ids.Count; index++)
        {
            var snapshot = categories[request.Ids[index]];
            var tracked = await _ledgerRepository.GetCategoryForUpdateAsync(ledger.Id, snapshot.Id, cancellationToken);
            if (tracked is null || tracked.Version != snapshot.Version)
            {
                return CategoryOperationResult.Conflict("分类已变化，请刷新后重试。");
            }

            if (tracked.Id == request.MovingId && parent is not null && tracked.ParentId != parent.Id)
            {
                tracked.MoveTo(parent.Id, now);
            }

            tracked.SetSortOrder(index, now);
        }

        try
        {
            await _ledgerRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CategoryOperationResult.Conflict("分类已变化，请刷新后重试。");
        }

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
            category.IsGroup,
            category.IconKey);
    }
}

namespace LifeUp.Domain.Ledgers;

public static class EntryCategoryDefaults
{
    private static readonly string[] s_expenseNames =
    [
        "餐饮",
        "购物",
        "日用",
        "交通",
        "居家",
        "服饰",
        "娱乐",
        "通讯",
        "医疗",
        "学习",
        "运动",
        "旅行",
        "宠物",
        "人情",
        "其他"
    ];

    private static readonly (string Name, string? SystemKey)[] s_incomeNames =
    [
        ("工资", "salary"),
        ("奖金", "bonus"),
        ("其他收入", "other-income")
    ];

    public static IEnumerable<EntryCategory> CreateFor(Guid ledgerId, DateTimeOffset now)
    {
        var categories = s_expenseNames
            .Select((name, index) => EntryCategory.Create(ledgerId, LedgerEntryKind.Expense, name, index, null, now))
            .Concat(s_incomeNames.Select((item, index) => EntryCategory.Create(ledgerId, LedgerEntryKind.Income, item.Name, index, item.SystemKey, now)))
            .ToList();
        Upgrade(ledgerId, categories, [], now);
        return categories;
    }

    public static void Upgrade(
        Guid ledgerId,
        List<EntryCategory> categories,
        IReadOnlyList<LedgerEntry> entries,
        DateTimeOffset now)
    {
        foreach (var (kind, name) in entries.Select(entry => (entry.Kind, entry.Category)).Distinct())
        {
            if (!categories.Any(category => !category.IsGroup && category.Kind == kind && category.Name == name))
            {
                var legacy = EntryCategory.Create(ledgerId, kind, name, categories.Count, null, now);
                legacy.SetActive(false, now);
                categories.Add(legacy);
            }
        }

        foreach (var kind in new[] { LedgerEntryKind.Expense, LedgerEntryKind.Income })
        {
            if (categories.Any(category => category.IsGroup && category.Kind == kind))
            {
                continue;
            }

            var names = kind == LedgerEntryKind.Expense
                ? new[] { "固定", "必要", "弹性", "其他" }
                : new[] { "经常收入", "其他收入" };
            var groups = names.Select((name, index) => EntryCategory.Create(ledgerId, kind, name, index, null, now, isGroup: true)).ToArray();

            foreach (var category in categories.Where(category => !category.IsGroup && category.Kind == kind && category.ParentId is null))
            {
                var index = kind == LedgerEntryKind.Income
                    ? (category.SystemKey == "salary" || category.Name == "工资" ? 0 : 1)
                    : category.Name switch
                    {
                        "房租" or "订阅" => 0,
                        "餐饮" or "日用" or "交通" or "居家" or "服饰" or "通讯" or "医疗" => 1,
                        "娱乐" or "旅行" => 2,
                        _ => 3
                    };
                category.MoveTo(groups[index].Id, now);
            }

            categories.AddRange(groups);
        }

        foreach (var category in categories.Where(category => !category.IsGroup && !category.IsActive && category.ParentId is null))
        {
            var group = categories.FirstOrDefault(item => item.IsGroup && item.Kind == category.Kind);
            if (group is not null)
            {
                category.MoveTo(group.Id, now);
            }
        }
    }
}

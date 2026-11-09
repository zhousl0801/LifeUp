namespace LifeUp.Application.History;

public class HistoryValidationException : Exception
{
    public HistoryValidationException(string message) : base(message)
    {
    }
}

/// <summary>
/// 同一账本内出现重复的业务顺序号。并发写入时可能瞬时发生，调用方可以重新读取账本后重试。
/// </summary>
public sealed class DuplicateFactSequenceException : HistoryValidationException
{
    public DuplicateFactSequenceException(string message) : base(message)
    {
    }
}

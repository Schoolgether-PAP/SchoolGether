namespace SchoolGether.Domain.Support;

public sealed class SupportRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

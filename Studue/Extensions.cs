namespace Studue;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class StudentOptionalAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class StudentRequiredAttribute : Attribute
{
    public bool RequireWriteAccess { get; init; }
}
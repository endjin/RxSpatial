using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RxSpatial.Tests.Golden;

/// <summary>
/// Runs a test only when an environment variable has a given value; otherwise the test is
/// reported as skipped. Used for tests with side effects, such as writing golden files.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class EnvironmentVariableConditionAttribute : ConditionBaseAttribute
{
    public EnvironmentVariableConditionAttribute(string name, string value)
        : base(ConditionMode.Include)
    {
        Name = name;
        Value = value;
        IgnoreMessage = $"Set {name}={value} to run this test.";
    }

    public string Name { get; }

    public string Value { get; }

    public override bool IsConditionMet => Environment.GetEnvironmentVariable(Name) == Value;

    public override string GroupName => nameof(EnvironmentVariableConditionAttribute);
}

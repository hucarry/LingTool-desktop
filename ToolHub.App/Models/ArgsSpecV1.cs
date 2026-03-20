namespace ToolHub.App.Models;

public sealed class ArgsSpecV1
{
    public int Version { get; set; } = 1;

    public List<ArgFieldSpec> Fields { get; set; } = new();

    public List<ArgTokenSpec> Argv { get; set; } = new();
}

public sealed class ArgFieldSpec
{
    public string Name { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string? Description { get; set; }

    public string Kind { get; set; } = "text";

    public bool Required { get; set; }

    public string? DefaultValue { get; set; }

    public string? Placeholder { get; set; }

    public List<ArgFieldOption> Options { get; set; } = new();
}

public sealed class ArgFieldOption
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class ArgTokenSpec
{
    public string Kind { get; set; } = "literal";

    public string? Value { get; set; }

    public string? Field { get; set; }

    public string? Prefix { get; set; }

    public string? Suffix { get; set; }

    public bool OmitWhenEmpty { get; set; } = true;

    public string? WhenTrue { get; set; }

    public string? WhenFalse { get; set; }
}

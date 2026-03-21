using System.Collections.Generic;

#nullable enable

internal static partial class HostRegressionTests
{
    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertNull(object? value, string message)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}Actual: {value}");
        }
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }
}

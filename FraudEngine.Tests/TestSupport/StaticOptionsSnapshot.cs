using Microsoft.Extensions.Options;

namespace FraudEngine.Tests.TestSupport;

/// <summary>Wraps a fixed value as <see cref="IOptionsSnapshot{T}"/> so services can be unit-tested without the DI options infrastructure.</summary>
internal sealed class StaticOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
{
    public T Value { get; } = value;

    public T Get(string? name) => Value;
}

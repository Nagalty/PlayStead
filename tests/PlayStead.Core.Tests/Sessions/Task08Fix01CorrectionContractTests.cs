using System.Reflection;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class Task08Fix01CorrectionContractTests
{
    private static readonly Guid SessionId =
        Guid.Parse("81818181-8181-4181-8181-818181818181");

    private static readonly Guid CorrectionId =
        Guid.Parse("82828282-8282-4282-8282-828282828282");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SessionCorrection_exposes_traceable_identity_reason_and_creation_time()
    {
        var type = typeof(SessionCorrection);

        AssertProperty<Guid>(type, "CorrectionId");
        AssertProperty<Guid>(type, "SessionId");
        AssertProperty<DateTimeOffset?>(type, "CorrectedStartedAtUtc");
        AssertProperty<DateTimeOffset?>(type, "CorrectedEndedAtUtc");
        AssertProperty<string>(type, "Reason");
        AssertProperty<DateTimeOffset>(type, "CreatedAtUtc");
    }

    [Fact]
    public void SessionCorrectionRequest_carries_session_identity_and_optional_reason()
    {
        var type = typeof(SessionCorrectionRequest);

        AssertProperty<Guid>(type, "SessionId");
        AssertProperty<DateTimeOffset?>(type, "CorrectedStartedAtUtc");
        AssertProperty<DateTimeOffset?>(type, "CorrectedEndedAtUtc");
        AssertProperty<string>(type, "Reason");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SessionCorrectionRequest_preserves_optional_reason(string? reason)
    {
        var request = CreateRequest(
            SessionId,
            T0.AddMinutes(5),
            T0.AddMinutes(65),
            reason);

        Assert.Equal(SessionId, Read<Guid>(request, "SessionId"));
        Assert.Equal(reason, Read<string?>(request, "Reason"));
    }

    [Fact]
    public void SessionCorrection_constructor_can_preserve_full_traceable_contract()
    {
        var correction = CreateCorrection(
            CorrectionId,
            SessionId,
            T0.AddMinutes(5),
            T0.AddMinutes(65),
            "Horaires corrigés après vérification",
            T0.AddHours(2));

        Assert.Equal(CorrectionId, Read<Guid>(correction, "CorrectionId"));
        Assert.Equal(SessionId, Read<Guid>(correction, "SessionId"));
        Assert.Equal(
            "Horaires corrigés après vérification",
            Read<string?>(correction, "Reason"));
        Assert.Equal(
            T0.AddHours(2),
            Read<DateTimeOffset>(correction, "CreatedAtUtc"));
    }

    private static void AssertProperty<T>(Type type, string propertyName)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(typeof(T), property!.PropertyType);
    }

    private static SessionCorrectionRequest CreateRequest(
        Guid sessionId,
        DateTimeOffset? correctedStartedAtUtc,
        DateTimeOffset? correctedEndedAtUtc,
        string? reason)
    {
        return CreateByConstructor<SessionCorrectionRequest>(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sessionId"] = sessionId,
                ["correctedStartedAtUtc"] = correctedStartedAtUtc,
                ["correctedEndedAtUtc"] = correctedEndedAtUtc,
                ["reason"] = reason
            });
    }

    private static SessionCorrection CreateCorrection(
        Guid correctionId,
        Guid sessionId,
        DateTimeOffset? correctedStartedAtUtc,
        DateTimeOffset? correctedEndedAtUtc,
        string? reason,
        DateTimeOffset createdAtUtc)
    {
        return CreateByConstructor<SessionCorrection>(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["correctionId"] = correctionId,
                ["sessionId"] = sessionId,
                ["correctedStartedAtUtc"] = correctedStartedAtUtc,
                ["correctedEndedAtUtc"] = correctedEndedAtUtc,
                ["reason"] = reason,
                ["createdAtUtc"] = createdAtUtc,
                ["correctedAtUtc"] = createdAtUtc
            });
    }

    private static T CreateByConstructor<T>(
        IReadOnlyDictionary<string, object?> values)
    {
        var constructor = typeof(T)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        Assert.NotNull(constructor);

        var arguments = constructor!
            .GetParameters()
            .Select(parameter =>
            {
                if (parameter.Name is not null &&
                    values.TryGetValue(parameter.Name, out var value))
                {
                    return value;
                }

                if (parameter.HasDefaultValue)
                {
                    return parameter.DefaultValue;
                }

                throw new Xunit.Sdk.XunitException(
                    $"No test value was supplied for constructor parameter '{parameter.Name}' on {typeof(T).Name}.");
            })
            .ToArray();

        return Assert.IsType<T>(constructor.Invoke(arguments));
    }

    private static T Read<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        var value = property!.GetValue(target);

        if (value is null)
        {
            return default!;
        }

        return Assert.IsType<T>(value);
    }
}

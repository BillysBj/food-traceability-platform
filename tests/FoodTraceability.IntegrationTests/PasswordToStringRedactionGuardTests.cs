using System.Reflection;
using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Identity.Infrastructure;
using ApiLoginRequest = FoodTraceability.Api.Contracts.Authentication.LoginRequest;
using ApiCreateUserRequest = FoodTraceability.Api.Contracts.Users.CreateUserRequest;
using ApplicationLoginRequest = FoodTraceability.Modules.Identity.Application.Authentication.LoginRequest;
using BootstrapPlatformAdministratorCommand = FoodTraceability.Modules.Identity.Application.Bootstrap.BootstrapPlatformAdministratorCommand;
using CreateUserCommand = FoodTraceability.Modules.Identity.Application.Users.CreateUserCommand;

namespace FoodTraceability.IntegrationTests;

public sealed class PasswordToStringRedactionGuardTests
{
    private const string PlaintextPasswordSentinel =
        "sentinel-plaintext-secret-4f2a9c";

    private static readonly Assembly[] Assemblies =
    [
        typeof(ApiLoginRequest).Assembly,
        typeof(ApplicationLoginRequest).Assembly,
        typeof(PasswordPolicy).Assembly,
        typeof(IdentityDbContext).Assembly,
        typeof(MicrosecondTimeProvider).Assembly
    ];

    private static readonly Type[] RequiredTypes =
    [
        typeof(BootstrapPlatformAdministratorCommand),
        typeof(CreateUserCommand),
        typeof(ApiCreateUserRequest),
        typeof(ApiLoginRequest),
        typeof(ApplicationLoginRequest)
    ];

    [Fact]
    public void PlaintextPasswordPropertiesAreNotExposedByToString()
    {
        var candidateTypes = Assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => GetPlaintextPasswordProperties(type).Length > 0)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var checkedTypes = new List<Type>();
        var untestedTypes = new List<string>();
        var leakingTypes = new List<string>();

        foreach (var candidateType in candidateTypes)
        {
            CheckType(candidateType, checkedTypes, untestedTypes, leakingTypes);
        }

        Assert.True(
            untestedTypes.Count == 0,
            "Types with plaintext password properties could not be tested:" +
            Environment.NewLine + string.Join(Environment.NewLine, untestedTypes));

        var missingRequiredTypes = RequiredTypes
            .Except(checkedTypes)
            .Select(GetTypeName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missingRequiredTypes.Length == 0,
            "Required plaintext-password types were not checked:" +
            Environment.NewLine + string.Join(Environment.NewLine, missingRequiredTypes));
        Assert.True(
            leakingTypes.Count == 0,
            "ToString() exposed a plaintext password for:" +
            Environment.NewLine + string.Join(Environment.NewLine, leakingTypes));
    }

    private static void CheckType(
        Type candidateType,
        ICollection<Type> checkedTypes,
        ICollection<string> untestedTypes,
        ICollection<string> leakingTypes)
    {
        if (candidateType.IsAbstract)
        {
            untestedTypes.Add($"{GetTypeName(candidateType)}: type is abstract");
            return;
        }

        if (candidateType.ContainsGenericParameters)
        {
            untestedTypes.Add($"{GetTypeName(candidateType)}: type has open generic parameters");
            return;
        }

        var constructor = candidateType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
        {
            untestedTypes.Add($"{GetTypeName(candidateType)}: no public constructor");
            return;
        }

        object instance;

        try
        {
            var plaintextPasswordProperties = GetPlaintextPasswordProperties(candidateType);
            var arguments = constructor
                .GetParameters()
                .Select(CreateArgument)
                .ToArray();

            instance = constructor.Invoke(arguments);

            foreach (var property in GetPublicStringProperties(candidateType)
                .Where(property => !IsPlaintextPasswordProperty(property)))
            {
                if (!string.Equals(
                    property.GetValue(instance) as string,
                    PlaintextPasswordSentinel,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (property.GetSetMethod(nonPublic: true) is null)
                {
                    untestedTypes.Add(
                        $"{GetTypeName(candidateType)}: non-password property " +
                        $"{property.Name} retained the sentinel and has no setter");
                    return;
                }

                property.SetValue(instance, null);
            }

            foreach (var property in plaintextPasswordProperties)
            {
                if (!string.Equals(
                    property.GetValue(instance) as string,
                    PlaintextPasswordSentinel,
                    StringComparison.Ordinal))
                {
                    property.SetValue(instance, PlaintextPasswordSentinel);
                }
            }
        }
        catch (Exception exception)
        {
            untestedTypes.Add(
                $"{GetTypeName(candidateType)}: construction failed with " +
                exception.GetBaseException().GetType().FullName);
            return;
        }

        string result;

        try
        {
            result = instance.ToString() ?? string.Empty;
        }
        catch (Exception exception)
        {
            untestedTypes.Add(
                $"{GetTypeName(candidateType)}: ToString() failed with " +
                exception.GetBaseException().GetType().FullName);
            return;
        }

        checkedTypes.Add(candidateType);

        if (result.Contains(PlaintextPasswordSentinel, StringComparison.Ordinal))
        {
            leakingTypes.Add(GetTypeName(candidateType));
        }
    }

    private static object? CreateArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType == typeof(string))
        {
            return PlaintextPasswordSentinel;
        }

        return parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : null;
    }

    private static PropertyInfo[] GetPlaintextPasswordProperties(Type type)
    {
        return GetPublicStringProperties(type)
            .Where(IsPlaintextPasswordProperty)
            .ToArray();
    }

    private static PropertyInfo[] GetPublicStringProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();
    }

    private static bool IsPlaintextPasswordProperty(PropertyInfo property)
    {
        return property.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !property.Name.Contains("hash", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTypeName(Type type) => type.FullName ?? type.Name;
}

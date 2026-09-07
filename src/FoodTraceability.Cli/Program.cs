using System.Text;
using FoodTraceability.Modules.Identity.Application.Bootstrap;
using FoodTraceability.Modules.Identity.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.Cli;

internal static class Program
{
    private const string BootstrapCommandName = "bootstrap-platform-admin";
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    public static async Task<int> Main(string[] args)
    {
        if (!TryParseBootstrapCommand(args, out var options))
        {
            WriteUsage();
            return FailureExitCode;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine(
                "Bootstrap requires an interactive terminal; redirected input is not supported.");
            return FailureExitCode;
        }

        var builder = Host.CreateApplicationBuilder();
        if (string.IsNullOrWhiteSpace(
            builder.Configuration.GetConnectionString("FoodTraceability")))
        {
            Console.Error.WriteLine(
                "ConnectionStrings:FoodTraceability must be configured before bootstrap.");
            return FailureExitCode;
        }

        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            Console.Write("Password: ");
            var password = ReadPassword(cancellationSource.Token);
            Console.Write("Confirm password: ");
            var confirmation = ReadPassword(cancellationSource.Token);

            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "Password entries do not match. Bootstrap was not performed.");
                return FailureExitCode;
            }

            cancellationSource.Token.ThrowIfCancellationRequested();
            builder.Services.AddIdentityAuthentication(builder.Configuration);

            using var host = builder.Build();
            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<BootstrapPlatformAdministratorService>();
            var result = await service.BootstrapAsync(
                new BootstrapPlatformAdministratorCommand(
                    options.Email,
                    options.FirstName,
                    options.LastName,
                    password),
                cancellationSource.Token);

            Console.WriteLine($"Platform administrator created. User ID: {result.UserId}");
            Console.WriteLine($"Email: {result.Email}");
            return SuccessExitCode;
        }
        catch (PlatformAdministratorAlreadyExistsException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return FailureExitCode;
        }
        catch (BootstrapValidationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return FailureExitCode;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Bootstrap was canceled.");
            return FailureExitCode;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Bootstrap failed due to an unexpected error.");
            return FailureExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static string ReadPassword(CancellationToken cancellationToken)
    {
        var password = new StringBuilder();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Console.ReadKey(intercept: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (key.KeyChar != '\0')
            {
                password.Append(key.KeyChar);
            }
        }
    }

    private static bool TryParseBootstrapCommand(
        string[] args,
        out BootstrapCommandOptions options)
    {
        options = new BootstrapCommandOptions(string.Empty, string.Empty, string.Empty);
        if (args.Length != 7
            || !string.Equals(args[0], BootstrapCommandName, StringComparison.Ordinal))
        {
            return false;
        }

        string? email = null;
        string? firstName = null;
        string? lastName = null;

        for (var index = 1; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            switch (args[index])
            {
                case "--email" when email is null:
                    email = value;
                    break;
                case "--first-name" when firstName is null:
                    firstName = value;
                    break;
                case "--last-name" when lastName is null:
                    lastName = value;
                    break;
                default:
                    return false;
            }
        }

        if (email is null || firstName is null || lastName is null)
        {
            return false;
        }

        options = new BootstrapCommandOptions(email, firstName, lastName);
        return true;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: FoodTraceability.Cli bootstrap-platform-admin "
            + "--email <address> --first-name <first-name> --last-name <last-name>");
    }

    private sealed record BootstrapCommandOptions(
        string Email,
        string FirstName,
        string LastName);
}

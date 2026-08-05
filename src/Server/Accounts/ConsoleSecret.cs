namespace Vtt.Server.Accounts;

/// <summary>
/// Reads a secret from the terminal without echoing it.
/// </summary>
/// <remarks>
/// A password must never be a command-line argument: arguments are visible in the process list to
/// every user on the machine, and they persist in shell history long after the command is forgotten.
/// Prompting is what keeps the password out of both.
/// </remarks>
internal static class ConsoleSecret
{
    public static string? Read(string prompt)
    {
        Console.Write(prompt);

        // Console.ReadKey needs a real terminal. `docker compose exec -it` provides one; a piped or
        // redirected stdin does not, and there it throws rather than returning anything useful.
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var secret = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return secret.ToString();

                case ConsoleKey.Backspace when secret.Length > 0:
                    secret.Length--;
                    break;

                case ConsoleKey.Backspace:
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        secret.Append(key.KeyChar);
                    }

                    break;
            }
        }
    }
}

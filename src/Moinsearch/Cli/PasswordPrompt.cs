using System.Text;

namespace Moinsearch.Cli;

internal static class PasswordPrompt
{
    public static string Read()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException("auth set は対話コンソールから実行してください。");
        }

        Console.Write("Wikiパスワード: ");
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    var last = password[password.Length - 1];
                    password.Length--;
                    if (char.IsLowSurrogate(last) &&
                        password.Length > 0 &&
                        char.IsHighSurrogate(password[password.Length - 1]))
                    {
                        password.Length--;
                    }
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}

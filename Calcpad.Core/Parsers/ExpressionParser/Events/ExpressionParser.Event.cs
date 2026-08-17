using System;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        private bool TryParseProgress(ReadOnlySpan<char> expression)
        {
            if (!TryGetProgressArguments(expression, out var valueExpression, out var message))
                return false;

            if (_calculate && _isVal > -1)
            {
                _parser.Parse(valueExpression, false);
                ProgressChanged?.Invoke(this, new ProgressEventArgs(GetProgressValue(), message));
            }
            return true;
        }

        private double GetProgressValue()
        {
            var value = _parser.CalculateReal();
            if (_parser.Units is not null)
                throw Exceptions.MustBeReal(Exceptions.Items.Argument);

            if (double.IsNaN(value) || double.IsInfinity(value))
                throw Exceptions.ArgumentOutOfRange("progress");

            return value;
        }

        private static bool TryGetProgressArguments(
            ReadOnlySpan<char> expression,
            out ReadOnlySpan<char> valueExpression,
            out string message
        )
        {
            valueExpression = default;
            message = string.Empty;

            ReadOnlySpan<char> s = expression.Trim();
            const string name = "progress";
            if (!s.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return false;

            var rest = s[name.Length..].TrimStart();
            if (rest.IsEmpty || rest[0] != '(')
                return false;

            var closingBracket = FindClosingBracket(rest);
            if (closingBracket != rest.Length - 1)
                return false;

            var args = rest[1..closingBracket].Trim();
            if (args.IsEmpty)
                throw Exceptions.InvalidNumberOfArguments();

            var separator = IndexOfTopLevelSeparator(args);
            if (separator < 0)
                valueExpression = args.Trim();
            else
            {
                valueExpression = args[..separator].Trim();
                var messageSpan = args[(separator + 1)..].Trim();
                if (IndexOfTopLevelSeparator(messageSpan) >= 0)
                    throw Exceptions.InvalidNumberOfArguments();

                message = UnquoteMessage(messageSpan);
            }

            if (valueExpression.IsEmpty)
                throw Exceptions.InvalidNumberOfArguments();

            return true;
        }

        private static int FindClosingBracket(ReadOnlySpan<char> s)
        {
            var depth = 0;
            var quote = '\0';
            for (int i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        if (i + 1 < s.Length && s[i + 1] == quote)
                            ++i;
                        else
                            quote = '\0';
                    }
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    quote = c;
                    continue;
                }

                if (c == '(')
                    ++depth;
                else if (c == ')')
                {
                    --depth;
                    if (depth == 0)
                        return i;
                    if (depth < 0)
                        return -1;
                }
            }
            return -1;
        }

        private static int IndexOfTopLevelSeparator(ReadOnlySpan<char> s)
        {
            var roundDepth = 0;
            var squareDepth = 0;
            var curlyDepth = 0;
            var quote = '\0';
            for (int i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        if (i + 1 < s.Length && s[i + 1] == quote)
                            ++i;
                        else
                            quote = '\0';
                    }
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    quote = c;
                    continue;
                }

                switch (c)
                {
                    case '(':
                        ++roundDepth;
                        break;
                    case ')':
                        --roundDepth;
                        break;
                    case '[':
                        ++squareDepth;
                        break;
                    case ']':
                        --squareDepth;
                        break;
                    case '{':
                        ++curlyDepth;
                        break;
                    case '}':
                        --curlyDepth;
                        break;
                    case ';' when roundDepth == 0 && squareDepth == 0 && curlyDepth == 0:
                        return i;
                }
            }
            return -1;
        }

        private static string UnquoteMessage(ReadOnlySpan<char> message)
        {
            if (
                message.Length > 1
                && (message[0] == '\'' || message[0] == '"')
                && message[^1] == message[0]
            )
            {
                var quote = message[0];
                return message[1..^1].ToString().Replace(new string(quote, 2), quote.ToString());
            }
            return message.ToString();
        }
    }
}

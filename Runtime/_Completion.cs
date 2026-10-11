using System;
using System.Collections.Generic;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.Linq;

namespace _CMD_
{
    public readonly struct CompletionResult
    {
        public readonly string Text;
        public readonly int Start, End;
        public readonly string[] Suggestions;

        public CompletionResult(string text, int start, int end, string[] suggestions)
        {
            Text = text;
            Start = start;
            End = end;
            Suggestions = suggestions;
        }
    }

    partial class Shell
    {
        IEnumerable<CompletionItem> GetCompletionItems(string text, int cursor)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (cursor < 0 || cursor > text.Length)
                throw new ArgumentOutOfRangeException(nameof(cursor));

            // Using parsed tokens avoids raw-text completion failures with quotes and inline option values.
            var arguments = Parse(text[..cursor]).Tokens.Select(token => token.Value).ToList();
            if (cursor > 0 && char.IsWhiteSpace(text[cursor - 1]))
                arguments.Add(string.Empty);

            var parse = Root.Parse(arguments.ToArray());
            var last = parse.Tokens.LastOrDefault();
            if (last?.Type == TokenType.Argument)
            {
                var option = parse.CommandResult.Children.OfType<OptionResult>().LastOrDefault(result => result.Tokens.Contains(last));
                if (option != null)
                    return option.Option.GetCompletions(parse.GetCompletionContext());
            }

            return parse.GetCompletions();
        }

        public CompletionResult Complete(string text, int cursor)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (cursor < 0 || cursor > text.Length)
                throw new ArgumentOutOfRangeException(nameof(cursor));

            int start = 0, end = text.Length;
            char quote = '\0';

            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';
                }
                else if (c == '"' || c == '\'')
                    quote = c;
                else if (char.IsWhiteSpace(c))
                {
                    if (i < cursor)
                        start = i + 1;
                    else
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (start < end && text[start] == '-')
            {
                int equals = text.IndexOf('=', start, end - start);
                if (equals >= 0 && cursor > equals)
                    start = equals + 1;
            }

            bool quoted = start < end && (text[start] == '"' || text[start] == '\'');
            if (quoted)
            {
                char opening = text[start++];
                if (end > start && text[end - 1] == opening)
                    --end;
            }

            string[] suggestions = GetCompletions(text, cursor).Select(item => item.InsertText).Where(value => !string.IsNullOrEmpty(value)).Select(value => !quoted && value.Any(char.IsWhiteSpace) ? '"' + value.Replace("\"", "\\\"") + '"' : value).Distinct(StringComparer.Ordinal).OrderBy(value => value.Length).ThenBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            return new CompletionResult(text, start, end, suggestions);
        }
    }
}

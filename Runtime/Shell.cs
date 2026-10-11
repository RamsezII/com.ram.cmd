using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.IO;

namespace _CMD_
{
    public sealed partial class Shell
    {
        public readonly RootCommand Root;
        public readonly InvocationConfiguration Configuration;
        public string WorkingDirectory = Environment.CurrentDirectory;
        public bool IsExecuting { get; private set; }

        //--------------------------------------------------------------------------------------------------------------

        public Shell(string description = "", TextWriter output = null, TextWriter error = null)
        {
            Root = new RootCommand(description);

            Configuration = new InvocationConfiguration
            {
                Output = output ?? Console.Out,
                Error = error ?? Console.Error,
                EnableDefaultExceptionHandler = false,
                ProcessTerminationTimeout = null
            };

            Scan();
        }

        //--------------------------------------------------------------------------------------------------------------

        // Parse without executing, including incomplete input for diagnostics and completion.
        public ParseResult Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return Root.Parse(text);
        }

        public int Execute(string text)
        {
            if (IsExecuting)
                throw new InvalidOperationException("This shell is already executing a command.");

            IsExecuting = true;
            try
            {
                return Parse(text).Invoke(Configuration);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public IEnumerable<CompletionItem> GetCompletions(string text, int? position = null) => GetCompletionItems(text, position ?? text?.Length ?? 0);

        public void WriteLine(object value) => Configuration.Output.WriteLine(value);

        //--------------------------------------------------------------------------------------------------------------

        public void Clear()
        {
            Root.Subcommands.Clear();
        }
    }
}

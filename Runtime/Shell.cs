using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace _CMD_
{
    public sealed partial class Shell
    {
        public RootCommand Root { get; }
        public InvocationConfiguration Configuration { get; }

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
        }

        //--------------------------------------------------------------------------------------------------------------

        // Parse without executing, including incomplete input for diagnostics and completion.
        public ParseResult Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return Root.Parse(text);
        }

        // Use ExecuteAsync for asynchronous actions so the Unity thread is not blocked.
        public int Execute(string text) => Parse(text).Invoke(Configuration);

        public int ExecuteSync(string text) => ExecuteAsync(text).GetAwaiter().GetResult();

        public Task<int> ExecuteAsync(string text, CancellationToken cancellationToken = default) => Parse(text).InvokeAsync(Configuration, cancellationToken);

        public IEnumerable<CompletionItem> GetCompletions(string text, int? position = null) => Parse(text).GetCompletions(position);
    }
}

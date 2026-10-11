using System;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Linq;

namespace _CMD_
{
    partial class Shell
    {
        void RegisterManCommand()
        {
            var topic = new Argument<string[]>("commande")
            {
                Description = "Commande à documenter ; sans argument, afficher l'aide générale.",
                Arity = ArgumentArity.ZeroOrMore,
            };
            topic.CompletionSources.Add(_ => Root.Subcommands.Where(command => !command.Hidden).SelectMany(command => new[] { command.Name }.Concat(command.Aliases)).Select(name => new CompletionItem(name)));

            var man = new Command("man", "Afficher l'aide générale ou celle d'une commande.");
            man.Arguments.Add(topic);
            man.SetAction(parse =>
            {
                string[] names = parse.GetValue(topic) ?? Array.Empty<string>();
                Command command = Root;
                foreach (string name in names)
                {
                    var child = command.Subcommands.FirstOrDefault(candidate => candidate.Name == name || candidate.Aliases.Contains(name));
                    if (child == null)
                    {
                        Configuration.Error.WriteLine($"Commande inconnue : {string.Join(" ", names)}");
                        return 1;
                    }
                    command = child;
                }

                // Invoke the native help action directly, keeping the current shell execution active.
                return Root.Parse(names.Append("--help").ToArray()).Invoke(Configuration);
            });
            Root.Subcommands.Add(man);
        }
    }
}

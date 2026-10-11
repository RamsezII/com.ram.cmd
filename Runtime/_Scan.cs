using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using _ARK_;

namespace _CMD_
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class Cmd : Attribute
    {
        public string french, english;

        internal string Description => Traductable.language._value == Languages.French ? french ?? english ?? "" : english ?? french ?? "";
    }

    public interface ICompletable
    {
        IEnumerable<string> GetCompletions();
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class CmdArg : Attribute
    {
        public readonly string[] completions = Array.Empty<string>();
        public readonly Type completionType;
        public CmdArg(params string[] completions) => this.completions = completions ?? Array.Empty<string>();
        public CmdArg(Type completionType) => this.completionType = completionType ?? throw new ArgumentNullException(nameof(completionType));
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class CmdOpt : Attribute
    {
        public readonly char shortName;
        public CmdOpt(char shortName = '\0') => this.shortName = shortName;
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class CmdFlag : Attribute
    {
        public readonly char shortName;
        public CmdFlag(char shortName = '\0') => this.shortName = shortName;
    }

    partial class Shell
    {
        void Scan()
        {
            RegisterManCommand();

            Assembly cmdAssembly = typeof(Cmd).Assembly;
            string cmdAssemblyName = cmdAssembly.GetName().Name;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || (assembly != cmdAssembly && !assembly.GetReferencedAssemblies().Any(reference => reference.Name == cmdAssemblyName)))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.ContainsGenericParameters)
                        continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (method.IsDefined(typeof(Cmd), false))
                            AddCommand(method);
                }
            }
        }

        public void AddCommand(Delegate action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            AddCommand(action.Method);
        }

        void AddCommand(MethodInfo method)
        {
            var metadata = method.GetCustomAttribute<Cmd>();
            if (metadata == null)
                throw new ArgumentException($"{method.Name} must have a [Cmd] attribute.", nameof(method));
            if (!method.IsStatic || method.ContainsGenericParameters)
                throw new InvalidOperationException($"{method.DeclaringType.FullName}.{method.Name} must be a non-generic static method.");
            Type returnType = method.ReturnType;
            bool returnsTask = typeof(Task).IsAssignableFrom(returnType);
            bool returnsValueTask = returnType == typeof(ValueTask) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
            if (method.IsDefined(typeof(AsyncStateMachineAttribute), false) || returnsTask || returnsValueTask)
                throw new InvalidOperationException($"{method.Name} must execute synchronously.");

            if (Root.Subcommands.Any(command => command.Name == method.Name || command.Aliases.Contains(method.Name)))
                throw new InvalidOperationException($"Duplicate command name: '{method.Name}'.");

            var command = new Command(method.Name, metadata.Description);
            var bindings = new List<Func<ParseResult, object>>();
            MethodInfo createBinding = typeof(Shell).GetMethod(nameof(CreateParameterBinding), BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                var argument = parameter.GetCustomAttribute<CmdArg>();
                var option = parameter.GetCustomAttribute<CmdOpt>();
                var flag = parameter.GetCustomAttribute<CmdFlag>();
                int attributes = (argument != null ? 1 : 0) + (option != null ? 1 : 0) + (flag != null ? 1 : 0);
                if (attributes != 1 || parameter.ParameterType.IsByRef)
                    throw new InvalidOperationException($"{method.Name}.{parameter.Name} needs one command parameter attribute and cannot be passed by reference.");
                if (flag != null && parameter.ParameterType != typeof(bool))
                    throw new InvalidOperationException($"[CmdFlag] requires a bool parameter: {method.Name}.{parameter.Name}.");

                var binding = (Func<ParseResult, object>)createBinding.MakeGenericMethod(parameter.ParameterType).Invoke(this, new object[] { command, parameter, argument != null, option?.shortName ?? flag?.shortName ?? '\0', flag != null });
                bindings.Add(binding);
            }

            command.SetAction(parse =>
            {
                try
                {
                    object result = method.Invoke(null, bindings.Select(bind => bind(parse)).ToArray());
                    if (returnType == typeof(int))
                        return (int)result;
                    if (result != null)
                        Configuration.Output.WriteLine(result);
                    return 0;
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            });
            Root.Subcommands.Add(command);
        }

        Func<ParseResult, object> CreateParameterBinding<T>(Command command, ParameterInfo parameter, bool positional, char shortName, bool flag)
        {
            if (positional)
            {
                var argument = new Argument<T>(parameter.Name) { Arity = ArgumentArity.ExactlyOne };
                var metadata = parameter.GetCustomAttribute<CmdArg>();
                Func<IEnumerable<string>> complete = null;
                if (metadata.completionType != null)
                {
                    Type type = metadata.completionType;
                    if (type.IsEnum)
                        complete = () => Enum.GetNames(type);
                    else
                    {
                        if (!typeof(ICompletable).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                            throw new InvalidOperationException($"Completion type for {command.Name}.{parameter.Name} must be an enum or a concrete ICompletable type.");
                        // Keep the provider for this shell; read its current suggestions on every request.
                        var provider = (ICompletable)Activator.CreateInstance(type, nonPublic: true);
                        complete = provider.GetCompletions;
                    }
                }
                else if (metadata.completions.Length > 0)
                    complete = () => metadata.completions;
                if (complete != null)
                    argument.CompletionSources.Add(_ => complete().Select(value => new CompletionItem(value)));
                command.Arguments.Add(argument);
                return parse => parse.GetValue(argument);
            }

            T initialValue = !flag && parameter.HasDefaultValue ? (T)parameter.DefaultValue : default;
            var option = new Option<T>("--" + parameter.Name)
            {
                Arity = flag ? ArgumentArity.Zero : ArgumentArity.ExactlyOne,
                DefaultValueFactory = _ => initialValue,
            };
            if (shortName != '\0')
            {
                if (!char.IsLetterOrDigit(shortName))
                    throw new InvalidOperationException($"Invalid short option name: '{shortName}'.");
                option.Aliases.Add("-" + shortName);
            }
            if (command.Options.Any(existing => existing.Name == option.Name || existing.Aliases.Any(alias => option.Aliases.Contains(alias))))
                throw new InvalidOperationException($"Duplicate option name or alias in command '{command.Name}': {option.Name}.");

            command.Options.Add(option);
            return parse => parse.GetValue(option);
        }
    }
}

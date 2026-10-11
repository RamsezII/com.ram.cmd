using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using _ARK_;

namespace _CMD_
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class Cmd : Attribute
    {
        public string french, english;

        internal string Description => Traductable.language._value == Languages.French ? french ?? english ?? "" : english ?? french ?? "";
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CmdArg : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CmdOpt : Attribute
    {
        public readonly char shortName;
        public CmdOpt(char shortName = '\0') => this.shortName = shortName;
    }

    [AttributeUsage(AttributeTargets.Field)]
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
                    if (type != null && type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters && type.IsDefined(typeof(Cmd), false))
                        AddCommand(Activator.CreateInstance(type, nonPublic: true));
                }
            }
        }

        // Also accepts an existing instance when registering a command explicitly.
        public void AddCommand(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();
            var metadata = type.GetCustomAttribute<Cmd>();
            if (metadata == null)
                throw new ArgumentException($"{type.FullName} must have a [Cmd] attribute.", nameof(instance));

            string name = type.Name.StartsWith("Cmd", StringComparison.Ordinal) ? type.Name.Substring(3) : type.Name;
            if (name.Length == 0 || Root.Subcommands.Any(command => command.Name == name || command.Aliases.Contains(name)))
                throw new InvalidOperationException($"Invalid or duplicate command name: '{name}'.");

            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.Name == "Execute" && !method.ContainsGenericParameters).ToArray();
            MethodInfo execute = methods.FirstOrDefault(method => method.GetParameters().Length == 0);
            execute ??= methods.FirstOrDefault(method => method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(Shell));
            if (execute == null || (execute.ReturnType != typeof(void) && execute.ReturnType != typeof(int)))
                throw new InvalidOperationException($"{type.FullName} needs an Execute() or Execute(Shell) method returning void or int.");

            var command = new Command(name, metadata.Description);
            var bindings = new List<Action<ParseResult>>();
            MethodInfo createBinding = typeof(Shell).GetMethod(nameof(CreateFieldBinding), BindingFlags.Instance | BindingFlags.NonPublic);

            // Roslyn emits field metadata in declaration order, including multiple fields on the same line.
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken))
            {
                var argument = field.GetCustomAttribute<CmdArg>();
                var option = field.GetCustomAttribute<CmdOpt>();
                var flag = field.GetCustomAttribute<CmdFlag>();
                int attributes = (argument != null ? 1 : 0) + (option != null ? 1 : 0) + (flag != null ? 1 : 0);
                if (attributes == 0)
                    continue;
                if (attributes != 1 || field.IsStatic || field.IsInitOnly || field.IsLiteral)
                    throw new InvalidOperationException($"{type.FullName}.{field.Name} needs one command field attribute and must be a writable instance field.");
                if (flag != null && field.FieldType != typeof(bool))
                    throw new InvalidOperationException($"[CmdFlag] requires a bool field: {type.FullName}.{field.Name}.");

                var binding = (Action<ParseResult>)createBinding.MakeGenericMethod(field.FieldType).Invoke(this, new object[] { command, instance, field, argument != null, option?.shortName ?? flag?.shortName ?? '\0', flag != null });
                bindings.Add(binding);
            }

            object[] parameters = execute.GetParameters().Length == 0 ? null : new object[] { this };
            command.SetAction(parse =>
            {
                foreach (Action<ParseResult> bind in bindings)
                    bind(parse);

                try
                {
                    return execute.Invoke(instance, parameters) is int exitCode ? exitCode : 0;
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            });
            Root.Subcommands.Add(command);
        }

        Action<ParseResult> CreateFieldBinding<T>(Command command, object instance, FieldInfo field, bool positional, char shortName, bool flag)
        {
            if (positional)
            {
                var argument = new Argument<T>(field.Name) { Arity = ArgumentArity.ExactlyOne };
                command.Arguments.Add(argument);
                return parse => field.SetValue(instance, parse.GetValue(argument));
            }

            // Reset absent options to their initial value, and absent flags to false.
            T initialValue = flag ? default : (T)field.GetValue(instance);
            var option = new Option<T>("--" + field.Name)
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
            return parse => field.SetValue(instance, parse.GetValue(option));
        }
    }
}

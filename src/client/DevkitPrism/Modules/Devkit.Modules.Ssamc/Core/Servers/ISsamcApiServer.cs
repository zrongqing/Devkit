using System.Reflection;

namespace Devkit.Modules.Ssamc.Core.Servers;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ActionCodeAttribute : Attribute
{
    public string Code { get; }

    public ActionCodeAttribute(string code)
    {
        Code = code;
    }
}

public sealed class ActionRegistry
{
    private readonly Dictionary<string, (object instance, MethodInfo method)>
        _actions = new();

    public void RegisterType<T>() where T : new()
    {
        RegisterType(typeof(T));
    }

    public void RegisterType(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var instance = Activator.CreateInstance(type)!;

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<ActionCodeAttribute>();
            if (attr == null)
                continue;

            if (_actions.ContainsKey(attr.Code))
                throw new InvalidOperationException($"重复的 Code：{attr.Code}");

            _actions[attr.Code] = (instance, method);
        }
    }

    public void AutoRegister()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RegisterFromAssembly(assembly);
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            var methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ActionCodeAttribute>();
                if (attr == null)
                    continue;

                if (_actions.ContainsKey(attr.Code))
                {
                    throw new InvalidOperationException(
                        $"重复的 ActionCode：{attr.Code}");
                }

                var instance = Activator.CreateInstance(type)!;

                _actions[attr.Code] = (instance, method);
            }
        }
    }

    public object? Invoke(string code, params object?[] parameters)
    {
        if (!_actions.TryGetValue(code, out var target))
        {
            throw new KeyNotFoundException($"未找到 ActionCode：{code}");
        }

        return target.method.Invoke(target.instance, parameters);
    }

    public async Task InvokeAsync(string code, params object?[] parameters)
    {
        var result = Invoke(code, parameters);
        if (result is Task task)
        {
            await task;
        }
    }
}
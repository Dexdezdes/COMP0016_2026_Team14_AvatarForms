using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AvatarFormsApp.Tests;

public static class TestSetup
{
    // This attribute forces .NET to run this method the instant the test DLL is loaded,
    // before xUnit even starts looking for your tests.
    [ModuleInitializer]
    public static void InitializeAssemblyResolver()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}.dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }
}

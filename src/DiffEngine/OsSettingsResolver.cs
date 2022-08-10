﻿using System.Diagnostics.CodeAnalysis;

static class OsSettingsResolver
{
    static string[] envPaths;

    static OsSettingsResolver()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH")!;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            envPaths = pathVariable.Split(';');
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            envPaths = pathVariable.Split(':');
        }
        else
        {
            envPaths = Array.Empty<string>();
        }
    }

    public static bool Resolve(
        OsSettings? windows,
        OsSettings? linux,
        OsSettings? osx,
        [NotNullWhen(true)] out string? path,
        [NotNullWhen(true)] out BuildArguments? leftArguments,
        [NotNullWhen(true)] out BuildArguments? rightArguments)
    {
        if (TryResolveForOs(windows, out path, out leftArguments, out rightArguments, OSPlatform.Windows))
        {
            return true;
        }

        if (TryResolveForOs(linux, out path, out leftArguments, out rightArguments, OSPlatform.Linux))
        {
            return true;
        }

        if (TryResolveForOs(osx, out path, out leftArguments, out rightArguments, OSPlatform.OSX))
        {
            return true;
        }

        path = null;
        leftArguments = null;
        rightArguments = null;
        return false;
    }

    static bool TryResolveForOs(
        OsSettings? os,
        [NotNullWhen(true)] out string? path,
        [NotNullWhen(true)] out BuildArguments? leftArguments,
        [NotNullWhen(true)] out BuildArguments? rightArguments,
        OSPlatform platform)
    {
        if (os != null &&
            RuntimeInformation.IsOSPlatform(platform))
        {
            if (TryFindExe(os.ExeName, os.SearchDirectories, out path))
            {
                leftArguments = os.LeftArguments;
                rightArguments = os.RightArguments;
                return true;
            }
        }

        leftArguments = null;
        rightArguments = null;
        path = null;
        return false;
    }

    public static IEnumerable<string> ExpandProgramFiles(IEnumerable<string> paths)
    {
        // Note: Windows can have multiple paths, and will resolve %ProgramFiles% as 'C:\Program Files (x86)'
        // when running inside a 32-bit process. To
        // overcome this issue, we need to manually add any option so the correct paths will be resolved

        foreach (var path in paths)
        {
            yield return path;

            if (!path.Contains("%ProgramFiles%"))
            {
                continue;
            }

            yield return path.Replace("%ProgramFiles%", "%ProgramW6432%");
            yield return path.Replace("%ProgramFiles%", "%ProgramFiles(x86)%");
        }
    }

    static bool TryFindExe(string exeName, IEnumerable<string> searchDirectories, [NotNullWhen(true)] out string? exePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            searchDirectories = ExpandProgramFiles(searchDirectories);
        }

        foreach (var path in searchDirectories.Distinct())
        {
            if (WildcardFileFinder.TryFind(path, out exePath))
            {
                return true;
            }
        }

        return TryFindInEnvPath(exeName, out exePath);
    }

    public static bool TryFindInEnvPath(string exeName, [NotNullWhen(true)] out string? exePath)
    {
        // For each path in PATH, append cliApp and check if it exists.
        // Return the first one that exists.
        exePath = envPaths
            .Select(_ => Path.Combine(_, exeName))
            .FirstOrDefault(_ => File.Exists(_));

        return exePath != null;
    }
}
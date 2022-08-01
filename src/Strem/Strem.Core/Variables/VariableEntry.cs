﻿namespace Strem.Core.Variables;

public struct VariableEntry
{
    public const string DefaultContext = "";

    public readonly string Name;
    public readonly string Context;

    public VariableEntry(string name, string context = DefaultContext)
    {
        Name = name;
        Context = context;
    }
}
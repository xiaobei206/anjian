namespace Anjian.App.Models;

public sealed record ScriptEntry(string Name, string FullPath)
{
    public override string ToString() => Name;
}

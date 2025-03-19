using System;
using XTMF;

namespace TMG.Tasha;

[ModuleInformation(Description = "Used for specifying extra attributes for different data types.  Values must be floating point.")]
public sealed class CustomDataColumn : IModule
{
    [RunParameter("Variable Name", "", "The name of the variable to assign to.")]
    public string VariableName;

    [RunParameter("Column Index", -1, "The 0 indexed column to read from.")]
    public int ColumnIndex;

    public string Name { get; set; } = string.Empty;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}

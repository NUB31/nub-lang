namespace Nub.Lang.Backend.Custom;

public class LabelFactory
{
    private int _index;
    public string Create() => $"label{++_index}";
}
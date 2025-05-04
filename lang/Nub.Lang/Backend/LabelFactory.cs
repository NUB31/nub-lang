namespace Nub.Lang.Backend;

public class LabelFactory
{
    private int _index;
    public string Create() => $"label{++_index}";
}
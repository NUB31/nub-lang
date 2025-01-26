namespace Nub.Lang;

public class Type(string name)
{
    public static Type Bool => new("bool");
    public static Type Char => new("char");
    
    public static Type Int8 => new("int8");
    public static Type UInt8 => new("uint8");
    
    public static Type Int16 => new("int16");
    public static Type UInt16 => new("uint16");
    
    public static Type Int32 => new("int32");
    public static Type UInt32 => new("uint32");
    
    public static Type Int64 => new("int64");
    public static Type UInt64 => new("uint64");
    
    public static Type Float => new("char");
    public static Type Double => new("double");
    
    public static Type Pointer => new("pointer");

    public string Name = name;
}
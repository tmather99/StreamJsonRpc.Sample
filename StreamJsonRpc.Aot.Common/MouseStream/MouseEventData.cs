namespace StreamJsonRpc.Aot.Common;

public class MouseEventData
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseAction Action { get; set; }
    public DateTime Timestamp { get; set; }
    public List<string> ValuedList { get; set; } = [];
    public Dictionary<Guid, string> ValuedDictionary { get; set; } = [];
}

public enum MouseAction
{
    Move = 0,
    LeftClick = 1,
    RightClick = 2,
    MiddleClick = 3,
    DoubleClick = 4,
    Scroll = 5
}

using System;
using System.Runtime.Serialization;

namespace StreamJsonRpc.Aot.Common;

[DataContract]
public class MouseEventData
{
    [DataMember(Order = 0)] public int X { get; set; }

    [DataMember(Order = 1)] public int Y { get; set; }

    [DataMember(Order = 2)] public MouseAction Action { get; set; }

    [DataMember(Order = 3)] public DateTime Timestamp { get; set; }
}

[DataContract]
public enum MouseAction
{
    [EnumMember] Move = 0,

    [EnumMember] LeftClick = 1,

    [EnumMember] RightClick = 2,

    [EnumMember] MiddleClick = 3,

    [EnumMember] DoubleClick = 4,

    [EnumMember] Scroll = 5
}
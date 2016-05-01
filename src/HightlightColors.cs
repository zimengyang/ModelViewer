using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Libs.MiscFunctions;
using Common.Libs.ParallelFunctions;
using Common.Libs.VMath;

namespace MeshFlowViewer
{
    public enum HighlightColors
    {
        None,
        Red,
        Orange,
        Yellow,
        Green,
        Cyan,
        Blue,
        Purple,
        Pink,
        White
    }

    public static class Highlight
    {
        public static readonly Vec3f?[] Colors = new Vec3f?[] {
            null,
            new Vec3f(1,0,0),
            new Vec3f(1,0.5f,0),
            new Vec3f(1,1,0),
            new Vec3f(0,1,0),
            new Vec3f(0,1,1),
            new Vec3f(0,0,1),
            new Vec3f(0.5f,0,0.5f),
            new Vec3f(1,0.5f,1),
            new Vec3f(1,1,1)
        };

        public static Vec3f? GetColor(HighlightColors color) { return Colors[(int)color]; }
    }

}

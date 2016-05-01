using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

using Common.Libs.VMath;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public enum CompositionPresets
    {
        Default, MeshDiff, Extrude, CompareBeforeAfter, CompareBeforeCurrentAfter, Intervals, Subdivide, Transform, Select, AddEdgeFace
    }

    public enum ComparisonModes
    {
        OnlyCurrent, BeforeAfter, BeforeCurrentAfter
    }

    public enum SelectionChoices
    {
        None, Before, Current, After
    }

    public class Composition : IBinaryConvertible
    {
        #region Presets

        public static string[] GetPresetNames() { return Presets.Select((Composition comp) => comp.name).ToArray(); }
        public static Composition GetPreset(string name)
        {
            foreach (Composition comp in Presets) if (comp.name.ToLower() == name.ToLower()) return comp;
            return null;
        }
        public static Composition GetPreset(CompositionPresets preset)
        {
            switch (preset)
            {
                case CompositionPresets.Default: return GetPreset("Default");
                case CompositionPresets.MeshDiff: return GetPreset("MeshDiff");
                case CompositionPresets.Extrude: return GetPreset("Extrude");
                case CompositionPresets.CompareBeforeAfter: return GetPreset("Compare Before,After");
                case CompositionPresets.CompareBeforeCurrentAfter: return GetPreset("Compare Before,Current,After");
                case CompositionPresets.Intervals: return GetPreset("Intervals");
                case CompositionPresets.Subdivide: return GetPreset("Subdivide");
                case CompositionPresets.Transform: return GetPreset("Transform");
                case CompositionPresets.Select: return GetPreset("Select");
                case CompositionPresets.AddEdgeFace: return GetPreset("AddEdgeFace");
                default: throw new Exception("Unimplemented preset");
            }
        }

        private static Composition[] Presets = new Composition[] {
            new Composition() { Name = "Default", Show_After = true },
            new Composition() { Name = "MeshDiff", Show_Diff_AfterBefore = true, Show_Diff_BeforeAfter = true, Show_Intersect_BeforeAfter = true },
            new Composition() { Name = "Extrude", Show_Diff_AfterBefore = true, Show_Intersect_BeforeAfter = true, Show_Provenance = true },
            new Composition() { Name = "Compare Before,After", Mode = ComparisonModes.BeforeAfter, SeparateViewports_Use = true, Show_Diff_AfterBefore = true, Show_Diff_BeforeAfter = true, Show_Intersect_BeforeAfter = true, Show_Provenance = true },
            new Composition() { Name = "Compare Before,Current,After", Mode = ComparisonModes.BeforeCurrentAfter, Compare_Offset = new Vec3f( 10.0f, 0.0f, 0.0f ), Show_Diff_AfterBefore = true, Show_Diff_BeforeAfter = true, Show_Intersect_BeforeAfter = true },
            new Composition() { Name = "Intervals", Show_Diff_AfterBefore = true, Show_Intersect_BeforeAfter = true, Show_Annotations_Transforms = true, Intervals_Use = true, Intervals_Count = 11, SeparateViewports_Use = true },
            new Composition() { Name = "Subdivide", Show_After = true, Show_Provenance = true },
            new Composition() { Name = "Transform", Show_After = true, Show_Provenance = true, Show_Annotations_Transforms = true },
            new Composition() { Name = "Select", Show_After = true, Show_Provenance = true },
            new Composition() { Name = "AddEdgeFace", Show_Diff_AfterBefore = true, Show_Intersect_BeforeAfter = true, Show_Annotations_Transforms = true, Show_Provenance = true },
        };

        public void SetToPreset(string name)
        {
            Composition preset = GetPreset(name);
            if (preset == null) throw new ArgumentException("Could not find preset " + name);
            SetToComposition(preset);
        }

        public void SetToPreset(CompositionPresets preset)
        {
            switch (preset)
            {
                case CompositionPresets.Default: SetToPreset("Default"); break;
                case CompositionPresets.MeshDiff: SetToPreset("MeshDiff"); break;
                case CompositionPresets.Extrude: SetToPreset("Extrude"); break;
                case CompositionPresets.CompareBeforeAfter: SetToPreset("Compare Before,After"); break;
                case CompositionPresets.CompareBeforeCurrentAfter: SetToPreset("Compare Before,Current,After"); break;
                case CompositionPresets.Intervals: SetToPreset("Intervals"); break;
                case CompositionPresets.Subdivide: SetToPreset("Subdivide"); break;
                case CompositionPresets.Transform: SetToPreset("Transform"); break;
                default: throw new Exception("Unimplemented preset");
            }
        }

        #endregion

        #region Defaults

        public static Composition GetCompositionByOperation(string operation)
        {
            switch (operation.SplitOnce('.')[0])
            {
                case "topo":
                    if (operation == "topo.extrude") return GetPreset(CompositionPresets.Extrude);
                    else if (operation == "topo.loopcut") return GetPreset(CompositionPresets.Subdivide);
                    else if (operation == "topo.subdivide") return GetPreset(CompositionPresets.Subdivide);
                    else if (operation == "topo.merge") return new Composition() { Show_After = true, Show_Provenance = true };
                    else if (operation == "topo.convert.quads_to_tris") return GetPreset(CompositionPresets.Subdivide);
                    else if (operation == "topo.convert.tris_to_quads") return GetPreset(CompositionPresets.Subdivide);
                    else if (operation == "topo.add.edge_face") return GetPreset(CompositionPresets.AddEdgeFace);
                    else if (operation.StartsWith("topo.add")) return GetPreset(CompositionPresets.AddEdgeFace);
                    return GetPreset(CompositionPresets.AddEdgeFace);

                case "transform":
                    return GetPreset(CompositionPresets.Transform);

                case "select":
                    return GetPreset(CompositionPresets.Select);
            }

            return GetPreset(CompositionPresets.Default);
        }

        #endregion

        #region Properties

        private string name;
        private ComparisonModes mode;
        private Vec3f compare_offset;
        private bool show_diff_afterbefore, show_diff_beforeafter, show_intersect_beforeafter;
        private bool show_before, show_after, show_end;
        private bool separateviewports_use;
        private SelectionChoices selections;
        private bool intervals_use;
        private int intervals_count;
        private bool show_provenance;
        private bool show_annotations_transforms;

        public string Name { get { return name; } set { name = value; FireChangeHandler(); } }
        public ComparisonModes Mode { get { return mode; } set { mode = value; FireChangeHandler(); } }
        public Vec3f Compare_Offset { get { return compare_offset; } set { compare_offset = value; FireChangeHandler(); } }
        public bool Show_Diff_AfterBefore { get { return show_diff_afterbefore; } set { show_diff_afterbefore = value; FireChangeHandler(); } }
        public bool Show_Diff_BeforeAfter { get { return show_diff_beforeafter; } set { show_diff_beforeafter = value; FireChangeHandler(); } }
        public bool Show_Intersect_BeforeAfter { get { return show_intersect_beforeafter; } set { show_intersect_beforeafter = value; FireChangeHandler(); } }
        public bool Show_Before { get { return show_before; } set { show_before = value; FireChangeHandler(); } }
        public bool Show_After { get { return show_after; } set { show_after = value; FireChangeHandler(); } }
        public bool Show_End { get { return show_end; } set { show_end = value; FireChangeHandler(); } }
        public bool SeparateViewports_Use { get { return separateviewports_use; } set { separateviewports_use = value; FireChangeHandler(); } }
        public SelectionChoices Selections { get { return selections; } set { selections = value; FireChangeHandler(); } }
        public bool Intervals_Use { get { return intervals_use; } set { intervals_use = value; FireChangeHandler(); } }
        public int Intervals_Count { get { return intervals_count; } set { intervals_count = value; FireChangeHandler(); } }

        public bool Show_Provenance { get { return show_provenance; } set { show_provenance = value; FireChangeHandler(); } }
        public bool Show_Annotations_Transforms { get { return show_annotations_transforms; } set { show_annotations_transforms = value; FireChangeHandler(); } }

        #endregion

        #region Changed Event

        public delegate void ChangeHandler();
        public event ChangeHandler Changed;
        public void FireChangeHandler() { if (Changed != null) Changed(); }

        #endregion

        public Composition() { name = ""; selections = SelectionChoices.After; }

        public Composition(CompositionPresets preset) : this() { SetToPreset(preset); }

        public Composition(Composition copy) { name = copy.name; SetToComposition(copy); }

        public void SetToComposition(Composition comp)
        {
            this.name = comp.name;
            this.mode = comp.mode;
            this.compare_offset = comp.compare_offset;
            this.show_diff_afterbefore = comp.show_diff_afterbefore;
            this.show_diff_beforeafter = comp.show_diff_beforeafter;
            this.show_intersect_beforeafter = comp.show_intersect_beforeafter;
            this.show_after = comp.show_after;
            this.show_end = comp.show_end;
            this.separateviewports_use = comp.separateviewports_use;
            this.selections = comp.selections;
            this.intervals_use = comp.intervals_use;
            this.intervals_count = comp.intervals_count;
            this.show_provenance = comp.show_provenance;
            this.show_annotations_transforms = comp.show_annotations_transforms;
            FireChangeHandler();
        }

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    bw.WriteT(name);
        //    bw.WriteT(mode);
        //    bw.WriteT(compare_offset);
        //    bw.WriteParams(show_diff_afterbefore, show_diff_beforeafter, show_intersect_beforeafter);
        //    bw.WriteParams(show_before, show_after, show_end);
        //    bw.WriteT(separateviewports_use);
        //    bw.WriteT(selections);
        //    bw.WriteT(intervals_use);
        //    bw.WriteT(intervals_count);
        //    bw.WriteT(show_provenance);
        //    bw.WriteT(show_annotations_transforms);
        //}

        public void ReadBinary(BinaryReader br)
        {
            this.name = br.ReadString();
            this.mode = br.ReadEnum<ComparisonModes>();
            this.compare_offset = br.ReadVec3f();
            this.show_diff_afterbefore = br.ReadBoolean();
            this.show_diff_beforeafter = br.ReadBoolean();
            this.show_intersect_beforeafter = br.ReadBoolean();
            this.show_before = br.ReadBoolean();
            this.show_after = br.ReadBoolean();
            this.show_end = br.ReadBoolean();
            this.separateviewports_use = br.ReadBoolean();
            this.selections = br.ReadEnum<SelectionChoices>();
            this.intervals_use = br.ReadBoolean();
            this.intervals_count = br.ReadInt32();
            this.show_provenance = br.ReadBoolean();
            this.show_annotations_transforms = br.ReadBoolean();
            FireChangeHandler();
        }

    }

}


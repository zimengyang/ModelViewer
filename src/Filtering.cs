using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public delegate bool IsFilteredFunction(Cluster cluster);

    public class Filtering
    {
        public Property<bool> Enabled = new Property<bool>("Enabled", false);
        public string Label;

        protected IsFilteredFunction filterfunc;

        public delegate void ReevaluatedHandler();
        public event ReevaluatedHandler Reevaluated;
        public void FireReevalutadeHandler() { if (Reevaluated != null) Reevaluated(); }

        public void ToggleEnabled() { Enabled.Set(!Enabled.Val); }

        protected Filtering(string label, bool enabled)
        {
            Label = label;
            Enabled.Val = enabled;
            Enabled.PropertyChanged += delegate { FireReevalutadeHandler(); };
        }
        public Filtering(string label, bool enabled, IsFilteredFunction filterfunc) : this(label, enabled) { this.filterfunc = filterfunc; }

        public bool IsFiltered(Cluster cluster) { return Enabled && filterfunc(cluster); }
    }

    public class FilteringName : Filtering
    {
        private string name;

        public FilteringName(string label, string name, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
            this.name = name;
        }

        private bool filteringfunc(Cluster cluster)
        {
            return (cluster.name == name);
        }
    }

    public class FilteringNameStartsWith : Filtering
    {
        private string[] names;

        public FilteringNameStartsWith(string label, string name, bool enabled) : this(label, new string[] { name }, enabled) { }

        public FilteringNameStartsWith(string label, string[] names, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
            this.names = names;
        }

        private bool filteringfunc(Cluster cluster)
        {
            foreach (string name in names) if (cluster.name.StartsWith(name)) return true;
            return false;
        }
    }

    public class FilteringVertexTag : Filtering
    {
        private string tag;

        public FilteringVertexTag(string label, string tag, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
            this.tag = tag;
        }

        private bool filteringfunc(Cluster cluster)
        {
            ModelingHistory hist = ModelingHistory.history;
            IndexedViewableAlpha[] vs = hist.GetViewables(cluster, false, true, true, false);
            bool[] selected = new bool[hist.SnapshotCount];
            foreach (IndexedViewableAlpha viewable in vs)
                for (int ivert = 0; ivert < viewable.nVerts; ivert++) if (viewable.Selected[ivert]) selected[viewable.VertUIDs[ivert]] = true;
            for (int ivert = 0; ivert < hist.UniqueVertCount; ivert++) if (selected[ivert] && hist.GetVTags(ivert).Contains(tag)) return true;
            return false;
        }
    }

    public class UnFilteringVertexTag : Filtering
    {
        private string tag;

        public UnFilteringVertexTag(string label, string tag, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
            this.tag = tag;
        }

        private bool filteringfunc(Cluster cluster)
        {
            ModelingHistory hist = ModelingHistory.history;
            IndexedViewableAlpha[] vs = hist.GetViewables(cluster, false, true, true, false);
            bool[] selected = new bool[hist.SnapshotCount];
            foreach (IndexedViewableAlpha viewable in vs)
                for (int ivert = 0; ivert < viewable.nVerts; ivert++) if (viewable.Selected[ivert]) selected[viewable.VertUIDs[ivert]] = true;
            for (int ivert = 0; ivert < hist.UniqueVertCount; ivert++) if (selected[ivert] && hist.GetVTags(ivert).Contains(tag)) return false;
            return true;
        }
    }

    public class FilteringVertexHighlightedTag : Filtering
    {
        public FilteringVertexHighlightedTag(string label, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
        }

        private bool filteringfunc(Cluster cluster)
        {
            ModelingHistory hist = ModelingHistory.history;
            for (int i0 = cluster.start; i0 <= cluster.end; i0++)
            {
                bool[] sel = hist.GetSelectedVerts_Snapshot(i0);
                for (int ivert = 0; ivert < sel.Length; ivert++)
                    if (sel[ivert] && hist.IsHighlighted(ivert)) return true;
            }
            return false;
        }
    }

    public class UnFilteringVertexHighlightedTag : Filtering
    {
        public UnFilteringVertexHighlightedTag(string label, bool enabled)
            : base(label, enabled)
        {
            this.filterfunc = filteringfunc;
        }

        private bool filteringfunc(Cluster cluster)
        {
            ModelingHistory hist = ModelingHistory.history;
            foreach (int i0 in cluster.snapshots)
            //for( int i0 = cluster.start; i0 <= cluster.end; i0++ )
            {
                bool[] sel = hist.GetSelectedVerts_Snapshot(i0);
                for (int ivert = 0; ivert < sel.Length; ivert++)
                    if (sel[ivert] && hist.IsHighlighted(ivert)) return false;
            }
            return true;
        }
    }
}


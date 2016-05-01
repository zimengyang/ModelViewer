using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;

using Common.Libs.VMath;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    [Serializable]
    public abstract class Clustering
    {
        protected Clustering below;
        protected Clustering above;
        protected ModelingHistory history;

        // cached values
        private List<Cluster> clusterscache = null;
        private CameraProperties[][] camerascache = null;
        private List<string> distinctnamesall = null;
        private List<int> distinctnamesall_eachcount = null;
        protected int mylevel;

        #region Reevaluate Handler Event

        public delegate void ReevaluatedClusteringHandler(Clustering cluster);
        public event ReevaluatedClusteringHandler Reevaluated;
        public void FireReevalutadeHandler() { if (Reevaluated != null) Reevaluated(this); }

        #endregion

        #region Properties

        public string Label { get; protected set; }

        public List<Cluster> clusters { get; protected set; }

        public List<string> DistinctNamesAll { get { return distinctnamesall; } }
        public List<int> DistinctNamesAll_EachCount { get { return distinctnamesall_eachcount; } }
        public List<string> DistinctNames { get { return clusters.Select((Cluster cluster) => cluster.name).Distinct().ToList(); } }
        public List<string> DistinctNames_First { get { return clusters.Select((Cluster cluster) => cluster.name.SplitOnce('.')[0]).Distinct().ToList(); } }

        public Clustering Below
        {
            get { return below; }
            set
            {
                below = value;
                if (below != null) { below.above = this; Evaluate(); }
            }
        }

        public Clustering Above
        {
            get { return above; }
            set
            {
                if (above is ClusteringLevel0) throw new ArgumentException("ClusteringLevel0 must be bottommost");
                above = value;
                if (above != null) { above.below = this; above.Evaluate(); }
            }
        }

        public int Level { get { return mylevel; } }

        #endregion

        public Clustering(ModelingHistory history) : this(history, "") { }
        public Clustering(ModelingHistory history, string label)
        {
            clusters = new List<Cluster>();
            this.history = history;
            this.Label = label;
            this.history.Filters.Reevaluated += delegate { camerascache = null; };
        }

        protected abstract void EvaluateCluster();

        protected virtual void WriteBinaryData(BinaryWriter bw) { }
        protected virtual void ReadBinaryData(BinaryReader br) { }

        public void CacheViewables(long msthreshold) { CacheViewables(msthreshold, 0, GetClusters().Count - 1); }
        public void CacheViewables(long msthreshold, int indstart, int indend)
        {
            int ccached = 0;
            int cskipped = 0;
            //var inds = Enumerable.Range( 0, clusters.Count ).ToList().PermuteRandomly( new Random() );
            //foreach( int ind in inds ) {
            //Cluster cluster = clusters[ind];

            List<Cluster> clustersfull = GetClusters();
            List<Cluster> clusters = new List<Cluster>(indend - indstart + 1);
            for (int ind = indstart; ind <= indend; ind++) clusters.Add(clustersfull[ind]);

            //foreach( Cluster cluster in clusters )
            clusters.EachInParallel((Cluster cluster, int ind) =>
            {
                if (ModelingHistory.ENDING) return;
                if (cluster.viewable != null) return;
                int i0before = history.BaseModifierIndex(cluster.start);
                int i0after = cluster.end;
                IndexedViewableAlpha viewable = null;
                long ms = Timer.GetExecutionTime_ms(delegate {
                    viewable = history.GetViewable(cluster, i0before, i0after, false, false, false, false);
                });
                if (ms > msthreshold)
                {
                    cluster.viewable = viewable; //IndexedViewableAlpha.Trim( viewable );
                    cluster.viewable_ann = true;
                    cluster.viewable_rem = false;
                    cluster.viewable_sel = false;
                    Interlocked.Increment(ref ccached);
                    //ccached++;
                    System.Console.Write(".");
                }
                else {
                    Interlocked.Increment(ref cskipped);
                    //cskipped++;
                }
                if (ModelingHistory.ENDING) return;
            });
            System.Console.WriteLine("Level: {0}, Cached: {1}, Skipped: {2}", Level, ccached, cskipped);
        }

        public void InsertLayerAbove(Clustering abovenew)
        {
            if (abovenew != null)
            {
                abovenew.above = above;
                abovenew.below = this;
            }

            if (above != null) above.below = abovenew;
            above = abovenew;

            if (above != null) above.Evaluate();
        }

        public virtual void Remove()
        {
            if (above != null) above.below = below;
            if (below != null) below.Above = above;

            if (above != null) above.Evaluate();

            below = above = null;
        }

        public void CreateCache()
        {
            FillClustersCache();
            FillCamerasCache();

            var names = clusterscache.Select((Cluster cluster) => cluster.name);
            distinctnamesall = names.Distinct().ToList();
            distinctnamesall_eachcount = distinctnamesall.Select((string dname) => names.Count((string name) => (name == dname))).ToList();
        }

        public void ClearCache()
        {
            clusterscache = null;
            camerascache = null;
            distinctnamesall = null;
            distinctnamesall_eachcount = null;
        }

        public virtual List<Cluster> GetClusters()
        {
            //CreateCache();
            FillClustersCache();
            return clusterscache;
        }

        private void FillClustersCache()
        {
            if (clusterscache != null) return;

            if (below == null)
            {
                clusterscache = new List<Cluster>(clusters);
                return;
            }

            clusterscache = new List<Cluster>(below.GetClusters());
            foreach (Cluster cluster in clusters)
            {
                int index = clusterscache.FindIndex((Cluster c) => (c.start == cluster.start));
                int inext = clusterscache.FindIndex((Cluster c) => (c.end == cluster.end));
                clusterscache.RemoveRange(index, inext - index + 1);
                clusterscache.Insert(index, cluster);
            }
        }

        private object cameracachelock = new object();
        private bool fillingcache = false;
        private void FillCamerasCache()
        {
            if (camerascache != null) return;

            // the following prevents multiple threads filling cache at the same time
            bool dofill = false;
            while (!dofill)
            {
                lock (cameracachelock)
                {
                    if (!fillingcache && camerascache == null) { fillingcache = true; dofill = true; }
                }
                if (camerascache != null) return;
                if (dofill) break;
                Thread.Sleep(10);
            }

            FillClustersCache();

            CameraProperties[][] cams = clusterscache.Select(cluster => cluster.GetCameras()).ToArray();

            int lastgood = -1;
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i][1] != null) { lastgood = i; continue; }

                if (lastgood >= 0) { cams[i][1] = cams[lastgood][1]; continue; }

                for (lastgood = i + 1; lastgood < cams.Length; lastgood++) if (cams[lastgood][1] != null) break;

                if (lastgood < cams.Length) { cams[i][1] = cams[lastgood][1]; continue; }

                throw new Exception("failed sanity check");
            }

            for (int i = 0; i < cams.Length; i++) if (cams[i][1] == null) throw new Exception("failed sanity check");
            camerascache = cams; //.ToArray();

            fillingcache = false;
        }

        public int GetClusterIndex(Cluster clusterfind)
        {
            List<Cluster> clusters = GetClusters();

            for (int icluster = 0; icluster < clusters.Count; icluster++)
            {
                Cluster cluster = clusters[icluster];
                if (cluster.start <= clusterfind.start && cluster.end >= clusterfind.end) return icluster;
            }
            //System.Console.WriteLine( "Could not find cluster " + clusterfind.start + "-" + clusterfind.end + " in layer level " + this.Level );
            return -1;
        }
        public int GetClusterIndex(int index0)
        {
            List<Cluster> clusters = GetClusters();

            for (int icluster = 0; icluster < clusters.Count; icluster++)
            {
                Cluster cluster = clusters[icluster];
                if (cluster.start <= index0 && cluster.end >= index0) return icluster;
            }
            return -1;
        }

        public CameraProperties[][] GetCameras()
        {
            FillCamerasCache();
            return camerascache;
        }

        public Cluster GetCluster(int index0)
        {
            List<Cluster> clusters = GetClusters();

            foreach (Cluster cluster in clusters)
                if (cluster.Index0Within(index0)) return cluster;

            throw new ArgumentException("No Cluster found containing index0 = " + index0);
        }

        public virtual List<int> GetLevels()
        {
            List<int> lst = below.GetLevels();

            clusters.Each(delegate (Cluster cluster, int i) {
                lst.RemoveRange(cluster.start, cluster.duration);
                lst.AddRange(Enumerable.Repeat(mylevel, cluster.duration));
            });

            return lst;
        }

        public virtual Clustering GetTopLayer() { if (above != null) return above.GetTopLayer(); return this; }

        public void Evaluate()
        {
            CheckValidLayering();
            ClearCache();
            if (below != null) mylevel = below.Level + 1; else mylevel = 0;
            long timeeval = Timer.GetExecutionTime_ms(delegate {
                EvaluateCluster();
            });

            //foreach( Cluster cluster in clusters ) cluster.viewables = null;

            //Dump();
            FireReevalutadeHandler();
            if (above != null) above.Evaluate();
        }
        #region Consistency Checking Functions

        public void TestIfCanAdd(Cluster cluster)
        {
            List<Cluster> lstbelow = below.GetClusters();

            if (clusters.Any((Cluster c) => (c.start >= cluster.start && c.start <= cluster.end) || (c.end >= cluster.start && c.end <= cluster.end)))
                throw new ArgumentException("Overlapping Clusters");

            if (!lstbelow.Any((Cluster c) => (c.start == cluster.start)))
                throw new ArgumentException("Bad Clustering Start");

            if (!lstbelow.Any((Cluster c) => (c.end == cluster.end)))
                throw new ArgumentException("Bad Clustering End");

            for (Clustering a = above; a != null; a = a.Above)
            {
                if (a.clusters.Any((Cluster c) => (c.start > cluster.start && c.start < cluster.end) || (c.end > cluster.start && c.end < cluster.end)))
                    throw new ArgumentException("Overlapping Clusters");
            }
        }

        public void Dump() { System.Console.WriteLine("Dump"); foreach (Cluster c in clusters) System.Console.WriteLine("  {0}-{1}", c.start, c.end); }

        public void CheckValidLayering()
        {
            if (below == null)
            {
                if (!(this is ClusteringLevel0)) throw new Exception("Improper Layering of Clusterings");
                return;
            }
            else {
                if (below.above != this) throw new Exception("below.above != this");
            }

            if (above != null && above.below != this) throw new Exception("above.below != this");

            List<Cluster> lstbelow = below.GetClusters();
            foreach (Cluster cluster in clusters)
            {
                if (clusters.Any((Cluster c) => (c != cluster) && ((c.start >= cluster.start && c.start <= cluster.end) || (c.end >= cluster.start && c.end <= cluster.end))))
                {
                    Dump();
                    throw new ArgumentException("Overlapping Clusters");
                }

                if (!lstbelow.Any((Cluster c) => (c.start == cluster.start)))
                    throw new Exception("Bad Clustering Start");

                if (!lstbelow.Any((Cluster c) => (c.end == cluster.end)))
                    throw new Exception("Bad Clustering End");
            }
        }

        #endregion

        #region Serialization Functions

        public static readonly Dictionary<Type, string> dctClusteringTypes = new Dictionary<Type, string>() {
            { typeof(ClusteringLevel0), "level0" },
            { typeof(ClusteringPredicate_PairWithIgnorable), "predicate_pairwithignorable" },
        };

        public static Clustering ReadBinary(BinaryReader br, ModelingHistory history)
        {
            string fullname = br.ReadString();
            Clustering nclustering = CallDerivedConstructor(fullname, history);
            nclustering.Label = br.ReadString();
            nclustering.mylevel = br.ReadInt32();
            nclustering.clusterscache = null;
            nclustering.clusters = br.ReadList<Cluster>();
            nclustering.ReadBinaryData(br);

            if (br.ReadBoolean())
            {
                nclustering.above = ReadBinary(br, history);
                nclustering.above.below = nclustering;
            };

            return nclustering;
        }

        private static Clustering CallDerivedConstructor(string fullname, ModelingHistory history)
        {
            ConstructorInfo constructor = null;
            Type[] ctortypesig = new Type[] { typeof(ModelingHistory) };

            List<Type> clusteringtypes = typeof(Clustering).GetDerivedTypes().ToList();
            foreach (Type ctype in clusteringtypes)
            {
                if (ctype.FullName == fullname) constructor = ctype.GetConstructor(ctortypesig);
            }
            if (constructor == null) throw new Exception("Could not find constructor for " + fullname);

            return (Clustering)constructor.Invoke(new object[] { history });
        }

        #endregion
    }

    [Serializable]
    public class ClusteringUndos : Clustering
    {
        public ClusteringUndos(ModelingHistory history) : base(history, "Cluster Undos") { }

        protected override void EvaluateCluster()
        {
            List<Cluster> lstbelow = below.GetClusters();
            Composition comp = new Composition(CompositionPresets.Default);

            List<Cluster> lstundos = new List<Cluster>();

            lstbelow.Each(delegate (Cluster cluster, int ind)
            //foreach( Cluster cluster in lstbelow )
            {
                if (cluster.name == "undo.undo" && cluster.start == cluster.end)
                {
                    //lstundos.Add( new Cluster( history.BaseModifierIndex( cluster.start ) + 1, cluster.end, "undo.undo", comp, new int[] { cluster.start } ) );

                    int basemodifyid = history.BaseModifierIndex(cluster.start);
                    int indprev = ind;
                    for (; indprev >= 0; indprev--) if (lstbelow[indprev].start == basemodifyid) break;
                    if (indprev < 0) throw new Exception("failed sanity check");

                    Cluster basecluster = lstbelow[indprev];
                    lstundos.Add(new Cluster(basemodifyid, cluster.end, basecluster.name, basecluster.composition, new int[] { indprev }));
                }
            });

            // remove overlapping (nested) undos
            for (int i = 0; i < lstundos.Count; i++)
            {
                int j = 0;
                while (j < lstundos.Count)
                {
                    if (i == j) { j++; continue; }
                    if (lstundos[j].start >= lstundos[i].start && lstundos[j].end < lstundos[i].end)
                    {
                        if (i > j) i--;
                        lstundos.RemoveAt(j);
                    }
                    else j++;
                }
            }

            clusters = lstundos;
        }
    }

    [Serializable]
    public abstract class ClusteringPredicate : Clustering
    {
        public ClusteringPredicate(ModelingHistory history, string label) : base(history, label) { }

        protected override void EvaluateCluster()
        {
            List<Cluster> lstbelow = below.GetClusters();
            int index = 0;
            int deltaindex = 0;
            string name = null;
            SolidBrush brush = null;
            Composition comp = null;
            List<int> snapshots = null;

            clusters.Clear();

            long timedelta = 0;
            long timeagg = 0;
            long timecluster = 0;

            while (index < lstbelow.Count)
            {
                timedelta += Timer.GetExecutionTime_ms(delegate {
                    deltaindex = GetDeltaIndex(lstbelow, index, out name, out brush, out comp, out snapshots);
                });

                if (deltaindex > 0)
                {
                    timeagg += Timer.GetExecutionTime_ms(delegate {
                        snapshots = snapshots.Select(ind => lstbelow[ind].snapshots).Aggregate(new List<int>(), (agg, cur) => agg.AddReturn(cur));
                    });

                    timecluster += Timer.GetExecutionTime_ms(delegate {
                        int i0s = lstbelow[index].start;
                        int i0e = lstbelow[index + deltaindex - 1].end;
                        Cluster cluster = new Cluster(i0s, i0e, name, comp, snapshots);
                        //try { TestIfCanAdd( cluster ); clusters.Add( cluster ); }
                        //catch { System.Console.WriteLine( "Could not add cluster: " + cluster.name + ":" + cluster.start + "," + cluster.end ); }
                        clusters.Add(cluster);
                    });
                }
                else deltaindex = 1;

                index += deltaindex;
            }
            //System.Console.WriteLine( "delta: {0}ms; aggregate: {1}ms; cluster: {2}ms", timedelta, timeagg, timecluster );
        }

        // returns 0 if no matching exists, or the delta to move index0 beyond matching cluster
        protected abstract int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots);
    }

    [Serializable]
    public class ClusteringPredicate_Repeats : ClusteringPredicate
    {
        public ClusteringPredicate_Repeats(ModelingHistory history) : base(history, "Cluster Repeated Operations") { }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            Cluster cstart = lst[istart];
            int d = 1;
            while (istart + d < lst.Count && lst[istart + d].name == cstart.name) { d++; }
            if (d == 1) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = cstart.name;
            brush = cstart.brush;
            comp = cstart.composition;
            snapshots = Enumerable.Range(istart, d).ToList();
            return d;
        }
    }

    [Serializable]
    public class ClusteringPredicate_Repeats2 : ClusteringPredicate
    {
        string[] ignorable;
        public ClusteringPredicate_Repeats2(string[] ignorable, ModelingHistory history, string label) : base(history, label)
        {
            this.ignorable = ignorable;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            Cluster cstart = lst[istart];
            int d = 1, ld = 0;
            while (istart + d < lst.Count && (ignorable.Contains(lst[istart + d].name) || lst[istart + d].name == cstart.name))
            {
                if (lst[istart + d].name == cstart.name) ld = d;
                d++;
            }
            if (ld == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = cstart.name;
            brush = cstart.brush;
            comp = cstart.composition;
            snapshots = new List<int>(ld);
            for (int i = istart; i < istart + ld + 1; i++) if (!ignorable.Contains(lst[i].name)) snapshots.Add(i);
            return ld + 1;
        }
    }

    [Serializable]
    public class ClusteringPredicate_StartsWith : ClusteringPredicate
    {
        private string startswith;

        public ClusteringPredicate_StartsWith(ModelingHistory history, string label) : base(history, label) { }

        public ClusteringPredicate_StartsWith(string startswith, ModelingHistory history, string label) : base(history, label)
        {
            this.startswith = startswith;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            Cluster cstart = lst[istart];
            if (!cstart.name.StartsWith(startswith)) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            int d = 1;
            while (istart + d < lst.Count && lst[istart + d].name.StartsWith(startswith)) { d++; }
            if (d == 1) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = startswith;
            brush = cstart.brush;
            comp = cstart.composition;
            snapshots = Enumerable.Range(istart, d).ToList();
            return d;
        }
        protected override void WriteBinaryData(BinaryWriter bw) { bw.Write(startswith); }
        protected override void ReadBinaryData(BinaryReader br) { startswith = br.ReadString(); }
    }

    public class ClusteringPredicate_MixedRepeats : ClusteringPredicate
    {
        private string[] bag;
        private string name;
        private SolidBrush brush;
        private Composition comp;

        public ClusteringPredicate_MixedRepeats(string name, string[] bag, SolidBrush brush, Composition comp, ModelingHistory history, string label) : base(history, label)
        {
            this.bag = bag;
            this.name = name;
            this.brush = brush;
            this.comp = comp;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            if (!bag.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            int d = 1;
            while (istart + d < lst.Count && bag.Contains(lst[istart + d].name)) { d++; }
            if (d == 1) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = this.name;
            brush = this.brush;
            comp = this.comp;
            snapshots = Enumerable.Range(istart, d).ToList();
            return d;
        }

        //protected override void WriteBinaryData(BinaryWriter bw) { bw.WriteArray(bag); }
        protected override void ReadBinaryData(BinaryReader br) { br.ReadArray(out bag); }
    }

    [Serializable]
    public class ClusteringPredicate_PairWithIgnorable : ClusteringPredicate
    {
        private string[] start;
        private string[] end;
        private string[] ignorable;
        private string name;
        private SolidBrush brush;
        private Composition comp;
        private bool multistart;
        private bool multiend;
        private bool restartable;

        public ClusteringPredicate_PairWithIgnorable(ModelingHistory history, string label) : base(history, label) { }

        protected override void ReadBinaryData(BinaryReader br)
        {
            start = br.ReadArray<string>();
            end = br.ReadArray<string>();
            ignorable = br.ReadArray<string>();
            name = br.ReadString();
            brush = br.ReadSolidBrush();
            br.Read(out comp);
            multistart = br.ReadBoolean();
            multiend = br.ReadBoolean();
            restartable = br.ReadBoolean();
        }

        public ClusteringPredicate_PairWithIgnorable(string name, SolidBrush brush, Composition comp, string[] start, string[] end, string[] ignorable, bool multistart, bool multiend, bool restartable, ModelingHistory history, string label)
            : base(history, label)
        {
            this.name = name;
            this.brush = brush;
            this.comp = comp;
            this.start = start;
            this.end = end;
            this.ignorable = ignorable;
            this.multistart = multistart;
            this.multiend = multiend;
            this.restartable = restartable;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0, lend = 0;
            if (!start.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            do
            {
                if (multistart) d += GetDeltaIndexWhile(lst, istart + d, start, ignorable);
                if (!start.Contains(lst[istart + d].name)) break;
                d++;
                if (istart + d >= lst.Count) break;
                d += GetDeltaIndexUntil(lst, istart + d, end, ignorable);
                if (!end.Contains(lst[istart + d].name)) break;
                if (multiend) d += GetDeltaIndexWhile(lst, istart + d, end, ignorable);
                lend = d;
                d++;
                if ((istart + d) >= lst.Count || !start.Contains(lst[istart + d].name)) break;
            } while (restartable);

            if (lend == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            name = this.name;
            brush = this.brush;
            comp = this.comp;
            snapshots = new List<int>(lend);
            for (int i = istart; i < istart + lend + 1; i++) if (!ignorable.Contains(lst[i].name)) snapshots.Add(i);

            return lend + 1;
        }

        private int GetDeltaIndexUntil(List<Cluster> lst, int istart, string[] stop, string[] ignore)
        {
            int d = 0;
            while (istart + d < lst.Count && ignore.Contains(lst[istart + d].name)) { d++; }
            if (istart + d == lst.Count) return 0;
            if (istart + d < 0 || istart + d > lst.Count) throw new Exception("failed sanity check: istart+d = " + (istart + d));
            if (stop.Contains(lst[istart + d].name)) return d;
            return 0;
        }

        private int GetDeltaIndexWhile(List<Cluster> lst, int istart, string[] cont, string[] ignore)
        {
            int d = 0;
            int ld = 0;
            while (istart + d < lst.Count && (ignore.Contains(lst[istart + d].name) || cont.Contains(lst[istart + d].name)))
            {
                if (!ignore.Contains(lst[istart + d].name) && cont.Contains(lst[istart + d].name)) ld = d;
                d++;
            }
            return ld;
        }
    }

    [Serializable]
    public class ClusteringPredicate_PairWithIgnorable_UseLast : ClusteringPredicate
    {
        private string[] start;
        private string[] end;
        private string[] ignorable;
        private bool multistart;
        private bool multiend;
        private bool restartable;
        private bool onlylastsnapshot;

        public ClusteringPredicate_PairWithIgnorable_UseLast(ModelingHistory history, string label) : base(history, label) { }

        protected override void ReadBinaryData(BinaryReader br)
        {
            start = br.ReadArray<string>();
            end = br.ReadArray<string>();
            ignorable = br.ReadArray<string>();
            multistart = br.ReadBoolean();
            multiend = br.ReadBoolean();
            restartable = br.ReadBoolean();
            onlylastsnapshot = br.ReadBoolean();
        }

        public ClusteringPredicate_PairWithIgnorable_UseLast(string[] start, string[] end, string[] ignorable, bool multistart, bool multiend, bool restartable, ModelingHistory history, string label)
            : this(start, end, ignorable, multistart, multiend, restartable, false, history, label)
        { }

        public ClusteringPredicate_PairWithIgnorable_UseLast(string[] start, string[] end, string[] ignorable, bool multistart, bool multiend, bool restartable, bool onlylastsnapshot, ModelingHistory history, string label)
            : base(history, label)
        {

            this.start = start;
            this.end = end;
            this.ignorable = ignorable;
            this.multistart = multistart;
            this.multiend = multiend;
            this.restartable = restartable;
            this.onlylastsnapshot = onlylastsnapshot;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0, lend = 0;
            if (!start.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            do
            {
                if (multistart) d += GetDeltaIndexWhile(lst, istart + d, start, ignorable);
                if (!start.Contains(lst[istart + d].name)) break;
                d++;
                if (istart + d >= lst.Count) break;
                d += GetDeltaIndexUntil(lst, istart + d, end, ignorable);
                if (!end.Contains(lst[istart + d].name)) break;
                if (multiend) d += GetDeltaIndexWhile(lst, istart + d, end, ignorable);
                lend = d;
                d++;
                if ((istart + d) >= lst.Count || !start.Contains(lst[istart + d].name)) break;
            } while (restartable);

            if (lend == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            name = lst[lend + istart].name;
            brush = lst[lend + istart].brush;
            comp = lst[lend + istart].composition;

            if (onlylastsnapshot)
            {
                snapshots = new List<int>() { istart + lend };
            }
            else {
                snapshots = new List<int>(lend);
                for (int i = istart; i < istart + lend + 1; i++) if (!ignorable.Contains(lst[i].name)) snapshots.Add(i);
            }

            return lend + 1;
        }

        private int GetDeltaIndexUntil(List<Cluster> lst, int istart, string[] stop, string[] ignore)
        {
            int d = 0;
            while (istart + d < lst.Count && ignore.Contains(lst[istart + d].name)) { d++; }
            if (istart + d == lst.Count) return 0;
            if (istart + d < 0 || istart + d > lst.Count) throw new Exception("failed sanity check: istart+d = " + (istart + d));
            if (stop.Contains(lst[istart + d].name)) return d;
            return 0;
        }

        private int GetDeltaIndexWhile(List<Cluster> lst, int istart, string[] cont, string[] ignore)
        {
            int d = 0;
            int ld = 0;
            while (istart + d < lst.Count && (ignore.Contains(lst[istart + d].name) || cont.Contains(lst[istart + d].name)))
            {
                if (!ignore.Contains(lst[istart + d].name) && cont.Contains(lst[istart + d].name)) ld = d;
                d++;
            }
            return ld;
        }
    }

    [Serializable]
    public class ClusteringPredicate_PairWithIgnorable_UseFirst : ClusteringPredicate
    {
        private string[] start;
        private string[] end;
        private string[] ignorable;
        private bool multistart;
        private bool multiend;
        private bool restartable;

        public ClusteringPredicate_PairWithIgnorable_UseFirst(ModelingHistory history, string label) : base(history, label) { }

        protected override void ReadBinaryData(BinaryReader br)
        {
            start = br.ReadArray<string>();
            end = br.ReadArray<string>();
            ignorable = br.ReadArray<string>();
            multistart = br.ReadBoolean();
            multiend = br.ReadBoolean();
            restartable = br.ReadBoolean();
        }

        public ClusteringPredicate_PairWithIgnorable_UseFirst(string[] start, string[] end, string[] ignorable, bool multistart, bool multiend, bool restartable, ModelingHistory history, string label)
            : base(history, label)
        {
           
            this.start = start;
            this.end = end;
            this.ignorable = ignorable;
            this.multistart = multistart;
            this.multiend = multiend;
            this.restartable = restartable;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0, lend = 0;
            if (!start.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            do
            {
                if (multistart) d += GetDeltaIndexWhile(lst, istart + d, start, ignorable);
                if (!start.Contains(lst[istart + d].name)) break;
                d++;
                if (istart + d >= lst.Count) break;
                d += GetDeltaIndexUntil(lst, istart + d, end, ignorable);
                if (!end.Contains(lst[istart + d].name)) break;
                if (multiend) d += GetDeltaIndexWhile(lst, istart + d, end, ignorable);
                lend = d;
                d++;
                if ((istart + d) >= lst.Count || !start.Contains(lst[istart + d].name)) break;
            } while (restartable);

            if (lend == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            name = lst[istart].name;
            brush = lst[istart].brush;
            comp = lst[istart].composition;
            snapshots = new List<int>(lend);
            for (int i = istart; i < istart + lend + 1; i++) if (!ignorable.Contains(lst[i].name)) snapshots.Add(i);

            return lend + 1;
        }

        private int GetDeltaIndexUntil(List<Cluster> lst, int istart, string[] stop, string[] ignore)
        {
            int d = 0;
            while (istart + d < lst.Count && ignore.Contains(lst[istart + d].name)) { d++; }
            if (istart + d == lst.Count) return 0;
            if (istart + d < 0 || istart + d > lst.Count) throw new Exception("failed sanity check: istart+d = " + (istart + d));
            if (stop.Contains(lst[istart + d].name)) return d;
            return 0;
        }

        private int GetDeltaIndexWhile(List<Cluster> lst, int istart, string[] cont, string[] ignore)
        {
            int d = 0;
            int ld = 0;
            while (istart + d < lst.Count && (ignore.Contains(lst[istart + d].name) || cont.Contains(lst[istart + d].name)))
            {
                if (!ignore.Contains(lst[istart + d].name) && cont.Contains(lst[istart + d].name)) ld = d;
                d++;
            }
            return ld;
        }
    }

    [Serializable]
    public class ClusteringPredicate_RepeatsWithIgnorable : ClusteringPredicate
    {
        private string[] repeated;
        private string[] ignorable;
        private string name;
        private SolidBrush brush;
        private Composition comp;

        public ClusteringPredicate_RepeatsWithIgnorable(ModelingHistory history, string label) : base(history, label) { }
    
        protected override void ReadBinaryData(BinaryReader br)
        {
            repeated = br.ReadArray<string>();
            ignorable = br.ReadArray<string>();
            name = br.ReadString();
            brush = br.ReadSolidBrush();
            br.Read(out comp);
        }

        public ClusteringPredicate_RepeatsWithIgnorable(string name, SolidBrush brush, Composition comp, string repeated, string[] ignorable, ModelingHistory history, string label)
            : this(name, brush, comp, new string[] { repeated }, ignorable, history, label)
        { }

        public ClusteringPredicate_RepeatsWithIgnorable(string name, SolidBrush brush, Composition comp, string[] repeated, string[] ignorable, ModelingHistory history, string label)
            : base(history, label)
        {
            this.name = name;
            this.brush = brush;
            this.comp = comp;
            this.repeated = repeated;
            this.ignorable = ignorable;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            if (!repeated.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            int d = 1, ld = 0;
            while (istart + d < lst.Count && (ignorable.Contains(lst[istart + d].name) || repeated.Contains(lst[istart + d].name)))
            {
                if (repeated.Contains(lst[istart + d].name)) ld = d;
                d++;
            }
            if (ld == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = this.name;
            brush = this.brush;
            comp = this.comp;
            snapshots = new List<int>(ld);
            for (int i = istart; i < istart + ld + 1; i++) if (!ignorable.Contains(lst[i].name)) snapshots.Add(i);
            return ld + 1;
        }
    }

    [Serializable]
    public class ClusteringPredicate_RepeatsWithIgnorable_UseLast : ClusteringPredicate
    {
        private string[] repeated;
        private string[] ignorable;
        private string name;
        private SolidBrush brush;
        private Composition comp;

        public ClusteringPredicate_RepeatsWithIgnorable_UseLast(ModelingHistory history, string label) : base(history, label) { }

        protected override void ReadBinaryData(BinaryReader br)
        {
            repeated = br.ReadArray<string>();
            ignorable = br.ReadArray<string>();
            name = br.ReadString();
            brush = br.ReadSolidBrush();
            br.Read(out comp);
        }

        public ClusteringPredicate_RepeatsWithIgnorable_UseLast(string name, SolidBrush brush, Composition comp, string repeated, string[] ignorable, ModelingHistory history, string label)
            : this(name, brush, comp, new string[] { repeated }, ignorable, history, label)
        { }

        public ClusteringPredicate_RepeatsWithIgnorable_UseLast(string name, SolidBrush brush, Composition comp, string[] repeated, string[] ignorable, ModelingHistory history, string label)
            : base(history, label)
        {
            this.name = name;
            this.brush = brush;
            this.comp = comp;
            this.repeated = repeated;
            this.ignorable = ignorable;
        }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            if (!repeated.Contains(lst[istart].name)) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            int d = 1, ld = 0;
            while (istart + d < lst.Count && (ignorable.Contains(lst[istart + d].name) || repeated.Contains(lst[istart + d].name)))
            {
                if (repeated.Contains(lst[istart + d].name)) ld = d;
                d++;
            }
            if (ld == 0) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = this.name;
            brush = this.brush;
            comp = this.comp;
            snapshots = new List<int>() { istart + ld };
            //snapshots = new List<int>( ld );
            //for( int i = istart; i < istart + ld + 1; i++ ) if( !ignorable.Contains( lst[i].name ) ) snapshots.Add( i );
            return ld + 1;
        }
    }

    [Serializable]
    public class ClusteringPredicate_Connected : ClusteringPredicate
    {
        public ClusteringPredicate_Connected(ModelingHistory history, string label) : base(history, label) { }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0;
            Cluster cluster;
            HashSet<int> connecteduids = null;
            IndexedViewableAlpha viewable = null;
            while (istart + d < lst.Count)
            {
                cluster = lst[istart + d];
                viewable = history.GetViewables(cluster, false, false, true, false)[0];
                if (viewable.Selected.Any()) break;
                d++;
            }
            List<int>[] connected = GetConnectedComponents(viewable);
            connecteduids = GetConnectedUIDs(viewable, connected);

            d++;
            while (istart + d < lst.Count)
            {
                cluster = lst[istart + d];
                if (cluster.name == "topo.delete.vert" || cluster.name == "topo.delete.edge" || cluster.name == "topo.delete.face") { d++; continue; }
                IndexedViewableAlpha newviewable = history.GetViewables(cluster, false, false, true, false)[0];
                if (!newviewable.Selected.Any()) { d++; continue; }

                List<int>[] newconnected = GetConnectedComponents(newviewable);
                HashSet<int> newconnecteduids = GetConnectedUIDs(newviewable, newconnected);

                bool intersect = false;
                foreach (int uid in newconnecteduids) if (connecteduids.Contains(uid)) { intersect = true; break; }
                if (!intersect) break;

                foreach (int uid in newconnecteduids) connecteduids.Add(uid);
                d++;
            }

            if (d == 1) { name = null; brush = null; comp = null; snapshots = null; return 0; }
            name = "Connected Component";
            brush = new SolidBrush(Color.Azure);
            comp = new Composition(CompositionPresets.MeshDiff);
            snapshots = Enumerable.Range(istart, d).ToList();
            return d;
        }

        protected HashSet<int> GetConnectedUIDs(IndexedViewableAlpha viewable, List<int>[] components)
        {
            HashSet<int> uids = new HashSet<int>();
            bool[] addcomps = new bool[components.Length];
            for (int i = 0; i < viewable.nVerts; i++) if (viewable.Selected[i])
                {
                    int uid = viewable.VertUIDs[i];
                    for (int ic = 0; ic < components.Length; ic++) if (components[ic].Contains(uid)) { addcomps[ic] = true; break; }
                }
            for (int ic = 0; ic < components.Length; ic++) if (addcomps[ic])
                    foreach (int cuid in components[ic]) uids.Add(cuid);
            return uids;
        }

        protected List<int>[] GetConnectedComponents(IndexedViewableAlpha viewable)
        {
            List<int> edges = new List<int>();
            for (int ig = 0; ig < viewable.GroupSizes.Length; ig++) if (viewable.GroupSizes[ig] == 2) edges.AddRange(viewable.Indices[ig]);
            for (int i = 0; i < edges.Count; i++) edges[i] = viewable.VertUIDs[edges[i]];

            int[] connectedto = Enumerable.Repeat(-1, history.UniqueVertCount).ToArray();
            int[] connectedfrom = Enumerable.Repeat(-1, history.UniqueVertCount).ToArray();
            int[] connectbases = Enumerable.Repeat(-1, history.UniqueVertCount).ToArray();
            bool[] touched = new bool[history.UniqueVertCount];

            for (int i = 0; i < edges.Count; i += 2)
            {
                int i0 = edges[i], i1 = edges[i + 1];
                if (connectedto[i0] == i1 || connectedto[i1] == i0) continue;

                int i0b = FindEnd(i0, connectedfrom);
                int i1b = FindEnd(i1, connectedfrom);
                if (i0b == i1b) continue;
                int i0e = FindEnd(i0, connectedto);

                connectedto[i0e] = i1b;
                connectedfrom[i1b] = i0e;

                touched[i0] = true;
                touched[i1] = true;
            }

            List<int> ibases = new List<int>();
            for (int i = 0; i < history.UniqueVertCount; i++)
            {
                if (connectbases[i] != -1 || !touched[i]) continue;
                int ibase = FindEnd(i, connectedfrom);
                if (connectbases[ibase] == -1) ibases.Add(ibase);
                for (int iter = ibase; iter != -1; iter = connectedto[iter]) connectbases[iter] = ibase;
            }

            int count = ibases.Count;
            List<int>[] comps = new List<int>[count];
            for (int i = 0; i < count; i++) comps[i] = new List<int>();
            for (int i = 0; i < history.UniqueVertCount; i++)
            {
                if (!touched[i]) continue;
                int ind = ibases.IndexOf(connectbases[i]);
                comps[ind].Add(i);
            }

            return comps;
        }

        private int FindEnd(int i, int[] connections)
        {
            while (connections[i] != -1) i = connections[i];
            return i;
        }
    }

    [Serializable]
    public class ClusteringPredicate_Hide : ClusteringPredicate
    {
        protected string[] hide;

        public ClusteringPredicate_Hide(ModelingHistory history, string label, string[] hide) : base(history, label) { this.hide = hide; }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0;
            while (istart + d < lst.Count && hide.Contains(lst[istart + d].name)) { d++; }
            if (d == 0 || istart + d == lst.Count) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            Cluster cluster = lst[istart + d];
            name = cluster.name;
            brush = cluster.brush;
            comp = cluster.composition;
            snapshots = new List<int>() { istart + d };
            return d + 1;
        }
    }

    [Serializable]
    public class ClusteringPredicate_Views : ClusteringPredicate_StartsWith
    {
        public ClusteringPredicate_Views(ModelingHistory history) : base("view", history, "Cluster View OTags") { }
    }

    [Serializable]
    public class ClusteringPredicate_Selects : ClusteringPredicate
    {
        public ClusteringPredicate_Selects(ModelingHistory history) : base(history, "Cluster Select OTags") { }

        protected override int GetDeltaIndex(List<Cluster> lst, int istart, out string name, out SolidBrush brush, out Composition comp, out List<int> snapshots)
        {
            int d = 0;
            while (istart + d < lst.Count && lst[istart + d].name.StartsWith("select")) { d++; }
            if (d == 0 || istart + d == lst.Count) { name = null; brush = null; comp = null; snapshots = null; return 0; }

            Cluster cluster = lst[istart + d];
            name = cluster.name;
            brush = cluster.brush;
            comp = cluster.composition;
            snapshots = new List<int>() { istart + d };
            return d + 1;
        }
    }

    [Serializable]
    public class ClusteringCustom : Clustering
    {
        public ClusteringCustom(ModelingHistory history, string label) : base(history, label) { clusters = new List<Cluster>(); }

        public void ClusterAdd(Cluster cluster)
        {
            if (clusters.Count == 0) clusters.Add(cluster);
            else {
                bool added = false;
                for (int i = 0; i < clusters.Count; i++)
                    if (clusters[i].start > cluster.end) { added = true; clusters.Insert(i, cluster); break; }
                if (!added) clusters.Add(cluster);
            }
            Evaluate();
        }

        public void ClusterRemove(int icluster) { clusters.RemoveAt(icluster); Evaluate(); }

        protected override void EvaluateCluster() { }
    }

    [Serializable]
    public class ClusteringLevel0 : Clustering
    {
        public ClusteringLevel0(ModelingHistory history) : base(history, "Original") { }

        public ClusteringLevel0(Func<string, int, SolidBrush> CommandToBrush, ModelingHistory history)
            : base(history, "Original")
        {
            this.mylevel = 0;

            clusters = Enumerable.Range(0, history.SnapshotCount)
                .Select((int i) => new Cluster(i, i, history[i].command, Composition.GetCompositionByOperation(history[i].command)) { brush = CommandToBrush(history[i].command, i) })
                    .ToList();
        }

        public override void Remove() { throw new Exception("Cannot remove Level0"); }

        protected override void EvaluateCluster() { }

        public override List<Cluster> GetClusters() { return clusters; }

        public override List<int> GetLevels() { return Enumerable.Repeat(0, history.SnapshotCount).ToList(); }
    }
}


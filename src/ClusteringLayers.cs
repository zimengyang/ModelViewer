using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    [Serializable]
    public class ClusteringLayers
    {
        private ModelingHistory history;
        private ClusteringLevel0 level0;
        //private Clustering levelTop;

        public List<int> leveltrans = new List<int>();
        public List<string> leveltransnames = new List<string>();

        public int nlevels { get; private set; }

        public delegate void ReevaluatedHandler();
        public event ReevaluatedHandler Reevaluated;
        public void FireReevalutadeHandler() { if (Reevaluated != null) Reevaluated(); }

        public ClusteringLayers() { }

        public ClusteringLayers(ModelingHistory history, Func<string, int, SolidBrush> CommandToBrush)
        {
            this.history = history;
            level0 = new ClusteringLevel0(CommandToBrush, history); //levelTop = 
            level0.Reevaluated += ClusteringReevaluated;
            nlevels = 1;
        }

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    bw.Write(nlevels);
        //    level0.WriteBinary(bw);
        //}

        //public static ClusteringLayers ReadBinary(BinaryReader br, ModelingHistory history)
        //{
        //    ClusteringLayers layers = new ClusteringLayers();
        //    layers.nlevels = br.ReadInt32();
        //    layers.level0 = (ClusteringLevel0)Clustering.ReadBinary(br, history);

        //    for (Clustering cl = layers.level0; cl != null; cl = cl.Above)
        //        cl.Reevaluated += layers.ClusteringReevaluated;

        //    layers.level0.Evaluate();
        //    return layers;
        //}

        public void Debug_CheckIntegrity()
        {
            if (GetClusteringLayers().Count != nlevels) throw new Exception("nlevels incorrect");
            for (Clustering c = level0; c != null; c = c.Above)
            {
                if (c.Below != null && c.Below.Above != c) throw new Exception("c.Below.Above != this");
                if (c.Above != null && c.Above.Below != c) throw new Exception("c.Above.Below != this ");
                if (c.Above != null && c.Above.Level != c.Level + 1) throw new Exception("c.Above.Level != c.Level + 1");
                c.CheckValidLayering();
            }
        }

        public void ClusteringReevaluated(Clustering cluster)
        {
            if (cluster.Level == nlevels - 1) FireReevalutadeHandler();
        }

        /*public void AddLayer( Clustering newlayer )
		{
			Clustering lasttop = levelTop;
			nlevels++;
			levelTop = newlayer;
			newlayer.Reevaluated += ClusteringReevaluated;
			lasttop.InsertLayerAbove( newlayer );
			//FireReevalutadeHandler();
		}*/

        public int AddLayer(Clustering newlayer) { return AddLayer(nlevels, newlayer); }
        public int AddLayer(int level, Clustering newlayer)
        {
            if (level == 0) throw new ArgumentException("Cannot insert Clustering Layer into level 0");
            if (level > nlevels) throw new ArgumentException("Level is out of range");

            Clustering layer = GetClusteringLayer(level - 1);

            //if( level == nlevels ) { levelTop = newlayer; }
            nlevels++;

            newlayer.Reevaluated += ClusteringReevaluated; ;
            layer.InsertLayerAbove(newlayer);

            return level;
        }

        public Clustering RemoveLevel()
        {
            if (nlevels == 1) throw new Exception("Must have at least 1 level");
            //levelTop = levelTop.Below;
            return RemoveLevel(nlevels - 1);
        }

        public Clustering RemoveLevel(int ilevel)
        {
            if (ilevel == 0) throw new ArgumentException("Cannot remove Level0 Clustering Layer");

            Clustering removed = GetClusteringLayer(ilevel);
            removed.Reevaluated -= ClusteringReevaluated;
            removed.Remove();
            nlevels--;
            FireReevalutadeHandler();
            return removed;
        }

        public List<Clustering> GetClusteringLayers()
        {
            List<Clustering> lst = new List<Clustering>();
            for (Clustering c = level0; c != null; c = c.Above) lst.Add(c);
            return lst;
        }

        public Clustering GetClusteringLayer() { return level0.GetTopLayer(); } // levelTop; }

        public Clustering GetClusteringLayer(int level)
        {
            Clustering c = level0;
            while (c.Level < level) c = c.Above;
            return c;
        }

        public List<Cluster> GetClusters() { return level0.GetTopLayer().GetClusters(); } // levelTop.GetClusters(); }
        public List<Cluster> GetClusters(int maxlevel)
        {
            return GetClusteringLayer(maxlevel).GetClusters();
        }

        public List<int> GetLevels() { return level0.GetTopLayer().GetLevels(); } // levelTop.GetLevels(); }
        public List<int> GetLevels(int maxlevel)
        {
            return GetClusteringLayer(maxlevel).GetLevels();
        }

        public Cluster GetCluster(int index0) { return GetCluster(nlevels - 1, index0); }
        public Cluster GetCluster(int maxlevel, int index0)
        {
            return GetClusteringLayer(maxlevel).GetCluster(index0);
        }

    }

}


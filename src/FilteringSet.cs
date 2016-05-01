using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public class FilteringSet
    {
        List<Filtering> filters;

        public delegate void ReevaluatedHandler();
        public event ReevaluatedHandler Reevaluated;
        public void FireReevalutadeHandler() { if (Reevaluated != null) Reevaluated(); }

        public FilteringSet() { filters = new List<Filtering>(); }

        public IEnumerator GetEnumerator() { foreach (Filtering filter in filters) yield return filter; }

        public int AddFilter(Filtering newfilter)
        {
            int i = filters.Count;
            filters.Add(newfilter);
            newfilter.Reevaluated += FireReevalutadeHandler;
            FireReevalutadeHandler();
            return i;
        }

        public void AddFilters(params Filtering[] newfilters)
        {
            foreach (Filtering filter in newfilters) AddFilter(filter);
        }

        public void RemoveFilter(int i)
        {
            filters[i].Reevaluated -= FireReevalutadeHandler;
            filters.RemoveAt(i);
            FireReevalutadeHandler();
        }

        public bool IsFiltered(Cluster cluster)
        {
            return filters.Any((Filtering filter) => filter.IsFiltered(cluster));
        }

        public bool[] Filter(List<Cluster> lstClusters)
        {
            return lstClusters.Select((Cluster cluster) => IsFiltered(cluster)).ToArray();
        }

        public int[] FilteredIndices(List<Cluster> lstClusters)
        {
            bool[] filtered = Filter(lstClusters);
            return Enumerable.Range(0, lstClusters.Count).Where((int i) => filtered[i]).ToArray();
        }

        public List<Cluster> FilteredClusters(List<Cluster> lstClusters)
        {
            int[] inds = FilteredIndices(lstClusters);
            return inds.Select((int ind) => lstClusters[ind]).ToList();
        }
    }
}


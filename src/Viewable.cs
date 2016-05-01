using System;

using System.Collections.Generic;
using System.Linq;
using Common.Libs.VMath;
using Common.Libs.MiscFunctions;
using Common.Libs.MatrixMath;

namespace MeshFlowViewer
{
    public class IndexedViewableAlpha
    {
        public int nVerts;
        public int[] VertUIDs = null;
        public Vec3f[] Verts = null;
        public Vec3f[] Norms = null;
        public Vec4f[][] Colors = null;
        public bool[] Selected = null;

        public int[] GroupSizes;
        public int[][] Indices = null;
        public int[][] GroupsUIDs = null;
        public int[][] GroupsUIDsThin = null;
        public ulong[][] GroupsUIDsThinKey = null;
        public int[][] GroupsUIDsThinInds = null;
        public float[] PointSizes = null;
        public float[] LineWidths = null;

        public IndexedViewableAlpha attached = null;

        #region Locking for Editing

        private object alock = new object();
        private bool editing = false;

        public void StartEdit()
        {
            for (bool loop = true; loop;) lock (alock)
               if (!editing) { editing = true; loop = false; }
        }

        public void EndEdit() { editing = false; }

        #endregion

        //public IndexedViewableAlpha( Vec3f[] verts, Vec4f[][] colors, int[][] indices, float[] pointsizes, float[] linewidths, int[] groupsizes )
        //	: this( verts, colors, indices, pointsizes, linewidths, groupsizes, null, null ) { }

        public IndexedViewableAlpha(Vec3f[] verts, Vec4f[][] colors, int[][] indices, float[] pointsizes, float[] linewidths, int[] groupsizes, int[] vertuids, bool[] selected)
        {
            this.nVerts = verts.Length;
            this.Verts = verts;
            this.Colors = colors;
            this.GroupSizes = groupsizes;
            this.Indices = indices;
            this.PointSizes = pointsizes;
            this.LineWidths = linewidths;

            if (vertuids == null)
            {
                throw new Exception();
                //this.VertUIDs = new int[nVerts];
                //for( int i = 0; i < nVerts; i++ ) VertUIDs[i] = i;
            }
            else this.VertUIDs = vertuids;

            if (selected == null) selected = new bool[nVerts];
            this.Selected = selected;
        }

        private IndexedViewableAlpha() { }

        public IndexedViewableAlpha DeepCopy()
        {
            IndexedViewableAlpha viewable = new IndexedViewableAlpha();

            viewable.nVerts = nVerts;
            viewable.Verts = Verts.DeepCopy();
            if (Norms != null) viewable.Norms = Norms.DeepCopy();
            viewable.Colors = Colors.DeepCopy();
            viewable.GroupSizes = GroupSizes.DeepCopy();
            viewable.Indices = Indices.DeepCopy();
            viewable.PointSizes = PointSizes.DeepCopy();
            viewable.LineWidths = LineWidths.DeepCopy();
            viewable.VertUIDs = VertUIDs.DeepCopy();
            viewable.Selected = Selected.DeepCopy();
            viewable.GroupsUIDs = GroupsUIDs.DeepCopy();
            viewable.GroupsUIDsThin = GroupsUIDsThin.DeepCopy();
            viewable.GroupsUIDsThinKey = GroupsUIDsThinKey.DeepCopy();
            viewable.GroupsUIDsThinInds = GroupsUIDsThinInds.DeepCopy();

            return viewable;
        }

        public void Attach(IndexedViewableAlpha next) { if (this.attached == null) attached = next; else attached.Attach(next); }
        public static IndexedViewableAlpha Attach(IndexedViewableAlpha v, IndexedViewableAlpha next)
        {
            if (v == null) return next;
            if (next != null) v.Attach(next);
            return v;
        }

        public void FillGroupUIDs()
        {
            if (GroupsUIDs != null) return;
            GroupsUIDs = new int[Indices.Length][];
            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int[] inds = Indices[ig];
                GroupsUIDs[ig] = VertUIDs.Reorder(inds);
            }
        }

        public void FillGroupUIDsAll()
        {
            if (GroupsUIDs != null) return;

            GroupsUIDs = new int[Indices.Length][];
            GroupsUIDsThin = new int[4][]; // { lstgroups[0].ToArray(), lstgroups[1].ToArray(), lstgroups[2].ToArray(), lstgroups[3].ToArray() };
            GroupsUIDsThinKey = new ulong[4][];
            GroupsUIDsThinInds = new int[4][];

            List<ulong>[] lstkeys = new List<ulong>[] { new List<ulong>(), new List<ulong>(), new List<ulong>(), new List<ulong>() };
            List<int>[] lstgroups = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            List<int>[] lstinds = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int[] inds = Indices[ig];
                int sz = GroupSizes[ig];
                int fullcount = inds.Length;
                int count = fullcount / sz;

                // convert indices to uids
                int[] uids = VertUIDs.Reorder(inds);

                // calculate a key for each group
                ulong[] keys = new ulong[count];
                for (int i = 0, ik = 0; ik < count; i += sz, ik++)
                {
                    ulong key = 0;
                    for (int iadd = 0; iadd < sz; iadd++) key = key * 65536 + (ulong)uids[i + iadd];
                    keys[ik] = key;
                }

                GroupsUIDs[ig] = uids;
                lstkeys[sz - 1].AddRange(keys);
                lstgroups[sz - 1].AddRange(uids);
                lstinds[sz - 1].AddRange(inds);
            }


            for (int ig = 0; ig < 4; ig++)
            {
                int sz = ig + 1;
                List<ulong> unkeys = lstkeys[ig];
                List<int> uids = lstgroups[ig];
                List<int> inds = lstinds[ig];
                int fullcount = uids.Count;
                int count = fullcount / sz;

                if (fullcount % sz != 0) throw new Exception("failed sanity check");
                if (uids.Count != inds.Count) throw new Exception("failed sanity check");
                if (unkeys.Count != count) throw new Exception("failed sanity check");

                int[] ouids = new int[fullcount];
                int[] oinds = new int[fullcount];
                ulong[] okeys = new ulong[count];

                // order keys, uids, and inds based on keys
                int[] order = unkeys.GetSortIndices_QuickSort();
                for (int i = 0, k = 0; k < count; i += sz, k++)
                {
                    int ind = order[k];
                    int ind2 = ind * sz;
                    okeys[k] = unkeys[ind];
                    for (int iadd = 0; iadd < sz; iadd++)
                    {
                        ouids[i + iadd] = uids[ind2 + iadd];
                        oinds[i + iadd] = inds[ind2 + iadd];
                    }
                }

                GroupsUIDsThin[ig] = ouids;
                GroupsUIDsThinKey[ig] = okeys;
                GroupsUIDsThinInds[ig] = oinds;

                if (fullcount > 0)
                {
                    ulong keycheck = okeys[0];
                    for (int ic = 1; ic < count; ic++) if (okeys[ic] >= keycheck) keycheck = okeys[ic]; else throw new Exception();
                }
            }
        }

        // offsets all of the verts by vector
        public IndexedViewableAlpha Offset(Vec3f offset)
        {
            StartEdit();
            for (int i = 0; i < nVerts; i++) Verts[i] = Verts[i] + offset;
            EndEdit();
            return this;
        }

        // turns all tris into quads (duplicate a vert)
        public IndexedViewableAlpha Quadify()
        {
            StartEdit();

            for (int ig = 0; ig < Indices.Length; ig++)
            {
                if (GroupSizes[ig] != 3) continue;
                int[] oldgroups = Indices[ig];
                int count = oldgroups.Length / 3;
                int[] newgroups = new int[count * 4];
                for (int i = 0; i < count; i++)
                {
                    newgroups[i * 4 + 0] = oldgroups[i * 3 + 0];
                    newgroups[i * 4 + 1] = oldgroups[i * 3 + 1];
                    newgroups[i * 4 + 2] = oldgroups[i * 3 + 2];
                    newgroups[i * 4 + 3] = oldgroups[i * 3 + 2];
                }
                GroupSizes[ig] = 4;
                Indices[ig] = newgroups;
            }
            GroupsUIDs = null;
            GroupsUIDsThin = null;

            EndEdit();
            return this;
        }

        /*
		// forces data into 4 groups (verts, edges, tris, quads)
		// cannot have different pt and ln sizes within same group!
		public IndexedViewableAlpha MakeThin()
		{
			StartEdit();
			
			int newnverts = nVerts * Indices.Length;
			
			List<Vec3f> newverts = new List<Vec3f>( newnverts );
			List<int> newvertuids = new List<int>( newnverts );
			List<Vec4f>[] newcolors = new List<Vec4f>[] { new List<Vec4f>( newnverts ), new List<Vec4f>( newnverts ), new List<Vec4f>( newnverts ), new List<Vec4f>( newnverts ) };
			List<int>[] newgroupslist = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
			List<bool> newselected = new List<bool>( newnverts );
			
			var newblankcolors = Enumerable.Repeat( new Vec4f(), nVerts );
			
			for( int ig = 0, aiv = 0; ig < Indices.Length; ig++, aiv += nVerts )
			{
				int sz = GroupSizes[ig];
				
				newverts.AddReturn( Verts );
				newvertuids.AddReturn( VertUIDs );
				newselected.AddReturn( Selected );
				newgroupslist[GroupSizes[ig] - 1].AddReturn( Indices[ig].Select( (int ind) => ind + aiv ) );
				
				for( int ig2 = 0; ig2 < 4; ig2++ )
				{
					if( ig2 == sz - 1 ) newcolors[ig2].AddReturn( Colors[ig].DeepCopy() );
					else newcolors[ig2].AddReturn( newblankcolors );
				}
			}
			
			Verts = newverts.ToArray();
			Selected = newselected.ToArray();
			Colors = newcolors.Select( (List<Vec4f> lst) => lst.ToArray() ).ToArray();
			Indices = newgroupslist.Select( (List<int> lst) => lst.ToArray() ).ToArray();
			VertUIDs = newvertuids.ToArray();
			nVerts = newnverts;
			
			PointSizes = new float[] { PointSizes[0], PointSizes[1], PointSizes[2], PointSizes[3] };
			LineWidths = new float[] { LineWidths[0], LineWidths[1], LineWidths[2], LineWidths[3] };
			GroupSizes = new int[] { 1, 2, 3, 4 };
			GroupsUIDs = null;
			GroupsUIDsThin = null;
			
			EndEdit();
			return this;
		}
		*/

        // removes any unreferenced verts (and trims associated selected, colors, uids arrays)
        public static IndexedViewableAlpha Trim(IndexedViewableAlpha viewable)
        {
            int nverts = 0;
            bool[] used = new bool[viewable.nVerts];
            int[][] lstoldgroups = viewable.Indices;
            int cgroups = lstoldgroups.Length;
            for (int igroups = 0; igroups < cgroups; igroups++)
                foreach (int ind in lstoldgroups[igroups]) used[ind] = true;

            int[] mapind = new int[viewable.nVerts];
            for (int i = 0; i < viewable.nVerts; i++)
            {
                if (!used[i]) { mapind[i] = -1; continue; }
                mapind[i] = nverts++;
            }

            Vec3f[] verts = new Vec3f[nverts];
            int[] uids = new int[nverts];
            bool[] selected = new bool[nverts];
            Vec4f[][] colors = new Vec4f[cgroups][];
            int[][] lstgroups = new int[cgroups][];
            for (int ig = 0; ig < cgroups; ig++) { colors[ig] = new Vec4f[nverts]; lstgroups[ig] = new int[lstoldgroups[ig].Length]; }

            for (int i = 0; i < viewable.nVerts; i++)
            {
                int ivert = mapind[i];
                if (ivert == -1) continue;

                verts[ivert] = viewable.Verts[i];
                uids[ivert] = viewable.VertUIDs[i];
                selected[ivert] = viewable.Selected[i];
                for (int ig = 0; ig < cgroups; ig++) colors[ig][ivert] = viewable.Colors[ig][i];
            }

            for (int ig = 0; ig < cgroups; ig++)
            {
                int[] groups = lstgroups[ig];
                int[] oldgroups = lstoldgroups[ig];
                for (int i = 0; i < oldgroups.Length; i++) groups[i] = mapind[oldgroups[i]];
            }

            return new IndexedViewableAlpha(verts, colors, lstgroups, viewable.PointSizes.DeepCopy(), viewable.LineWidths.DeepCopy(), viewable.GroupSizes.DeepCopy(), uids, selected);
        }

        public static IndexedViewableAlpha TrimInvisible(IndexedViewableAlpha viewable)
        {
            if (viewable == null) return null;

            int nverts = 0;
            int[][] lstoldgroups = viewable.Indices;
            int cgroups = lstoldgroups.Length;

            for (int igroups = 0; igroups < cgroups; igroups++)
            {
                int[] lstgroups = lstoldgroups[igroups];
                Vec4f[] lstcolors = viewable.Colors[igroups];
                List<int> newinds = new List<int>(lstgroups.Length);
                foreach (int ind in lstgroups) if (lstcolors[ind].w >= 0.01f) newinds.Add(ind);
                if (newinds.Count == lstgroups.Length) continue;
                viewable.Indices[igroups] = newinds.ToArray();
            }

            if (viewable.attached != null) viewable.attached = TrimInvisible(viewable.attached);

            return viewable;
        }

        public IndexedViewableAlpha ZSortGroups(CameraProperties camera, Matrix mat)
        {
            StartEdit();

            Vec3f cam = camera.GetPosition();

            for (int i = 0; i < nVerts; i++)
            {
                if (VertUIDs[i] >= 0) continue;
                if (camera.GetOrtho()) Verts[i] = Verts[i] - camera.GetForward() * 0.1f;
                else Verts[i] = Verts[i] + Vec3f.Normalize(cam - Verts[i]) * 0.1f;
            }

            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int sz = GroupSizes[ig];
                int[] groups = Indices[ig];
                int cinds = groups.Length;
                int cgroups = cinds / sz;
                int[] ngroups = new int[cinds];

                Vec4f[] colors = Colors[ig];

                float[] z = new float[cgroups];
                for (int i = 0, ind = 0; i < cgroups; i++, ind += sz)
                {
                    float zaccum = 0;
                    for (int j = 0; j < sz; j++)
                    {
                        // assume that gizmos (with no uid) are drawn on top
                        float projz = mat.Project(Verts[groups[ind + j]]).z;
                        float alphaoff = (1.0f - colors[groups[ind + j]].w) * 0.1f;
                        float gizmooff = (VertUIDs[groups[ind + j]] < 0 ? 0.05f : 0.0f);
                        zaccum += projz - alphaoff - gizmooff;
                    }
                    z[i] = -zaccum;
                }

                int[] indsordered = z.GetQuickSortedIndices();

                for (int i = 0, ij = 0; i < cgroups; i++)
                {
                    int io = indsordered.ElementAt(i) * sz;
                    for (int j = 0; j < sz; j++, ij++, io++) ngroups[ij] = groups[io];
                }

                Indices[ig] = ngroups;
            }

            EndEdit();
            return this;
        }

        public static IndexedViewableAlpha operator -(IndexedViewableAlpha v0, IndexedViewableAlpha v1)
        {
            if (v0 == null) return null;
            if (v1 == null) return v0;

            int nverts = v0.nVerts;

            int[][] groups = new int[4][];
            Vec4f[][] colors = new Vec4f[][] {
                Enumerable.Repeat( new Vec4f( 0.0f, 0.0f, 0.0f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.0f, 0.0f, 0.0f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.6f, 0.6f, 0.6f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.6f, 0.6f, 0.6f, 1.0f ), nverts ).ToArray(),
            };
            //Vec4f[][] colors = new Vec4f[][] { v0.Colors[0].DeepCopy(), v0.Colors[1].DeepCopy(), v0.Colors[2].DeepCopy(), v0.Colors[3].DeepCopy() };
            float[] pointsizes = new float[] { v0.PointSizes[0], v0.PointSizes[1], v0.PointSizes[2], v0.PointSizes[3] };
            float[] linewidths = new float[] { v0.LineWidths[0], v0.LineWidths[1], v0.LineWidths[2], v0.LineWidths[3] };
            int[] groupsizes = { 1, 2, 3, 4 };

            v0.FillGroupUIDsAll();
            v1.FillGroupUIDsAll();

            //int[] uidvert = new int[ModelingHistory.history.UniqueVertCount];
            //for( int ivert = 0; ivert < v0.nVerts; ivert++ ) uidvert[v0.VertUIDs[ivert]] = ivert;

            for (int igrps = 0; igrps < 4; igrps++)
            {
                int sz = igrps + 1;
                int[] g0inds = v0.GroupsUIDsThinInds[igrps];
                ulong[] g0keys = v0.GroupsUIDsThinKey[igrps];
                ulong[] g1keys = v1.GroupsUIDsThinKey[igrps];
                int c0 = g0keys.Length;
                int c1 = g1keys.Length;
                int i0 = 0;
                int i1 = 0;
                List<int> ngroups = new List<int>(c0 * sz);

                while (i0 < c0)
                {
                    ulong key0 = g0keys[i0];
                    ulong key1 = (i1 < c1 ? g1keys[i1] : ulong.MaxValue);
                    if (key0 < key1)
                    {
                        int istart = i0 * sz;
                        for (int i = 0; i <= igrps; i++) ngroups.Add(g0inds[istart + i]);
                    }

                    if (key0 <= key1) i0++;
                    else i1++;
                }

                groups[igrps] = ngroups.ToArray();
            }

            IndexedViewableAlpha vd = new IndexedViewableAlpha(v0.Verts.DeepCopy(), colors, groups, pointsizes, linewidths, groupsizes, v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy());
            return (vd);
        }
        public static IndexedViewableAlpha operator %(IndexedViewableAlpha v0, IndexedViewableAlpha v1)
        {
            if (v0 == null) return null;
            if (v1 == null) return v0;

            int nverts = v0.nVerts;

            int[][] groups = new int[4][];
            Vec4f[][] colors = new Vec4f[][] {
                Enumerable.Repeat( new Vec4f( 0.0f, 0.0f, 0.0f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.0f, 0.0f, 0.0f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.6f, 0.6f, 0.6f, 1.0f ), nverts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.6f, 0.6f, 0.6f, 1.0f ), nverts ).ToArray(),
            };
            //Vec4f[][] colors = new Vec4f[][] { v0.Colors[0].DeepCopy(), v0.Colors[1].DeepCopy(), v0.Colors[2].DeepCopy(), v0.Colors[3].DeepCopy() };
            float[] pointsizes = new float[] { v0.PointSizes[0], v0.PointSizes[1], v0.PointSizes[2], v0.PointSizes[3] };
            float[] linewidths = new float[] { v0.LineWidths[0], v0.LineWidths[1], v0.LineWidths[2], v0.LineWidths[3] };
            int[] groupsizes = { 1, 2, 3, 4 };

            v0.FillGroupUIDsAll();
            v1.FillGroupUIDsAll();

            //int[] uidvert = new int[ModelingHistory.history.UniqueVertCount];
            //for( int ivert = 0; ivert < v0.nVerts; ivert++ ) uidvert[v0.VertUIDs[ivert]] = ivert;

            for (int igrps = 0; igrps < 4; igrps++)
            {
                int sz = igrps + 1;
                int[] tinds = v0.GroupsUIDsThinInds[igrps];
                int[] g0uids = v0.GroupsUIDsThin[igrps];
                ulong[] g0keys = v0.GroupsUIDsThinKey[igrps];
                ulong[] g1keys = v1.GroupsUIDsThinKey[igrps];
                int c0 = g0keys.Length;
                int c1 = g1keys.Length;
                int i0 = 0;
                int i1 = 0;

                if (c0 == 0 || c1 == 0) { groups[igrps] = new int[0]; continue; }

                List<int> ngroups = new List<int>(g0uids.Length);

                while (i0 < c0 && i1 < c1)
                {
                    ulong key0 = g0keys[i0];
                    ulong key1 = g1keys[i1];
                    if (key0 == key1)
                    {
                        int istart = i0 * sz;
                        for (int i = 0; i <= igrps; i++) ngroups.Add(tinds[istart + i]);
                    }

                    if (key0 <= key1) i0++;
                    else i1++;
                }

                groups[igrps] = ngroups.ToArray();
            }

            IndexedViewableAlpha vd = new IndexedViewableAlpha(v0.Verts.DeepCopy(), colors, groups, pointsizes, linewidths, groupsizes, v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy());
            return (vd);
        }
        /*public static IndexedViewableAlpha operator%( IndexedViewableAlpha v0, IndexedViewableAlpha v1 )
		{
			if( v0 == null ) return null;
			if( v1 == null ) return v0;
			return null;
			
			int[][] groups = new int[4][];
			Vec4f[][] colors = new Vec4f[][] { v0.Colors[0], v0.Colors[1], v0.Colors[2], v0.Colors[3] };
			float[] pointsizes = new float[] { v0.PointSizes[0], v0.PointSizes[1], v0.PointSizes[2], v0.PointSizes[3] };
			float[] linewidths = new float[] { v0.LineWidths[0], v0.LineWidths[1], v0.LineWidths[2], v0.LineWidths[3] };
			int[] groupsizes = { 1, 2, 3, 4 };
			
			v0.FillGroupUIDs();
			v1.FillGroupUIDs();
			
			int[] uidvert = new int[ModelingHistory.history.UniqueVertCount];
			for( int ivert = 0; ivert < v0.nVerts; ivert++ ) uidvert[v0.VertUIDs[ivert]] = ivert;
			
			for( int igrps = 0; igrps < 4; igrps++ )
			{
				int[] g0uids = v0.GroupsUIDsThin[igrps];
				ulong[] g0keys = v0.GroupsUIDsThinKey[igrps];
				ulong[] g1keys = v1.GroupsUIDsThinKey[igrps];
				int c0 = g0keys.Length;
				int c1 = g1keys.Length;
				int i0 = 0;
				int i1 = 0;
				List<int> ngroups = new List<int>( g0uids.Length );
				
				while( i0 < c0 && i1 < c1 )
				{
					ulong key = g0keys[i0];
					if( key == g1keys[i1] ) {
						int istart = i0 * igrps;
						for( int i = 0; i <= igrps; i++ ) {
							ngroups.Add( uidvert[g0uids[istart+i]] );;
						}
						i0++;
					} else if( key < g1keys[i1] ) {
						i0++;
					} else {
						i1++;
					}
				}
				
				groups[igrps] = ngroups.ToArray();
			}
			
			IndexedViewableAlpha vd = new IndexedViewableAlpha( v0.Verts.DeepCopy(), colors, groups, pointsizes, linewidths, groupsizes, v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy() );
			return vd;
		}*/

        /*public static IndexedViewableAlpha operator-( IndexedViewableAlpha v0, IndexedViewableAlpha v1 )
		{
			if( v0 == null ) return null;
			if( v1 == null ) return v0;
			int[][] groups = new int[v0.Indices.Length][];
			
			v0.FillGroupUIDs();
			v1.FillGroupUIDs();
			
			ulong key;
			//int c;
			//bool found;
			
			for( int igrps = 0; igrps < v0.Indices.Length; igrps++ )
			{
				int sz = v0.GroupSizes[igrps];
				int[] g0 = v0.Indices[igrps];
				int[] g0t = v0.GroupsUIDs[igrps];
				//int[] g1t = v1.GroupsUIDsThin[sz - 1];
				ulong[] g1keys = v1.GroupsUIDsThinKey[sz - 1];
				
				List<int> ngroups = new List<int>();
				
				for( int ig0 = 0; ig0 < g0t.Length; ig0 += sz )
				{
					key = 0;
					for( int ig02 = 0; ig02 < sz; ig02++ ) key = key * 65536 + (ulong)g0t[ig0 + ig02];
					if( g1keys.ContainsBinarySearch_Ascending( key ) ) continue;
					for( int j = 0; j < sz; j++ ) ngroups.Add( g0[ig0 + j] );
					
					//found = false;
					//for( int ig1 = 0; ig1 < g1t.Length; ig1 += sz )
					//{
					//	if( g0t[ig0] != g1t[ig1] ) continue;
					//	if( g0t[ig0] < g1t[ig1] ) break;
					//	for( c = 1; c < sz; c++ ) if( g0t[ig0 + c] != g1t[ig1 + c] ) break;
					//	if( c == sz ) { found = true; break; }
					//}
					//if( found ) continue;
					//for( int j = 0; j < sz; j++ ) ngroups.Add( g0[ig0 + j] );
				}
				
				groups[igrps] = ngroups.ToArray();
			}
			
			IndexedViewableAlpha vd = new IndexedViewableAlpha( v0.Verts.DeepCopy(), v0.Colors.DeepCopy(), groups, v0.PointSizes.DeepCopy(), v0.LineWidths.DeepCopy(), v0.GroupSizes.DeepCopy(), v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy() );
			return vd;
		}
		
		public static IndexedViewableAlpha operator%( IndexedViewableAlpha v0, IndexedViewableAlpha v1 )
		{
			if( v0 == null ) return null;
			if( v1 == null ) return null;
			int[][] groups = new int[v0.Indices.Length][];
			
			v0.FillGroupUIDs();
			v1.FillGroupUIDs();
			
			ulong key;
			ulong nuverts = (ulong)ModelingHistory.history.UniqueVertCount;
			
			for( int igrps = 0; igrps < v0.Indices.Length; igrps++ )
			{
				int sz = v0.GroupSizes[igrps];
				int[] g0 = v0.Indices[igrps];
				int[] g0t = v0.GroupsUIDs[igrps];
				ulong[] g1keys = v1.GroupsUIDsThinKey[sz - 1];
				
				List<int> ngroups = new List<int>();
				
				for( int ig0 = 0; ig0 < g0t.Length; ig0 += sz )
				{
					key = 0;
					for( int ig02 = 0; ig02 < sz; ig02++ ) key = key * nuverts + (ulong)g0t[ig0 + ig02];
					if( !g1keys.ContainsBinarySearch_Ascending( key ) ) continue;
					for( int j = 0; j < sz; j++ ) ngroups.Add( g0[ig0 + j] );
				}
				
				groups[igrps] = ngroups.ToArray();
			}
			
			IndexedViewableAlpha vd = new IndexedViewableAlpha( v0.Verts.DeepCopy(), v0.Colors.DeepCopy(), groups, v0.PointSizes.DeepCopy(), v0.LineWidths.DeepCopy(), v0.GroupSizes.DeepCopy(), v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy() );
			return vd;
		}*/

        /*public static IndexedViewableAlpha operator%( IndexedViewableAlpha v0, IndexedViewableAlpha v1 )
		{
			if( v0 == null || v1 == null ) return null;
			List<List<int>> groups = new List<List<int>>();
			
			v0.FillGroupUIDs();
			v1.FillGroupUIDs();
			
			//List<int>[] g1ts = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
			//for( int igrps = 0; igrps < v1.Indices.Length; igrps++ )
			//	g1ts[v1.GroupSizes[igrps] - 1].AddRange( v1.Indices[igrps].Select( (int ind) => v1.VertUIDs[ind] ).ToList() );
			
			int c;
			bool found;
			
			for( int igrps = 0; igrps < v0.Indices.Length; igrps++ )
			{
				int sz = v0.GroupSizes[igrps];
				int[] g0 = v0.Indices[igrps];
				//int[] g0t = g0.Select( (int i) => v0.VertUIDs[i] ).ToArray();
				int[] g0t = v0.GroupsUIDs[igrps];
				//List<int> g1t = g1ts[sz - 1];
				int[] g1t = v1.GroupsUIDsThin[sz - 1];
				
				List<int> ngroups = new List<int>();
				
				for( int ig0 = 0; ig0 < g0t.Length; ig0 += sz )
				{
					found = false;
					for( int ig1 = 0; ig1 < g1t.Length; ig1 += sz )
					{
						if( g0t[ig0] != g1t[ig1] ) continue;
						if( g0t[ig0] < g1t[ig1] ) break;
						for( c = 1; c < sz; c++ ) if( g0t[ig0 + c] != g1t[ig1 + c] ) break;
						if( c == sz ) { found = true; break; }
					}
					if( !found ) continue;
					for( int j = 0; j < sz; j++ ) ngroups.Add( g0[ig0 + j] );
				}
				
				groups.Add( ngroups );
			}
			
			int[][] agroups = groups.Select( (List<int> grps) => grps.ToArray() ).ToArray();
			
			return new IndexedViewableAlpha( v0.Verts.DeepCopy(), v0.Colors.DeepCopy(), agroups, v0.PointSizes.DeepCopy(), v0.LineWidths.DeepCopy(), v0.GroupSizes.DeepCopy(), v0.VertUIDs.DeepCopy(), v0.Selected.DeepCopy() );
		}*/

        public static IndexedViewableAlpha operator +(IndexedViewableAlpha v0, IndexedViewableAlpha v1)
        {
            if (v0 == null) return v1;
            if (v1 == null) return v0;
            //v0.Append( v1 );
            //return v0;
            return Trim(CombineFat(v0, v1));
        }


        // combines two indexedviewablealpha, sharing the groups array (pointsizes, linewidths, groupsizes).
        // this produces an indexedviewablealpha object that is of the exact same type as v0 and v1 (which are assumed to be exactly the same, too)
        // note: does not work to combine viewables with different pointsizes, linewidths, or groupsizes!
        // note2: may be duplication
        public static IndexedViewableAlpha CombineThin(IndexedViewableAlpha v0, IndexedViewableAlpha v1)
        {
            // FIXME: vertuids may contain duplicates, but their corresponding verts may be different!!!

            Vec3f[] verts = v0.Verts.ToList().AddReturn(v1.Verts).ToArray();
            int[] vertuids = v0.VertUIDs.ToList().AddReturn(v1.VertUIDs).ToArray();
            bool[] selected = v0.Selected.ToList().AddReturn(v1.Selected).ToArray();

            float[] pointsizes = v0.PointSizes.DeepCopy();
            float[] linewidths = v0.LineWidths.DeepCopy();
            int[] groupsizes = v0.GroupSizes.DeepCopy();

            Vec4f[][] colors = v0.Colors.Select2(v1.Colors, (Vec4f[] cs0, Vec4f[] cs1) => cs0.ToList().AddReturn(cs1).ToArray()).ToArray();

            int addi = v0.Verts.Length;
            int[][] v1inds = v1.Indices.Select((int[] inds) => inds.Select((int ind) => ind + addi).ToArray()).ToArray();
            int[][] groups = v0.Indices.Select2(v1inds, (int[] inds0, int[] inds1) => inds0.ToList().AddReturn(inds1).ToArray()).ToArray();

            return new IndexedViewableAlpha(verts, colors, groups, pointsizes, linewidths, groupsizes, vertuids, selected);
        }

        // combines two indexedviewablealpha objects combining the groups arrays by concatenation
        // this produces a viewable that perfectly combines v0 and v1.
        // note: this is a wasteful combination, as colors array contain empty, unused colors and there may be duplication
        public static IndexedViewableAlpha CombineFat(IndexedViewableAlpha v0, IndexedViewableAlpha v1)
        {
            // FIXME: vertuids may contain duplicates, but their corresponding verts may be different!!!

            if (v0 == null && v1 == null) return null;
            if (v0 == null) return v1.DeepCopy();
            if (v1 == null) return v0.DeepCopy();

            Vec4f nocolor = new Vec4f(0.0f, 0.0f, 0.0f, 0.0f);
            Vec4f[] nocolors0 = ArrayExt.CreateCopies(nocolor, v0.nVerts);
            Vec4f[] nocolors1 = ArrayExt.CreateCopies(nocolor, v1.nVerts);

            Vec3f[] verts = v0.Verts.AddReturn(v1.Verts); //v0.Verts.ToList().AddReturn( v1.Verts ).ToArray();
            int[] vertuids = v0.VertUIDs.AddReturn(v1.VertUIDs);//v0.VertUIDs.ToList().AddReturn( v1.VertUIDs ).ToArray();
            bool[] selected = v0.Selected.AddReturn(v1.Selected); //v0.Selected.ToList().AddReturn( v1.Selected ).ToArray();

            float[] pointsizes = v0.PointSizes.AddReturn(v1.PointSizes); //v0.PointSizes.ToList().AddReturn( v1.PointSizes ).ToArray();
            float[] linewidths = v0.LineWidths.AddReturn(v1.LineWidths); //v0.LineWidths.ToList().AddReturn( v1.LineWidths ).ToArray();
            int[] groupsizes = v0.GroupSizes.AddReturn(v1.GroupSizes); //v0.GroupSizes.ToList().AddReturn( v1.GroupSizes ).ToArray();

            List<Vec4f[]> colors = new List<Vec4f[]>(v0.Colors.Length + v1.Colors.Length);
            colors.AddRange(v0.Colors.Select((Vec4f[] cs) => cs.AddReturn(nocolors1))); // new List<Vec4f>( cs ).AddReturn( nocolors1 ).ToArray() ) );
            colors.AddRange(v1.Colors.Select((Vec4f[] cs) => nocolors0.AddReturn(cs))); // new List<Vec4f>( nocolors0 ).AddReturn( cs ).ToArray() ) );

            int addi = v0.nVerts; //.Verts.Length;
            int[][] groups = v0.Indices.AddReturn(v1.Indices.Select((int[] grps) => grps.Select((int i) => i + addi)));
            //List<int[]> groups = v0.Indices.ToList();
            //groups.AddRange( v1.Indices.Select( (int[] grps) => grps.Select( (int i) => i + addi ).ToArray() ) );


            return new IndexedViewableAlpha(verts, colors.ToArray(), groups, pointsizes, linewidths, groupsizes, vertuids, selected);
        }

        public HashSet<int> GetVisibleVertsUID()
        {
            HashSet<int> setUIDs = new HashSet<int>();
            for (int igroup = 0; igroup < GroupSizes.Length; igroup++)
            {
                if (GroupSizes[igroup] != 1) continue;
                foreach (int ind in Indices[igroup]) setUIDs.Add(VertUIDs[ind]);
            }
            return setUIDs;
        }

        public List<Vec3f> GetSelectedVerts(bool allowduplicates)
        {
            List<Vec3f> verts = new List<Vec3f>();
            HashSet<int> uids = new HashSet<int>();
            IndexedViewableAlpha v = this;
            while (v != null)
            {
                v.GetSelectedVerts(uids, verts, allowduplicates);
                v = v.attached;
            }
            return verts;
        }
        private void GetSelectedVerts(HashSet<int> addeduids, List<Vec3f> verts, bool allowduplicates)
        {
            for (int ivert = 0; ivert < nVerts; ivert++) if (Selected[ivert])
                {
                    if (allowduplicates || !addeduids.Contains(VertUIDs[ivert]))
                    {
                        addeduids.Add(VertUIDs[ivert]);
                        verts.Add(Verts[ivert]);
                    }
                }
        }

        public Vec3f GetVec3f(int uid) { return Verts[VertUIDs.IndexOf(uid)]; }

        public IndexedViewableAlpha RecolorGroups(Func<int, int[], Vec4f[]> colorfunc)
        {
            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int sz = GroupSizes[ig];
                int[] grps = Indices[ig];
                int[] grp = new int[sz];
                for (int i = 0, ind = 0; i < grps.Length; i += sz, ind++)
                {
                    for (int j = 0; j < sz; j++) grp[j] = grps[i + j];
                    Vec4f[] c = colorfunc(ind, grp);
                    if (c != null) for (int j = 0; j < sz; j++) Colors[ig][i + j] = c[j];
                }
            }
            return this;
        }
        public IndexedViewableAlpha RecolorGroups(Func<int[], Vec4f[]> colorfunc)
        {
            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int sz = GroupSizes[ig];
                int[] grps = Indices[ig];
                int[] grp = new int[sz];
                for (int i = 0; i < grps.Length; i += sz)
                {
                    for (int j = 0; j < sz; j++) grp[j] = grps[i + j];
                    Vec4f[] c = colorfunc(grp);
                    if (c != null) for (int j = 0; j < sz; j++) Colors[ig][i + j] = c[j];
                }
            }
            return this;
        }

        public IndexedViewableAlpha RecolorGroups(Func<int, int[], Vec4f?> colorfunc)
        {
            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int sz = GroupSizes[ig];
                int[] grps = Indices[ig];
                int[] grp = new int[sz];
                for (int i = 0, ind = 0; i < grps.Length; i += sz, ind++)
                {
                    for (int j = 0; j < sz; j++) grp[j] = grps[i + j];
                    Vec4f? c = colorfunc(ind, grp);
                    if (c != null) for (int j = 0; j < sz; j++) Colors[ig][grps[i + j]] = (Vec4f)c;
                }
            }
            return this;
        }
        public IndexedViewableAlpha RecolorGroups(Func<int[], Vec4f?> colorfunc)
        {
            for (int ig = 0; ig < Indices.Length; ig++)
            {
                int sz = GroupSizes[ig];
                int[] grps = Indices[ig];
                int[] grp = new int[sz];
                for (int i = 0, ind = 0; i < grps.Length; i += sz, ind++)
                {
                    for (int j = 0; j < sz; j++) grp[j] = grps[i + j];
                    Vec4f? c = colorfunc(grp);
                    if (c != null) for (int j = 0; j < sz; j++) Colors[ig][grps[i + j]] = (Vec4f)c;
                }
            }
            return this;
        }

        // NOTE: does not make a full copy!
        public IndexedViewableAlpha MirrorX() { return VertMultiply(new Vec3f(-1.0f, 1.0f, 1.0f)); }
        public IndexedViewableAlpha MirrorY() { return VertMultiply(new Vec3f(1.0f, -1.0f, 1.0f)); }
        public IndexedViewableAlpha MirrorZ() { return VertMultiply(new Vec3f(1.0f, 1.0f, -1.0f)); }

        public IndexedViewableAlpha VertMultiply(Vec3f vertmult)
        {
            Vec3f[] newverts = Verts.Select((Vec3f v) => v * vertmult).ToArray();

            int[] uids = VertUIDs;
            //int[] uids = Enumerable.Repeat( -1, nVerts ).ToArray();

            //bool[] sel = Selected;
            bool[] sel = Enumerable.Repeat(false, nVerts).ToArray();

            IndexedViewableAlpha viewable = new IndexedViewableAlpha(newverts, Colors, Indices, PointSizes, LineWidths, GroupSizes, uids, sel);

            return viewable;
        }

        public IndexedViewableAlpha UpdateVertPositions(Vec3f[] uvpos)
        {
            for (int i = 0; i < nVerts; i++) if (VertUIDs[i] != -1) Verts[i] = uvpos[VertUIDs[i]];
            return this;
        }

        public IndexedViewableAlpha CreateMirrorData_Each(bool mirrorx, bool mirrory, bool mirrorz, float threshold)
        {
            IndexedViewableAlpha viewable = null;
            if (mirrorx) viewable += CreateMirrorData_All(true, false, false, threshold);
            if (mirrory) viewable += CreateMirrorData_All(false, true, false, threshold);
            if (mirrorz) viewable += CreateMirrorData_All(false, false, true, threshold);
            return viewable;
        }

        public IndexedViewableAlpha CreateMirrorData_All(bool mirrorx, bool mirrory, bool mirrorz, float threshold)
        {
            Vec3f[] newverts = new Vec3f[nVerts];
            for (int i = 0; i < nVerts; i++)
            {
                Vec3f p = Verts[i]; float x = p.x, y = p.y, z = p.z;
                if (mirrorx && (x > threshold || x < -threshold)) x = -x;
                if (mirrory && (y > threshold || y < -threshold)) y = -y;
                if (mirrorz && (z > threshold || z < -threshold)) z = -z;
                newverts[i] = new Vec3f(x, y, z);
            }

            int[] uids = VertUIDs; //Enumerable.Repeat( -1, nVerts ).ToArray(); //VertUIDs;
            bool[] sel = Selected; //Enumerable.Repeat( false, nVerts ).ToArray();

            IndexedViewableAlpha viewable = new IndexedViewableAlpha(newverts, Colors.DeepCopy(), Indices.DeepCopy(), PointSizes.DeepCopy(), LineWidths.DeepCopy(), GroupSizes.DeepCopy(), uids, sel);
            //IndexedViewableAlpha viewable = new IndexedViewableAlpha( newverts, Colors, Indices, PointSizes, LineWidths, GroupSizes, uids, sel );
            //if( next != null ) viewable.next = next.CreateMirrorData_All( mirrorx, mirrory, mirrorz, threshold );

            return viewable;
        }

        public IndexedViewableAlpha MirrorAndMerge(bool mirrorx, bool mirrory, bool mirrorz, float mergethreshold)
        {
            IndexedViewableAlpha viewable = this;
            if (mirrorx) viewable = MergeMirrors(viewable, viewable.MirrorX(), mergethreshold);
            if (mirrory) viewable = MergeMirrors(viewable, viewable.MirrorY(), mergethreshold);
            if (mirrorz) viewable = MergeMirrors(viewable, viewable.MirrorZ(), mergethreshold);
            return viewable;

        }

        // assumes viewable0 and viewable1 are perfect mirrors!
        private IndexedViewableAlpha MergeMirrors(IndexedViewableAlpha viewable0, IndexedViewableAlpha viewable1, float mergethreshold)
        {
            List<Vec3f> verts = new List<Vec3f>(viewable0.Verts);
            List<int>[] groups = viewable0.Indices.Select((int[] inds) => new List<int>(inds)).ToArray();
            List<Vec4f>[] colors = viewable0.Colors.Select((Vec4f[] c) => new List<Vec4f>(c)).ToArray();
            List<int> vertuids = new List<int>(viewable0.VertUIDs);
            List<bool> selected = new List<bool>(viewable0.Selected);

            verts.AddRange(viewable1.Verts);
            vertuids.AddRange(viewable1.VertUIDs);
            selected.AddRange(viewable1.Selected);
            for (int i = 0; i < 4; i++) colors[i].AddRange(viewable1.Colors[i]);

            bool[] ismerged = new bool[viewable1.nVerts];
            int[] indtrans = new int[viewable1.nVerts];

            float mt2 = mergethreshold;// * mergethreshold;

            for (int i = 0; i < viewable1.nVerts; i++)
            {
                float d2 = (viewable0.Verts[i] - viewable1.Verts[i]).Length;
                if (d2 <= mt2)
                {
                    // merge!
                    indtrans[i] = i;
                    ismerged[i] = true;
                }
                else {
                    // no merge!
                    indtrans[i] = i + viewable0.nVerts;
                    ismerged[i] = false;
                }
            }

            for (int ig = 0; ig < 4; ig++)
            {
                int[] grp = viewable1.Indices[ig];
                int sz = viewable1.GroupSizes[ig];
                for (int iv = 0; iv < grp.Length; iv += sz)
                {
                    bool allmerged = true;
                    for (int i = 0; i < sz; i++) allmerged &= ismerged[grp[i + iv]];
                    if (allmerged) continue;

                    for (int i = 0; i < sz; i++) groups[ig].Add(indtrans[grp[iv + i]]);
                }
            }

            Vec3f[] averts = verts.ToArray();
            Vec4f[][] acolors = colors.Select((List<Vec4f> c) => c.ToArray()).ToArray();
            int[][] agroups = groups.Select((List<int> g) => g.ToArray()).ToArray();
            int[] avertuids = vertuids.ToArray();
            bool[] aselected = selected.ToArray();

            return new IndexedViewableAlpha(averts, acolors, agroups, viewable0.PointSizes, viewable0.LineWidths, viewable0.GroupSizes, avertuids, aselected);
        }

    }

}


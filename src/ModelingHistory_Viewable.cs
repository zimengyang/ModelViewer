using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Common.Libs.VMath;
using Common.Libs.MiscFunctions;
using Common.Libs.MatrixMath;

namespace MeshFlowViewer
{
    public partial class ModelingHistory : IBinaryConvertible
    {
        private static Vec4f[] recoloradd = new Vec4f[] {
            new Vec4f( 0.25f, 1.00f, 0.50f, 1.00f ),
            new Vec4f( 0.25f, 1.00f, 0.50f, 1.00f ),
            new Vec4f( 0.13f, 0.50f, 0.25f, 1.00f ),
            new Vec4f( 0.13f, 0.50f, 0.25f, 1.00f ),
        };
        private static Vec4f[] recolordel = new Vec4f[] {
            new Vec4f( 1.00f, 0.25f, 0.50f, 1.00f ),
            new Vec4f( 1.00f, 0.25f, 0.50f, 1.00f ),
            new Vec4f( 0.50f, 0.13f, 0.25f, 1.00f ),
            new Vec4f( 0.50f, 0.13f, 0.25f, 1.00f ),
        };
        private static Vec4f?[] recolormerge = new Vec4f?[] {
            new Vec4f( 1.00f, 0.50f, 1.00f, 1.00f ),
            new Vec4f( 1.00f, 0.50f, 1.00f, 1.00f ),
            null,
            null,
        };
        private static Vec4f[] colorSelected = new Vec4f[] {
            new Vec4f( 1.0f, 0.75f, 0.0f, 1.0f ),
            new Vec4f( 1.0f, 0.75f, 0.0f, 1.0f ),
            new Vec4f( 0.75f, 0.5625f, 0.0f, 1.0f ),
            new Vec4f( 0.75f, 0.5625f, 0.0f, 1.0f )
        };


        #region Viewable Modification Functions

        public IndexedViewableAlpha Viewable_Highlight(IndexedViewableAlpha viewable)
        {
            for (int ivert = 0; ivert < viewable.nVerts; ivert++)
            {
                int uid = viewable.VertUIDs[ivert];
                if (highlighted[uid] != HighlightColors.None)
                {
                    Vec3f c = (Vec3f)Highlight.GetColor(highlighted[uid]);
                    viewable.Colors[0][ivert] = new Vec4f(c.x, c.y, c.z, 1.0f);
                }
            }
            return viewable;
        }

        public IndexedViewableAlpha[] Viewables_FinalPositions(params IndexedViewableAlpha[] viewables)
        {
            foreach (IndexedViewableAlpha viewable in viewables) Viewables_FinalPositions(viewable);
            return viewables;
        }

        public IndexedViewableAlpha Viewables_MatchPositions(IndexedViewableAlpha viewsrc, IndexedViewableAlpha viewdst)
        {
            if (viewsrc == null) return null;

            viewsrc.StartEdit();

            Vec3f[] vpos = new Vec3f[nuverts];
            int uid;
            for (int ivert = 0; ivert < viewsrc.nVerts; ivert++) if ((uid = viewsrc.VertUIDs[ivert]) >= 0) vpos[uid] = viewsrc.Verts[ivert];
            for (int ivert = 0; ivert < viewdst.nVerts; ivert++) if ((uid = viewdst.VertUIDs[ivert]) >= 0) vpos[uid] = viewdst.Verts[ivert];

            for (int ivert = 0; ivert < viewsrc.nVerts; ivert++) if ((uid = viewsrc.VertUIDs[ivert]) >= 0) viewsrc.Verts[ivert] = vpos[uid];

            viewsrc.EndEdit();
            return viewsrc;
        }
        public IndexedViewableAlpha Viewables_FinalPositions(IndexedViewableAlpha viewable)
        {
            IndexedViewableAlpha v = viewable;
            while (viewable != null)
            {
                viewable.StartEdit();
                for (int ivert = 0; ivert < viewable.nVerts; ivert++)
                {
                    int uid = viewable.VertUIDs[ivert];
                    if (uid < 0) continue;
                    viewable.Verts[ivert] = finalposition[uid];
                }
                viewable.EndEdit();
                viewable = viewable.attached;
            }
            return v;
        }

        public IndexedViewableAlpha[] Viewables_Selections(IndexedViewableAlpha[] viewables)
        {
            foreach (IndexedViewableAlpha viewable in viewables) Viewable_Selections(viewable);
            return viewables;
        }

        public IndexedViewableAlpha Viewable_Selections(IndexedViewableAlpha viewable)
        {
            IndexedViewableAlpha v = viewable;
            Vec4f colorsel = new Vec4f(1.0f, 0.75f, 0.0f, 1.0f);
            while (viewable != null)
            {
                for (int ivert = 0; ivert < viewable.nVerts; ivert++)
                {
                    if (!viewable.Selected[ivert]) continue;
                    for (int ig = 0; ig < viewable.Indices.Length; ig++)
                    {
                        if (viewable.GroupSizes[ig] <= 2) viewable.Colors[ig][ivert] = colorsel;
                        //for( int igrp = 0; igrp < groups.Length; igrp++ ) if( groups[igrp] == ivert ) viewable.Colors[ig][igrp] = colorsel;
                    }
                }
                viewable = viewable.attached;
            }
            return v;
        }


        public IndexedViewableAlpha Viewable_TransparentForeground(IndexedViewableAlpha viewable, Matrix projectionmatrix, float epsilon, float alphamult)
        {
            float closestz = float.PositiveInfinity;

            for (int ivert = 0; ivert < viewable.nVerts; ivert++)
            {
                if (!viewable.Selected[ivert]) continue;
                Vec3f p = projectionmatrix.Project(viewable.Verts[ivert]);
                closestz = Math.Min(closestz, p.z);
            }

            if (closestz == float.PositiveInfinity) return viewable;

            viewable = viewable.DeepCopy();

            for (int ivert = 0; ivert < viewable.nVerts; ivert++)
            {
                Vec3f p = projectionmatrix.Project(viewable.Verts[ivert]);
                float a = (closestz - epsilon) - p.z;
                if (a <= 0) continue;
                a = (2.0f - Math.Min(1.0f, a)) / 2.0f;

                for (int ig = 0; ig < viewable.Indices.Length; ig++)
                {
                    Vec4f c = viewable.Colors[ig][ivert];
                    viewable.Colors[ig][ivert] = new Vec4f(c.x, c.y, c.z, c.w * alphamult * a);
                }
            }

            return viewable;
        }

        public IndexedViewableAlpha GetHighlightedVertsViewable(IndexedViewableAlpha viewable)
        {
            int count = 0;
            HashSet<int> visuids = viewable.GetVisibleVertsUID();
            bool[] vis = new bool[nuverts];
            foreach (int uid in visuids) if (highlighted[uid] != HighlightColors.None) { count++; vis[uid] = true; }

            Vec3f[] verts = new Vec3f[count];
            int[] uids = new int[count];
            for (int uid = 0, j = 0; j < count; uid++)
            {
                if (vis[uid]) { verts[j] = viewable.Verts[viewable.VertUIDs.IndexOf(uid)]; uids[j] = uid; j++; }
            }
            Vec4f[][] colors = { Enumerable.Repeat(new Vec4f(1.0f, 0.0f, 0.0f, 1.0f), count).ToArray() };
            int[][] groups = new int[][] { Enumerable.Range(0, count).ToArray() };
            float[] ptsizes = new float[] { 20.0f };
            float[] lnwidths = new float[] { 0.1f };
            int[] groupsizes = new int[] { 1 };
            bool[] selected = new bool[count];

            return new IndexedViewableAlpha(verts, colors, groups, ptsizes, lnwidths, groupsizes, uids, selected);

        }

        public IndexedViewableAlpha DeEmphasizeNonHighlighted(IndexedViewableAlpha viewable, System.Drawing.Color backcolor)
        {
            //Vec4f deemph = new Vec4f( 1.0f, 1.0f, 1.0f, 0.25f );
            float amount = 0.125f;
            Color bg = backcolor; //Color.DimGray;
            Vec4f addc = new Vec4f((float)bg.R / 255.0f, (float)bg.G / 255.0f, (float)bg.B / 255.0f, 1.0f) * (1.0f - amount);

            if (!highlighted.Any(c => c != HighlightColors.None)) return viewable;

            Vec4f[][] colors = new Vec4f[viewable.Indices.Length][];
            for (int ig = 0; ig < viewable.Indices.Length; ig++)
            {
                Vec4f[] oldcolors = viewable.Colors[ig];
                Vec4f[] newcolors = new Vec4f[viewable.nVerts];
                for (int iv = 0; iv < viewable.nVerts; iv++)
                {
                    int uid = viewable.VertUIDs[iv];
                    if (uid < 0) newcolors[iv] = oldcolors[iv];
                    else if (highlighted[uid] != HighlightColors.None) newcolors[iv] = oldcolors[iv];
                    else newcolors[iv] = oldcolors[iv] * amount + addc; // * deemph;
                }
                colors[ig] = newcolors;
            }

            IndexedViewableAlpha viewdeemph = new IndexedViewableAlpha(viewable.Verts, colors, viewable.Indices, viewable.PointSizes, viewable.LineWidths, viewable.GroupSizes, viewable.VertUIDs, viewable.Selected);
            if (viewable.attached != null) viewdeemph.attached = DeEmphasizeNonHighlighted(viewable.attached, backcolor);
            return viewdeemph;
        }

        #endregion

        public IndexedViewableAlpha[] GetViewables(Cluster cluster, bool usefinalpos, bool addselectedverts, bool quick, bool renderremoved)
        {
            Composition comp = cluster.composition;
            ComparisonModes mode = comp.Mode;

            int i0before = basemodifyid[cluster.start];
            int i0after = cluster.end;
            //int i0current = Timeline.timeline.GetIndex0Current();

            if (comp.Intervals_Use)
            {
                int count = cluster.snapshots.Count;
                IndexedViewableAlpha[] aviewables = new IndexedViewableAlpha[cluster.snapshots.Count];
                //Vec3f[] verts = GetFinalVerts( cluster.snapshots );

                //int ipsnapshot = -1;
                Enumerable.Range(0, count).EachInParallel((int i, int ind) => {
                    //for( int i = 0; i < count; i++ )
                    //{
                    int ipsnapshot = -1;
                    if (i != 0)
                    {
                        for (int k = i - 1; k >= 0; k--)
                            if (cluster.snapshots[k] != -1) { ipsnapshot = cluster.snapshots[k]; break; }
                    }
                    int isnapshot = cluster.snapshots[i];

                    if (isnapshot == -1)
                    {
                        aviewables[i] = null;
                    }
                    else {
                        IndexedViewableAlpha viewcur = snapshots[isnapshot].GetViewables(true);
                        if (i == 0)
                        {
                            aviewables[i] = viewcur;
                        }
                        else {
                            Cluster c = new Cluster(ipsnapshot, isnapshot, "", comp);
                            //Vec3f[] verts = GetFinalVerts( new List<int>() { ipsnapshot, isnapshot } );
                            aviewables[i] = GetViewable(c, ipsnapshot, isnapshot, usefinalpos, addselectedverts, quick, false);
                            //viewprev.UpdateVertPositions( verts );
                            //aviewables[i] = GetViewable( c, ipsnapshot, viewprev, isnapshot, viewcur, verts, usefinalpos, addselectedverts, quick );
                        }
                        //viewprev = viewcur;
                        //ipsnapshot = isnapshot;
                    }
                });

                return aviewables;
            }

            List<IndexedViewableAlpha> viewables;


            if (mode == ComparisonModes.BeforeCurrentAfter || mode == ComparisonModes.OnlyCurrent)
            {
                IndexedViewableAlpha viewable = null;
                if (cluster.viewable != null)
                {
                    if (!quick == cluster.viewable_ann && renderremoved == cluster.viewable_rem && addselectedverts == cluster.viewable_sel) viewable = cluster.viewable;
                    else {
                        System.Console.WriteLine("{0}-{1}: {2} {3} {4}, {5} {6} {7}", cluster.start, cluster.end, cluster.viewable_ann, cluster.viewable_rem, cluster.viewable_sel, !quick, renderremoved, addselectedverts);
                        cluster.viewable = null;
                    }
                }
                if (viewable == null)
                {
                    long ms = Timer.GetExecutionTime_ms(delegate {
                        viewable = GetViewable(cluster, i0before, i0after, usefinalpos, addselectedverts, quick, renderremoved);
                    });
                    if (ms > 400)
                    {
                        cluster.viewable = viewable; //IndexedViewableAlpha.Trim( viewable );
                        cluster.viewable_ann = !quick;
                        cluster.viewable_rem = renderremoved;
                        cluster.viewable_sel = addselectedverts;
                    }
                    //System.Console.WriteLine( "getviewable: " + ms );
                }
                viewables = new List<IndexedViewableAlpha>(3) { viewable };
            }
            else {
                viewables = new List<IndexedViewableAlpha>(2);
            }

            if (mode == ComparisonModes.BeforeAfter || mode == ComparisonModes.BeforeCurrentAfter)
            {
                //IndexedViewableAlpha before = snapshots[basemodifyid[cluster.start]].GetViewable();
                //IndexedViewableAlpha after = snapshots[cluster.end].GetViewable();

                Composition cobefore = new Composition() { Show_Diff_BeforeAfter = true, Show_Intersect_BeforeAfter = true };
                Composition coafter = new Composition() { Show_Diff_AfterBefore = true, Show_Intersect_BeforeAfter = true };
                Cluster clbefore = new Cluster(cluster.start, cluster.end, "Before", cobefore);
                Cluster clafter = new Cluster(cluster.start, cluster.end, "After", coafter);

                IndexedViewableAlpha before = GetViewable(clbefore, i0before, i0after, true, false, quick, true);
                IndexedViewableAlpha after = GetViewable(clafter, i0before, i0after, true, false, quick, false);

                if (!comp.SeparateViewports_Use)
                {
                    before.Offset(comp.Compare_Offset * -1.0f);
                    after.Offset(comp.Compare_Offset);
                }

                viewables = viewables.InsertReturn(0, before).AddReturn(after);
            }

            if (cluster.composition.Show_End)
            {
                IndexedViewableAlpha viewend = snapshots[nsnapshots - 1].GetViewables();
                Vec4f avec = new Vec4f(1.0f, 1.0f, 1.0f, 0.5f);
                int cgrps = viewend.Indices.Length;
                for (int iv = 0; iv < viewend.nVerts; iv++)
                    for (int ig = 0; ig < cgrps; ig++)
                        viewend.Colors[ig][iv] *= avec;
                viewables.Add(viewend);
            }

            return viewables.ToArray();
        }

        private IndexedViewableAlpha finalpos(bool enabled, IndexedViewableAlpha viewable)
        {
            if (enabled) return Viewables_FinalPositions(viewable);
            return viewable;
        }

        public Vec3f[] GetFinalVerts(List<int> lstsnapshots)
        {
            Vec3f[] p = Enumerable.Repeat(new Vec3f(float.NaN, float.NaN, float.NaN), nuverts).ToArray();// new Vec3f[nuverts];
            foreach (int isnapshot in lstsnapshots)
            {
                SnapshotScene scene = snapshots[isnapshot];
                foreach (SnapshotModel model in scene.Models)
                {
                    int count = model.nverts;
                    Vec3f[] verts = model.GetVerts();
                    int[] uids = model.GetVertUIDs();
                    for (int i = 0; i < count; i++) p[uids[i]] = verts[i];
                }
            }
            return p;
        }

        public IndexedViewableAlpha GetViewable(Cluster cluster, int index0base, int index0after, bool usefinalpos, bool addselectedverts, bool quick, bool renderremoved)
        {
            IndexedViewableAlpha viewafter = null;
            IndexedViewableAlpha viewsel = null;
            IndexedViewableAlpha viewbefore = null;
            IndexedViewableAlpha viewable = null;
            Vec3f[] verts = null;

            long timeafter = Timer.GetExecutionTime_ms(delegate {
                viewafter = snapshots[index0after].GetViewablesAttached(null, true);
            });

            long timeverts = Timer.GetExecutionTime_ms(delegate {
                verts = GetFinalVerts(cluster.snapshots);
            });

            if (addselectedverts) viewsel = snapshots[index0after].GetSelection(verts, true);
            //if( addselectedverts ) viewsel = Viewable_Selections( viewable );

            if (quick)
            {
                //System.Console.WriteLine( "after: " + timeafter + ", verts: " + timeverts );
                return IndexedViewableAlpha.Attach(viewafter, viewsel);
            }

            long timebefore = Timer.GetExecutionTime_ms(delegate {
                viewbefore = snapshots[index0base].GetViewablesAttached(verts, true);
            });

            // sanity checks
            if (viewbefore == null) { System.Console.WriteLine("viewbefore == null, index0base = " + index0base); }
            if (viewafter == null) { System.Console.WriteLine("viewafter == null, index0after = " + index0after); }

            long timeview = Timer.GetExecutionTime_ms(delegate {
                viewable = GetViewable(cluster, index0base, viewbefore, index0after, viewafter, verts, usefinalpos, addselectedverts, quick, renderremoved);
            });

            //System.Console.WriteLine( "after: " + timeafter + ", verts: " + timeverts + ", before: " + timebefore + ", view: " + timeview );

            return IndexedViewableAlpha.Attach(viewable, viewsel);
        }
        public IndexedViewableAlpha GetViewable(Cluster cluster, int index0base, IndexedViewableAlpha viewbefore, int index0after, IndexedViewableAlpha viewafter, Vec3f[] verts, bool usefinalpos, bool addselectedverts, bool quick, bool renderremoved)
        {

            Composition comp = cluster.composition;
            IndexedViewableAlpha viewable = null;
            long timebefore = -1;
            long timeafter = -1;
            long timeadded = -1;
            long timedeled = -1;
            long timesame = -1;
            long timeprov = -1;
            long timetrans = -1;

            if (comp.Show_Before) timebefore = Timer.GetExecutionTime_ms(delegate { viewable = IndexedViewableAlpha.Attach(viewable, viewbefore); }); //viewable += viewbefore;

            if (comp.Show_After) timeafter = Timer.GetExecutionTime_ms(delegate { viewable = IndexedViewableAlpha.Attach(viewable, viewafter); }); //viewable += viewafter;

            if (comp.Show_Diff_AfterBefore)
            {
                timeadded = Timer.GetExecutionTime_ms(delegate {
                    //IndexedViewableAlpha viewadded = viewafter - viewbefore;
                    IndexedViewableAlpha viewadded = SnapshotScene.MeshDiffAttach(snapshots[index0after], snapshots[index0base], verts, true);
                    if (viewadded != null)
                    {
                        IndexedViewableAlpha va = viewadded;
                        while (va != null)
                        {
                            va.RecolorGroups((int[] inds) => recoloradd[inds.Length - 1]);
                            for (int i = 0; i < va.GroupSizes.Length; i++) if (va.GroupSizes[i] == 2)
                                    va.LineWidths[i] = 3.0f;
                            va = va.attached;
                        }
                        viewable = IndexedViewableAlpha.Attach(viewable, viewadded);
                        //viewable = IndexedViewableAlpha.CombineFat( viewable, viewadded );
                        //viewable += viewadded;
                    }
                });
            }

            if (comp.Show_Diff_BeforeAfter && renderremoved)
            {
                timedeled = Timer.GetExecutionTime_ms(delegate {
                    //IndexedViewableAlpha viewdeled = viewbefore - viewafter;
                    IndexedViewableAlpha viewdeled = SnapshotScene.MeshDiff(snapshots[index0base], snapshots[index0after], verts, true);
                    if (viewdeled != null)
                    {
                        viewdeled.RecolorGroups((int[] inds) => recolordel[inds.Length - 1]);
                        for (int i = 0; i < viewdeled.GroupSizes.Length; i++) if (viewdeled.GroupSizes[i] == 2)
                                viewdeled.LineWidths[i] = 3.0f;
                        viewable = IndexedViewableAlpha.Attach(viewable, viewdeled);
                        //viewable = IndexedViewableAlpha.CombineFat( viewable, viewdeled );
                        //viewable += viewdeled;
                    }
                });
            }

            if (comp.Show_Intersect_BeforeAfter)
            {
                timesame = Timer.GetExecutionTime_ms(delegate {
                    IndexedViewableAlpha viewinter = SnapshotScene.MeshIntersectAttach(snapshots[index0after], snapshots[index0base], verts, true);
                    viewable = IndexedViewableAlpha.Attach(viewable, viewinter);

                    //viewable = IndexedViewableAlpha.CombineFat( viewable, viewbefore % viewafter );
                    //viewable = IndexedViewableAlpha.CombineFat( viewable, viewinter );
                    //viewable += viewbefore % viewafter;
                });
            }

            if (comp.Show_Provenance)
            {
                timeprov = Timer.GetExecutionTime_ms(delegate {
                    foreach (int isnapshot in cluster.snapshots)
                    {
                        //vannotations = IndexedViewableAlpha.CombineFat( vannotations, GetAnnotations( usefinalpos, isnapshot, verts, true ) );
                        IndexedViewableAlpha vannotations = GetAnnotations(usefinalpos, isnapshot, verts, true);
                        // modify here zimeng 
                        viewable = IndexedViewableAlpha.Attach(viewable, vannotations);

                        //vannotations += GetAnnotations( usefinalpos, isnapshot, verts, true );
                    }
                    //viewable += vannotations;
                    //viewable = IndexedViewableAlpha.CombineFat( viewable, vannotations );
                });

            }

            if (comp.Show_Provenance || comp.Show_Annotations_Transforms)
            {
                timetrans = Timer.GetExecutionTime_ms(delegate {
                    bool[] sel = new bool[nuverts];

                    foreach (int isnapshot in cluster.snapshots)
                    {
                        if (!snapshots[isnapshot].command.StartsWith("transform")) continue;

                        int cselected = 0, cedited = 0;
                        foreach (SnapshotModel model in snapshots[isnapshot].Models)
                        {
                            if (model.objselected) cselected++;
                            if (model.objedit) cedited++;
                        }
                        if (cselected == 0 && cedited == 0) continue;

                        foreach (SnapshotModel model in snapshots[isnapshot].Models)
                        {
                            if (cedited == 0 && model.objselected)
                            {
                                int[] uids = model.GetVertUIDs();
                                foreach (int uind in uids) sel[uind] = true;
                            }
                            else if (model.objedit)
                            {
                                int[] uids = model.GetVertUIDs();
                                foreach (int ind in model.selinds) sel[uids[ind]] = true;
                            }
                        }
                    }

                    viewable = IndexedViewableAlpha.Attach(viewable, GetAnnotations_Transform(viewafter, sel));
                    //viewable = IndexedViewableAlpha.CombineFat( viewable, GetAnnotations_Transform( viewafter, sel ) );
                    //viewable += GetAnnotations_Transform( viewafter, sel );
                });
            }

            bool[] isselected = new bool[nuverts];

            foreach (int isnapshot in cluster.snapshots)
            {
                bool editing = (snapshots[isnapshot].GetEditModel() != null);
                foreach (SnapshotModel model in snapshots[isnapshot].Models)
                {
                    if (model.objedit)
                    {
                        int[] uids = model.GetVertUIDs();
                        foreach (int ind in model.selinds) isselected[uids[ind]] = true;
                    }
                    else if (!editing && model.objselected)
                    {
                        int[] uids = model.GetVertUIDs();
                        foreach (int uid in uids) isselected[uid] = true;
                    }
                }
            }

            IndexedViewableAlpha v = viewable;
            while (v != null)
            {
                for (int ivert = 0; ivert < v.nVerts; ivert++)
                {
                    int uid = v.VertUIDs[ivert];
                    if (uid >= 0) v.Selected[ivert] = isselected[uid];
                }
                v = v.attached;
            }

            //System.Console.Write( "timings: " );
            //if( timebefore != -1 ) System.Console.Write( "before: " + timebefore + ", " );
            //if( timeafter != -1 ) System.Console.Write( "after: " + timeafter + ", " );
            //if( timeadded != -1 ) System.Console.Write( "added: " + timeadded + ", " );
            //if( timedeled != -1 ) System.Console.Write( "deled: " + timedeled + ", " );
            //if( timesame != -1 ) System.Console.Write( "same: " + timesame + ", " );
            //if( timeprov != -1 ) System.Console.Write( "prov: " + timeprov + ", " );
            //if( timetrans != -1 ) System.Console.Write( "trans: " + timetrans + ", " );
            //System.Console.WriteLine();

            /*if( addselectedverts )
			{
				//IndexedViewableAlpha viewselected = GetSelectedGroupsViewable( viewable, index0base+1, index0after );
				//if( comp.Show_Diff_AfterBefore ) viewselected -= NoVertsEdges( viewafter - viewbefore );
				IndexedViewableAlpha viewselected = null;
				//foreach( int isnapshot in cluster.snapshots )
				//	viewselected += snapshots[isnapshot].GetSelection( verts, true );
				viewselected = snapshots[cluster.snapshots[cluster.snapshots.Count-1]].GetSelection( verts, true );
				viewable += viewselected;
			}*/

            return viewable;
        }

        public IndexedViewableAlpha NoVertsEdges(IndexedViewableAlpha viewable)
        {
            IndexedViewableAlpha v = viewable;
            while (viewable != null)
            {
                for (int i = 0; i < viewable.Indices.Length; i++)
                {
                    if (viewable.GroupSizes[i] == 1 || viewable.GroupSizes[i] == 2) viewable.Indices[i] = new int[0];
                }
                viewable = viewable.attached;
            }
            return v;
        }

        public IndexedViewableAlpha MakeVertsConsistent(IndexedViewableAlpha viewable, IndexedViewableAlpha withviewable, bool bvert, bool bselection, bool bOrSelection)
        {
            IndexedViewableAlpha v = viewable;
            if (!bvert && !bselection) return v;

            while (viewable != null)
            {
                for (int ivert = 0; ivert < viewable.nVerts; ivert++)
                {
                    int uid = viewable.VertUIDs[ivert]; if (uid < 0) continue;
                    int ivertwith = withviewable.VertUIDs.LastIndexOf(uid);
                    if (ivertwith == -1) continue;
                    if (bvert) viewable.Verts[ivert] = withviewable.Verts[ivertwith];
                    if (bselection)
                    {
                        if (bOrSelection) viewable.Selected[ivert] |= withviewable.Selected[ivertwith];
                        else viewable.Selected[ivert] = withviewable.Selected[ivertwith];
                    }
                }
                viewable = viewable.attached;
            }
            return v;
        }

        public IndexedViewableAlpha MakeVertsConsistent(IndexedViewableAlpha viewable, Vec3f[] match)
        {
            IndexedViewableAlpha v = viewable;
            while (viewable != null)
            {
                for (int ivert = 0; ivert < viewable.nVerts; ivert++)
                {
                    int uid = viewable.VertUIDs[ivert]; if (uid < 0) continue;
                    Vec3f vert = match[uid]; if (float.IsNaN(vert.x)) continue;
                    viewable.Verts[ivert] = vert;
                }
                viewable = viewable.attached;
            }
            return v;
        }

        private IndexedViewableAlpha GetAnnotations(bool usefinalpos, int isnapshot, Vec3f[] verts, bool applymods)
        {
            string command = snapshots[isnapshot].command;
            string[] commandslist = new string[] { "topo.extrude", "topo.merge", "topo.subdivide", "topo.loopcut", "topo.convert.quads_to_tris", "topo.convert.tris_to_quads" };

            if (!commandslist.Contains(command)) return null;

            SnapshotScene snap0 = snapshots[basemodifyid[isnapshot]];
            SnapshotScene snap1 = snapshots[isnapshot];
            IndexedViewableAlpha viewableBefore = null;
            IndexedViewableAlpha viewableAfter = null;
            IndexedViewableAlpha viewableAdded = null;

            long timeview1 = Timer.GetExecutionTime_ms(delegate {
                //viewableBefore = snap0.GetViewables( verts, applymods );
                viewableBefore = snap0.GetViewablesAttached(verts, applymods);
            });
            long timeview2 = Timer.GetExecutionTime_ms(delegate {
                //viewableAfter = snap1.GetViewables( verts, applymods );
                viewableAfter = snap1.GetViewablesAttached(verts, applymods);
            });
            long timeview3 = Timer.GetExecutionTime_ms(delegate {
                viewableAdded = SnapshotScene.MeshDiff(snap1, snap0, verts, applymods);
            });
            //System.Console.Write("[" + timeview1 + "," + timeview2 + "," + timeview3 + "]" );

            //if( true ) return viewableAdded;

            if (command == "topo.extrude")
            {
                //IndexedViewableAlpha viewableBefore = snapshots[basemodifyid[isnapshot]].GetViewables( verts, applymods );
                //IndexedViewableAlpha viewableAfter = snapshots[isnapshot].GetViewables( verts, applymods );
                //return Timer.PrintTimeToExecute( "extrude", delegate {
                return GetAnnotations_Extrude(viewableBefore, viewableAfter, viewableAdded);
                //} );
            }

            if (command == "topo.merge")
            {
                return GetAnnotations_Merge(viewableBefore, viewableAfter);
            }

            if (command == "topo.subdivide" || command == "topo.loopcut" || command == "topo.convert.quads_to_tris")
            {
                return GetAnnotations_Subdivide(viewableBefore, viewableAdded);
            }

            if (command == "topo.convert.tris_to_quads")
            {
                return GetAnnotations_TrisToQuads(viewableAdded);
            }

            return null;
        }

        private IndexedViewableAlpha GetAnnotations_Transform(IndexedViewableAlpha viewafter)
        {
            bool[] sel = new bool[nuverts];
            for (int i = 0; i < viewafter.nVerts; i++) sel[viewafter.VertUIDs[i]] |= viewafter.Selected[i];
            return GetAnnotations_Transform(viewafter, sel);
        }

        private IndexedViewableAlpha GetAnnotations_Transform(IndexedViewableAlpha viewable, bool[] sel)
        {
            Vec4f[][] colors = new Vec4f[][] {
                Enumerable.Repeat( new Vec4f( 0.25f, 1.00f, 1.00f, 1.00f ), viewable.nVerts ).ToArray(),
                Enumerable.Repeat( new Vec4f( 0.25f, 1.00f, 1.00f, 1.00f ), viewable.nVerts ).ToArray()
            };

            List<int> pts = new List<int>();
            List<int> edges = new List<int>();

            for (int ig = 0; ig < viewable.GroupSizes.Length; ig++)
            {
                int[] groups = viewable.Indices[ig];

                switch (viewable.GroupSizes[ig])
                {
                    case 1:
                        for (int i = 0; i < groups.Length; i++)
                        {
                            int i0 = groups[i];
                            int uid = viewable.VertUIDs[i0];
                            if (uid >= 0 && sel[uid]) pts.Add(i0);
                        }
                        break;
                    case 2:
                        /*for( int i = 0; i < groups.Length; i += 2 )
                        {
                            int i0 = groups[i+0];
                            int i1 = groups[i+1];
                            int uid0 = viewafter.VertUIDs[i0];
                            int uid1 = viewafter.VertUIDs[i1];
                            if( uid0 >= 0 && uid1 >= 0 && sel[uid0] && sel[uid1] )
                            {
                                edges.Add( i0 );
                                edges.Add( i1 );
                            }
                        }*/
                        break;
                }
            }

            IndexedViewableAlpha newview = new IndexedViewableAlpha(viewable.Verts.DeepCopy(), colors, new int[][] { pts.ToArray(), edges.ToArray() }, new float[] { 5.0f, 0.1f }, new float[] { 0.1f, 3.0f }, new int[] { 1, 2 }, viewable.VertUIDs.DeepCopy(), viewable.Selected.DeepCopy());

            if (viewable.attached != null) newview.attached = GetAnnotations_Transform(viewable.attached, sel);

            return newview;
        }

        private IndexedViewableAlpha GetAnnotations_Subdivide(IndexedViewableAlpha viewbefore, IndexedViewableAlpha viewdiff)
        {
            bool[] before = new bool[nuverts];
            while (viewbefore != null)
            {
                for (int ig = 0; ig < viewbefore.GroupSizes.Length; ig++)
                {
                    if (viewbefore.GroupSizes[ig] != 1) continue;
                    foreach (int i in viewbefore.Indices[ig]) before[viewbefore.VertUIDs[i]] = true;
                }
                viewbefore = viewbefore.attached;
            }

            Vec4f[][] colors = new Vec4f[][] { Enumerable.Repeat(new Vec4f(0.25f, 1.00f, 0.50f, 1.00f), viewdiff.nVerts).ToArray() };
            List<int> edges = new List<int>();
            List<int> alledges = new List<int>();
            bool added = false;
            for (int ig = 0; ig < viewdiff.GroupSizes.Length; ig++)
            {
                if (viewdiff.GroupSizes[ig] != 2) continue;
                int[] groups = viewdiff.Indices[ig];
                for (int i = 0; i < groups.Length; i += 2)
                {
                    int i0 = i + 0;
                    int i1 = i + 1;
                    if (before[viewdiff.VertUIDs[groups[i0]]] || before[viewdiff.VertUIDs[groups[i1]]])
                    {
                        if (!added) { alledges.Add(groups[i0]); alledges.Add(groups[i1]); }
                    }
                    else {
                        edges.Add(groups[i0]);
                        edges.Add(groups[i1]);
                        added = true;
                    }
                }
            }
            if (!added) edges = alledges;

            return new IndexedViewableAlpha(viewdiff.Verts.DeepCopy(), colors, new int[][] { edges.ToArray() }, new float[] { 0.1f }, new float[] { 3.0f }, new int[] { 2 }, viewdiff.VertUIDs.DeepCopy(), viewdiff.Selected.DeepCopy());
        }


        private IndexedViewableAlpha GetAnnotations_TrisToQuads(IndexedViewableAlpha viewdiff)
        {

            List<Vec3f> verts = new List<Vec3f>();
            List<int> groups = new List<int>();
            List<Vec4f> colors = new List<Vec4f>();

            for (int ig = 0; ig < viewdiff.GroupSizes.Length; ig++)
            {
                if (viewdiff.GroupSizes[ig] != 4) continue;
                int[] inds = viewdiff.Indices[ig];
                for (int i = 0; i < inds.Length; i += 4)
                {
                    Vec3f w = viewdiff.Verts[inds[i + 0]];
                    Vec3f x = viewdiff.Verts[inds[i + 1]];
                    Vec3f y = viewdiff.Verts[inds[i + 2]];
                    Vec3f z = viewdiff.Verts[inds[i + 3]];
                    Vec3f center = (w + x + y + z) * 0.25f;
                    float twistdist = CalcTwistDistance(w, x, y, z);
                    float lwy = (y - w).Length;
                    float lxz = (z - x).Length;
                    float size = Math.Min(0.1f, Math.Max(twistdist * 2.0f, Math.Min(lwy, lxz) * 0.25f));
                    AddCircles(center, size, verts, colors, groups);
                }
            }

            Vec3f[] averts = verts.ToArray();
            Vec4f[][] acolors = new Vec4f[][] { colors.ToArray() };
            int[][] agroups = new int[][] { groups.ToArray() };
            float[] aptszs = new float[] { 0.1f };
            float[] alnwidths = new float[] { 3.0f };
            int[] agrpszs = new int[] { 2 };
            int[] auids = Enumerable.Repeat(-1, verts.Count).ToArray();
            bool[] asels = Enumerable.Repeat(false, verts.Count).ToArray();
            IndexedViewableAlpha viewdecor = new IndexedViewableAlpha(averts, acolors, agroups, aptszs, alnwidths, agrpszs, auids, asels);

            return viewdecor;
        }

        private IndexedViewableAlpha GetAnnotations_Merge(IndexedViewableAlpha viewableBefore, IndexedViewableAlpha viewableAfter)
        {
            List<Vec3f> verts = new List<Vec3f>();
            List<int> groups = new List<int>();
            List<Vec4f> colors = new List<Vec4f>();

            List<Vec3f> selverts = viewableBefore.GetSelectedVerts(false);
            int c = selverts.Count;
            float max = 0.0f;
            for (int i0 = 0; i0 < c; i0++) for (int i1 = i0 + 1; i1 < c; i1++) max = Math.Max(max, (selverts[i0] - selverts[i1]).LengthSqr);
            max = Math.Max(FMath.Sqrt(max) * 0.25f, 0.01f);

            List<Vec3f> newverts = viewableAfter.GetSelectedVerts(true);
            //if( newverts.Count != 1 ) throw new Exception( "failed sanity check, count = " + newverts.Count );
            foreach (Vec3f v in newverts) AddCircles(v, max, verts, colors, groups);

            /*for( int ig = 0; ig < viewableAfter.GroupSizes.Length; ig++ )
			{
				if( viewableAfter.GroupSizes[ig] != 1 ) continue;
				int[] inds = viewableAfter.Indices[ig];
				for( int ivert = 0; ivert < inds.Length; ivert++ )
				{
					if( viewableAfter.Selected[inds[ivert]] )
						AddCircles( viewableAfter.Verts[inds[ivert]], max, verts, colors, groups );
						//AddCircle( viewableAfter.Verts[inds[ivert]], 0.2f, Vec3f.Normalize( viewableAfter.Verts[inds[ivert]] ), 0.1f, verts, colors, groups );
				}
			}*/


            Vec3f[] averts = verts.ToArray();
            Vec4f[][] acolors = new Vec4f[][] { colors.ToArray() };
            int[][] agroups = new int[][] { groups.ToArray() };
            float[] aptszs = new float[] { 0.1f };
            float[] alnwidths = new float[] { 3.0f };
            int[] agrpszs = new int[] { 2 };
            int[] auids = Enumerable.Repeat(-1, verts.Count).ToArray();
            bool[] asels = Enumerable.Repeat(false, verts.Count).ToArray();
            IndexedViewableAlpha viewdecor = new IndexedViewableAlpha(averts, acolors, agroups, aptszs, alnwidths, agrpszs, auids, asels);

            return viewdecor; // + viewbefore.Offset(new Vec3f(-10,0,0)) + viewafter.Offset(new Vec3f(10,0,0));
        }

        private void AddCircles(Vec3f center, float radius, List<Vec3f> verts, List<Vec4f> colors, List<int> groups)
        {
            Vec4f color = new Vec4f(1.0f, 1.0f, 0.0f, 1.0f);
            Vec3f axisx = Vec3f.X;
            Vec3f axisy = Vec3f.Y;
            Vec3f axisz = Vec3f.Z;

            int count = verts.Count;

            for (int degree = 0; degree <= 360; degree += 10)
            {
                int i = degree / 10;
                int j = ((degree + 10) % 360) / 10;
                float radians = (float)degree * FMath.PI / 180.0f;
                float cos = FMath.Cos(radians) * radius;
                float sin = FMath.Sin(radians) * radius;
                verts.Add(center + axisx * cos + axisy * sin);
                verts.Add(center + axisy * cos + axisz * sin);
                verts.Add(center + axisz * cos + axisx * sin);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                groups.AddRange(new int[] { count + i * 3 + 0, count + j * 3 + 0, count + i * 3 + 1, count + j * 3 + 1, count + i * 3 + 2, count + j * 3 + 2 });
            }
        }

        private void AddCircle(Vec3f center, float radius, Vec3f norm, float offset, List<Vec3f> verts, List<Vec4f> colors, List<int> groups)
        {
            Vec4f color = new Vec4f(1.0f, 1.0f, 0.0f, 1.0f);
            Vec3f offvec = norm * offset;
            Vec3f r = Vec3f.RandomUnitVectorSphere();
            Vec3f axisx = Vec3f.Normalize(norm ^ r);
            Vec3f axisy = Vec3f.Normalize(norm ^ axisx);

            int count = verts.Count;
            for (int degree = 0; degree <= 360; degree += 10)
            {
                int i = degree / 10;
                int j = ((degree + 10) % 360) / 10;
                float radians = (float)degree * FMath.PI / 180.0f;
                Vec3f x = axisx * (FMath.Cos(radians) * radius);
                Vec3f y = axisy * (FMath.Sin(radians) * radius);
                Vec3f p = center + x + y;
                verts.Add(p + offvec);
                verts.Add(p - offvec);
                colors.Add(color);
                colors.Add(color);
                groups.AddRange(new int[] { count + i * 2 + 0, count + j * 2 + 0, count + i * 2 + 1, count + j * 2 + 1 });
            }
        }

        //private IndexedViewableAlpha GetAnnotations_Extrude( IndexedViewableAlpha viewBefore, IndexedViewableAlpha viewAfter )
        //{
        //	return GetAnnotations_Extrude( viewBefore, viewAfter, viewAfter - viewBefore );
        //}

        private IndexedViewableAlpha GetAnnotations_Extrude(IndexedViewableAlpha viewBefore, IndexedViewableAlpha viewAfter, IndexedViewableAlpha viewableAdd)
        {
            List<Vec3f> arrowverts = new List<Vec3f>();
            List<int>[] arrowgroups = new List<int>[] { new List<int>(), new List<int>(), new List<int>() };
            List<Vec4f>[] arrowcolors = new List<Vec4f>[] { new List<Vec4f>(), new List<Vec4f>(), new List<Vec4f>() };

            //viewableAdd.FillGroupUIDsAll();
            viewableAdd.FillGroupUIDs();

            // adding extrusion arrows
            HashSet<int> visuidsadd = viewableAdd.GetVisibleVertsUID();
            for (int ig = 0; ig < viewableAdd.GroupSizes.Length; ig++)
            {
                if (viewableAdd.GroupSizes[ig] != 4) continue;

                int[] added = viewableAdd.GroupsUIDs[ig];
                int[] inds = viewableAdd.Indices[ig];
                bool[] a = { false, false, false, false };
                int[] u = new int[4];

                for (int i = 0; i < added.Length; i += 4)
                {
                    int count = 0;
                    for (int j = 0; j < 4; j++) { a[j] = visuidsadd.Contains((u[j] = added[i + j])); if (a[j]) count++; }
                    if (count != 2) continue;

                    int istart = 0;
                    if (a[1] && a[2]) istart = 1;
                    else if (a[2] && a[3]) istart = 2;
                    else if (a[3] && a[0]) istart = 3;

                    Vec3f[] vecs = new Vec3f[4];
                    for (int j = 0; j < 4; j++) vecs[j] = viewableAdd.Verts[inds[i + ((istart + j) % 4)]]; //viewableAdd.GetVec3f( u[(istart + j) % 4] );

                    Vec3f arrowfrom = (vecs[2] + vecs[3]) / 2.0f;
                    Vec3f arrowto = (vecs[0] + vecs[1]) / 2.0f;
                    Vec3f side0 = (vecs[1] + vecs[2]) / 2.0f;
                    Vec3f side1 = (vecs[3] + vecs[0]) / 2.0f;
                    Vec3f norm = Vec3f.Normalize(arrowto - arrowfrom) ^ Vec3f.Normalize(side1 - side0);
                    float tolength = (vecs[0] - vecs[1]).Length;

                    //float twistdist = Math.Max( CalcTwistDistance( vecs[0], vecs[1], vecs[2], vecs[3] ), CalcTwistDistance( vecs[1], vecs[2], vecs[3], vecs[0] ) );
                    float twistdist = CalcTwistDistance(vecs[0], vecs[1], vecs[2], vecs[3]);

                    AddArrow(arrowfrom, arrowto, tolength, norm, twistdist + 0.05f, arrowverts, arrowcolors, arrowgroups); //0.1f
                }
            }

            Vec3f[] averts = arrowverts.ToArray();
            Vec4f[][] acolors = arrowcolors.Select((List<Vec4f> cs) => cs.ToArray()).ToArray();
            int[][] agroups = arrowgroups.Select((List<int> gs) => gs.ToArray()).ToArray();
            float[] aptszs = new float[] { 0.1f, 0.1f, 0.1f };
            float[] alnwidths = new float[] { 0.1f, 0.1f, 2.0f };
            int[] agrpszs = new int[] { 4, 3, 2 };
            int[] auids = Enumerable.Repeat(-1, arrowverts.Count).ToArray();
            bool[] asels = new bool[arrowverts.Count];
            IndexedViewableAlpha viewdecor = new IndexedViewableAlpha(averts, acolors, agroups, aptszs, alnwidths, agrpszs, auids, asels);

            return viewdecor; // + viewbefore.Offset(new Vec3f(-10,0,0)) + viewafter.Offset(new Vec3f(10,0,0));
        }

        private float CalcTwistDistance(Vec3f w, Vec3f x, Vec3f y, Vec3f z)
        {
            Vec3f a = (x - w);
            Vec3f dn = Vec3f.Normalize(z - x);
            Vec3f bn = Vec3f.Normalize(y - w);
            Vec3f cn_ = Vec3f.Normalize(dn ^ bn);
            //Vec3f c = cn_ * ( a % cn_ );
            //return c.Length;
            return Math.Abs(a % cn_);
        }

        private void AddArrow(Vec3f arrowfrom, Vec3f arrowto, float tomaxwidth, Vec3f norm, float offset, List<Vec3f> arrowverts, List<Vec4f>[] arrowcolors, List<int>[] arrowgroups)
        {
            float arrowlen = (arrowto - arrowfrom).Length;
            float mult = Math.Min(1.0f, arrowlen);
            float headlength = 0.50f * Math.Min(1.0f, arrowlen);
            float headwidth = Math.Min(0.20f, tomaxwidth * 0.25f);
            float barsize = 0.05f * Math.Min(1.0f, tomaxwidth);

            Vec3f arrowvec = Vec3f.Normalize(arrowto - arrowfrom);
            Vec3f arrowperp = Vec3f.Normalize(arrowvec) ^ norm;

            Vec3f arrowcorner0 = arrowto + (arrowperp * headwidth) - (arrowvec * headlength);
            Vec3f arrowcorner1 = arrowto - (arrowperp * headwidth) - (arrowvec * headlength);
            Vec3f arrowcornerc = (arrowcorner0 + arrowcorner1) / 2.0f;

            Vec3f arrowb0 = arrowfrom + (arrowperp * barsize);
            Vec3f arrowb1 = arrowcornerc + (arrowperp * barsize);
            Vec3f arrowb2 = arrowcornerc - (arrowperp * barsize);
            Vec3f arrowb3 = arrowfrom - (arrowperp * barsize);

            Vec3f arrowoffset = norm * offset * mult;

            int nverts = arrowverts.Count;

            arrowverts.Add(arrowb0 + arrowoffset);
            arrowverts.Add(arrowb1 + arrowoffset);
            arrowverts.Add(arrowb2 + arrowoffset);
            arrowverts.Add(arrowb3 + arrowoffset);
            arrowverts.Add(arrowto + arrowoffset);
            arrowverts.Add(arrowcorner0 + arrowoffset);
            arrowverts.Add(arrowcorner1 + arrowoffset);

            arrowverts.Add(arrowb0 - arrowoffset);
            arrowverts.Add(arrowb1 - arrowoffset);
            arrowverts.Add(arrowb2 - arrowoffset);
            arrowverts.Add(arrowb3 - arrowoffset);
            arrowverts.Add(arrowto - arrowoffset);
            arrowverts.Add(arrowcorner0 - arrowoffset);
            arrowverts.Add(arrowcorner1 - arrowoffset);

            //arrowverts.Add( arrowb0 );
            //arrowverts.Add( arrowb1 );
            //arrowverts.Add( arrowb2 );
            //arrowverts.Add( arrowb3 );
            //arrowverts.Add( arrowto );
            //arrowverts.Add( arrowcorner0 );
            //arrowverts.Add( arrowcorner1 );

            Vec3f c0 = (arrowcorner0 * 0.95f) + (arrowto * 0.05f);
            Vec3f c1 = (arrowcorner1 * 0.95f) + (arrowto * 0.05f); ;
            arrowverts.Add((arrowfrom * 0.95f) + (arrowto * 0.05f) + arrowoffset);
            arrowverts.Add((arrowto * 0.95f) + (arrowfrom * 0.05f) + arrowoffset);
            arrowverts.Add((c0 * 0.95f) + (c1 * 0.05f) + arrowoffset);
            arrowverts.Add((c1 * 0.95f) + (c0 * 0.05f) + arrowoffset);

            arrowverts.Add((arrowfrom * 0.95f) + (arrowto * 0.05f) - arrowoffset);
            arrowverts.Add((arrowto * 0.95f) + (arrowfrom * 0.05f) - arrowoffset);
            arrowverts.Add((c0 * 0.95f) + (c1 * 0.05f) - arrowoffset);
            arrowverts.Add((c1 * 0.95f) + (c0 * 0.05f) - arrowoffset);

            arrowcolors[0].AddRange(Enumerable.Repeat(new Vec4f(1.0f, 1.0f, 0.0f, 1.0f), 25));
            arrowcolors[1].AddRange(Enumerable.Repeat(new Vec4f(1.0f, 1.0f, 0.0f, 1.0f), 25));
            arrowcolors[2].AddRange(Enumerable.Repeat(new Vec4f(1.0f, 1.0f, 0.0f, 1.0f), 25));

            arrowgroups[0].Add(nverts + 0); arrowgroups[0].Add(nverts + 1);
            arrowgroups[0].Add(nverts + 2); arrowgroups[0].Add(nverts + 3);
            arrowgroups[1].Add(nverts + 4); arrowgroups[1].Add(nverts + 5); arrowgroups[1].Add(nverts + 6);

            arrowgroups[0].Add(nverts + 7); arrowgroups[0].Add(nverts + 8);
            arrowgroups[0].Add(nverts + 9); arrowgroups[0].Add(nverts + 10);
            arrowgroups[1].Add(nverts + 11); arrowgroups[1].Add(nverts + 12); arrowgroups[1].Add(nverts + 13);

            /*arrowgroups[2].AddRange( new int[] {
				nverts+14, nverts+15,
				nverts+15, nverts+19,
				nverts+19, nverts+18,
				nverts+18, nverts+20,
				nverts+20, nverts+16,
				nverts+16, nverts+17,
				nverts+17, nverts+14
			} );*/
            arrowgroups[2].AddRange(new int[] { nverts + 14, nverts + 15, nverts + 16, nverts + 15, nverts + 17, nverts + 15 });
            arrowgroups[2].AddRange(new int[] { nverts + 18, nverts + 19, nverts + 20, nverts + 19, nverts + 21, nverts + 19 });
        }

        public IndexedViewableAlpha GetSelectedGroupsViewable(IndexedViewableAlpha viewable, int index0start, int index0end)
        {
            int cselinds = 0;
            List<int> seluids = new List<int>();
            List<int> selinds = new List<int>();
            List<Vec3f> selverts = new List<Vec3f>();
            List<int> selvuids = new List<int>();
            List<int>[] selgroups = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            Vec4f[][] selcolors;

            for (int ivert = 0; ivert < viewable.nVerts; ivert++) if (viewable.Selected[ivert]) seluids.Add(viewable.VertUIDs[ivert]);
            //for( int index0 = index0start; index0 <= index0end; index0++ )
            //	foreach( SnapshotModel model in snapshots[index0].models )
            //		seluids.AddRange( model.selinds.Select( (int ind) => model.vertuids[ind] ) );

            for (int ivert = 0; ivert < viewable.nVerts; ivert++) if (seluids.Contains(viewable.VertUIDs[ivert]))
                {
                    selinds.Add(ivert);
                    selverts.Add(viewable.Verts[ivert]);
                    selvuids.Add(viewable.VertUIDs[ivert]);
                    cselinds++;
                }

            selcolors = colorSelected.Select((Vec4f c) => ArrayExt.CreateCopies(c, cselinds)).ToArray();

            int[][] ngrps = new int[][] { new int[] { -1 }, new int[] { -1, -1 }, new int[] { -1, -1, -1 }, new int[] { -1, -1, -1, -1 } };

            for (int igrps = 0; igrps < viewable.Indices.Length; igrps++)
            {
                int c = viewable.GroupSizes[igrps];
                int[] groups = viewable.Indices[igrps];
                for (int igrp = 0; igrp < groups.Length; igrp += c)
                {
                    int i;
                    for (i = 0; i < c; i++) if (!selinds.Contains(groups[igrp + i])) break; else ngrps[c - 1][i] = selinds.IndexOf(groups[igrp + i]);
                    if (i != c) continue;
                    selgroups[c - 1].AddRange(ngrps[c - 1].CloneArray());
                }
            }

            //System.Console.WriteLine( "showing {0} + {1} + {2} + {3} groups", selgroups[0].Count, selgroups[1].Count, selgroups[2].Count, selgroups[3].Count );

            Vec3f[] averts = selverts.ToArray();
            Vec4f[][] acolors = selcolors;
            int[][] agroups = selgroups.Select((List<int> gs) => gs.ToArray()).ToArray();
            float[] aptszs = new float[] { 10.0f, 10.0f, 10.0f, 10.0f };
            float[] alnwidths = new float[] { 0.1f, 3.0f, 0.1f, 0.1f };
            int[] agrpszs = new int[] { 1, 2, 3, 4 };
            int[] auids = selvuids.ToArray();
            bool[] asels = Enumerable.Repeat(true, cselinds).ToArray();
            IndexedViewableAlpha selviewable = new IndexedViewableAlpha(averts, acolors, agroups, aptszs, alnwidths, agrpszs, auids, asels);

            return selviewable;
        }

        /*public IndexedViewableAlpha[] GetViewables( Cluster cluster, int LevelOfDetail, bool usefinalpos, bool selectverts )
		{
			IndexedViewableAlpha[] viewables = null;
			
			if( LevelOfDetail == 0 )
			{
				viewables = new IndexedViewableAlpha[] { snapshots[cluster.end].GetViewable() };
			} else {
				switch( cluster.name.SplitOnce('.')[0] )
				{
					
				case "topo":
					return new IndexedViewableAlpha[] { GetViewableTopo( cluster, LevelOfDetail, usefinalpos, selectverts ) };
					
				case "timesteps":
					int start = cluster.start;
					int dur = cluster.duration;
					int steps = 11;
					viewables = new IndexedViewableAlpha[steps];
					for( int i = 0; i < steps; i++ )
					{
						int isnapshot = start + (int)( (float)(dur - 1) * (float) i / (float)(steps - 1) );
						viewables[i] = snapshots[isnapshot].GetViewable();
					}
					break;
					
				default:
					viewables = new IndexedViewableAlpha[] { snapshots[cluster.end].GetViewable() };
					break;
					
				}
			}
			
			if( usefinalpos ) Viewables_FinalPositions( viewables );
			if( selectverts ) Viewables_Selections( viewables );
			
			return viewables;
		}*/

        /*private IndexedViewableAlpha GetViewableTopo( Cluster cluster, int LevelOfDetail, bool usefinalpos, bool selectverts )
		{
			int b = basemodifyid[cluster.start];
			IndexedViewableAlpha viewbefore = snapshots[b].GetViewable();
			IndexedViewableAlpha viewafter = snapshots[cluster.end].GetViewable();
			
			if( usefinalpos ) { Viewables_FinalPositions( viewbefore ); Viewables_FinalPositions( viewafter ); }
			if( selectverts ) { Viewable_Selections( viewbefore ); Viewable_Selections( viewafter ); }
			
			IndexedViewableAlpha viewadd = viewafter - viewbefore;
			IndexedViewableAlpha viewsub = viewbefore - viewafter;
			
			if( new string[] {
				"topo.add.circle", "topo.add.cone", "topo.add.cube", "topo.add.cylinder",
				"topo.add.grid", "topo.add.sphere.ico", "topo.add.sphere.uv", "topo.add.monkey",
				"topo.add.plane", "topo.add.torus" }.Contains( cluster.name ) )
			{
				viewadd.RecolorGroups( (int[] inds) => recoloradd[inds.Length-1] );
				return ( viewafter % viewbefore ) + viewadd;
			}
			
			if( cluster.name == "topo.loopcut" || cluster.name == "topo.subdivide" )
			{
				viewadd.RecolorGroups( (int[] inds) => recoloradd[inds.Length-1] );
				return ( viewafter % viewbefore ) + viewadd;
			}
			
			
			IndexedViewableAlpha viewsame = viewbefore % viewafter;
			
			if( cluster.name == "topo.merge" )
			{
				viewsub.RecolorGroups( (int[] inds) => recolormerge[inds.Length-1] );
				return viewsame + viewsub;
			}
			
			viewadd.RecolorGroups( (int[] inds) => recoloradd[inds.Length-1] );
			viewsub.RecolorGroups( (int[] inds) => recolordel[inds.Length-1] );
			
			if( cluster.name == "topo.extrude" )
			{
				List<Vec3f> arrowverts = new List<Vec3f>();
				List<int>[] arrowgroups = new List<int>[] { new List<int>(), new List<int>() };
				List<Vec4f>[] arrowcolors = new List<Vec4f>[] { new List<Vec4f>(), new List<Vec4f>() };
				
				
				viewadd.FillGroupUIDs();
				// adding extrusion arrows
				HashSet<int> visuidsadd = viewadd.GetVisibleVertsUID();
				for( int ig = 0; ig < viewadd.GroupSizes.Length; ig++ )
				{
					if( viewadd.GroupSizes[ig] != 4 ) continue;
					int[] added = viewadd.GroupsUIDs[ig];
					bool[] a = { false, false, false, false };
					int[] u = new int[4];
					for( int i = 0; i < added.Length; i += 4 )
					{
						for( int j = 0; j < 4; j++ ) { a[j] = visuidsadd.Contains( added[i+j] ); u[j] = added[i+j]; }
						int count = a.Sum( (bool add) => ( add ? 1 : 0 ) );
						if( count >= 3 ) continue;
						if( count != 2 ) throw new Exception( "Unexpected number of added verts (" + count + ")! " + cluster.start + "," + cluster.end );
						Vec3f arrowfrom = new Vec3f();
						Vec3f arrowto = new Vec3f();
						for( int j = 0; j < 4; j++ )
						{
							if( a[j] ) arrowto += viewadd.GetVec3f( u[j] );
							else arrowfrom += viewadd.GetVec3f( u[j] );
						}
						
						arrowfrom /= 2.0f;
						arrowto /= 2.0f;
						
						Vec3f arrowvec = Vec3f.Normalize( arrowto - arrowfrom );
						Vec3f arrowoff = Vec3f.Normalize( viewadd.GetVec3f( u[0] ) - arrowto );
						Vec3f arrowperp = Vec3f.Normalize( arrowvec ^ arrowoff ^ arrowvec );
						
						Vec3f arrowcorner0 = arrowto + ( arrowperp * 0.20f ) - ( arrowvec * 0.20f );
						Vec3f arrowcorner1 = arrowto - ( arrowperp * 0.20f ) - ( arrowvec * 0.20f );
						Vec3f arrowcornerc = ( arrowcorner0 + arrowcorner1 ) / 2.0f;
						
						Vec3f arrowb0 = arrowfrom + (arrowperp * 0.05f);
						Vec3f arrowb1 = arrowcornerc + (arrowperp * 0.05f);
						Vec3f arrowb2 = arrowcornerc - (arrowperp * 0.05f);
						Vec3f arrowb3 = arrowfrom - (arrowperp * 0.05f);
						
						int nverts = arrowverts.Count;
						
						arrowverts.Add( arrowb0 );
						arrowverts.Add( arrowb1 );
						arrowverts.Add( arrowb2 );
						arrowverts.Add( arrowb3 );
						
						arrowverts.Add( arrowto );
						arrowverts.Add( arrowcorner0 );
						arrowverts.Add( arrowcorner1 );
						
						arrowcolors[0].AddRange( Enumerable.Repeat( new Vec4f( 1.0f, 1.0f, 0.0f, 1.0f ), 7 ) );
						arrowcolors[1].AddRange( Enumerable.Repeat( new Vec4f( 1.0f, 1.0f, 0.0f, 1.0f ), 7 ) );
						
						arrowgroups[0].Add( nverts + 0 ); arrowgroups[0].Add( nverts + 1 );
						arrowgroups[0].Add( nverts + 2 ); arrowgroups[0].Add( nverts + 3 );
						
						arrowgroups[1].Add( nverts + 4 ); arrowgroups[1].Add( nverts + 5 ); arrowgroups[1].Add( nverts + 6 );
					}
				}
				
				Vec3f[] averts = arrowverts.ToArray();
				Vec4f[][] acolors = arrowcolors.Select( (List<Vec4f> cs) => cs.ToArray() ).ToArray();
				int[][] agroups = arrowgroups.Select( (List<int> gs) => gs.ToArray() ).ToArray();
				float[] aptszs = new float[] { 0.1f, 0.1f };
				float[] alnwidths = new float[] { 3.0f, 0.1f };
				int[] agrpszs = new int[] { 4, 3 };
				int[] auids = Enumerable.Repeat( -1, arrowverts.Count ).ToArray();
				bool[] asels = Enumerable.Repeat( false, arrowverts.Count ).ToArray();
				IndexedViewableAlpha viewdecor = new IndexedViewableAlpha( averts, acolors, agroups, aptszs, alnwidths, agrpszs, auids, asels );
				
				return viewsame + viewdecor + viewadd; // + viewbefore.Offset(new Vec3f(-10,0,0)) + viewafter.Offset(new Vec3f(10,0,0));
			}
			
			return viewsame + viewadd + viewsub;
		}*/

        /*public CameraProperties[] GetCamerasSimple( int index0 )
		{
			return snapshots[index0].cameras;
		}*/

        /*public CameraProperties[] GetCameras( Clustering layer, Cluster cluster ) { return GetCameras( layer, cluster, null ); }*/

        /*public CameraProperties[] GetCameras( Clustering layer, Cluster cluster, IndexedViewableAlpha viewable )
		{
			bool ortho = false;
			Vec3f target = new Vec3f();
			Vec3f normal = new Vec3f();
			float dist = 10.0f;
			
			int cselected = ( viewable == null ? 0 : viewable.Selected.Count( sel => sel ) );
			if( cselected == 0 ) {
				SnapshotScene scene = snapshots[cluster.end];
				SnapshotModel editmodel = scene.GetEditModel();
				
				if( editmodel != null ) {
					if( editmodel.selinds.Length == 0 ) target = editmodel.GetVerts().Average();
					else {
						Vec3f[] verts = editmodel.GetVerts();
						Vec3f[] norms = editmodel.GetVertNormals();
						
						target = editmodel.selinds.Select( (int ind) => verts[ind] ).Average();
						normal = editmodel.selinds.Select( (int ind) => norms[ind] ).Average();
						
						// sanity checks
						if( float.IsNaN( normal.x ) || float.IsNaN( normal.y ) || float.IsNaN( normal.z ) )
						{
							System.Console.WriteLine( "normal = NaN" );
							System.Console.WriteLine( "norms.Length = " + norms.Length );
							System.Console.WriteLine( "editmodel.selinds.Length = " + editmodel.selinds.Length );
							foreach( int i in editmodel.selinds )
							{
								if( float.IsNaN( norms[i].x ) || float.IsNaN( norms[i].y ) || float.IsNaN( norms[i].z ) )
								{
									System.Console.WriteLine( "norm[" + i + "] is NaN" );
								}
							}
						}
				
					}
				} else if( scene.selectedobjects.Length > 0 ) {
					var objs = scene.selectedobjects;
					var objlocs = objs.Select( (SnapshotModel obj) => obj.GetVerts().Average() );
					target = objlocs.Average();
					var objnorms = objs.Select( (SnapshotModel obj) => obj.GetVertNormals().Average() );
					normal = objnorms.Average();
				} else {
					var objs = scene.models;
					var objlocs = objs.Select( (SnapshotModel obj) => obj.GetVerts().Average() );
					target = objlocs.Average();
					var objnorms = objs.Select( (SnapshotModel obj) => obj.GetVertNormals().Average() );
					normal = objnorms.Average();
				}
			} else {
				SnapshotScene scene = snapshots[cluster.end];
				Vec3f[] norms = scene.models.Select( model => model.GetVertNormals() ).Aggregate( (Vec3f[]) null, (a, ns) => a.AddReturn(ns) ).ToArray();
				int[] uids = scene.models.Select( model => model.GetVertUIDs() ).Aggregate( (int[])null, (a,ns) => a.AddReturn(ns) ).ToArray();
				
				target = new Vec3f();
				normal = new Vec3f();
				for( int i = 0; i < viewable.nVerts; i++ )
				{
					if( !viewable.Selected[i] || viewable.VertUIDs[i] < 0 ) continue;
					target += viewable.Verts[i];
					int n = uids.IndexOf( viewable.VertUIDs[i] );
					if( n >= 0 ) normal += norms[n];
				}
				target /= cselected;
				normal /= cselected;
			}
			
			if( normal.LengthSqr < 0.00001f ) normal = -Vec3f.Y;
			if( Math.Abs( normal % Vec3f.Z ) > 0.90f ) normal = -Vec3f.Y * 0.90f + normal * 0.10f;
			
			Matrix lookat = Matrix.LookAt( new Vec3f(), -normal, Vec3f.Z );
			Quatf rotation = Quatf.MatrixToQuatf( lookat );
			
			CameraProperties optcam = new CameraProperties();
			optcam.Set( target, rotation, dist, ortho );
			
			CameraProperties[] cams = layer.GetCamerasSimple().Select( camsset =>
			                                                    CameraProperties.GetSpecificCamera( camsset, ViewSelections.Artist )
			                                                    ).ToArray();
			CameraProperties smoothgauscam;
			CameraProperties smoothbilatcam;
			int icluster = layer.GetClusterIndex( cluster );
			if( icluster == -1 ) {
				smoothgauscam = CameraProperties.GetSpecificCamera( cams, ViewSelections.Artist );
				smoothbilatcam = CameraProperties.GetSpecificCamera( cams, ViewSelections.Artist );
			} else {
				smoothgauscam = CameraProperties.SmoothGaussian( cams, icluster, CameraSmoothSigmat );
				smoothbilatcam = CameraProperties.SmoothBilateral( cams, icluster, CameraSmoothSigmat, CameraSmoothSigmax );
			}
			
			// sanity check
			if( float.IsNaN( optcam.GetRotation().Scalar ) || float.IsNaN( optcam.GetRotation().Vector.x ) )
			{
				System.Console.WriteLine( "Bad BestCam (NaN) " );
				System.Console.WriteLine( "lookat:\n" + lookat );
				System.Console.WriteLine( "normal: " + normal.ToStringFormatted() );
				System.Console.WriteLine( "target: " + target.ToStringFormatted() );
			}
			
			return snapshots[cluster.end].cameras.AddReturn( optcam, smoothgauscam, smoothbilatcam );
		}*/
    }
}


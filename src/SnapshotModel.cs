using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Common.Libs.MiscFunctions;
using Common.Libs.VMath;

namespace MeshFlowViewer
{
    public enum Modifiers
    {
        Mirror,
        //SubDiv,
        //BoolOp,
        //Solidify
    }
    public enum ModifierBoolOps
    {
        Intersect,
        Union,
        Difference
    }

    public abstract class Modifier : IBinaryConvertible
    {
        public Modifiers type { get; set; }
        public string name { get; set; }

        public Modifier(Modifiers type) { this.type = type; }
        public Modifier(Modifiers type, string name) { this.type = type; this.name = name; }

        protected abstract void WriteBinaryInfo(BinaryWriter bw);
        protected abstract void ReadBinaryInfo(BinaryReader br);

        //public virtual void WriteBinary(BinaryWriter bw)
        //{
        //    bw.Write(name);
        //    bw.WriteT(type);
        //    WriteBinaryInfo(bw);
        //}

        public virtual void ReadBinary(BinaryReader br)
        {
            name = br.ReadString();
            type = br.ReadEnum<Modifiers>();
            ReadBinaryInfo(br);
        }

        public static Modifier ReadBinaryFile(BinaryReader br)
        {
            string name = br.ReadString();
            Modifiers type = br.ReadEnum<Modifiers>();
            switch (type)
            {
                //case Modifiers.BoolOp: return new ModifierBoolOp( name, br );
                case Modifiers.Mirror: return new ModifierMirror(name, br);
                //case Modifiers.Solidify: return new ModifierSolidify( name, br );
                //case Modifiers.SubDiv: return new ModifierSubDiv( name, br );
                default: throw new Exception("unimplemented");
            }
        }
    }

    public class ModifierMirror : Modifier
    {
        public bool usex { get; set; }
        public bool usey { get; set; }
        public bool usez { get; set; }
        public float mergethreshold { get; set; }


        public ModifierMirror() : base(Modifiers.Mirror) { }

        public ModifierMirror(string name, bool usex, bool usey, bool usez, float mergethreshold)
            : base(Modifiers.Mirror, name)
        {
            this.usex = usex;
            this.usey = usey;
            this.usez = usez;
            this.mergethreshold = mergethreshold;
        }

        public ModifierMirror(string name, BinaryReader br)
            : base(Modifiers.Mirror, name)
        {
            usex = br.ReadBoolean();
            usey = br.ReadBoolean();
            usez = br.ReadBoolean();
            mergethreshold = br.ReadSingle();
        }

        protected override void WriteBinaryInfo(BinaryWriter bw)
        {
            bw.Write(usex);
            bw.Write(usey);
            bw.Write(usez);
            bw.Write(mergethreshold);
        }

        protected override void ReadBinaryInfo(BinaryReader br)
        {
            usex = br.ReadBoolean();
            usey = br.ReadBoolean();
            usez = br.ReadBoolean();
            mergethreshold = br.ReadSingle();
        }
    }

    public class SnapshotModel : IBinaryConvertible
    {
        public static ModelingHistory history;

        public SnapshotModel prev;
        public bool nochange;

        public int objuid;
        public int objind;
        public string objlabel = "";
        public string objname = "";
        public string objfilename = "";

        public bool objvisible;
        public bool objselected;
        public bool objactive;
        public bool objedit;

        public Modifier[] modifiers;

        public int nverts;
        protected Vec3f[] verts;
        protected Vec3f[] vertnormals;
        public string[] vertlabels;
        public int[] vertuids;

        public int[] selinds;
        public bool[] isvertselected;

        public List<GroupInfo>[] groups;
        public List<bool>[] groupselected;
        public int[][] univgroups = null;
        public List<Vec3f>[] facenormals;

        public static readonly int[] groupsizes = new int[] { 1, 2, 3, 4 };     // need to include 5?

        public static float[] pointsizes_edit = new float[] { 1.0f, 0.1f, 0.1f, 0.1f }; //3.0f,
        public static float[] pointsizes_view = new float[] { 1.0f, 0.1f, 0.1f, 0.1f };

        public static float[] linewidths_edit = new float[] { 0.1f, 1.0f, 0.1f, 0.1f };
        public static float[] linewidths_selected = new float[] { 0.1f, 1.0f, 0.1f, 0.1f }; //1.5f
        public static float[] linewidths_unselected = new float[] { 0.1f, 1.0f, 0.1f, 0.1f };

        public static Vec4f colorobjhidden = new Vec4f(0.0f, 0.0f, 0.0f, 0.0f);
        public static Vec4f colorobjunselected = new Vec4f(0.0f, 0.0f, 0.0f, 1.0f);
        public static Vec4f colorobjselected = new Vec4f(0.0f, 0.0f, 0.0f, 1.0f); //new Vec4f( 1.0f, 0.6f, 0.0f, 1.0f );
        public static Vec4f colorobjactive = new Vec4f(0.0f, 0.0f, 0.0f, 1.0f); // new Vec4f( 0.9f, 0.7f, 0.0f, 1.0f );

        public static Vec4f colorvertunselected = new Vec4f(0.0f, 0.0f, 0.0f, 1.0f);
        public static Vec4f colorvertselected = new Vec4f(1.0f, 0.75f, 0.0f, 1.0f);
        public static Vec4f colorface = new Vec4f(0.6f, 0.6f, 0.6f, 1.0f);

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    bw.WriteParams(objuid, objind);
        //    bw.WriteParams(objlabel, objname, objfilename);
        //    bw.WriteParams(objvisible, objselected, objactive, objedit);
        //    bw.WriteArray(modifiers);
        //    bw.Write(nverts);
        //    bw.WriteT(nochange);
        //    bw.WriteArray(verts);
        //    bw.WriteArray(vertnormals);
        //    bw.WriteArray(vertlabels);
        //    bw.WriteArray(vertuids);
        //    bw.WriteArray(selinds);
        //    bw.WriteJaggedList(groups);
        //    bw.WriteJaggedList(facenormals);
        //    bw.WriteJaggedArray(univgroups);
        //}

        public void ReadBinary(BinaryReader br)
        {
            br.Read(out objuid);
            br.Read(out objind);
            br.Read(out objlabel);
            br.Read(out objname);
            br.Read(out objfilename);
            br.Read(out objvisible);
            br.Read(out objselected);
            br.Read(out objactive);
            br.Read(out objedit);
            br.ReadArray(out modifiers); //, ( BinaryReader r ) => Modifier.ReadBinaryFile( r ) );
            br.Read(out nverts);
            br.Read(out nochange);
            br.ReadArray(out verts);
            br.ReadArray(out vertnormals);
            br.ReadArray(out vertlabels);
            br.ReadArray(out vertuids);
            br.ReadArray(out selinds);
            br.ReadJaggedList(out groups);
            br.ReadJaggedList(out facenormals);
            br.ReadJaggedArray(out univgroups);

            FillIsVertSelected();
            //model.ReorderGroups();
        }

        public static SnapshotModel ReadBinaryFile(BinaryReader br)
        {
            SnapshotModel model = new SnapshotModel();
            model.ReadBinary(br);
            return model;
        }


        public SnapshotModel(Vec3f[] verts, int[] selinds, List<GroupInfo>[] groups)
        {
            this.verts = verts;
            this.selinds = selinds;
            this.groups = groups;

            this.nverts = verts.Length;

            FillIsVertSelected();
            ReorderGroups();
        }

        public SnapshotModel() { }

        public SnapshotModel(string sFilename)
        {
            int nverts = 0;
            int nfaces = 0;
            int nvertsel = 0;
            //int nedgesel = 0;
            //int nfacesel = 0;
            int nmods = 0;

            Vec3f[] coords;

            int[] selected;
            Modifier[] modifiers;

            using (Stream s = new FileStream(sFilename, FileMode.Open))
            {
                string plyline = FileIOFunctions.ReadTextString(s);
                if (plyline != "ply") throw new ArgumentException("TVModel.LoadObjectFromPLY: Specified file is not .ply file");

                bool header = true;
                while (header)
                {
                    string cmd = FileIOFunctions.ReadTextString(s);
                    switch (cmd)
                    {
                        case "format":
                        case "comment":
                        case "property":
                            while (s.ReadByte() != 10) ; // ignore the rest of the line
                            break;
                        case "element":
                            string variable = FileIOFunctions.ReadTextString(s);
                            int val = FileIOFunctions.ReadTextInteger(s);
                            switch (variable)
                            {
                                case "vertex": nverts = val; break;
                                case "face": nfaces = val; break;
                                case "selected":
                                    nvertsel = val;
                                    FileIOFunctions.ReadTextInteger(s); // nedgesel = 
                                    FileIOFunctions.ReadTextInteger(s); // nfacesel = 
                                    break;
                                case "modifiers": nmods = val; break;
                                default: throw new Exception("TVModel.LoadObjectFromPLY: Unhandled element type " + variable);
                            }
                            break;
                        case "end_header": header = false; break;
                        default: throw new Exception("TVModel.LoadObjectFromPLY: Unhandled command type " + cmd);
                    }
                }

                coords = new Vec3f[nverts];
                selected = new int[nvertsel];
                modifiers = new Modifier[nmods];
                groups = new List<GroupInfo>[] {
                    new List<GroupInfo>( nverts ), new List<GroupInfo>( nfaces ), new List<GroupInfo>( nfaces ), new List<GroupInfo>( nfaces )
                };
                groupselected = new List<bool>[] {
                    new List<bool>( nverts ), new List<bool>( nverts ), new List<bool>( nverts ), new List<bool>( nverts )
                };
                facenormals = new List<Vec3f>[] { new List<Vec3f>(), new List<Vec3f>() };

                // read vert locations and visibility
                for (int i = 0; i < nverts; i++)
                {
                    coords[i] = new Vec3f(FileIOFunctions.ReadTextFloat(s), FileIOFunctions.ReadTextFloat(s), FileIOFunctions.ReadTextFloat(s));
                    bool v = (FileIOFunctions.ReadTextInteger(s) == 1);
                    groups[0].Add(new GroupInfo(new int[] { i }, v));
                }

                // read inds of edges and faces
                for (int i = 0; i < nfaces; i++)
                {
                    int count = FileIOFunctions.ReadTextInteger(s);
                    int[] inds = new int[count];
                    for (int j = 0; j < count; j++) inds[j] = FileIOFunctions.ReadTextInteger(s);
                    bool v = (FileIOFunctions.ReadTextInteger(s) == 1);
                    bool gsel = (FileIOFunctions.ReadTextInteger(s) == 1);
                    groups[count - 1].Add(new GroupInfo(inds, v));
                    groupselected[count - 1].Add(gsel);

                    if (count == 3 || count == 4)
                    {
                        Vec3f v0 = Vec3f.Normalize(coords[inds[0]] - coords[inds[1]]);
                        Vec3f v1 = Vec3f.Normalize(coords[inds[2]] - coords[inds[1]]);
                        Vec3f vn = v1 ^ v0;
                        if (vn.LengthSqr > 0.00001f) facenormals[count - 3].Add(Vec3f.Normalize(vn));
                        else facenormals[count - 3].Add(new Vec3f());
                    }
                }

                groups[1].TrimExcess();
                groups[2].TrimExcess();
                groups[3].TrimExcess();
                groupselected[1].TrimExcess();
                groupselected[2].TrimExcess();
                groupselected[3].TrimExcess();

                // read inds of selected verts
                for (int i = 0; i < nvertsel; i++) selected[i] = FileIOFunctions.ReadTextInteger(s);

                // read modifiers
                for (int i = 0; i < nmods; i++)
                {
                    string modifier = FileIOFunctions.ReadTextString(s);
                    switch (modifier)
                    {
                        case "mirror":
                            modifiers[i] = new ModifierMirror()
                            {
                                name = FileIOFunctions.ReadTextQuotedString(s),
                                usex = (FileIOFunctions.ReadTextInteger(s) == 1),
                                usey = (FileIOFunctions.ReadTextInteger(s) == 1),
                                usez = (FileIOFunctions.ReadTextInteger(s) == 1),
                                mergethreshold = FileIOFunctions.ReadTextFloat(s)
                            };
                            break;
                        /*case "subdiv":
                            modifiers[i] = new ModifierSubDiv() {
                                name = FileIOFunctions.ReadTextQuotedString(s),
                                levels = FileIOFunctions.ReadTextInteger(s)
                            };
                            break;
                        case "boolop":
                            modifiers[i] = new ModifierBoolOp
                                (
                                 FileIOFunctions.ReadTextQuotedString(s),
                                 ModifierBoolOp.StringToBoolOp( FileIOFunctions.ReadTextString(s) ),
                                 FileIOFunctions.ReadTextString(s)
                                 );
                            break;
                        case "solidify":
                            modifiers[i] = new ModifierSolidify
                                (
                                 FileIOFunctions.ReadTextString(s),
                                 FileIOFunctions.ReadTextFloat(s),
                                 FileIOFunctions.ReadTextFloat(s),
                                 FileIOFunctions.ReadTextFloat(s),
                                 FileIOFunctions.ReadTextFloat(s),
                                 FileIOFunctions.ReadTextFloat(s),
                                 ( FileIOFunctions.ReadTextInteger(s) == 1 ),
                                 ( FileIOFunctions.ReadTextInteger(s) == 1 ),
                                 ( FileIOFunctions.ReadTextInteger(s) == 1 )
                                 );
                            break;*/
                        default: throw new Exception("TVModel.LoadObjectFromPLY: Unknown modifier " + modifier);
                    }
                }
            }

            this.verts = coords;
            this.selinds = selected;
            this.modifiers = modifiers;

            this.nverts = nverts;
            FillIsVertSelected();
            CalcVertexNormals();
        }

        private int editcountcache = -1;
        public int GetEditCount()
        {
            if (editcountcache == -1)
            {
                if (prev == null) editcountcache = (nochange ? 0 : 1); //editcountcache = ( objedit ? 1 : 0 );
                else editcountcache = prev.GetEditCount() + (nochange ? 0 : 1); //( objedit ? 1 : 0 );
            }
            return editcountcache;
        }

        private void CalcVertexNormals()
        {
            //System.Console.WriteLine( "Calculating Vertex Normals..." );
            vertnormals = new Vec3f[nverts];
            for (int ivert = 0; ivert < nverts; ivert++)
            {
                Vec3f n = new Vec3f();
                int count = 0;
                for (int igrps = 0; igrps < 2; igrps++)
                {
                    List<GroupInfo> grps = groups[igrps + 2];
                    List<Vec3f> nrmls = facenormals[igrps];
                    grps.Each(delegate (GroupInfo grp, int igrp) {
                        if (!grp.inds.Contains(ivert)) return;
                        n += nrmls[igrp];
                        count++;
                    });
                }
                if (count > 0) vertnormals[ivert] = n / count;
            }
        }

        private void FillIsVertSelected()
        {
            this.isvertselected = new bool[nverts];
            foreach (int ind in selinds) isvertselected[ind] = true;
        }

        public void ReorderGroups()
        {
            if (groups == null) return;

            //groups.EachInParallel( (List<GroupInfo> grps, int i) =>
            for (int igroups = 0; igroups < 4; igroups++)
            {
                List<GroupInfo> grps = groups[igroups];
                int c = grps.Count;

                if (c == 0) continue;

                String[] keys = new String[c];

                for (int i = 0; i < c; i++)
                {
                    GroupInfo g = grps[i];
                    int[] uids = g.inds.Select(ind => vertuids[ind]).ToArray();
                    g.Reorder(uids);
                    keys[i] = g.GetKeyNoVis(vertuids);
                }

                int[] order = keys.GetSortIndices_QuickSort();
                groups[igroups] = order.Select((int i) => grps[i]).ToList();
            }
            /*foreach( List<GroupInfo> grps in groups )
			{
				foreach( GroupInfo g in grps )
				{
					int[] uids = g.inds.Select( ind => vertuids[ind] ).ToArray();
					g.Reorder( uids );
					
					//if( g.inds.Length >= 2 && vertuids[g.inds[0]] <= vertuids[g.inds[1]] ) Debug.Assert( false );
					//if( g.inds.Length >= 3 && vertuids[g.inds[0]] <= vertuids[g.inds[2]] ) Debug.Assert( false );
					//if( g.inds.Length >= 4 && vertuids[g.inds[0]] <= vertuids[g.inds[3]] ) Debug.Assert( false );
				}
			} 
			
			groups = groups.Select( (List<GroupInfo> grps) => grps.OrderBy( (GroupInfo g) => g.GetKeyNoVis( vertuids ) ).ToList() ).ToArray(); // .inds[0]*/
        }

        public void DeleteNoChangeData(SnapshotModel prev)
        {
            this.verts = null;
            this.vertnormals = null;
            this.vertuids = null;
            this.vertlabels = null;

            this.groups[0] = null;
            this.groups[1] = null;
            this.groups[2] = null;
            this.groups[3] = null;
            this.groups = null;

            this.facenormals[0] = null;
            this.facenormals[1] = null;
            this.facenormals = null;

            this.nochange = true;
            this.prev = prev;
        }

        public List<GroupInfo>[] GetGroups()
        {
            if (nochange) return prev.GetGroups();
            if (univgroups == null) return groups;
            return history.GetGroups(univgroups).Select((GroupInfo[] grps) => grps.ToList()).ToArray();
        }

        public List<Vec3f>[] GetFaceNormals()
        {
            if (nochange) return prev.GetFaceNormals();
            return facenormals;
        }

        public Vec3f[] GetVerts()
        {
            if (nochange) return prev.GetVerts();
            return verts;
        }

        public Vec3f[] GetVertNormals()
        {
            if (nochange) return prev.GetVertNormals();
            return vertnormals;
        }

        public string[] GetVertLabels()
        {
            if (nochange) return prev.GetVertLabels();
            return vertlabels;
        }

        public int[] GetVertUIDs()
        {
            if (nochange) return prev.GetVertUIDs();
            return vertuids;
        }

        /*public IndexedViewableAlpha GetViewable_OnlyChanged( Vec3f[] match, bool applymodifiers )
		{
			if( nochange ) return null;
			return GetViewable( match, applymodifiers );
		}*/

        public IndexedViewableAlpha GetViewable(bool applymodifiers) { return GetViewable(null, applymodifiers); }
        public IndexedViewableAlpha GetViewable() { return GetViewable(null, true); }
        public IndexedViewableAlpha GetViewable(Vec3f[] match) { return GetViewable(match, true); }
        public IndexedViewableAlpha GetViewable(Vec3f[] match, bool applymodifiers)
        {
            //if( !objvisible ) return null;

            Vec3f[] verts = null;
            int[] vertuids = null;
            bool[] vsel = null;
            int[][] visiblegroups = null;
            Vec4f[][] colors = { new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts] };
            float[] pointsizes = (this.objedit ? pointsizes_edit : pointsizes_view).DeepCopy();
            float[] linewidths = (this.objedit ? linewidths_edit : (this.objselected ? linewidths_selected : linewidths_unselected)).DeepCopy();
            IndexedViewableAlpha viewable = null;

            //Timer.PrintTimeToExecute( "coloring", delegate {
            for (int i = 0; i < nverts; i++)
            {
                if (!objvisible)
                {
                    colors[0][i] = colorobjhidden;
                    colors[1][i] = colorobjhidden;
                    colors[2][i] = colorobjhidden;
                    colors[3][i] = colorobjhidden;
                }
                else if (objedit)
                {
                    colors[0][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[1][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
                else if (objselected)
                {
                    if (objactive)
                    {
                        colors[0][i] = colorobjactive;
                        colors[1][i] = colorobjactive;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                    else {
                        colors[0][i] = colorobjselected;
                        colors[1][i] = colorobjselected;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                }
                else {
                    colors[0][i] = colorobjunselected;
                    colors[1][i] = colorobjunselected;
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
            }
            //} );

            //Timer.PrintTimeToExecute( "visgrps", delegate {
            List<GroupInfo>[] groups = GetGroups();
            List<int>[] lstvisiblegroups = new List<int>[] { new List<int>(groups[0].Count), new List<int>(groups[1].Count * 2), new List<int>(groups[2].Count * 3), new List<int>(groups[3].Count * 4) };
            for (int igroups = 0; igroups < 4; igroups++)
            {
                List<int> lstcurrent = new List<int>();
                foreach (GroupInfo grp in groups[igroups]) //if( grp.visible )
                    lstcurrent.AddRange(grp.inds);
                lstvisiblegroups[igroups] = lstcurrent;
            }
            visiblegroups = new int[][] { lstvisiblegroups[0].ToArray(), lstvisiblegroups[1].ToArray(), lstvisiblegroups[2].ToArray(), lstvisiblegroups[3].ToArray() };
            //} );

            //Timer.PrintTimeToExecute( "cloning", delegate {
            verts = GetVerts().CloneArray();
            vertuids = GetVertUIDs().CloneArray();
            if (objedit) vsel = isvertselected.CloneArray();
            else vsel = new bool[verts.Length];
            //} );

            //Timer.PrintTimeToExecute( "creating", delegate {
            viewable = new IndexedViewableAlpha(verts, colors, visiblegroups, pointsizes, linewidths, groupsizes.CloneArray(), vertuids, vsel);
            //} );

            if (match != null) //Timer.PrintTimeToExecute( "matching", delegate {
                viewable = history.MakeVertsConsistent(viewable, match);
            //} );

            if (applymodifiers) //Timer.PrintTimeToExecute( "mod", delegate {
                foreach (Modifier m in modifiers) if (m is ModifierMirror)
                    {
                        ModifierMirror mirror = (ModifierMirror)m;
                        viewable += viewable.CreateMirrorData_Each(mirror.usex, mirror.usey, mirror.usez, mirror.mergethreshold);
                    }
            //} );

            return viewable;
        }

        public static IndexedViewableAlpha MeshDiff(SnapshotModel model0, SnapshotModel model1, Vec3f[] match, bool applymods)
        {
            int nverts = model0.nverts;
            Vec3f[] verts = null;
            int[] vertuids = null;
            bool[] vsel = null;
            int[][] visiblegroups = null;
            Vec4f[][] colors = { new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts] };
            float[] pointsizes = (model0.objedit ? pointsizes_edit : pointsizes_view).DeepCopy();
            float[] linewidths = (model0.objedit ? linewidths_edit : (model0.objselected ? linewidths_selected : linewidths_unselected)).DeepCopy();
            IndexedViewableAlpha viewable = null;


            for (int i = 0; i < nverts; i++)
            {
                if (!model0.objvisible)
                {
                    colors[0][i] = colorobjhidden;
                    colors[1][i] = colorobjhidden;
                    colors[2][i] = colorobjhidden;
                    colors[3][i] = colorobjhidden;
                }
                else if (model0.objedit)
                {
                    colors[0][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[1][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
                else if (model0.objselected)
                {
                    if (model0.objactive)
                    {
                        colors[0][i] = colorobjactive;
                        colors[1][i] = colorobjactive;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                    else {
                        colors[0][i] = colorobjselected;
                        colors[1][i] = colorobjselected;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                }
                else {
                    colors[0][i] = colorobjunselected;
                    colors[1][i] = colorobjunselected;
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
            }

            List<GroupInfo>[] groups0 = model0.GetGroups();
            List<GroupInfo>[] groups1 = model1.GetGroups();
            List<int>[] lstvisiblegroups = new List<int>[] {
                new List<int>( groups0[0].Count ),
                new List<int>( groups0[1].Count*2 ),
                new List<int>( groups0[2].Count*3 ),
                new List<int>( groups0[3].Count*4 )
            };
            int[] uids0 = model0.GetVertUIDs();
            int[] uids1 = model1.GetVertUIDs();

            for (int igroups = 0; igroups < 4; igroups++)
            {
                List<int> lstcurrent = lstvisiblegroups[igroups];

                int i0 = 0;
                int i1 = 0;
                List<GroupInfo> grps0 = groups0[igroups];
                List<GroupInfo> grps1 = groups1[igroups];
                int c0 = grps0.Count;
                int c1 = grps1.Count;

                while (i0 < c0)
                {
                    GroupInfo g0 = grps0[i0];
                    if (i1 < c1)
                    {
                        GroupInfo g1 = grps1[i1];
                        string k0 = g0.GetKeyNoVis(uids0);
                        string k1 = g1.GetKeyNoVis(uids1);

                        int comp = k0.CompareTo(k1);
                        //System.Console.WriteLine( k0 + "  " + comp + "  " + k1 );

                        if (comp == 0) { i0++; i1++; continue; }
                        if (comp == 1) { i1++; continue; }
                    }
                    lstcurrent.AddRange(g0.inds);
                    i0++;
                }
            }
            visiblegroups = new int[][] { lstvisiblegroups[0].ToArray(), lstvisiblegroups[1].ToArray(), lstvisiblegroups[2].ToArray(), lstvisiblegroups[3].ToArray() };

            verts = model0.GetVerts().CloneArray();
            vertuids = model0.GetVertUIDs().CloneArray();
            if (model0.objedit) vsel = model0.isvertselected.CloneArray();
            else vsel = new bool[nverts];



            viewable = new IndexedViewableAlpha(verts, colors, visiblegroups, pointsizes, linewidths, groupsizes.CloneArray(), vertuids, vsel);

            if (match != null) viewable = history.MakeVertsConsistent(viewable, match);

            if (applymods)
            {
                foreach (Modifier m in model0.modifiers) if (m is ModifierMirror)
                    {
                        ModifierMirror mirror = (ModifierMirror)m;
                        viewable += viewable.CreateMirrorData_Each(mirror.usex, mirror.usey, mirror.usez, mirror.mergethreshold);
                    }
            }

            return viewable;
        }

        public static IndexedViewableAlpha MeshIntersect(SnapshotModel model0, SnapshotModel model1, Vec3f[] match, bool applymods)
        {
            int nverts = model0.nverts;
            Vec3f[] verts = null;
            int[] vertuids = null;
            bool[] vsel = null;
            int[][] visiblegroups = null;
            Vec4f[][] colors = { new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts], new Vec4f[nverts] };
            float[] pointsizes = (model0.objedit ? pointsizes_edit : pointsizes_view).DeepCopy();
            float[] linewidths = (model0.objedit ? linewidths_edit : (model0.objselected ? linewidths_selected : linewidths_unselected)).DeepCopy();
            IndexedViewableAlpha viewable = null;


            for (int i = 0; i < nverts; i++)
            {
                if (!model0.objvisible)
                {
                    colors[0][i] = colorobjhidden;
                    colors[1][i] = colorobjhidden;
                    colors[2][i] = colorobjhidden;
                    colors[3][i] = colorobjhidden;
                }
                else if (model0.objedit)
                {
                    colors[0][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[1][i] = colorvertunselected; //( isvertselected[i] ? colorvertselected : colorvertunselected );
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
                else if (model0.objselected)
                {
                    if (model0.objactive)
                    {
                        colors[0][i] = colorobjactive;
                        colors[1][i] = colorobjactive;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                    else {
                        colors[0][i] = colorobjselected;
                        colors[1][i] = colorobjselected;
                        colors[2][i] = colorface;
                        colors[3][i] = colorface;
                    }
                }
                else {
                    colors[0][i] = colorobjunselected;
                    colors[1][i] = colorobjunselected;
                    colors[2][i] = colorface;
                    colors[3][i] = colorface;
                }
            }

            List<GroupInfo>[] groups0 = model0.GetGroups();
            List<GroupInfo>[] groups1 = model1.GetGroups();
            List<int>[] lstvisiblegroups = new List<int>[] {
                new List<int>( groups0[0].Count ),
                new List<int>( groups0[1].Count*2 ),
                new List<int>( groups0[2].Count*3 ),
                new List<int>( groups0[3].Count*4 )
            };
            int[] uids0 = model0.GetVertUIDs();
            int[] uids1 = model1.GetVertUIDs();

            for (int igroups = 0; igroups < 4; igroups++)
            {
                List<int> lstcurrent = lstvisiblegroups[igroups];

                int i0 = 0;
                int i1 = 0;
                List<GroupInfo> grps0 = groups0[igroups];
                List<GroupInfo> grps1 = groups1[igroups];
                int c0 = grps0.Count;
                int c1 = grps1.Count;

                while (i0 < c0 && i1 < c1)
                {
                    GroupInfo g0 = grps0[i0];
                    GroupInfo g1 = grps1[i1];
                    string k0 = g0.GetKeyNoVis(uids0);
                    string k1 = g1.GetKeyNoVis(uids1);

                    int comp = k0.CompareTo(k1);
                    if (comp == 1) { i1++; continue; }
                    if (comp == -1) { i0++; continue; }
                    lstcurrent.AddRange(g0.inds);
                    i0++;
                    i1++;
                }
            }
            visiblegroups = new int[][] { lstvisiblegroups[0].ToArray(), lstvisiblegroups[1].ToArray(), lstvisiblegroups[2].ToArray(), lstvisiblegroups[3].ToArray() };

            verts = model0.GetVerts().CloneArray();
            vertuids = model0.GetVertUIDs().CloneArray();
            if (model0.objedit) vsel = model0.isvertselected.CloneArray();
            else vsel = new bool[nverts];



            viewable = new IndexedViewableAlpha(verts, colors, visiblegroups, pointsizes, linewidths, groupsizes.CloneArray(), vertuids, vsel);

            if (match != null) viewable = history.MakeVertsConsistent(viewable, match);

            if (applymods)
            {
                foreach (Modifier m in model0.modifiers) if (m is ModifierMirror)
                    {
                        ModifierMirror mirror = (ModifierMirror)m;
                        viewable += viewable.CreateMirrorData_Each(mirror.usex, mirror.usey, mirror.usez, mirror.mergethreshold);
                    }
            }

            return viewable;
        }

        public IndexedViewableAlpha GetSelection(Vec3f[] match, bool applymods) { return GetSelection(match, applymods, false); }
        public IndexedViewableAlpha GetSelection(Vec3f[] match, bool applymods, bool allselected)
        {
            Vec3f[] verts = GetVerts();
            int[] vertuids = GetVertUIDs();
            List<GroupInfo>[] groups = GetGroups();
            Vec3f[] norms = GetVertNormals();

            int newnverts = nverts * 2;

            Vec3f[] newverts = new Vec3f[newnverts];
            int[] newvuids = new int[newnverts];
            Vec4f colface = new Vec4f(colorvertselected.x * 0.75f, colorvertselected.y * 0.75f, colorvertselected.z * 0.75f, 1.0f);
            Vec4f[][] newcolors = new Vec4f[][] {
                Enumerable.Repeat( colorvertselected, nverts * 2 ).ToArray(),
                Enumerable.Repeat( colorvertselected, nverts * 2 ).ToArray(),
                Enumerable.Repeat( colface, nverts * 2 ).ToArray(),
                Enumerable.Repeat( colface, nverts * 2 ).ToArray(),
            };
            List<int>[] newgroups = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            bool[] newsel = new bool[newnverts];
            float[] newptsizes = { 10.0f, 0.1f, 0.1f, 0.1f };
            float[] newlnwidths = { 0.1f, 2.0f, 0.1f, 0.1f };

            for (int i = 0; i < nverts; i++)
            {
                newverts[i * 2 + 0] = verts[i] + norms[i] * 2.0f;
                newverts[i * 2 + 1] = verts[i] - norms[i] * 2.0f;
                newvuids[i * 2 + 0] = vertuids[i];
                newvuids[i * 2 + 1] = vertuids[i];
            }
            for (int igroups = 0; igroups < 4; igroups++)
            {
                List<GroupInfo> cgroups = groups[igroups];

                foreach (GroupInfo grp in cgroups)
                {
                    if (!allselected)
                    {
                        bool sel = true;
                        foreach (int ind in grp.inds) sel &= isvertselected[ind];
                        if (!sel) continue;
                    }
                    foreach (int ind in grp.inds) newgroups[igroups].Add(ind * 2 + 0);
                    foreach (int ind in grp.inds) newgroups[igroups].Add(ind * 2 + 1);
                }
            }

            int[][] newagroups = new int[][] { newgroups[0].ToArray(), newgroups[1].ToArray(), newgroups[2].ToArray(), newgroups[3].ToArray() };

            IndexedViewableAlpha viewable = new IndexedViewableAlpha(newverts, newcolors, newagroups, newptsizes, newlnwidths, groupsizes.CloneArray(), newvuids, newsel);

            if (match != null) //Timer.PrintTimeToExecute( "matching", delegate {
                viewable = history.MakeVertsConsistent(viewable, match);
            //} );

            if (applymods) //Timer.PrintTimeToExecute( "mod", delegate {
                foreach (Modifier m in modifiers) if (m is ModifierMirror)
                    {
                        ModifierMirror mirror = (ModifierMirror)m;
                        viewable += viewable.CreateMirrorData_Each(mirror.usex, mirror.usey, mirror.usez, mirror.mergethreshold);
                    }
            //} );

            return viewable;
        }

    }
}


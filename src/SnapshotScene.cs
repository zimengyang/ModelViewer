using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using Common.Libs.MiscFunctions;
using Common.Libs.VMath;

namespace MeshFlowViewer
{
    public enum ApplicationTypes
    {
        UNKNOWN, BLENDER
    }

    public class SnapshotScene : IBinaryConvertible
    {
        public ApplicationTypes ApplicationType = ApplicationTypes.UNKNOWN;

        private SnapshotModel[] models;
        private SnapshotModel[] modelscached = null;
        public CameraProperties[] cameras;
        public SnapshotScene prevscene;

        public int cselected;
        public int cedited;

        public int nmodels;
        public int ncameras;

        public bool nochange;

        public int timeindex;
        public string file;
        public string command;
        public string opts;

        #region Constructors

        public SnapshotScene() { }

        public SnapshotScene(string sPLYFilename, int timeindex, string command, string opts, SnapshotScene prev, bool nochange, bool cmdobjlist)
        {
            this.file = MiscFileIO.GetFileNameOnly(sPLYFilename);
            this.timeindex = timeindex;
            this.command = command;
            this.opts = opts;
            this.prevscene = prev;

            cselected = 0;
            cedited = 0;

            string[] objnames;
            bool[] objvisibles;
            bool[] objselecteds;
            bool[] objactives;
            bool[] objedits;
            string[] objplyfilenames;

            using (Stream s = new FileStream(sPLYFilename, FileMode.Open))
            {
                string plyline = FileIOFunctions.ReadTextString(s);
                if (plyline != "ply") throw new ArgumentException("SnapshotScene: Specified file is not .ply file");

                ncameras = 0;
                nmodels = 0;
                bool header = true;
                while (header)
                {
                    string cmd = FileIOFunctions.ReadTextString(s);
                    switch (cmd)
                    {
                        case "format":
                        case "property":
                            while (s.ReadByte() != 10) ; // ignore the rest of the line
                            break;

                        case "comment":
                            string str = FileIOFunctions.ReadTextLine(s);
                            if (str.StartsWith("Created"))
                            {
                                switch (str.Split(new char[] { ' ' })[2])
                                {
                                    case "Blender": ApplicationType = ApplicationTypes.BLENDER; break;
                                }
                            }
                            break;

                        case "element":
                            string variable = FileIOFunctions.ReadTextString(s);
                            int val = FileIOFunctions.ReadTextInteger(s);
                            switch (variable)
                            {
                                case "views": ncameras = val; break;
                                case "objects": nmodels = val; break;
                                default: throw new Exception("SnapshotScene: Unhandled element type " + variable);
                            }
                            break;

                        case "end_header": header = false; break;

                        default: throw new Exception("SnapshotScene: Unhandled command type " + cmd);

                    }
                }

                if (ApplicationType == ApplicationTypes.UNKNOWN) throw new Exception("SnapshotScene: PLY was created by an unknown application");

                cameras = new CameraProperties[ncameras];
                for (int i = 0; i < ncameras; i++)
                {
                    // read viewing info
                    Vec3f loc = new Vec3f(FileIOFunctions.ReadTextFloat(s), FileIOFunctions.ReadTextFloat(s), FileIOFunctions.ReadTextFloat(s));
                    float w = FileIOFunctions.ReadTextFloat(s);
                    float x = FileIOFunctions.ReadTextFloat(s);
                    float y = FileIOFunctions.ReadTextFloat(s);
                    float z = FileIOFunctions.ReadTextFloat(s);
                    Quatf qua = (new Quatf(w, x, y, z)).Normalize();
                    float dis = FileIOFunctions.ReadTextFloat(s);
                    String per = FileIOFunctions.ReadTextString(s);
                    cameras[i] = new CameraProperties(loc, qua, dis, (per == "ORTHO"));
                }

                int istart = 0, iend = 0, iinc = 0;
                switch (ApplicationType)
                {

                    case ApplicationTypes.BLENDER:          // blender writes list of objects "backwards"; new objects are at beginning of list!
                        istart = nmodels - 1;
                        iend = 0;
                        iinc = -1;
                        break;

                    default: throw new Exception("SnapshotScene: Unimplemented ApplicationType");

                }

                objnames = new string[nmodels];
                objvisibles = new bool[nmodels];
                objselecteds = new bool[nmodels];
                objactives = new bool[nmodels];
                objedits = new bool[nmodels];
                objplyfilenames = new string[nmodels];

                for (int i = istart; i != iend + iinc; i += iinc)
                {
                    objnames[i] = FileIOFunctions.ReadTextQuotedString(s);
                    objvisibles[i] = (FileIOFunctions.ReadTextInteger(s) == 1);
                    objselecteds[i] = (FileIOFunctions.ReadTextInteger(s) == 1);
                    objactives[i] = (FileIOFunctions.ReadTextInteger(s) == 1);
                    objedits[i] = (FileIOFunctions.ReadTextInteger(s) == 1);
                    objplyfilenames[i] = FileIOFunctions.ReadTextString(s);

                    if (objselecteds[i]) cselected++;
                    if (objedits[i]) cedited++;
                }
            }

            if (cedited > 1) throw new Exception("more than one object being edited");

            bool loadall = (prev == null || cmdobjlist);                // need to load every object?

            models = new SnapshotModel[nmodels];
            modelscached = new SnapshotModel[nmodels];
            SnapshotModel[] pmodels = null;
            if (prev != null) pmodels = prev.Models;
            for (int i = 0; i < nmodels; i++)
            {
                bool prevsel = !cmdobjlist && (pmodels != null && pmodels[i] != null && pmodels[i].objselected);
                if (loadall || (objselecteds[i] && !nochange) || objselecteds[i] != prevsel || pmodels == null || pmodels[i] == null)
                {
                    models[i] = new SnapshotModel(objplyfilenames[i]);
                    models[i].objind = i;
                    models[i].objname = objnames[i];
                    models[i].objvisible = objvisibles[i];
                    models[i].objselected = objselecteds[i];
                    models[i].objactive = objactives[i];
                    models[i].objedit = objedits[i];
                    modelscached[i] = models[i];
                }
                else {
                    models[i] = null;
                    modelscached[i] = pmodels[i];
                }
            }
        }

        #endregion

        #region Serialization Functions

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    //bw.WriteEnum( ApplicationType );
        //    bw.WriteArray(models);
        //    bw.WriteArray(cameras);
        //    bw.WriteParams(nmodels, ncameras, timeindex);
        //    bw.WriteParams(file, command, opts);
        //    bw.WriteT(nochange);
        //}

        public void ReadBinary(BinaryReader br)
        {
            //br.ReadEnum( out ApplicationType );
            br.ReadArray(out models);
            br.ReadArray(out cameras);
            br.Read(out nmodels);
            br.Read(out ncameras);
            br.Read(out timeindex);
            br.Read(out file);
            br.Read(out command);
            br.Read(out opts);
            br.Read(out nochange);
        }

        public static SnapshotScene ReadBinaryFile(BinaryReader br)
        {
            SnapshotScene scene = new SnapshotScene();
            scene.ReadBinary(br);
            return scene;
        }

        #endregion

        #region Properties

        public SnapshotModel[] Models
        {
            get
            {
                if (prevscene == null) modelscached = models;
                if (modelscached == null)
                {
                    SnapshotModel[] pmodels = prevscene.Models;
                    modelscached = new SnapshotModel[nmodels];
                    for (int i = 0; i < nmodels; i++)
                    {
                        if (models[i] == null) modelscached[i] = pmodels[i];
                        else modelscached[i] = models[i];
                    }
                }
                return modelscached;
            }
        }

        public SnapshotModel[] Models_unfilled
        {
            get { return models; }
        }

        public int[] ObjectUIDs
        {
            get { return Models.Select(model => model.objuid).ToArray(); }
            set { value.Each(delegate (int uid, int i) { Models[i].objuid = uid; }); }
        }

        public bool[] ObjectSelected
        {
            get { return Models.Select(model => model.objselected).ToArray(); }
        }

        public SnapshotModel[] SelectedObjects
        {
            get { return Models.Where(model => model.objselected).ToArray(); }
        }

        public SnapshotModel[] VisibleObjects
        {
            get { return Models.Where(model => model.objvisible).ToArray(); }
        }

        #endregion

        #region Getters

        public string GetLabel() { return String.Format("{0:d4}:{1:s}", timeindex, command); }

        public CameraProperties GetCamera()
        {
            if (cameras.Length > 1) return cameras[1];
            return cameras[0];
        }

        public SnapshotModel GetObjectFromUID(int uid)
        {
            foreach (SnapshotModel model in Models) if (model.objuid == uid) return model;
            return null;
        }

        public bool InEditMode() { return (GetEditModel() != null); }

        public SnapshotModel GetEditModel()
        {
            foreach (SnapshotModel model in Models) if (model.objedit) return model;
            return null;
        }

        public SnapshotModel[] GetSelectedModels()
        {
            return Models.Where((SnapshotModel model) => model.objselected).ToArray();
        }

        #endregion

        public void SetPrevScene(SnapshotScene prev)
        {
            this.prevscene = prev;
            foreach (SnapshotModel model in models)
                if (model != null) model.prev = prev.GetObjectFromUID(model.objuid);
        }

        public void ReorderGroups()
        {
            foreach (SnapshotModel model in models)
                if (model != null) model.ReorderGroups();
        }

        public bool SuperficialComparison(SnapshotScene other)
        {
            if (other.nmodels != nmodels) return false;
            SnapshotModel[] lmodels = Models;
            SnapshotModel[] omodels = other.Models;
            for (int i = 0; i < nmodels; i++)
            {
                if (omodels[i].nverts != lmodels[i].nverts) return false;
                for (int ig = 0; ig < 4; ig++)
                    if (omodels[i].GetGroups()[ig].Count != lmodels[i].GetGroups()[ig].Count) return false; //.groups
                if (omodels[i].selinds.Where((int ind) => !lmodels[i].selinds.Contains(ind)).Count() > 0) return false;
                if (lmodels[i].selinds.Where((int ind) => !omodels[i].selinds.Contains(ind)).Count() > 0) return false;
            }
            return true;
        }

        public IndexedViewableAlpha GetViewables() { return GetViewables(null, true); }
        public IndexedViewableAlpha GetViewables(bool applymodifiers) { return GetViewables(null, applymodifiers); }
        public IndexedViewableAlpha GetViewables(Vec3f[] match) { return GetViewables(match, true); }
        public IndexedViewableAlpha GetViewables(Vec3f[] match, bool applymodifiers)
        {
            IndexedViewableAlpha viewable = null;
            foreach (SnapshotModel model in Models)
            {
                IndexedViewableAlpha viewadd = null;
                //Timer.PrintTimeToExecute( "getviewable", delegate {
                viewadd = model.GetViewable(match, applymodifiers);
                //} );
                //Timer.PrintTimeToExecute( "adding", delegate {
                viewable = IndexedViewableAlpha.CombineFat(viewable, viewadd);
                //viewable += viewadd;
                //} );
            }
            return viewable;
        }

        public IndexedViewableAlpha GetViewablesAttached(Vec3f[] match, bool applymods)
        {
            IndexedViewableAlpha viewable = null;
            foreach (SnapshotModel model in Models)
            {
                viewable = IndexedViewableAlpha.Attach(viewable, model.GetViewable(match, applymods));
            }
            return viewable;
        }

        public IndexedViewableAlpha GetSelection(Vec3f[] match, bool applymodifiers)
        {
            SnapshotModel modeledit = GetEditModel();

            if (modeledit != null) return modeledit.GetSelection(match, applymodifiers);

            IndexedViewableAlpha viewable = null;
            foreach (SnapshotModel model in Models)
            {
                if (!model.objselected || !model.objvisible) continue;
                IndexedViewableAlpha nview = model.GetSelection(match, applymodifiers, true);
                viewable = IndexedViewableAlpha.Attach(viewable, nview);
            }
            return viewable;

            /*IndexedViewableAlpha viewable = null;
			foreach( SnapshotModel model in Models )
			{
				IndexedViewableAlpha viewadd = model.GetSelection( match, applymodifiers );
				viewable = IndexedViewableAlpha.CombineFat( viewable, viewadd );
			}
			return viewable;*/
        }

        public static IndexedViewableAlpha MeshDiff(SnapshotScene snapshot0, SnapshotScene snapshot1, Vec3f[] verts, bool applymods)
        {
            SnapshotModel[] models0 = snapshot0.Models;
            SnapshotModel[] models1 = snapshot1.Models;

            IndexedViewableAlpha viewable = null;

            int i;
            int c = models1.Length;
            foreach (SnapshotModel model0 in models0)
            {
                int uid = model0.objuid;
                for (i = 0; i < c; i++) if (models1[i].objuid == uid) break;
                if (i == c) { viewable += model0.GetViewable(verts, applymods); continue; }
                SnapshotModel model1 = models1[i];

                if (model0.GetEditCount() == model1.GetEditCount()) continue;
                IndexedViewableAlpha diff = SnapshotModel.MeshDiff(model0, model1, verts, applymods);
                viewable += diff;

                //IndexedViewableAlpha view0 = model0.GetViewable( verts, applymods );
                //IndexedViewableAlpha view1 = model1.GetViewable( verts, applymods );
                //viewable += view0 - view1;
            }

            return viewable;
        }

        public static IndexedViewableAlpha MeshDiffAttach(SnapshotScene snapshot0, SnapshotScene snapshot1, Vec3f[] verts, bool applymods)
        {
            SnapshotModel[] models0 = snapshot0.Models;
            SnapshotModel[] models1 = snapshot1.Models;

            IndexedViewableAlpha viewable = null;

            int i;
            int c = models1.Length;
            foreach (SnapshotModel model0 in models0)
            {
                int uid = model0.objuid;
                for (i = 0; i < c; i++) if (models1[i].objuid == uid) break;
                if (i == c)
                {
                    //viewable += model0.GetViewable( verts, applymods );
                    viewable = IndexedViewableAlpha.Attach(viewable, model0.GetViewable(verts, applymods));
                    continue;
                }
                SnapshotModel model1 = models1[i];

                if (model0.GetEditCount() == model1.GetEditCount()) continue;
                IndexedViewableAlpha diff = SnapshotModel.MeshDiff(model0, model1, verts, applymods);
                //viewable += diff;
                viewable = IndexedViewableAlpha.Attach(viewable, diff);
            }

            return viewable;
        }

        public static IndexedViewableAlpha MeshIntersect(SnapshotScene snapshot0, SnapshotScene snapshot1, Vec3f[] verts, bool applymods)
        {
            SnapshotModel[] models0 = snapshot0.Models;
            SnapshotModel[] models1 = snapshot1.Models;

            IndexedViewableAlpha viewable = null;

            int i;
            int c = models1.Length;
            foreach (SnapshotModel model0 in models0)
            {
                int uid = model0.objuid;
                for (i = 0; i < c; i++) if (models1[i].objuid == uid) break;
                if (i == c) continue;
                SnapshotModel model1 = models1[i];

                if (model0.GetEditCount() == model1.GetEditCount())
                {
                    viewable += model0.GetViewable(verts, applymods);
                }
                else {
                    viewable += SnapshotModel.MeshIntersect(model0, model1, verts, applymods);
                }
            }

            return viewable;
        }

        public static IndexedViewableAlpha MeshIntersectAttach(SnapshotScene snapshot0, SnapshotScene snapshot1, Vec3f[] verts, bool applymods)
        {
            SnapshotModel[] models0 = snapshot0.Models;
            SnapshotModel[] models1 = snapshot1.Models;

            IndexedViewableAlpha viewable = null;

            int i;
            int c = models1.Length;
            foreach (SnapshotModel model0 in models0)
            {
                int uid = model0.objuid;
                for (i = 0; i < c; i++) if (models1[i].objuid == uid) break;
                if (i == c) continue;
                SnapshotModel model1 = models1[i];

                if (model0.GetEditCount() == model1.GetEditCount())
                {
                    viewable = IndexedViewableAlpha.Attach(viewable, model0.GetViewable(verts, applymods));
                }
                else {
                    viewable = IndexedViewableAlpha.Attach(viewable, SnapshotModel.MeshIntersect(model0, model1, verts, applymods));
                }
            }

            return viewable;
        }

    }
}


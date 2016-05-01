using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

using Common.Libs.MatrixMath;
using Common.Libs.VMath;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public class Cluster : IBinaryConvertible
    {
        public int start { get; private set; }
        public int duration { get; private set; }
        public int end { get; private set; }

        public string name { get; set; }
        public string annotation { get; set; }
        public SolidBrush brush { get; set; }

        public Composition composition { get; set; }

        public List<int> snapshots { get; set; }

        public IndexedViewableAlpha viewable = null;
        public bool viewable_sel = false;
        public bool viewable_ann = false;
        public bool viewable_rem = false;

        //public IndexedViewableAlpha[] viewables;

        public delegate void ChangeHandler();
        public event ChangeHandler Changed;
        public void FireChangeHandler() { if (Changed != null) Changed(); }

        public Cluster()
            : this(0, -1, "", new Composition(CompositionPresets.Default))
        { }

        /*public Cluster( int start, int end, string name )
			: this( start, end, name, Composition.GetCompositionByOperation( name ) )
		{ }*/

        public Cluster(int start, int end, string name, Composition composition)
            : this(start, end, name, composition, Enumerable.Range(start, end - start + 1).ToList())
        { }

        public Cluster(int start, int end, string name, Composition composition, int[] snapshots)
            : this(start, end, name, composition, snapshots.ToList())
        { }

        public Cluster(int start, int end, string name, Composition composition, List<int> snapshots)
        {
            // sanity check
            if (end < start) throw new ArgumentException("Duration must be positive");

            this.name = name;
            this.start = start;
            this.end = end;
            this.duration = end - start + 1;
            this.snapshots = snapshots;
            this.composition = new Composition(composition);
            this.brush = new SolidBrush(Color.White);
        }

        public Cluster(Cluster copy) : this()
        {
            this.name = (string)copy.name.Clone();
            this.annotation = (string)copy.annotation.Clone();
            this.start = copy.start;
            this.end = copy.end;
            this.duration = copy.duration;
            this.snapshots = new List<int>(copy.snapshots);
            this.brush = (SolidBrush)copy.brush.Clone();
            this.composition.SetToComposition(copy.composition);
        }

        public bool Index0Within(int index0) { return (index0 >= start && index0 <= end); }

        public string[] GetTTags()
        {
            ModelingHistory hist = ModelingHistory.history;
            HashSet<string> tags = new HashSet<string>();
            foreach (int i0 in snapshots) foreach (string tag in hist.GetTTags(i0)) tags.Add(tag);
            return tags.ToArray();
        }

        public string GetOTag() { return name; }

        public CameraProperties[] GetCameras()
        {
            ModelingHistory history = ModelingHistory.history;

            CameraProperties artist = null;
            CameraProperties bestview = null;

            Vec3f tar = new Vec3f();
            Quatf rot = new Quatf();
            float dist = 0.0f;
            float ortho = 0.0f;

            List<Vec3f> selverts = new List<Vec3f>();
            Vec3f anorm = new Vec3f();

            foreach (int isnapshot in snapshots)
            {
                SnapshotScene scene = history[isnapshot];
                CameraProperties cam = scene.GetCamera();

                tar += cam.GetTarget();
                rot += cam.GetRotation();
                dist += cam.GetDistance();
                ortho += (cam.GetOrtho() ? 1.0f : 0.0f);

                foreach (SnapshotModel model in scene.GetSelectedModels())
                {
                    Vec3f[] verts = model.GetVerts();
                    Vec3f[] vnorms = model.GetVertNormals();
                    foreach (int ind in model.selinds) { selverts.Add(verts[ind]); anorm += vnorms[ind]; }
                }
            }

            int nsnapshots = snapshots.Count;
            if (nsnapshots == 0)
            {
                rot = new Quatf(0.5f, -0.5f, -0.5f, -0.5f);
                dist = 10.0f;
                System.Console.WriteLine("Cluster with no snapshots " + start + ":" + end);
            }
            else {
                tar /= (float)nsnapshots;
                rot /= (float)nsnapshots;
                dist /= (float)nsnapshots;
                ortho /= (float)nsnapshots;
            }

            artist = new CameraProperties(tar, rot, dist, (ortho >= 0.5f)) { Name = "Artist" };

            bestview = artist;

            return new CameraProperties[] { artist, bestview };
        }

        #region Binary Writing / Reading Functions

        //public void WriteBinary(BinaryWriter bw)
        //{
        //    bw.Write(start);
        //    bw.Write(end);
        //    bw.Write(name);
        //    bw.WriteT(brush);
        //    bw.Write(annotation);
        //    bw.WriteT(composition);
        //    bw.WriteT(snapshots);
        //}

        public void ReadBinary(BinaryReader br)
        {
            Composition comp;
            start = br.ReadInt32();
            end = br.ReadInt32();
            name = br.ReadString();
            brush = br.ReadSolidBrush();
            annotation = br.ReadString();
            br.Read(out comp);
            composition.SetToComposition(comp);
            snapshots = br.ReadList<int>();

            duration = end - start + 1;
        }

        #endregion

    }
}


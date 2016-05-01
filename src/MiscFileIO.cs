using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using System.Windows.Forms;
using Common.Libs.MatrixMath;
using Common.Libs.VMath;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public interface IBinaryConvertible
    {
        //void WriteBinary(BinaryWriter bw);
        void ReadBinary(BinaryReader br);
    }

    public static class MiscFileIO
    {
        // ugly casting requiring (un)boxing, but it'll do the job until .NET gives a better option

        #region BinaryWriter and BinaryReader Generic Extensions

        //public static void WriteT<T>(this BinaryWriter bw, T v)
        //{
        //    if (typeof(T) == typeof(int) || typeof(T) == typeof(Int32)) bw.Write((int)(object)v);
        //    else if (typeof(T).IsEnum) bw.Write(v.ToString());
        //    else if (typeof(T) == typeof(bool)) bw.Write((bool)(object)v);
        //    else if (typeof(T) == typeof(Vec3f)) { Vec3f vec = (Vec3f)(object)v; bw.WriteParams(vec.x, vec.y, vec.z); }
        //    else if (typeof(T) == typeof(Quatf)) { Quatf q = (Quatf)(object)v; bw.Write(q.Scalar); bw.WriteT(q.Vector); }
        //    else if (typeof(T) == typeof(float)) bw.Write((float)(object)v);
        //    else if (typeof(T) == typeof(double)) bw.Write((double)(object)v);
        //    else if (typeof(T) == typeof(string)) bw.Write((string)(object)v);
        //    else if (typeof(T) == typeof(GroupInfo)) { GroupInfo g = (GroupInfo)(object)v; bw.WriteArray(g.inds); bw.Write(g.visible); }
        //    else if (typeof(T) == typeof(SolidBrush)) { SolidBrush sb = (SolidBrush)(object)v; bw.WriteParams((int)sb.Color.A, (int)sb.Color.R, (int)sb.Color.G, (int)sb.Color.B); }
        //    else if (typeof(T).GetInterfaces().Contains(typeof(IBinaryConvertible)))
        //    {
        //        bool nnull = (v != null);
        //        bw.Write(nnull);
        //        if (!nnull) return;

        //        // handles only one level!!!!
        //        var subclasses = typeof(T).GetDerivedTypes();
        //        bool found = false;
        //        foreach (Type subclass in subclasses)
        //        {
        //            if (subclass.IsInstanceOfType(v))
        //            {
        //                bw.Write(subclass.FullName);
        //                found = true;
        //                break;
        //            }
        //        }
        //        if (!found) bw.Write(typeof(T).FullName);  // not good!
        //        ((IBinaryConvertible)v).WriteBinary(bw);
        //    }
        //    else throw new ArgumentException("Unhandled type: " + typeof(T));
        //}

        public static void Read<T>(this BinaryReader br, out T v)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(Int32)) v = (T)(object)br.ReadInt32();
            else if (typeof(T).IsEnum) v = (T)(object)Enum.Parse(typeof(T), br.ReadString());
            else if (typeof(T) == typeof(bool)) v = (T)(object)br.ReadBoolean();
            else if (typeof(T) == typeof(Vec3f)) v = (T)(object)new Vec3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            else if (typeof(T) == typeof(Quatf)) v = (T)(object)br.ReadQuatf();
            else if (typeof(T) == typeof(float)) v = (T)(object)br.ReadSingle();
            else if (typeof(T) == typeof(double)) v = (T)(object)br.ReadDouble();
            else if (typeof(T) == typeof(string)) v = (T)(object)br.ReadString();
            else if (typeof(T) == typeof(GroupInfo)) v = (T)(object)new GroupInfo(br.ReadArray<int>(), br.ReadBoolean());
            else if (typeof(T) == typeof(SolidBrush)) v = (T)(object)br.ReadSolidBrush();
            else if (typeof(T).GetInterfaces().Contains(typeof(IBinaryConvertible)))
            {
                bool nnull = br.ReadBoolean();
                if (!nnull) { v = default(T); return; }
                string _name = br.ReadString();
                string name = _name.Split('.').Last();

                //if (name == typeof(T).FullName)
                if(name == typeof(T).Name)
                {
                    v = (T)typeof(T).GetConstructor(new Type[0]).Invoke(new object[0]);
                    ((IBinaryConvertible)v).ReadBinary(br);
                }
                else {
                    var subclasses = typeof(T).GetDerivedTypes();
                    foreach (Type subclass in subclasses)
                    {
                        //if (subclass.FullName == name)
                        if(subclass.Name == name)
                        {
                            v = (T)subclass.GetConstructor(new Type[0]).Invoke(new object[0]);
                            ((IBinaryConvertible)v).ReadBinary(br);
                            return;
                        }
                    }
                    throw new Exception("Could not find propert subclass");
                }
            }
            else throw new ArgumentException("Unhandled type: " + typeof(T));
        }

        #endregion



        #region Various BinaryWriter Extension Functions

        //public static void WriteEnum<T>(this BinaryWriter bw, T v)
        //{
        //    bw.Write(v.ToString());
        //}

        //public static void WriteParams<T>(this BinaryWriter bw, params T[] args)
        //{
        //    foreach (T arg in args) bw.WriteT(arg);
        //}

        //public static void WriteArray<T>(this BinaryWriter bw, T[] vs)
        //{
        //    if (vs == null) { bw.Write(-1); return; }
        //    bw.Write(vs.Length);
        //    foreach (T v in vs) bw.WriteT(v);
        //}

        //public static void WriteJaggedArray<T>(this BinaryWriter bw, T[][] vss)
        //{
        //    if (vss == null) { bw.Write(-1); return; }
        //    bw.Write(vss.Length);
        //    foreach (T[] vs in vss) bw.WriteArray(vs);
        //}

        //public static void WriteList<T>(this BinaryWriter bw, List<T> vs)
        //{
        //    if (vs == null) { bw.Write(-1); return; }
        //    bw.Write(vs.Count);
        //    foreach (T v in vs) bw.WriteT(v);
        //}

        //public static void WriteJaggedList<T>(this BinaryWriter bw, List<T>[] vss)
        //{
        //    if (vss == null) { bw.Write(-1); return; }
        //    bw.Write(vss.Length);
        //    foreach (List<T> vs in vss) bw.WriteList(vs);
        //}

        #endregion



        #region Various BinaryReader Extension Functions

        public static T ReadEnum<T>(this BinaryReader br)
        {
            return (T)Enum.Parse(typeof(T), br.ReadString());
        }
        public static void ReadEnum<T>(this BinaryReader br, out T v) { v = br.ReadEnum<T>(); }

        public static T[] ReadArray<T>(this BinaryReader br)
        {
            int c = br.ReadInt32();
            if (c == -1) return null;
            T[] vs = new T[c];
            for (int i = 0; i < c; i++) br.Read<T>(out vs[i]);
            return vs;
        }
        public static void ReadArray<T>(this BinaryReader br, out T[] vs) { vs = br.ReadArray<T>(); }
        /*public static void ReadArray<T>( this BinaryReader br, out T[] vs, Func<BinaryReader,T> constructor ) { vs = br.ReadArray( constructor ); }
		public static T[] ReadArray<T>( this BinaryReader br, Func<BinaryReader,T> constructor )
		{
			int c = br.ReadInt32();
			if( c == -1 ) return null;
			T[] vs = new T[c];
			for( int i = 0; i < c; i++ ) vs[i] = constructor( br );
			return vs;
		}*/

        public static T[][] ReadJaggedArray<T>(this BinaryReader br)
        {
            int c = br.ReadInt32();
            if (c == -1) return null;
            T[][] vss = new T[c][];
            for (int i = 0; i < c; i++) br.ReadArray(out vss[i]);
            return vss;
        }
        public static void ReadJaggedArray<T>(this BinaryReader br, out T[][] vss) { vss = br.ReadJaggedArray<T>(); }

        public static void ReadList<T>(this BinaryReader br, out List<T> lst) { lst = br.ReadList<T>(); }
        public static List<T> ReadList<T>(this BinaryReader br)
        {
            T[] vsa = ReadArray<T>(br);
            if (vsa == null) return null;
            return new List<T>(vsa);
        }

        public static void ReadList<T>(this BinaryReader br, out List<T> lst, Func<BinaryReader, T> constructor) { lst = br.ReadList(constructor); }
        public static List<T> ReadList<T>(this BinaryReader br, Func<BinaryReader, T> constructor)
        {
            int c = br.ReadInt32();
            if (c == -1) return null;
            List<T> vs = new List<T>(c);
            for (int i = 0; i < c; i++) vs.Add(constructor(br));
            return vs;
        }

        public static List<T>[] ReadJaggedList<T>(this BinaryReader br)
        {
            int c = br.ReadInt32();
            if (c == -1) return null;
            List<T>[] vss = new List<T>[c];
            for (int i = 0; i < c; i++) br.ReadList(out vss[i]);
            return vss;
        }
        public static void ReadJaggedList<T>(this BinaryReader br, out List<T>[] vss) { vss = br.ReadJaggedList<T>(); }

        public static Vec3f ReadVec3f(this BinaryReader br) { return new Vec3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()); }

        public static Quatf ReadQuatf(this BinaryReader br) { return new Quatf(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()); }

        public static SolidBrush ReadSolidBrush(this BinaryReader br) { return new SolidBrush(Color.FromArgb(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32())); }

        public static void ReadProperty<T>(this BinaryReader br, Property<T> p) { T v; br.Read(out v); p.Set(v); }

        #endregion



        #region Serialization Functions

        public static Boolean SaveObjectToBinary(String sFile, Object obj)
        {
            string path = Path.GetDirectoryName(sFile);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            using (FileStream stream = new FileStream(sFile, FileMode.Create))
                new BinaryFormatter().Serialize(stream, obj);

            return true;
        }
        public static Boolean SaveMatrixToBinary(String sFile, Matrix m) { return SaveObjectToBinary(sFile, m.ToArray()); }

        public static Object ReadObjectFromBinary(String sFile)
        {
            Object obj;

            try
            {
                using (FileStream stream = new FileStream(sFile, FileMode.Open))
                    obj = new BinaryFormatter().Deserialize(stream);
            }
            catch (Exception)
            {
                return null;
            }

            return obj;
        }
        public static Matrix ReadMatrixFromBinary(String sFile)
        {
            double[,] darray = ReadObjectFromBinary(sFile) as double[,];
            return new Matrix(darray);
        }

        public static void WriteSerialInfoArray<T>(SerializationInfo info, T[] data, string label)
        {
            info.AddValue(label + ".length", data.Length);

            if (typeof(T) is ISerializable)
            {
                MemoryStream ms = new MemoryStream();
                BinaryFormatter bin = new BinaryFormatter();
                bin.Serialize(ms, data);
                info.AddValue(label + ".data", ms.ToArray(), typeof(byte[]));
            }
            else {
                for (int i = 0; i < data.Length; i++) info.AddValue(label + "[" + i + "]", data[i]);
            }
        }
        public static T[] ReadSerialInfoArray<T>(SerializationInfo info, string label)
        {
            T[] data;
            int n = info.GetInt32(label + ".length");
            if (typeof(T) is ISerializable)
            {
                MemoryStream ms = new MemoryStream((byte[])info.GetValue(label + ".data", typeof(byte[])));
                BinaryFormatter bin = new BinaryFormatter();
                data = (T[])bin.Deserialize(ms);
            }
            else {
                data = new T[n];
                for (int i = 0; i < n; i++) data[i] = (T)info.GetValue(label + "[" + i + "]", typeof(T));
            }
            return data;
        }

        public static void WriteSerialInfoJaggedArray<T>(SerializationInfo info, T[][] data, string label)
        {
            info.AddValue(label + ".length", data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                info.AddValue(label + "[" + i + "].length", data[i].Length);
                for (int j = 0; j < data[i].Length; j++)
                    info.AddValue(label + "[" + i + "][" + j + "]", data[i][j]);
            }
        }
        public static T[][] ReadSerialInfoJaggedArray<T>(SerializationInfo info, string label)
        {
            int n = info.GetInt32(label + ".length");
            T[][] data = new T[n][];
            for (int i = 0; i < n; i++)
            {
                int m = info.GetInt32(label + "[" + i + "].length");
                for (int j = 0; j < m; j++)
                    data[i][j] = (T)info.GetValue(label + "[" + i + "][" + j + "]", typeof(T));
            }
            return data;
        }

        #endregion



        #region Resource Loading Functions (Bitmap, Icon, Font, Cursor)

        //public static Bitmap LoadBitmapResource(String name)
        //{
        //    Bitmap bmp;
        //    using (Stream s = typeof(MiscFileIO).Assembly.GetManifestResourceStream("MeshFlowViewer.Icons." + name))
        //        bmp = new Bitmap(s);
        //    return bmp;
        //}

        //public static Icon LoadIconResource(String name)
        //{
        //    Icon ico;
        //    using (Stream s = typeof(MiscFileIO).Assembly.GetManifestResourceStream("MeshFlowViewer.Icons." + name))
        //        ico = new Icon(s);
        //    return ico;
        //}

        public static Bitmap LoadFontResource(String name)
        {
            Bitmap bmp;
            using (Stream s = typeof(MiscFileIO).Assembly.GetManifestResourceStream("MeshFlowViewer.Fonts." + name))
                bmp = new Bitmap(s);
            return bmp;
        }

        //public static Cursor LoadCursorResource(String name)
        //{
        //    Cursor c;
        //    using (Stream s = typeof(MiscFileIO).Assembly.GetManifestResourceStream("MeshFlowViewer.Cursors." + name))
        //        c = new Cursor(s);
        //    return c;
        //}

        #endregion



        public static String GetFileNameOnly(String sFullFileName)
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            int iLastSep = sFullFileName.LastIndexOf(sep);      // iLastSep = -1 if sep is not found
            return sFullFileName.Substring(iLastSep + 1);
        }


    }
}

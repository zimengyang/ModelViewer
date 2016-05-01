using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshFlowViewer
{
    public class GroupInfo
    {
        public int[] inds;
        public bool visible;

        public int this[int index] { get { return inds[index]; } set { inds[index] = value; } }

        public GroupInfo(int ninds) { inds = new int[ninds]; this.visible = true; }

        public GroupInfo(int[] inds, bool visible)
        {
            this.inds = inds;
            this.visible = visible;
        }

        public string GetKeyNoVis(int[] uids)
        {
            switch (inds.Length)
            {
                case 1: return String.Format("{0:0000000}", uids[inds[0]]);
                case 2: return String.Format("{0:0000000}:{1:0000000}", uids[inds[0]], uids[inds[1]]);
                case 3: return String.Format("{0:0000000}:{1:0000000}:{2:0000000}", uids[inds[0]], uids[inds[1]], uids[inds[2]]);
                case 4: return String.Format("{0:0000000}:{1:0000000}:{2:0000000}:{3:0000000}", uids[inds[0]], uids[inds[1]], uids[inds[2]], uids[inds[3]]);
            }
            throw new Exception("unhandled length!");
        }

        public ulong GetKey(int[] uids)
        {
            ulong key = 0;
            for (int i = 0; i < inds.Length; i++) key = key * 65535 + (ulong)uids[inds[i]];
            return key;
        }

        public string GetKey()
        {
            String key = "";
            foreach (int ind in inds) key = key + ind + ":";
            return key + (visible ? "1" : "0");
        }

        public void Reorder(int[] uids)
        {
            int l = inds.Length;
            if (l == 1) return;

            int minind = 0;
            if (l >= 2 && uids[1] < uids[minind]) minind = 1;
            if (l >= 3 && uids[2] < uids[minind]) minind = 2;
            if (l >= 4 && uids[3] < uids[minind]) minind = 3;

            if (l == 2)
            {
                inds = new int[] { inds[minind + 0], inds[(minind + 1) % l] };
            }
            else if (l == 3)
            {
                if (uids[(minind + 1) % l] < uids[(minind + 2) % l])
                    inds = new int[] { inds[minind + 0], inds[(minind + 1) % l], inds[(minind + 2) % l] };
                else
                    inds = new int[] { inds[minind + 0], inds[(minind + 2) % l], inds[(minind + 1) % l] };
            }
            else if (l == 4)
            {
                if (uids[(minind + 1) % l] < uids[(minind + 3) % l])
                    inds = new int[] { inds[minind + 0], inds[(minind + 1) % l], inds[(minind + 2) % l], inds[(minind + 3) % l] };
                else
                    inds = new int[] { inds[minind + 0], inds[(minind + 3) % l], inds[(minind + 2) % l], inds[(minind + 1) % l] };
            }
        }

        public static bool operator ==(GroupInfo g0, GroupInfo g1)
        {
            if (g0.inds.Length != g1.inds.Length) return false;

            for (int i = 0; i < g0.inds.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < g0.inds.Length; j++)
                {
                    int k = (i + j) % g0.inds.Length;
                    if (g0[i] != g1[k]) { same = false; break; }
                }
                if (same) return true;
            }

            return false;
        }

        public static bool EqualsExact(GroupInfo g0, GroupInfo g1)
        {
            if (g0.inds.Length != g1.inds.Length || g0.visible != g1.visible) return false;
            for (int i = 0; i < g0.inds.Length; i++) if (g0.inds[i] != g1.inds[i]) return false;
            return true;
        }

        public static bool operator !=(GroupInfo g0, GroupInfo g1) { return !(g0 == g1); }

        public override string ToString()
        {
            return inds.Aggregate("", (string s, int ind) => s + ind + " ") + visible;
        }

        public override bool Equals(object o) { return ((o is GroupInfo) && ((GroupInfo)o == this)); }

        public override int GetHashCode()
        {
            return inds.Aggregate(inds.Length, (int hash, int ind) => hash * 3511 + ind) + (visible ? 3 : 0);
        }

        public long GetHashCode(long mult)
        {
            return inds.Aggregate((long)inds.Length, (long hash, int ind) => hash * mult + (long)ind) + (visible ? 3 : 0);
        }
    }
}

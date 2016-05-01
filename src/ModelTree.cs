using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Common.Libs.MiscFunctions;
using Common.Libs.VMath;

namespace MeshFlowViewer
{
	public class ModelTreeRoot
	{
		public List<ModelTree> lst;
		public List<int> starts;
		public List<int> durations;
		public List<int> ends;
		
		public ModelTreeRoot( List<ModelTree> lst )
		{
			this.lst = lst;
			starts = new List<int>( lst.Count );
			durations = new List<int>( lst.Count );
			ends = new List<int>( lst.Count );
			
			int x = 0;
			foreach( ModelTree t in lst )
			{
				if( !t.IsProperTree() ) throw new ArgumentException( "ModelTreeRoot: a non-proper tree is found in lst" );
				
				int l = t.CountSubNodes();
				
				starts.Add( x );
				durations.Add( l );
				x += l;
				ends.Add( x - 1 );
			}
		}
		
		public List<ModelTreeSingle> FlattenUnfiltered()
		{
			List<ModelTreeSingle> l = new List<ModelTreeSingle>();
			foreach( ModelTree n in lst ) l.AddRange( n.FlattenUnfiltered() );
			return l;
		}
		
		public TreeNode[] ToTreeNodeArray()
		{
			TreeNode[] tnc = new TreeNode[lst.Count];
			lst.Each( delegate( ModelTree t, int i ) { tnc[i] = t.ToTreeNode(); } );
			return tnc;
		}
		
		public ListViewItem[] ToListViewItemArray()
		{
			ListViewItem[] lvitems = new ListViewItem[lst.Count];
			lst.Each( delegate( ModelTree t, int i ) { lvitems[i] = t.ToListViewItem(); } );
			return lvitems;
		}
		
		public int CountSubNodes() { return ends.Last(); }
		
		public List<ModelTree> GetModelTreePath( int value )
		{
			int i = 0;
			while( i < lst.Count && value >= durations[i] ) { value -= durations[i]; i++; }
			if( i == lst.Count ) return null;
			return lst[i].GetModelTreePath( value );
		}
	}
	
	public abstract class ModelTree
	{
		public string label;
		public string command;
		public string parameters;
		
		public Brush scrubbrush;
		
		public CameraProperties[] cameras;
		
		public abstract int CountSubNodes();
		public abstract bool IsProperTree();
		public abstract TreeNode ToTreeNode();
		public abstract ListViewItem ToListViewItem();
		
		public abstract IndexedViewableAlpha GetViewable();
		
		public abstract int GetRepIndex();
		public abstract SnapshotScene GetSnapshot();
		public abstract List<ModelTree> GetModelTreePath( int value );
		public abstract List<ModelTreeSingle> FlattenUnfiltered();
	}
	
	public class ModelTreeSingle : ModelTree
	{
		public SnapshotScene snapshot;
		
		public ModelTreeSingle( SnapshotScene snapshot )
		{
			this.snapshot = snapshot;
			this.label = snapshot.GetLabel();
			this.command = snapshot.command;
			this.parameters = snapshot.opts;
			this.cameras = snapshot.cameras;
		}
		
		public override int CountSubNodes() { return 1; }
		public override bool IsProperTree() { return true; }
		
		public override TreeNode ToTreeNode()
		{
			TreeNode tn = new TreeNode();
			tn.Text = label;
			tn.Tag = this;
			return tn;
		}
		
		public override ListViewItem ToListViewItem ()
		{
			ListViewItem lvi = new ListViewItem( label );
			lvi.Tag = this;;
			return lvi;
		}
		
		public override IndexedViewableAlpha GetViewable()
		{
			return snapshot.GetViewables();
		}
		
		public override int GetRepIndex()
		{
			return snapshot.timeindex;
		}
		
		public override SnapshotScene GetSnapshot()
		{
			return snapshot;
		}
		
		public override List<ModelTree> GetModelTreePath( int value )
		{
			if( value != 0 ) throw new ArgumentException( "value (" + value + ") != 0" );
			return new List<ModelTree>() { this };
		}
		
		public override List<ModelTreeSingle> FlattenUnfiltered()
		{
			return new List<ModelTreeSingle>() { this };
		}
	}
	
	public class ModelTreeFilter : ModelTree
	{
		public ModelTree node;
		public ModelTreeFilter( ModelTree node )
		{
			this.node = node;
			this.label = "Filtered: " + node.label;
			this.command = "Filtered: " + node.command;
			this.cameras = node.cameras;
			this.scrubbrush = new SolidBrush( Color.DarkGray );
		}
		
		public override bool IsProperTree() { return node.IsProperTree(); }
		public override int CountSubNodes() { return node.CountSubNodes(); }
		public override TreeNode ToTreeNode () { return node.ToTreeNode(); }
		public override ListViewItem ToListViewItem () { return node.ToListViewItem(); }
		public override IndexedViewableAlpha GetViewable() { return node.GetViewable(); }
		public override int GetRepIndex() { return node.GetRepIndex(); }
		public override SnapshotScene GetSnapshot() { return node.GetSnapshot(); }
		
		public override List<ModelTree> GetModelTreePath( int value )
		{
			return node.GetModelTreePath( value );
		}
		
		public override List<ModelTreeSingle> FlattenUnfiltered()
		{
			return new List<ModelTreeSingle>();
		}
	}
	
	public enum SummaryCompositions
	{
		Last, VertexTrails, AddDel, Add
	}
	
	public class ModelTreeSummary : ModelTree
	{
		public static Vec4f[] recoloradd = new Vec4f[] {
			new Vec4f( 0.25f, 1.00f, 0.50f, 1.00f ),
			new Vec4f( 0.25f, 1.00f, 0.50f, 1.00f ),
			new Vec4f( 0.13f, 0.50f, 0.25f, 0.50f ),
			new Vec4f( 0.13f, 0.50f, 0.25f, 0.50f ),
		};
		public static Vec4f[] recolordel = new Vec4f[] {
			new Vec4f( 1.00f, 0.25f, 0.50f, 1.00f ),
			new Vec4f( 1.00f, 0.25f, 0.50f, 1.00f ),
			new Vec4f( 0.50f, 0.13f, 0.25f, 0.50f ),
			new Vec4f( 0.50f, 0.13f, 0.25f, 0.50f ),
		};
		
		public List<ModelTree> lst;
		public SummaryCompositions composition;
		public List<int> starts;
		public List<int> durations;
		public List<int> ends;
		
		public ModelTreeSummary( string label, string command, string parameters, List<ModelTree> lst )
		{
			this.lst = lst;
			this.label = label;
			this.command = command;
			this.parameters = parameters;
			this.cameras = lst[lst.Count - 1].cameras; // grab the last camera
			this.scrubbrush = new SolidBrush( Color.Blue );
			this.composition = SummaryCompositions.Last;
			
			if( !IsProperTree() ) throw new ArgumentException( "ModelTreeSummary: not a proper tree" );
			
			starts = new List<int>( lst.Count );
			durations = new List<int>( lst.Count );
			ends = new List<int>( lst.Count );
			
			int x = 0;
			foreach( ModelTree t in lst )
			{
				int l = t.CountSubNodes();
				
				starts.Add( x );
				durations.Add( l );
				x += l;
				ends.Add( x - 1 );
			}
		}
		
		public override int CountSubNodes() { return lst.Sum( (ModelTree node) => (node.CountSubNodes()) ); }
		public override int GetRepIndex()
		{
			int i = -1;
			foreach( ModelTree t in lst ) i = Math.Max( i, t.GetRepIndex() );
			return i;
		}
		
		public override bool IsProperTree()
		{
			foreach( ModelTree t in lst ) if( !t.IsProperTree() ) return false;
			return true;
		}
		
		public override TreeNode ToTreeNode()
		{
			TreeNode tn = new TreeNode();
			tn.Text = label;
			tn.Tag = this;
			foreach( ModelTree t in lst )
				tn.Nodes.Add( t.ToTreeNode() );
			return tn;
		}
		
		public override ListViewItem ToListViewItem ()
		{
			ListViewItem lvi = new ListViewItem( label );
			lvi.Tag = this;;
			//foreach( ModelTree t in lst )
			//	lvi.SubItems.Add( t.ToListViewItem() );
			return lvi;
		}
		
		public override List<ModelTree> GetModelTreePath( int value )
		{
			int ovalue = value;
			int i = 0;
			while( i < lst.Count && value >= durations[i] ) { value -= durations[i]; i++; }
			if( i == lst.Count ) throw new ArgumentException( "value (" + ovalue + ") is out of bounds" );
			List<ModelTree> path = lst[i].GetModelTreePath( value );
			path.Insert( 0, this );
			return path;
		}
		
		public override IndexedViewableAlpha GetViewable()
		{
			switch( composition )
			{
			case SummaryCompositions.Last:				return GetSnapshot().GetViewables();
			case SummaryCompositions.VertexTrails:		return GetComposition_VertPosition();
			case SummaryCompositions.AddDel:		return GetComposition_SelectTopo();
			case SummaryCompositions.Add:				return GetComposition_Add();
			}
			
			throw new Exception( "unimplemented" );
		}
		
		public IndexedViewableAlpha GetComposition_VertPosition()
		{
			ColorGradient4f cg = new ColorGradient4f( ColorGradient4f.ROYGBIV );
			
			List<Vec3f> verts = new List<Vec3f>();
			List<Vec4f> ptcolors = new List<Vec4f>();
			List<Vec4f> lncolors = new List<Vec4f>();
			List<int> points = new List<int>();
			List<int> edges = new List<int>();
			List<int> vertuids = new List<int>();
			List<bool> selected = new List<bool>();
			
			int npts = 0;
			int tot = lst.Where( (ModelTree t) => !(t is ModelTreeFilter) ).Count();
			int cur = 0;
			int lper = 0;
			
			Random rnd = new Random();
			Dictionary<int,Vec4f> ucolors = new Dictionary<int, Vec4f>();
			
			lst.Each( delegate( ModelTree t, int index ) {
				float per0 = (float) cur / (float) (tot+1);
				float per1 = (float) (cur+1) / (float) (tot+1);
				Vec4f lncolor0 = cg.GetColor( per0 ) * 0.5f;
				Vec4f lncolor1 = cg.GetColor( per1 ) * 0.5f;
				Vec4f ucolor;
				int uid, k;
				
				if( t is ModelTreeFilter ) return;
				
				IndexedViewableAlpha viewable = t.GetViewable();
				for( int i = 0; i < viewable.nVerts; i++ ) {
					uid = viewable.VertUIDs[i];
					if( vertuids.Count == 0 ) k = -1; else k = vertuids.LastIndexOf( uid );
					
					if( ucolors.ContainsKey( uid ) ) ucolor = ucolors[uid];
					else {
						ucolor = new Vec4f( (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 1.0f );
						ucolors.Add( uid, ucolor );
					}
					
					verts.Add( viewable.Verts[i] );
					ptcolors.Add( ucolor );
					points.Add( npts );
					vertuids.Add( uid );
					selected.Add( false );
					
					if( k != -1 ) {
						edges.Add( k );
						edges.Add( npts );
						lncolors.Add( lncolor0 );
						lncolors.Add( lncolor1 );
					}
					
					npts++;
				}
				
				cur++;
				if( (int)(per1 * 10.0f) != lper ) { lper = (int)(per1 * 10.0f); System.Console.Write( " " + (lper*10) ); }
			} );
			System.Console.WriteLine();
			
			Vec3f[] averts = verts.ToArray();
			Vec4f[][] acolors = new Vec4f[][] { ptcolors.ToArray(), lncolors.ToArray() };
			int[][] agroups = new int[][] { points.ToArray(), edges.ToArray() };
			int[] avertuids = vertuids.ToArray();
			bool[] aselected = selected.ToArray();
			float[] ptsizes = new float[] { 1.0f, 0.0f };
			float[] lnwidths = new float[] { 0.0f, 1.0f };
			int[] groupszs = new int[] { 1, 2 };
			return new IndexedViewableAlpha( averts, acolors, agroups, ptsizes, lnwidths, groupszs, avertuids, aselected );
		}
		
		public IndexedViewableAlpha GetComposition_SelectTopo()
		{
			int itopo = GetLastIndexOfNonFiltered();
			ModelTree ttopo = lst[itopo];
			ModelTree tselect = lst[itopo-1];
			
			IndexedViewableAlpha viewselect = tselect.GetViewable();
			IndexedViewableAlpha viewtopo = ttopo.GetViewable();
			
			IndexedViewableAlpha viewadd = viewtopo - viewselect;
			IndexedViewableAlpha viewdel = viewselect - viewtopo;
			IndexedViewableAlpha viewsame = viewselect % viewtopo;
			
			viewadd.RecolorGroups( ( int i, int[] inds ) => recoloradd[inds.Length-1] );
			viewdel.RecolorGroups( ( int i, int[] inds ) => recolordel[inds.Length-1] );
			
			return viewsame + viewadd + viewdel;
		}
		
		public IndexedViewableAlpha GetComposition_Add()
		{
			ModelTree last = GetLastNonFiltered();
			IndexedViewableAlpha viewable = last.GetViewable();
			viewable.RecolorGroups( (int i, int[] inds) => (inds.Sum( (int ind) => ( viewable.Selected[ind] ? 1 : 0 ) ) > 0 ? (Vec4f?)recoloradd[inds.Length-1] : (Vec4f?)null) );
			return viewable;
		}
		
		public int GetLastIndexOfNonFiltered() { return lst.FindLastIndex( (ModelTree n) => !(n is ModelTreeFilter) ); }
		
		public ModelTree GetLastNonFiltered() { return lst.Find( (ModelTree n) => !(n is ModelTreeFilter) ); }
		
		public override SnapshotScene GetSnapshot()
		{
			return lst.Last().GetSnapshot();
		}
		
		public override List<ModelTreeSingle> FlattenUnfiltered()
		{
			List<ModelTreeSingle> l = new List<ModelTreeSingle>();
			foreach( ModelTree n in lst ) l.AddRange( n.FlattenUnfiltered() );
			return l;
		}
	}
}


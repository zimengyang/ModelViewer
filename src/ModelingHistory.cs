using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using Common.Libs.VMath;
using Common.Libs.MiscFunctions;
using Common.Libs.MatrixMath;

namespace MeshFlowViewer
{
	public enum ViewSelections
	{
		Artist, BestView, User
	}
	public enum AddVTagCriterion { Selected, Highlighted, Visible };
	public enum AddTTagCriterion { CurrentView, ViewWindow };
	
	[Serializable]
	public partial class ModelingHistory : IBinaryConvertible
	{
		public static bool DEBUGMODE = false;
		public static bool ENDING = false;
		public static ModelingHistory history;
		public static int CacheTopNLayers = 1;
		public static bool AddCustomHelmetLayer = false;
		public static bool AddClusterByConnection = false;
		public static bool ClusterLayers = false;
		
		private SnapshotScene[] snapshots;
		private GroupInfo[] groups;
		private bool[,] selectedverts;
		
		private HashSet<string>[] ttags;
		private HashSet<string>[] vtags;
		
		private HighlightColors[] highlighted;
		private Vec3f[] finalposition;
		
		private int nsnapshots;				// number of steps
		private int nuobjs;					// number of unique objs
		private int nuverts;				// number of unique verts
		
		private int[] basemodifyid;			// for each snapshot: the id of scene snapshot we're basing from
		
		public float CameraSmoothSigmat = 10.0f;
		public float CameraSmoothSigmax = 1.0f;
		
		public ClusteringLayers Layers { get; private set; }
		public FilteringSet Filters;
		public Property<Clustering> CurrentLevel = new Property<Clustering>( "Current Level", null );
		
		public delegate void HighlightedChangedHandler();
		public event HighlightedChangedHandler HighlightedChanged;
		public void FireHighlightedChangedHandler() { if( HighlightedChanged != null ) HighlightedChanged(); }
		
		public delegate void TagsChangedHandler();
		public event TagsChangedHandler TagsChanged;
		public void FireTagsChangedHandler() { if( TagsChanged != null ) TagsChanged(); }
		
		public ModelingHistory()
		{
			if( ModelingHistory.history != null ) throw new Exception( "Failed sanity check" );
			ModelingHistory.history = this;
		}
		

		#region Getters
		
		public int SnapshotCount { get { return nsnapshots; } }
		public SnapshotScene this[int index] { get { return snapshots[index]; } }
		
		public int BaseModifierIndex( int index ) { return basemodifyid[index]; }
		
		public GroupInfo GetGroup( int index ) { return groups[index]; }
		public GroupInfo[] GetGroups( int[] indices ) { return indices.Select( (int ind) => groups[ind] ).ToArray(); }
		public GroupInfo[][] GetGroups( int[][] indices )
		{
			return indices.Select( (int[] inds) => GetGroups( inds ) ).ToArray();
		}
		
		public int UniqueVertCount { get { return nuverts; } }
		
		public HashSet<string> GetVTags( int uid ) { return vtags[uid]; }
		public HashSet<string> GetTTags( int isnapshot ) { return ttags[isnapshot]; }
		
		public bool IsHighlighted( int uid ) { return highlighted[uid] != HighlightColors.None; }
		public HighlightColors GetHighlight( int uid ) { return highlighted[uid]; }
		
		public bool[] GetSelectedVerts_UID( int uid )
		{
			bool[] s = new bool[nsnapshots];
			for( int isnapshot = 0; isnapshot < nsnapshots; isnapshot++ ) s[isnapshot] = selectedverts[isnapshot,uid];
			return s;
		}
		public bool[] GetSelectedVerts_Snapshot( int isnapshot )
		{
			bool[] s = new bool[nuverts];
			for( int ivert = 0; ivert < nuverts; ivert++ ) s[ivert] = selectedverts[isnapshot,ivert];
			return s;
		}
		
		#endregion
		
		#region Binary Read Functions

		public void ReadBinary( BinaryReader br )
		{
			br.ReadArray( out snapshots ); //, (BinaryReader r) => SnapshotScene.ReadBinaryFile(r) );
			br.ReadArray( out highlighted );
			br.Read( out nsnapshots );
			br.Read( out nuobjs );
			br.Read( out nuverts );
			br.ReadArray( out basemodifyid );
			br.ReadArray( out finalposition );
			br.ReadArray( out groups );
			
			SetModelPrevs();
			
			SetHistories();
			
			//ReorderGroups();
			
			FindFinalPositions();
			//FillSelectedVerts();
			StartFiltering();
			StartClustering();
			//StartTags();
		}
		
		public static ModelingHistory ReadBinaryFile( BinaryReader br )
		{
			ModelingHistory hist = new ModelingHistory();
			hist.ReadBinary( br );
			return hist;
		}
		
		#endregion
	

		#region Misc
		
		private void SetHistories()
		{
			SnapshotModel.history = this;
		}
		
		private void SetModelPrevs()
		{
			for( int i = 1; i < nsnapshots; i++ ) snapshots[i].SetPrevScene( snapshots[i-1] );
		}
		
		private void FindFinalPositions()
		{
			System.Console.WriteLine( "Finding final positions for " + nuverts + " unique verts" );
			
			finalposition = new Vec3f[nuverts];
			foreach( SnapshotScene scene in snapshots )
			{
				if( scene.nochange ) continue;
				foreach( SnapshotModel model in scene.Models )
				{
					Vec3f[] verts = model.GetVerts();
					int[] vertuids = model.GetVertUIDs();
					for( int k = 0; k < model.nverts; k++ )
						finalposition[vertuids[k]] = verts[k];
				}
			}
		}
		
		private void FillSelectedVerts()
		{
			selectedverts = new bool[nsnapshots, nuverts];
			snapshots.Each( delegate( SnapshotScene scene, int isnapshot ) {
				foreach( SnapshotModel model in scene.Models )
				{
					if( model.objedit ) {
						foreach( int ind in model.selinds ) selectedverts[isnapshot,model.vertuids[ind]] = true;
					} else {
						if( model.objselected ) {
							int[] uids = model.GetVertUIDs();
							foreach( int uid in uids ) selectedverts[isnapshot,uid] = true;
						}
					}
				}
			} );
		}
        #endregion

        //private void ForceGarbageCollection() { ForceGarbageCollection( true ); }
        //private void ForceGarbageCollection( bool print )
        //{
        //	if( print ) {
        //		System.Console.WriteLine( "Collecting Garbage..." );
        //		System.Console.WriteLine( "memory {0}", GC.GetTotalMemory( true ) );
        //	}

        //	System.GC.Collect();
        //	System.GC.WaitForPendingFinalizers();

        //	if( print ) {
        //		System.Console.WriteLine( "memory {0}", GC.GetTotalMemory( true ) );
        //		System.Console.WriteLine( "Done!" );
        //	}
        //}

        //      private static string[] BlenderCommandsView = {
        //          "view3d.move", "view3d.rotate", "view3d.view_persportho",
        //          "view3d.viewnumpad", "view3d.view_orbit", "view3d.view_selected",
        //          "view3d.view_pan", "view3d.smoothview", "view3d.zoom",
        //          "view3d.select_vertex_mode", "view3d.select_edge_mode", "view3d.select_face_mode",
        //          "view3d.all", "view3d.localview", "view3d.view_all", "view3d.zoom_border",
        //      };

        //      public bool ContainsViewChangeCommand( string s )
        //{
        //	foreach( string cmd in BlenderCommandsView ) if( s.StartsWith(cmd) ) return true;
        //	return false;
        //}

        #region print information funcs
        //private List<string> ngrams( List<string> lst, int c, string delim )
        //{
        //	List<string> nlst = new List<string>();
        //	for( int i = 0; i < lst.Count - c + 1; i++ )
        //	{
        //		string s = "";
        //		for( int j = 0; j < c; j++ ) 
        //			s += ( j > 0 ? delim : "" ) + lst[i+j];
        //		nlst.Add( s );
        //	}
        //	return nlst;
        //}

        //private void PrintNgramInfo( List<string> cmds, int ngramsize, int printsize )
        //{
        //	List<string> lstngrams = ngrams( cmds, ngramsize, " " );
        //	List<string> lstngramsdistinct = lstngrams.Distinct().ToList();
        //	List<int> ngramscount = lstngramsdistinct.Select( (string s0) => lstngrams.Count( (string s1) => (s0 == s1) ) ).ToList();

        //	List<int> ngramsorder = Enumerable
        //		.Range( 0, lstngramsdistinct.Count )
        //			.OrderByDescending( (int i) => ngramscount[i] )
        //				.ToList();

        //	System.Console.WriteLine( "Top {1:d}-grams", printsize, ngramsize );
        //	for( int i = 0; i < Math.Min( printsize, ngramsorder.Count ); i++ )
        //		System.Console.WriteLine( "{1:000} {0:s}", lstngramsdistinct[ngramsorder[i]], ngramscount[ngramsorder[i]] );
        //	System.Console.WriteLine();
        //}

        //public void PrintTuplesInfo( List<Cluster> lstClusters )
        //{
        //	List<string> cmds = lstClusters.Where( cluster => !Filters.IsFiltered( cluster ) ).Select( (cluster) => {
        //		string n = cluster.name;
        //		string[] p = n.SplitOnce('.');
        //		string s = p[0];
        //		if( s != "topo" ) return s;
        //		if( TopoGroupA.Contains( n ) ) return "topoa";
        //		if( TopoGroupB.Contains( n ) ) return "topob";
        //		return "topoc";
        //	} ).ToList();
        //	PrintNgramInfo( cmds, 2, 40 );
        //	PrintNgramInfo( cmds, 3, 30 );
        //	PrintNgramInfo( cmds, 4, 20 );
        //}

        //public void StatisticsDump()
        //{
        //	string divider = "--------------------------------------------------------\n";
        //	string[] commands = snapshots.Select( (SnapshotScene scene) => scene.command ).ToArray();
        //	string[] opts = snapshots.Select( (SnapshotScene scene) => scene.opts ).ToArray();
        //	string[] dcmds = commands.Distinct().ToArray();

        //	using( FileStream fs = new FileStream( "statistics.txt", FileMode.Create ) )
        //	{
        //		using( TextWriter tw = new StreamWriter( fs ) )
        //		{
        //			tw.WriteLine( "statistics:" );
        //			tw.WriteLine( "handled count:     {0}", nsnapshots );
        //			tw.WriteLine( "distinct commands: {0}", dcmds.Length );
        //			tw.WriteLine( "unique objs:       {0}", nuobjs );
        //			tw.WriteLine( "unique verts:      {0}", nuverts );
        //			tw.WriteLine( divider );

        //			tw.WriteLine( "select: " + commands.Where( cmd => SelectionCommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "view: " + commands.Where( cmd => ViewCommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "vis: " + commands.Where( cmd => VisibilityCommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "gui: " + commands.Where( cmd => GUICommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "undo: " + commands.Where( cmd => UndoCommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "trans: " + commands.Where( cmd => TransformCommands.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "topoa: " + commands.Where( cmd => TopoGroupA.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "topob: " + commands.Where( cmd => TopoGroupB.Contains( cmd ) ).Count() );
        //			tw.WriteLine( "topoc: " + commands.Where( cmd => TopoGroupC.Contains( cmd ) ).Count() );
        //			tw.WriteLine( divider );

        //			tw.WriteLine( "distinct command list" );
        //			List<Clustering> layers = Layers.GetClusteringLayers();
        //			foreach( Clustering layer in layers )
        //			{
        //				tw.WriteLine( "layer " + layer.Level );
        //				layer.CreateCache();
        //				if( layer.DistinctNamesAll == null ) continue;
        //				string[] dnames = layer.DistinctNamesAll.ToArray();
        //				int[] dcount = layer.DistinctNamesAll_EachCount.ToArray();
        //				int[] order = dcount.GetSortIndices_QuickSort();
        //				foreach( int i in order ) tw.WriteLine( "  {0:0000}\t{1}", dcount[i], dnames[i] );

        //				//int[] cnts = dcmds.Select( dcmd => commands.Where( (string cmd)=> cmd == dcmd ).Count() ).ToArray();
        //				//int[] order = cnts.GetSortIndices_QuickSort();
        //				//foreach( int i in order ) tw.WriteLine( "  {0:0000}\t{1}", cnts[i], dcmds[i] );
        //			}
        //			tw.WriteLine( divider );

        //			tw.WriteLine( "command list:" );
        //			for( int i = 0; i < nsnapshots; i++ ) tw.WriteLine( "{0}\t{1}", commands[i], opts[i] );
        //			tw.WriteLine( divider );

        //			tw.WriteLine( "counts:" );
        //			tw.WriteLine( divider );
        //		}
        //	}
        //}

        //public void PrintSnapshotInfo( int i0 )
        //{
        //	SnapshotScene scene = snapshots[i0];
        //	System.Console.WriteLine( "Info:" );
        //	System.Console.WriteLine( "File: {0}", scene.file );
        //	System.Console.WriteLine( "Time: ", scene.timeindex );
        //}

        public void PrintLayers()
        {
            for (int layerIdx = 0; layerIdx < Layers.nlevels; layerIdx++)
            {
                //int finalClustersNumber = Layers.GetClusteringLayer(layerIdx).clusters.Count;
                int finalClustersNumber = Layers.GetClusteringLayer(layerIdx).GetClusters().Count;

                Console.WriteLine("Clustering level: {0}, clustered operation number = {1}",layerIdx,finalClustersNumber);
            }
        }

		#endregion
		
		#region Clustering, Filtering Functions
		
		private void StartClustering()
		{
			Layers = new ClusteringLayers( this, delegate( string command, int i ) {
				if( command.StartsWith( "gui" ) ) return new SolidBrush( Color.Gold );
				if( command.StartsWith( "undo" ) ) return new SolidBrush( Color.HotPink );
				if( command.StartsWith( "modifier" ) ) return new SolidBrush( Color.SaddleBrown );
				if( command.StartsWith( "topo" ) ) return new SolidBrush( Color.OrangeRed );
				if( command.StartsWith( "view" ) ) return new SolidBrush( Color.Green );
				if( command.StartsWith( "transform" ) ) return new SolidBrush( Color.Red );
				if( command.StartsWith( "select" ) ) return new SolidBrush( Color.Aqua );
				return new SolidBrush( Color.White );
			} );
			Layers.Reevaluated += Filters.FireReevalutadeHandler;
			
			CurrentLevel.Set( Layers.GetClusteringLayer() );
		}
		
		public List<string> ClusteringLayerNames = new List<string>();
		public List<int> ClusteringLayerLevels = new List<int>();
		
		public int CurrentClusteringLayerLevel_Get()
		{
			int m = ClusteringLayerLevels.Count;
			int c = CurrentLevel.Val.Level;
			for( int i = 0; i < ClusteringLayerLevels.Count; i++ )
			{
				int l = ClusteringLayerLevels[i];
				if( l >= c ) m = Math.Min( m, i );
			}
			return m;
		}
		public void CurrentClusteringLayerLevel_Set( int layer )
		{
			Clustering newclustering = ClusteringLayerLevel_Get( layer );
			if( newclustering != null ) CurrentLevel.Set( newclustering );
		}
		public void CurrentClusteringLayerLevel_Up()
		{
			int c = CurrentClusteringLayerLevel_Get();
			CurrentClusteringLayerLevel_Set( c + 1 );
		}
		public void CurrentClusteringLayerLevel_Down()
		{
			int c = CurrentClusteringLayerLevel_Get();
			CurrentClusteringLayerLevel_Set( c - 1 );
		}
		public Clustering ClusteringLayerLevel_Get( int layer )
		{
			if( layer < 0 || layer > ClusteringLayerLevels.Count - 1 ) return null;
			return Layers.GetClusteringLayer( ClusteringLayerLevels[layer] );
	}
		
		public void AddDefaultClusterLayers()
		{
			string[] sViewVisUndo = new List<string>().AddReturn( ModelingHistory.ViewCommands, ModelingHistory.VisibilityCommands, ModelingHistory.GUICommands ).AddReturn( "view", "undo.undo", "gui", "viewvis" ).ToArray();
			//string[] sViewVisUndoSelect = new List<string>().AddReturn( ModelingHistory.ViewCommands, ModelingHistory.VisibilityCommands, ModelingHistory.SelectionCommands, ModelingHistory.GUICommands ).AddReturn( "view", "undo.undo", "select", "gui" ).ToArray();
			string[] sSelections = new List<string>().AddReturn( ModelingHistory.SelectionCommands ).AddReturn( "select" ).ToArray();
			string[] sTransforms = new List<string>().AddReturn( ModelingHistory.TransformCommands ).AddReturn( "transform" ).ToArray();
			string[] sTopoA = ModelingHistory.TopoGroupA;
			string[] sTopoB = ModelingHistory.TopoGroupB;
			//string[] sExtrudes = new string[] { "topo.extrude" };
			//string[] sAddEdgeFace = new string[] { "topo.add.edge_face" };
			//string[] sGeoTopoOps = new List<string>().AddReturn( ModelingHistory.TransformCommands ).AddReturn( ModelingHistory.TopoCommands ).ToArray();
			string[] sGeoTopoNonPosOps = new List<string>().AddReturn( ModelingHistory.TransformCommands ).AddReturn( ModelingHistory.TopoCommands ).ToArray(); //TopoNonPosCommands
			string[] sTopoAddOps = ModelingHistory.TopoPosCommands;
			
			SolidBrush brDarkGreen = new SolidBrush( Color.FromArgb( 0, 128, 0 ) );
			SolidBrush brCyan = new SolidBrush( Color.FromArgb( 0, 255, 255 ) );
			//SolidBrush brOrange = new SolidBrush( Color.FromArgb( 255, 192, 0 ) );
			
			System.Console.Write( "Performing default clustering..." );
			
			Clustering clusterUndos = new ClusteringUndos( this );
			
			Clustering clusterViews = new ClusteringPredicate_StartsWith( "view", this, "Cluster Camera" );
			//Clustering clusterNonChanges = new ClusteringPredicate_Hide( this, "Cluster Nonchanges", sViewVisUndo );
			Clustering clusterViewVis = new ClusteringPredicate_MixedRepeats( "view", sViewVisUndo, brDarkGreen, Composition.GetPreset( CompositionPresets.Default ), history, "Cluster Visibility" );
			Clustering clusterSelections = new ClusteringPredicate_RepeatsWithIgnorable_UseLast( "select", brDarkGreen, Composition.GetPreset( CompositionPresets.Default ), sSelections, sViewVisUndo, this, "Cluster Selection" );
			//Clustering clusterNonChangeSelect = new ClusteringPredicate_PairWithIgnorable_UseLast( sViewVisUndo, new string[] { "select" },  new string[] { }, false, false, true, history, "Cluster Non-changes" );
			
			//Clustering clusterSelectAdd = new ClusteringPredicate_PairWithIgnorable_UseLast( sSelections, sTopoAddOps, sViewVisUndo, false, false, false, true, history, "Cluster Select+Add" );
			Clustering clusterSelectOp = new ClusteringPredicate_PairWithIgnorable_UseLast( sSelections, sGeoTopoNonPosOps, sViewVisUndo, false, false, false, false, history, "Cluster Selection+Operation" );
			
			Clustering clusterTransforms = new ClusteringPredicate_RepeatsWithIgnorable( "transform", brCyan, Composition.GetPreset( CompositionPresets.Transform ), sTransforms, sViewVisUndo, this, "Cluster Repeated Transform" );
			Clustering clusterRepeats1 = new ClusteringPredicate_Repeats2( sViewVisUndo, this, "Cluster Repeated Operations" );
			
			//Clustering clusterExtrudes = new ClusteringPredicate_RepeatsWithIgnorable( "topo.extrude", brOrange, Composition.GetPreset( CompositionPresets.Extrude ), sExtrudes, sViewVisUndo, this, "Cluster Repeated Extrudes" );
			//Clustering clusterExtrudeAddFace = new ClusteringPredicate_PairWithIgnorable( "topo.extrude", brOrange, Composition.GetPreset( CompositionPresets.Extrude ), sExtrudes, sAddEdgeFace, sViewVisUndo, false, true, false, history, "Cluster Extrude+Add Face" );
			Clustering clusterTopoTransform = new ClusteringPredicate_PairWithIgnorable_UseFirst( ModelingHistory.TopoNonNegCommands, sTransforms, sViewVisUndo, false, false, false, history, "Cluster Topology+Transform" );
			Clustering clusterTopoATopoB = new ClusteringPredicate_PairWithIgnorable_UseFirst( sTopoA, sTopoB, sViewVisUndo, false, true, false, history, "Cluster Structural Changes" );
			
			Clustering clusterRepeats2 = new ClusteringPredicate_Repeats2( sViewVisUndo, this, "Cluster Repeated Structural Changes" );
			
			//Clustering clusterSelectTransform = new ClusteringPredicate_PairWithIgnorable( "transform", brCyan, Composition.GetPreset( CompositionPresets.Transform ), sSelections, sTransforms, sViewVisUndo, true, true, true, this, "Cluster Select+Transform" );
			//Clustering clusterSelectExtrude = new ClusteringPredicate_PairWithIgnorable( "topo.extrude", brOrange, Composition.GetPreset( CompositionPresets.Extrude ), sSelections, sExtrudes, sViewVisUndo, true, true, true, this, "Cluster Select+Extrude" );
			//Layers.AddLayer( clusterSelectTransform );
			//Layers.AddLayer( clusterSelectExtrude );
			
			Layers.AddLayer( clusterUndos );			// 1
			
			Layers.AddLayer( clusterViews );			// 2
			Layers.AddLayer( clusterViewVis );		// 3
			Layers.AddLayer( clusterSelections );		// 4
			//Layers.AddLayer( clusterNonChangeSelect );	// 5
			
			//Layers.AddLayer( clusterSelectAdd );		// 5
			Layers.AddLayer( clusterSelectOp );			// 6
			
			Layers.AddLayer( clusterTransforms );		// 7
			Layers.AddLayer( clusterRepeats1 );			// 8
			
			Layers.AddLayer( clusterTopoTransform );	// 9
			Layers.AddLayer( clusterTopoATopoB );		// 10
			Layers.AddLayer( clusterRepeats2 );         // 11

            //if( ModelingHistory.ClusterLayers ) {
            //	ClusteringLayerNames.Add( "Original" ); ClusteringLayerLevels.Add( 0 );
            //	ClusteringLayerNames.Add( "Cluster Undone Work" ); ClusteringLayerLevels.Add( clusterUndos.Level );
            //	ClusteringLayerNames.Add( "Cluster Non-changes to Mesh" ); ClusteringLayerLevels.Add( clusterSelections.Level );
            //	ClusteringLayerNames.Add( "Cluster Selections with next change" ); ClusteringLayerLevels.Add( clusterSelectOp.Level );
            //	ClusteringLayerNames.Add( "Cluster Repeated Homogeneous Ops" ); ClusteringLayerLevels.Add( clusterRepeats1.Level );
            //	ClusteringLayerNames.Add( "Cluster Repeated Operation Groups" ); ClusteringLayerLevels.Add( clusterRepeats2.Level );
            //} else {
            //	for( Clustering c = Layers.GetClusteringLayer( 0 ); c != null; c = c.Above )
            //	{
            //		ClusteringLayerNames.Add( c.Label );
            //		ClusteringLayerLevels.Add( c.Level );
            //	}
            //}

            for (Clustering c = Layers.GetClusteringLayer(0); c != null; c = c.Above)
            {
                ClusteringLayerNames.Add(c.Label);
                ClusteringLayerLevels.Add(c.Level);
            }


            //if( AddClusterByConnection )
            //{
            //	Clustering clusterComponents = new ClusteringPredicate_Connected( this, "Cluster Connected Components" );
            //	Layers.AddLayer( clusterComponents );	// 13
            //	//if( clusterComponents.clusters.Count < 5 ) { System.Console.WriteLine( "Too few components.  Removing cluster by connected components" ); Layers.RemoveLevel( Layers.nlevels - 1 ); }

            //	ClusteringLayerNames.Add( "Cluster by Connected Components" ); ClusteringLayerLevels.Add( clusterComponents.Level );
            //}

            //if( AddCustomHelmetLayer )
            //{
            //	ClusteringCustom clusterCustom = new ClusteringCustom( history, "Custom" );
            //	clusterCustom.clusters.Add( new Cluster( 0, 647, "Chin", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 648, 1013, "Nose", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 1014, 1253, "Forehead", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 1254, 1359, "Ear", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 1360, 4619, "Back of Head", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 4620, 4917, "Ear", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 4918, 5836, "Thickness", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 5837, 7094, "Details", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 7095, 8165, "Eye Lens", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	clusterCustom.clusters.Add( new Cluster( 8166, 8511, "Fill-in", Composition.GetPreset( CompositionPresets.MeshDiff ) ) );
            //	Layers.AddLayer( clusterCustom );

            //	ClusteringLayerNames.Add( "Custom Clustering" ); ClusteringLayerLevels.Add( clusterCustom.Level );
            //}

            System.Console.WriteLine( "done" );
			
			CurrentLevel.Set( Layers.GetClusteringLayer() );
			
			//Thread processthread = new Thread( CacheTopLayers );
			//processthread.Start();
			CacheTopLayers();
		}
		
		private void CacheTopLayers()
		{
			//Thread.Sleep( 5000 ); // wait 20 secs before starting
			
			int c = ModelingHistory.CacheTopNLayers;
			System.Console.Write( "Caching top " + c + " layers fully" );
			List<Clustering> layers = Layers.GetClusteringLayers();
			Enumerable.Range(0, c).Each( (int i, int ind) => {
				if( ENDING ) return;
				Clustering layer = layers[layers.Count - 1 - i];
				layer.CacheViewables( 0 ); // 500 + ( layers.Count - ind ) * 100
				if( ENDING ) return;
			} );
			
			System.Console.WriteLine( "done" );
		}
		
		private void StartFiltering()
		{
			Filters = new FilteringSet();
			//Layers.Reevaluated += Filters.FireReevalutadeHandler;
		}
		
		private void StartTags()
		{
			vtags = Enumerable.Range( 0, nuverts ).Select( i =>  new HashSet<string>() ).ToArray();
			ttags = Enumerable.Range( 0, nsnapshots ).Select( i => new HashSet<string>() ).ToArray();
		}
	
		#endregion
	}
}


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Common.Libs.MatrixMath;
using Common.Libs.MiscFunctions;
using Common.Libs.VMath;
using System.Reflection;

namespace MeshFlowViewer
{
	public static class MiscExtensions
	{
		public static bool Within( this int v, int min, int max ) { return v >= min && v <= max; }
		
		public static int IndexOfTuple( this int[] searchin, int[] tuples, int start, int count )
		{
			for( int i = 0; i < searchin.Length; i += count )
			{
				bool found = true;
				for( int j = 0; j < count; j++ )
				{
					if( searchin[i+j] != tuples[start+j] ) found = false;
				}
				if( found ) return i;
			}
			return -1;
		}
		public static int IndexOfTuple( this List<int> searchin, int[] tuples, int start, int count )
		{
			for( int i = 0; i < searchin.Count; i += count )
			{
				bool found = true;
				for( int j = 0; j < count; j++ )
				{
					if( searchin[i+j] != tuples[start+j] ) found = false;
				}
				if( found ) return i;
			}
			return -1;
		}
		
		public static int IndexOf( this int[] searchin, int searchfor )
		{
			for( int i = 0; i < searchin.Length; i++ )
				if( searchin[i] == searchfor ) return i;
			return -1;
		}
		
		public static int IndexOfGroupInfo( this GroupInfo[] lstgroups, GroupInfo g )
		{
			for( int i = 0; i < lstgroups.Length; i++ )
				if( lstgroups[i] == g ) return i;
			return -1;
		}
		
		public static string[] SplitOnce( this string s, char delim )
		{
			int i = s.IndexOf( delim );
			if( i == -1 ) return new string[] { s };
			return new string[] { s.Substring( 0, i ), s.Substring( i + 1 ) };
		}
		
		public static Vec3f[] DeepCopy( this Vec3f[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static Vec4f[] DeepCopy( this Vec4f[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static int[] DeepCopy( this int[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static ulong[] DeepCopy( this ulong[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static float[] DeepCopy( this float[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static bool[] DeepCopy( this bool[] array )
		{
			if( array == null ) return null;
			return array.CloneArray();
		}
		public static Vec4f[][] DeepCopy( this Vec4f[][] array )
		{
			if( array == null ) return null;
			int count = array.Length;
			Vec4f[][] newarray = new Vec4f[count][];
			for( int i = 0; i < count; i++ ) newarray[i] = array[i].CloneArray();
			return newarray;
		}
		public static int[][] DeepCopy( this int[][] array )
		{
			if( array == null ) return null;
			int count = array.Length;
			int[][] newarray = new int[count][];
			for( int i = 0; i < count; i++ ) newarray[i] = array[i].CloneArray();
			return newarray;
		}
		public static ulong[][] DeepCopy( this ulong[][] array )
		{
			if( array == null ) return null;
			int count = array.Length;
			ulong[][] newarray = new ulong[count][];
			for( int i = 0; i < count; i++ ) newarray[i] = array[i].CloneArray();
			return newarray;
		}
		
		public static int CountTrue( this bool[] array )
		{
			int c = 0, count = array.Length;;
			for( int i = 0; i < count; i++ ) if( array[i] ) c++;
			return c;
		}
		
		// this should go in the Vec3f class
		public static Vec3f Average( this IEnumerable<Vec3f> vecs )
		{
			if( vecs == null || vecs.Count() == 0 ) return new Vec3f();
			return vecs.Aggregate( new Vec3f(), ( avg, vec ) => avg + vec ) / (float)vecs.Count();
		}
		
		
		// from: http://www.codemeit.com/code-collection/c-find-all-derived-types-from-assembly.html
		public static IEnumerable<Type> GetDerivedTypes( this Type basetype )
		{
			Assembly assembly = Assembly.GetEntryAssembly();
			foreach( var type in assembly.GetTypes() )
			{
				if( type.IsSubclassOf( basetype ) ) yield return type;
			}
		}
		
		
		// this should go in the Matrix class
		public static Vec3f Project( this Matrix mat, Vec3f v )
		{
			//if( mat.Width != 4 && mat.Height != 4 ) throw new Exception( "Matrix must be 4x4" );
			double x = mat[0,0] * v.x + mat[1,0] * v.y + mat[2,0] * v.z + 1 * mat[3,0];
			double y = mat[0,1] * v.x + mat[1,1] * v.y + mat[2,1] * v.z + 1 * mat[3,1];
			double z = mat[0,2] * v.x + mat[1,2] * v.y + mat[2,2] * v.z + 1 * mat[3,2];
			double w = mat[0,3] * v.x + mat[1,3] * v.y + mat[2,3] * v.z + 1 * mat[3,3];
			
			return new Vec3f( (float) (x / w), (float) (y / w), (float) (z / w) );
		}
		
	}
	
	public static class ArrayExt
	{
		public static T[] CloneArray<T>( this T[] array ) { return (T[]) array.Clone(); }
		
		public static T[] CreateCopies<T>( T obj, int count )
		{
			T[] ar = new T[count];
			for( int i = 0; i < count; i++ ) ar[i] = obj;
			return ar;
		}
		public static int[] CreateRange( int start, int count )
		{
			int[] range = new int[count];
			for( int i = 0, j = start; i < count; i++, j++ ) range[i] = j;
			return range;
		}
		public static int[] CreateRange( int count )
		{
			int[] range = new int[count];
			for( int i = 0; i < count; i++ ) range[i] = i;
			return range;
		}
		
		public static int[] ConcatAll( this IEnumerable<int[]> list )
		{
			int c = list.Sum( (int[] lst) => lst.Length );
			int[] concat = new int[c];
			for( int ilst = 0, iconcat = 0; ilst < list.Count(); ilst++ )
			{
				int[] lst = list.ElementAt( ilst );
				for( int i = 0; i < lst.Length; i++, iconcat++ ) concat[iconcat] = lst[i];
			}
			return concat;
		}
		
		public static T[][] AddReturn<T>( this T[][] array, IEnumerable<IEnumerable<T>> addlst )
		{
			int l0 = array.Length;
			int l1 = addlst.Count();
			T[][] newarray = new T[l0+l1][];
			int i,j;
			for( i = 0; i < l0; i++ ) newarray[i] = array[i];
			for( j = 0; j < l1; i++, j++ ) newarray[i] = addlst.ElementAt(j).ToArray();
			return newarray;
		}
		public static T[] AddReturn<T>( this T[] array, IEnumerable<T> addlst )
		{
			if( array == null ) return addlst.ToArray();
			int l0 = array.Length;
			int l1 = addlst.Count();
			T[] newarray = new T[l0+l1];
			int i,j;
			for( i = 0; i < l0; i++ ) newarray[i] = array[i];
			for( j = 0; j < l1; i++, j++ ) newarray[i] = addlst.ElementAt(j);
			return newarray;
		}
		public static T[] AddReturn<T>( this T[] array, params T[] addlst )
		{
			if( array == null ) return addlst.ToArray();
			int l0 = array.Length;
			int l1 = addlst.Length;
			T[] newarray = new T[l0+l1];
			int i,j;
			for( i = 0; i < l0; i++ ) newarray[i] = array[i];
			for( j = 0; j < l1; i++, j++ ) newarray[i] = addlst[j];
			return newarray;
		}
		public static T[] AddReturn<T>( this T[] array, T addlist )
		{
			int l = array.Length;
			T[] newarray = new T[l+1];
			for( int i = 0; i < l; i++ ) newarray[i] = array[i];
			newarray[l] = addlist;
			return newarray;
		}
		
		public static int LastIndexOf( this int[] array, int val )
		{
			for( int i = array.Length - 1; i >= 0; i-- ) if( array[i] == val ) return i;
			return -1;
		}
		
		public static bool ContainsBinarySearch_Ascending( this long[] ascarray, long val )
		{
			int count = ascarray.Length;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				long mv = ascarray[m];
				if( mv == val ) return true;
				if( mv > val ) l = m - 1;
				else f = m + 1;
			}
			return false;
		}
		
		public static bool ContainsBinarySearch_Descending( this long[] ascarray, long val )
		{
			int count = ascarray.Length;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				long mv = ascarray[m];
				if( mv == val ) return true;
				if( mv < val ) l = m - 1;
				else f = m + 1;
			}
			return false;
		}
		
		public static bool ContainsBinarySearch_Ascending( this ulong[] ascarray, ulong val )
		{
			int count = ascarray.Length;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				ulong mv = ascarray[m];
				if( mv == val ) return true;
				if( mv > val ) l = m - 1;
				else f = m + 1;
			}
			return false;
		}
		
		public static bool ContainsBinarySearch_Descending( this ulong[] ascarray, ulong val )
		{
			int count = ascarray.Length;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				ulong mv = ascarray[m];
				if( mv == val ) return true;
				if( mv < val ) l = m - 1;
				else f = m + 1;
			}
			return false;
		}
		
		#region Sorting Functions
		
		public static int[] GetBubbleSortedIndices( this float[] values )
		{
			int count = values.Length;
			int[] indsordered = CreateRange( count );
			int t;
			bool check;
			
			do {
				check = false;
				for( int i = 0; i < count - 1; i++ )
				{
					if( values[indsordered[i]] >= values[indsordered[i+1]] ) continue;
					
					t = indsordered[i];
					indsordered[i] = indsordered[i+1];
					indsordered[i+1] = t;
					check = true;
				}
			} while( check );
			
			return indsordered;
		}
		
		public static int[] GetQuickSortedIndices( this float[] values )
		{
			int count = values.Length;
			int[] inds = CreateRange( count );
			QuickSortIndices( values, ref inds, 0, count - 1 );
			return inds;
		}
		private static void QuickSortIndices( float[] values, ref int[] inds, int beg, int end )
		{
			if( end <= beg + 1 ) return;
			
			float piv = values[inds[beg]];
			int l = beg + 1;
			int r = end;
			int t;
			while( l < r )
			{
				if( piv <= values[inds[l]] ) l++;
				else { r--; t = inds[l]; inds[l] = inds[r]; inds[r] = t;}
			}
			l--; t = inds[l]; inds[l] = inds[beg]; inds[beg] = t;
			QuickSortIndices( values, ref inds, beg, l );
			QuickSortIndices( values, ref inds, r, end );
		}
		
		public static int[] GetQuickSortedIndices( this long[] values )
		{
			int count = values.Length;
			int[] inds = CreateRange( count );
			QuickSortIndices( values, ref inds, 0, count - 1 );
			return inds;
		}
		private static void QuickSortIndices( long[] values, ref int[] inds, int beg, int end )
		{
			if( end <= beg + 1 ) return;
			
			long piv = values[inds[beg]];
			int l = beg + 1;
			int r = end;
			int t;
			while( l < r )
			{
				if( piv <= values[inds[l]] ) l++;
				else { r--; t = inds[l]; inds[l] = inds[r]; inds[r] = t;}
			}
			l--; t = inds[l]; inds[l] = inds[beg]; inds[beg] = t;
			QuickSortIndices( values, ref inds, beg, l );
			QuickSortIndices( values, ref inds, r, end );
		}
		
		
		
		public static int[] GetSortIndices_QuickSort( this IEnumerable<ulong> values )
		{
			int count = values.Count();
			if( count == 0 ) return null;
			int[] inds = Enumerable.Range( 0, count ).ToArray();
			GetSortIndices_QuickSort( values, ref inds, 0, count - 1 );
			return inds;
		}
		private static void GetSortIndices_QuickSort( IEnumerable<ulong> values, ref int[] inds, int l, int r )
		{
			if( r <= l ) return;
			int p = ( l + r ) / 2;
			p = GetSortIndices_QuickSort_Partition( values, ref inds, l, r, p );
			GetSortIndices_QuickSort( values, ref inds, l, p - 1 );
			GetSortIndices_QuickSort( values, ref inds, p + 1, r );
		}
		private static int GetSortIndices_QuickSort_Partition( IEnumerable<ulong> values, ref int[] inds, int l, int r, int p )
		{
			ulong pv = values.ElementAt(inds[p]);
			int t = inds[p]; inds[p] = inds[r]; inds[r] = t;
			int si = l;
			for( int i = l; i < r; i++ )
			{
				if( values.ElementAt(inds[i]) > pv ) continue;
				t = inds[i]; inds[i] = inds[si]; inds[si] = t;
				si++;
			}
			t = inds[si]; inds[si] = inds[r]; inds[r] = t;
			return si;
		}
		
		public static int[] GetSortIndices_QuickSort( this IEnumerable<int> values )
		{
			int count = values.Count();
			if( count == 0 ) return null;
			int[] inds = Enumerable.Range( 0, count ).ToArray();
			GetSortIndices_QuickSort( values, ref inds, 0, count - 1 );
			return inds;
		}
		private static void GetSortIndices_QuickSort( IEnumerable<int> values, ref int[] inds, int l, int r )
		{
			if( r <= l ) return;
			int p = ( l + r ) / 2;
			p = GetSortIndices_QuickSort_Partition( values, ref inds, l, r, p );
			GetSortIndices_QuickSort( values, ref inds, l, p - 1 );
			GetSortIndices_QuickSort( values, ref inds, p + 1, r );
		}
		private static int GetSortIndices_QuickSort_Partition( IEnumerable<int> values, ref int[] inds, int l, int r, int p )
		{
			int pv = values.ElementAt(inds[p]);
			int t = inds[p]; inds[p] = inds[r]; inds[r] = t;
			int si = l;
			for( int i = l; i < r; i++ )
			{
				if( values.ElementAt(inds[i]) > pv ) continue;
				t = inds[i]; inds[i] = inds[si]; inds[si] = t;
				si++;
			}
			t = inds[si]; inds[si] = inds[r]; inds[r] = t;
			return si;
		}
		
		public static int[] GetSortIndices_QuickSort( this String[] values )
		{
			int count = values.Count();
			if( count == 0 ) return null;
			int[] inds = Enumerable.Range( 0, count ).ToArray();
			GetSortIndices_QuickSort( values, ref inds, 0, count - 1 );
			return inds;
		}
		private static void GetSortIndices_QuickSort( String[] values, ref int[] inds, int l, int r )
		{
			if( r <= l ) return;
			int p = ( l + r ) / 2;
			p = GetSortIndices_QuickSort_Partition( values, ref inds, l, r, p );
			GetSortIndices_QuickSort( values, ref inds, l, p - 1 );
			GetSortIndices_QuickSort( values, ref inds, p + 1, r );
		}
		private static int GetSortIndices_QuickSort_Partition( String[] values, ref int[] inds, int l, int r, int p )
		{
			String pv = values[inds[p]];
			int t = inds[p]; inds[p] = inds[r]; inds[r] = t;
			int si = l;
			for( int i = l; i < r; i++ )
			{
				if( values[inds[i]].CompareTo( pv ) == 1 ) continue;
				t = inds[i]; inds[i] = inds[si]; inds[si] = t;
				si++;
			}
			t = inds[si]; inds[si] = inds[r]; inds[r] = t;
			return si;
		}
		
		
		
		public static void Sort_QuickSort( this ulong[] values )
		{
			int count = values.Length;
			if( count <= 1 ) return;
			Sort_QuickSort( ref values, 0, count - 1 );
		}
		private static void Sort_QuickSort( ref ulong[] values, int l, int r )
		{
			if( r <= l ) return;
			int p = ( l + r ) / 2;
			p = Sort_QuickSorts_Partition( ref values, l, r, p );
			Sort_QuickSort( ref values, l, p - 1 );
			Sort_QuickSort( ref values, p + 1, r );
		}
		private static int Sort_QuickSorts_Partition( ref ulong[] values, int l, int r, int p )
		{
			ulong pv = values[p];
			ulong t = values[p]; values[p] = values[r]; values[r] = t;
			int si = l;
			for( int i = l; i < r; i++ )
			{
				if( values[i] > pv ) continue;
				t = values[i]; values[i] = values[si]; values[si] = t;
				si++;
			}
			t = values[si]; values[si] = values[r]; values[r] = t;
			return si;
		}
		
		public static T[] Reorder<T>( this T[] values, int[] order )
		{
			int count = order.Length;
			T[] nvalues = new T[count];
			for( int i = 0; i < count; i++ ) nvalues[i] = values[order[i]];
			return nvalues;
		}
		
		#endregion
	}
	
	public static class MiscGenericFunctions
	{
		public static List<T> InsertReturn<T>( this List<T> lst, int index, T item )
		{
			lst.Insert( index, item );
			return lst;
		}
		
		public static List<T> AddReturn<T>( this List<T> lst, T item )
		{
			lst.Add( item );
			return lst;
		}
		public static List<T> AddReturn<T>( this List<T> lst, params T[] additems )
		{
			foreach( T item in additems ) lst.Add( item );
			return lst;
		}
		public static List<T> AddReturn<T>( this List<T> lst, IEnumerable<T> addlst )
		{
			lst.AddRange( addlst );
			return lst;
		}
		public static List<T> AddReturn<T>( this List<T> lst, params IEnumerable<T>[] addlsts )
		{
			foreach( T[] addlst in addlsts )
				lst.AddRange( addlst );
			return lst;
		}
		
		public static IEnumerable<TResult> Select2<T1,T2,TResult>( this IEnumerable<T1> something, IEnumerable<T2> other, Func<T1,T2,TResult> selectfunc )
		{
			int c = something.Count();
			var inds = Enumerable.Range( 0, c );
			return inds.Select( (int ind) => selectfunc( something.ElementAt(ind), other.ElementAt(ind) ) );
		}
		
		public static bool ContainsBinarySearch_Ascending( this List<ulong> ascarray, ulong val ) { return ( ascarray.IndexOf_BinarySearch_Ascending( val ) != -1 ); }
		public static int IndexOf_BinarySearch_Ascending( this List<ulong> ascarray, ulong val )
		{
			int count = ascarray.Count;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				ulong mv = ascarray[m];
				if( mv == val ) return m;
				if( mv > val ) l = m - 1;
				else f = m + 1;
			}
			return -1;
		}
		
		public static bool ContainsBinarySearch_Descending( this List<ulong> ascarray, ulong val ) { return ( ascarray.IndexOf_BinarySearch_Ascending( val ) != -1 ); }
		public static int IndexOf_BinarySearch_Descending( this List<ulong> ascarray, ulong val )
		{
			int count = ascarray.Count;
			int f = 0;
			int l = count - 1;
			while( l >= f ) {
				int m = ( l + f ) / 2;
				ulong mv = ascarray[m];
				if( mv == val ) return m;
				if( mv < val ) l = m - 1;
				else f = m + 1;
			}
			return -1;
		}
		
		public static int IndexOf_GE_BinarySearch_Ascending( this List<ulong> list, ulong val )
		{
			int count = list.Count;
			if( count == 0 ) return -1;
			
			int f = 0;
			int l = count - 1;
			if( list[l] < val ) return -1;
			
			int m = -1;
			
			while( l >= f ) {
				m = ( l + f ) / 2;
				ulong mv = list[m];
				if( val == mv ) return m;
				if( val < mv ) l = m - 1;
				else f = m + 1;
			}
			
			if( list[f] < val )
				while( f < count - 1 && list[f] < val ) f++;
			else
				while( f > 0 && list[f-1] > val ) f--;
			return f;
		}
	}
	
}


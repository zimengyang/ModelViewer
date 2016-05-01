using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Libs.VMath;

namespace MeshFlowViewer
{
	public class ColorGradient3f
	{
		List<Vec3f> colors = new List<Vec3f>();
		
		public ColorGradient3f() { }
		public ColorGradient3f( List<Vec3f> colors ) { this.colors = colors; }
		public ColorGradient3f( Vec3f[] colors ) : this( new List<Vec3f>( colors ) ) { }
		
		public void AddColor( Vec3f color ) { colors.Add( color ); }
		
		public Vec3f GetColor( float p )
		{
			if ( colors.Count == 0 ) return Vec3f.Zero;
			p = FMath.Clamp( p, 0.0f, 1.0f );
			float p2 = p * (float) ( colors.Count - 1 );
			int c = (int) p2;
			if ( c >= colors.Count - 1 ) return colors[colors.Count - 1];
			p2 -= (float) c;
			Vec3f color1 = colors[c];
			Vec3f color2 = colors[c + 1];
			return color1 * ( 1.0f - p2 ) + color2 * p2;
		}
	}
	
	public class ColorGradient4f
	{
		public static Vec4f[] ROYGBIV = new Vec4f[] {
			new Vec4f( 1.0f, 0.0f, 0.0f, 1.0f ),
			new Vec4f( 0.75f, 0.5f, 0.0f, 1.0f ),
			new Vec4f( 1.0f, 1.0f, 0.0f, 1.0f ),
			new Vec4f( 0.0f, 1.0f, 0.0f, 1.0f ),
			new Vec4f( 0.0f, 0.0f, 1.0f, 1.0f ),
			new Vec4f( 0.5f, 0.0f, 1.0f, 1.0f ),
			new Vec4f( 0.25f, 0.0f, 0.5f, 1.0f ),
		};
		
		List<Vec4f> colors = new List<Vec4f>();
		
		public ColorGradient4f() { }
		public ColorGradient4f( List<Vec4f> colors ) { this.colors = colors; }
		public ColorGradient4f( Vec4f[] colors ) : this( new List<Vec4f>( colors ) ) { }
		
		public void AddColor( Vec4f color ) { colors.Add( color ); }
		
		public Vec4f GetColor( float p )
		{
			if ( colors.Count == 0 ) return Vec4f.Zero;
			p = FMath.Clamp( p, 0.0f, 1.0f );
			float p2 = p * (float) ( colors.Count - 1 );
			int c = (int) p2;
			if ( c >= colors.Count - 1 ) return colors[colors.Count - 1];
			p2 -= (float) c;
			Vec4f color1 = colors[c];
			Vec4f color2 = colors[c + 1];
			return color1 * ( 1.0f - p2 ) + color2 * p2;
		}
	}
}

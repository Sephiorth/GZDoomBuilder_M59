
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Geometry;
using System.Drawing;
using CodeImp.DoomBuilder.Rendering;
using System.Collections.ObjectModel;
using SlimDX;

#endregion

namespace CodeImp.DoomBuilder.Map
{
	public enum SectorFogMode //mxd
	{
		NONE,			   // no fog
		CLASSIC,		   // black fog when sector brightness < 243
		FOGDENSITY,		   // sector uses "fogdensity" MAPINFO property
		OUTSIDEFOGDENSITY, // sector uses "outsidefogdensity" MAPINFO property
		FADE			   // sector uses UDMF "fade" sector property
	}
	
	public sealed class Sector : SelectableElement, IMultiTaggedMapElement
	{
		#region ================== Constants

		internal const int SLOPE_DECIMALS = 7;

		#endregion

		#region ================== Variables

		// Map
		private MapSet map;

		// List items
		private LinkedListNode<Sector> selecteditem;
		
		// Sidedefs
		private LinkedList<Sidedef> sidedefs;
		
		// Properties
		private int fixedindex;
		private int floorheight;
		private int ceilheight;
		private int offsetx;
		private int offsety;
		private string floortexname;
		private string ceiltexname;
		private long longfloortexname;
		private long longceiltexname;
		private int effect;
		private List<int> tags; //mxd
		private int brightness;

		//mxd. UDMF properties
		private Dictionary<string, bool> flags;

		// Meridian 59 roo properties.
		private int sectortag;
		private int animationspeed;
		private int depth;
		private SDScrollFlags scrollflags;
		private bool scrollfloor;
		private bool scrollceiling;
		private bool flicker;
		private int floortexrot;
		private int ceiltexrot;
		private List<Vector3D> floorslopevertexes;
		private List<Vector3D> ceilslopevertexes;
		// Cloning
		private Sector clone;
		private int serializedindex;
		
		// Triangulation
		private bool updateneeded;
		private bool triangulationneeded;
		private RectangleF bbox;
		private Triangulation triangles;
		private FlatVertex[] flatvertices;
		private ReadOnlyCollection<LabelPositionInfo> labels;
		private readonly SurfaceEntryCollection surfaceentries;

		//mxd. Rendering
		private Color4 fogcolor;
		private SectorFogMode fogmode;

		//mxd. Slopes
		private Vector3D floorslope;
		private float flooroffset;
		private Vector3D ceilslope;
		private float ceiloffset;
		
		#endregion

		#region ================== Properties

		public MapSet Map { get { return map; } }
		public ICollection<Sidedef> Sidedefs { get { return sidedefs; } }

		/// <summary>
		/// An unique index that does not change when other sectors are removed.
		/// </summary>
		public int FixedIndex { get { return fixedindex; } }
		public int FloorHeight { get { return floorheight; } set { BeforePropsChange(); floorheight = value; } }
		public int CeilHeight { get { return ceilheight; } set { BeforePropsChange(); ceilheight = value; } }
		public int OffsetX { get { return offsetx; } set { BeforePropsChange(); offsetx = value; } }
		public int OffsetY { get { return offsety; } set { BeforePropsChange(); offsety = value; } }
		public string FloorTexture { get { return floortexname; } }
		public string CeilTexture { get { return ceiltexname; } }
		public long LongFloorTexture { get { return longfloortexname; } }
		public long LongCeilTexture { get { return longceiltexname; } }
		internal Dictionary<string, bool> Flags { get { return flags; } } //mxd
		public int Effect { get { return effect; } set { BeforePropsChange(); effect = value; } }
		public int Tag { get { return tags[0]; } set { BeforePropsChange(); tags[0] = value; if((value < General.Map.FormatInterface.MinTag) || (value > General.Map.FormatInterface.MaxTag)) throw new ArgumentOutOfRangeException("Tag", "Invalid tag number"); } } //mxd
		public List<int> Tags { get { return tags; } set { BeforePropsChange(); tags = value; } } //mxd
		public int Brightness { get { return brightness; } set { BeforePropsChange(); brightness = value; updateneeded = true; } }
		public bool UpdateNeeded { get { return updateneeded; } set { updateneeded |= value; triangulationneeded |= value; } }
		public RectangleF BBox { get { return bbox; } }
		internal Sector Clone { get { return clone; } set { clone = value; } }
		internal int SerializedIndex { get { return serializedindex; } set { serializedindex = value; } }
		public Triangulation Triangles { get { return triangles; } }
		public FlatVertex[] FlatVertices { get { return flatvertices; } }
		public ReadOnlyCollection<LabelPositionInfo> Labels { get { return labels; } }

		//mxd. Rednering
		public Color4 FogColor { get { return fogcolor; } }
		public SectorFogMode FogMode { get { return fogmode; } }

		//mxd. Slopes
		public Vector3D FloorSlope { get { return floorslope; } set { BeforePropsChange(); floorslope = value; updateneeded = true; } }
		public float FloorSlopeOffset { get { return flooroffset; } set { BeforePropsChange(); flooroffset = value; updateneeded = true; } }
		public Vector3D CeilSlope { get { return ceilslope; } set { BeforePropsChange(); ceilslope = value; updateneeded = true; } }
		public float CeilSlopeOffset { get { return ceiloffset; } set { BeforePropsChange(); ceiloffset = value; updateneeded = true; } }

		// Meridian 59 properties
		public int SectorTag { get { return sectortag; } set { BeforePropsChange(); sectortag = value; updateneeded = true; } }
		public int AnimationSpeed { get { return animationspeed; } set { BeforePropsChange(); animationspeed = value; updateneeded = true; } }
		public int Depth { get { return depth; } set { BeforePropsChange(); depth = value; updateneeded = true; } }
		public bool ScrollFloor { get { return scrollfloor; } set { BeforePropsChange(); scrollfloor = value; updateneeded = true; } }
		public bool ScrollCeiling { get { return scrollceiling; } set { BeforePropsChange(); scrollceiling = value; updateneeded = true; } }
		public bool Flicker { get { return flicker; } set { BeforePropsChange(); flicker = value; updateneeded = true; } }
		public int FloorTexRot { get { return floortexrot; } set { BeforePropsChange(); floortexrot = value; updateneeded = true; } }
		public int CeilTexRot { get { return ceiltexrot; } set { BeforePropsChange(); ceiltexrot = value; updateneeded = true; } }
		public List<Vector3D> FloorSlopeVertexes { get { return floorslopevertexes; } set { BeforePropsChange(); floorslopevertexes = value; updateneeded = true; } }
		public List<Vector3D> CeilSlopeVertexes { get { return ceilslopevertexes; } set { BeforePropsChange(); ceilslopevertexes = value; updateneeded = true; } }
		public SDScrollFlags ScrollFlags
		{
			get { return scrollflags; }
			set
			{
				scrollflags.Speed = value.Speed;
				scrollflags.Direction = value.Direction;
			}
		}

		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		internal Sector(MapSet map, int listindex, int index)
		{
			// Initialize
			this.elementtype = MapElementType.SECTOR; //mxd
			this.map = map;
			this.listindex = listindex;
			this.sidedefs = new LinkedList<Sidedef>();
			this.fixedindex = index;
			this.floortexname = "-";
			this.ceiltexname = "-";
			this.longfloortexname = MapSet.EmptyLongName;
			this.longceiltexname = MapSet.EmptyLongName;
			this.flags = new Dictionary<string, bool>(StringComparer.Ordinal); //mxd
			this.tags = new List<int> { 0 }; //mxd
			this.updateneeded = true;
			this.triangulationneeded = true;
			this.triangles = new Triangulation(); //mxd
			this.surfaceentries = new SurfaceEntryCollection();

			if(map == General.Map.Map)
				General.Map.UndoRedo.RecAddSector(this);

			this.sectortag = 0;
			this.depth = 0;
			this.animationspeed = 0;
			this.flicker = false;
			this.scrollceiling = false;
			this.scrollfloor = false;
			this.scrollflags = new SDScrollFlags();
			this.floorslopevertexes = new List<Vector3D>();
			this.ceilslopevertexes = new List<Vector3D>();

			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// Disposer
		public override void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Already set isdisposed so that changes can be prohibited
				isdisposed = true;
				
				// Dispose the sidedefs that are attached to this sector
				// because a sidedef cannot exist without reference to its sector.
				if(map.AutoRemove)
					foreach(Sidedef sd in sidedefs) sd.Dispose();
				else
					foreach(Sidedef sd in sidedefs) sd.SetSectorP(null);
				
				if(map == General.Map.Map)
					General.Map.UndoRedo.RecRemSector(this);

				// Remove from main list
				map.RemoveSector(listindex);
				
				// Register the index as free
				map.AddSectorIndexHole(fixedindex);
				
				// Free surface entry
				General.Map.CRenderer2D.Surfaces.FreeSurfaces(surfaceentries);

				// Clean up
				sidedefs = null;
				map = null;

				//mxd. Restore isdisposed so base classes can do their disposal job
				isdisposed = false;
				
				// Dispose base
				base.Dispose();
			}
		}

		#endregion

		#region ================== Management

		// Call this before changing properties
		protected override void BeforePropsChange()
		{
			if(map == General.Map.Map)
				General.Map.UndoRedo.RecPrpSector(this);
		}

		// Serialize / deserialize (passive: this doesn't record)
		new internal void ReadWrite(IReadWriteStream s)
		{
			if(!s.IsWriting)
			{
				BeforePropsChange();
				updateneeded = true;
			}
			
			base.ReadWrite(s);

			//mxd
			if(s.IsWriting)
			{
				s.wInt(flags.Count);

				foreach(KeyValuePair<string, bool> f in flags)
				{
					s.wString(f.Key);
					s.wBool(f.Value);
				}
			}
			else
			{
				int c; s.rInt(out c);

				flags = new Dictionary<string, bool>(c, StringComparer.Ordinal);
				for(int i = 0; i < c; i++)
				{
					string t; s.rString(out t);
					bool b; s.rBool(out b);
					flags.Add(t, b);
				}
			}

			s.rwInt(ref fixedindex);
			s.rwInt(ref floorheight);
			s.rwInt(ref ceilheight);
			s.rwString(ref floortexname);
			s.rwString(ref ceiltexname);
			s.rwLong(ref longfloortexname);
			s.rwLong(ref longceiltexname);
			s.rwInt(ref effect);
			s.rwInt(ref brightness);

			if (General.Map.MERIDIAN)
			{
				s.rwInt(ref offsetx);
				s.rwInt(ref offsety);
				s.rwInt(ref sectortag);
				s.rwInt(ref depth);
				s.rwInt(ref animationspeed);
				s.rwBool(ref flicker);
				s.rwBool(ref scrollceiling);
				s.rwBool(ref scrollfloor);
				s.rwInt(ref floortexrot);
				s.rwInt(ref ceiltexrot);
				if (s.IsWriting)
				{
					s.wInt(scrollflags.Speed);
					s.wInt(scrollflags.Direction);
				}
				else
				{
					int temp = 0;
					s.rInt(out temp);
					scrollflags.Speed = temp;
					s.rInt(out temp);
					scrollflags.Direction = temp;
				}
			}
			//mxd. (Re)store tags
			if(s.IsWriting) 
			{
				s.wInt(tags.Count);
				foreach(int tag in tags) s.wInt(tag);
			} 
			else 
			{
				int c;
				s.rInt(out c);
				tags = new List<int>(c);
				for(int i = 0; i < c; i++)
				{
					int t;
					s.rInt(out t);
					tags.Add(t);
				}
			}

			//mxd. Slopes
			s.rwFloat(ref flooroffset);
			s.rwVector3D(ref floorslope);
			s.rwFloat(ref ceiloffset);
			s.rwVector3D(ref ceilslope);
		}
		
		// After deserialization
		internal void PostDeserialize(MapSet map)
		{
			triangles.PostDeserialize(map);
			updateneeded = true;
			triangulationneeded = true;
		}
		
		// This copies all properties to another sector
		public void CopyPropertiesTo(Sector s)
		{
			s.BeforePropsChange();
			
			// Copy properties
			s.ceilheight = ceilheight;
			s.ceiltexname = ceiltexname;
			s.longceiltexname = longceiltexname;
			s.floorheight = floorheight;
			s.floortexname = floortexname;
			s.longfloortexname = longfloortexname;
			s.effect = effect;
			s.tags = new List<int>(tags); //mxd
			s.flags = new Dictionary<string, bool>(flags); //mxd
			s.brightness = brightness;
			s.flooroffset = flooroffset; //mxd
			s.floorslope = floorslope; //mxd
			s.ceiloffset = ceiloffset; //mxd
			s.ceilslope = ceilslope; //mxd

			if (General.Map.MERIDIAN)
			{
				s.offsetx = offsetx;
				s.offsety = offsety;
				s.sectortag = sectortag;
				s.depth = depth;
				s.animationspeed = animationspeed;
				s.flicker = flicker;
				s.scrollceiling = scrollceiling;
				s.scrollfloor = scrollfloor;
				s.scrollflags = new SDScrollFlags(scrollflags.Speed, scrollflags.Direction);
				s.floortexrot = floortexrot;
				s.ceiltexrot = ceiltexrot;
				s.floorslopevertexes = floorslopevertexes;
				s.ceilslopevertexes = ceilslopevertexes;
			}

			s.updateneeded = true;
			base.CopyPropertiesTo(s);
		}

		// This attaches a sidedef and returns the listitem
		internal LinkedListNode<Sidedef> AttachSidedefP(Sidedef sd)
		{
			updateneeded = true;
			triangulationneeded = true;
			return sidedefs.AddLast(sd);
		}

		// This detaches a sidedef
		internal void DetachSidedefP(LinkedListNode<Sidedef> l)
		{
			// Not disposing?
			if(!isdisposed)
			{
				// Remove sidedef
				updateneeded = true;
				triangulationneeded = true;
				sidedefs.Remove(l);

				// No more sidedefs left?
				if((sidedefs.Count == 0) && map.AutoRemove)
				{
					// This sector is now useless, dispose it
					this.Dispose();
				}
			}
		}
		
		// This triangulates the sector geometry
		internal void Triangulate()
		{
			if(updateneeded)
			{
				// Triangulate again?
				if(triangulationneeded || (triangles == null))
				{
					// Triangulate sector
					triangles = Triangulation.Create(this);
					triangulationneeded = false;
					updateneeded = true;
					
					// Make label positions
					labels = Array.AsReadOnly(Tools.FindLabelPositions(this).ToArray());
					
					// Number of vertices changed?
					if(triangles.Vertices.Count != surfaceentries.totalvertices)
						General.Map.CRenderer2D.Surfaces.FreeSurfaces(surfaceentries);
				}
			}
		}
		
		// This makes new vertices as well as floor and ceiling surfaces
		internal void CreateSurfaces()
		{
			if(updateneeded)
			{
				// Brightness color
				int brightint = General.Map.Renderer2D.CalculateBrightness(brightness);

				// Make vertices
				flatvertices = new FlatVertex[triangles.Vertices.Count];
				for(int i = 0; i < triangles.Vertices.Count; i++)
				{
					flatvertices[i].x = triangles.Vertices[i].x;
					flatvertices[i].y = triangles.Vertices[i].y;
					flatvertices[i].z = 1.0f;
					flatvertices[i].c = brightint;
					flatvertices[i].u = triangles.Vertices[i].x;
					flatvertices[i].v = triangles.Vertices[i].y;
				}

				// Create bounding box
				bbox = CreateBBox();
				
				// Make update info (this lets the plugin fill in texture coordinates and such)
				SurfaceUpdate updateinfo = new SurfaceUpdate(flatvertices.Length, true, true);
				flatvertices.CopyTo(updateinfo.floorvertices, 0);
				General.Plugins.OnSectorFloorSurfaceUpdate(this, ref updateinfo.floorvertices);
				flatvertices.CopyTo(updateinfo.ceilvertices, 0);
				General.Plugins.OnSectorCeilingSurfaceUpdate(this, ref updateinfo.ceilvertices);
				updateinfo.floortexture = longfloortexname;
				updateinfo.ceiltexture = longceiltexname;
				updateinfo.offsetx = offsetx;
				updateinfo.offsety = offsety;

				// Update surfaces
				General.Map.CRenderer2D.Surfaces.UpdateSurfaces(surfaceentries, updateinfo);

				// Updated
				updateneeded = false;
			}
		}

		// This updates the floor surface
		public void UpdateFloorSurface()
		{
			if(flatvertices == null) return;
			
			// Create floor vertices
			SurfaceUpdate updateinfo = new SurfaceUpdate(flatvertices.Length, true, false);
			flatvertices.CopyTo(updateinfo.floorvertices, 0);
			General.Plugins.OnSectorFloorSurfaceUpdate(this, ref updateinfo.floorvertices);
			updateinfo.floortexture = longfloortexname;
			updateinfo.offsetx = offsetx;
			updateinfo.offsety = offsety;
			// Update entry
			General.Map.CRenderer2D.Surfaces.UpdateSurfaces(surfaceentries, updateinfo);
			General.Map.CRenderer2D.Surfaces.UnlockBuffers();
		}

		// This updates the ceiling surface
		public void UpdateCeilingSurface()
		{
			if(flatvertices == null) return;

			// Create ceiling vertices
			SurfaceUpdate updateinfo = new SurfaceUpdate(flatvertices.Length, false, true);
			flatvertices.CopyTo(updateinfo.ceilvertices, 0);
			General.Plugins.OnSectorCeilingSurfaceUpdate(this, ref updateinfo.ceilvertices);
			updateinfo.ceiltexture = longceiltexname;
			updateinfo.offsetx = offsetx;
			updateinfo.offsety = offsety;
			// Update entry
			General.Map.CRenderer2D.Surfaces.UpdateSurfaces(surfaceentries, updateinfo);
			General.Map.CRenderer2D.Surfaces.UnlockBuffers();
		}
		
		// This updates the sector when changes have been made
		public void UpdateCache()
		{
			// Update if needed
			if(updateneeded)
			{
				Triangulate();
				
				CreateSurfaces();

				General.Map.CRenderer2D.Surfaces.UnlockBuffers();
			}
		}

		// Selected
		protected override void DoSelect()
		{
			base.DoSelect();
			selecteditem = map.SelectedSectors.AddLast(this);
		}

		// Deselect
		protected override void DoUnselect()
		{
			base.DoUnselect();
			if(selecteditem.List != null) selecteditem.List.Remove(selecteditem);
			selecteditem = null;
		}

		public bool IsNoAmbient()
		{
			return brightness < 128;
		}

		public int NoAmbientBrightness()
		{
			return brightness * 2;
		}

		public int AmbientBrightness()
		{
			return (brightness - 128) * 2;
		}

		// This removes UDMF stuff (mxd)
		internal void TranslateFromUDMF() 
		{
			// Clear UDMF-related properties (but keep VirtualSectorField!)
			bool isvirtual = this.Fields.ContainsKey(MapSet.VirtualSectorField);
			this.Fields.Clear();
			if(isvirtual) this.Fields.Add(MapSet.VirtualSectorField, MapSet.VirtualSectorValue);
			this.Flags.Clear();
			this.fogmode = SectorFogMode.NONE;

			// Reset Slopes
			floorslope = new Vector3D();
			flooroffset = 0;
			ceilslope = new Vector3D();
			ceiloffset = 0;
		}
		
		#endregion
		
		#region ================== Methods

		// This checks and returns a flag without creating it
		public bool IsFlagSet(string flagname)
		{
			return flags.ContainsKey(flagname) && flags[flagname];
		}

		// This sets a flag
		public void SetFlag(string flagname, bool value) 
		{
			if(!flags.ContainsKey(flagname) || (IsFlagSet(flagname) != value)) 
			{
				BeforePropsChange();

				flags[flagname] = value;
			}
		}

		// This returns a copy of the flags dictionary
		public Dictionary<string, bool> GetFlags() 
		{
			return new Dictionary<string, bool>(flags);
		}

		//mxd. This returns enabled flags
		public HashSet<string> GetEnabledFlags()
		{
			HashSet<string> result = new HashSet<string>();
			foreach(KeyValuePair<string, bool> group in flags)
				if(group.Value) result.Add(group.Key);
			return result;
		} 

		// This clears all flags
		public void ClearFlags() 
		{
			BeforePropsChange();
			flags.Clear();
		}
		
		// This checks if the given point is inside the sector polygon
		// See: http://paulbourke.net/geometry/polygonmesh/index.html#insidepoly
		public bool Intersect(Vector2D p) 
		{
			//mxd. Check bounding box first
			if(p.x < bbox.Left || p.x > bbox.Right || p.y < bbox.Top || p.y > bbox.Bottom) return false;
			
			uint c = 0;
			Vector2D v1, v2;
			
			// Go for all sidedefs
			foreach(Sidedef sd in sidedefs)
			{
				// Get vertices
				v1 = sd.Line.Start.Position;
				v2 = sd.Line.End.Position;

				//mxd. On top of a vertex?
				if(p == v1 || p == v2) return true;

				// Check for intersection
				if(v1.y != v2.y //mxd. If line is not horizontal...
				  && p.y >  (v1.y < v2.y ? v1.y : v2.y) //mxd. ...And test point y intersects with the line y bounds...
				  && p.y <= (v1.y > v2.y ? v1.y : v2.y) //mxd
				  && (p.x < (v1.x < v2.x ? v1.x : v2.x) || (p.x <= (v1.x > v2.x ? v1.x : v2.x) //mxd. ...And test point x is to the left of the line, or is inside line x bounds and intersects it
						&& (v1.x == v2.x || p.x <= ((p.y - v1.y) * (v2.x - v1.x) / (v2.y - v1.y) + v1.x)))))
					c++; //mxd. ...Count the line as crossed
			}

			// Inside this polygon when we crossed odd number of polygon lines
			return (c % 2 != 0);
		}
		
		// This creates a bounding box rectangle
		// This requires the sector triangulation to be up-to-date!
		private RectangleF CreateBBox()
		{
			if(sidedefs.Count == 0) return new RectangleF(); //mxd
			
			// Setup
			float left = float.MaxValue;
			float top = float.MaxValue;
			float right = float.MinValue;
			float bottom = float.MinValue;

			HashSet<Vertex> processed = new HashSet<Vertex>(); //mxd

			//mxd. This way bbox will be created even if triangulation failed (sector with 2 or less sidedefs and 2 vertices)
			foreach(Sidedef s in sidedefs) 
			{
				//start...
				if(!processed.Contains(s.Line.Start)) 
				{
					if(s.Line.Start.Position.x < left) left = s.Line.Start.Position.x;
					if(s.Line.Start.Position.x > right) right = s.Line.Start.Position.x;
					if(s.Line.Start.Position.y < top) top = s.Line.Start.Position.y;
					if(s.Line.Start.Position.y > bottom) bottom = s.Line.Start.Position.y;
					processed.Add(s.Line.Start);
				}

				//end...
				if(!processed.Contains(s.Line.End)) 
				{
					if(s.Line.End.Position.x < left) left = s.Line.End.Position.x;
					if(s.Line.End.Position.x > right) right = s.Line.End.Position.x;
					if(s.Line.End.Position.y < top) top = s.Line.End.Position.y;
					if(s.Line.End.Position.y > bottom) bottom = s.Line.End.Position.y;
					processed.Add(s.Line.End);
				}
			}
			
			// Return rectangle
			return new RectangleF(left, top, right - left, bottom - top);
		}

		//mxd
		internal void UpdateBBox()
		{
			bbox = CreateBBox();
		}
		
		// This joins the sector with another sector
		// This sector will be disposed
		public void Join(Sector other)
		{
			// Any sidedefs to move?
			if(sidedefs.Count > 0)
			{
				// Change secter reference on my sidedefs
				// This automatically disposes this sector
				while(sidedefs != null)
					sidedefs.First.Value.SetSector(other);
			}
			else
			{
				// No sidedefs attached
				// Dispose manually
				this.Dispose();
			}
			
			General.Map.IsChanged = true;
		}

		public List<Vertex> GetVertexes()
		{
			List<Vertex> vl = new List<Vertex>();

			foreach (Sidedef s in Sidedefs)
			{
				if (vl.Count == 0)
				{
					vl.Add(s.Line.Start);
					vl.Add(s.Line.End);
				}
				else
				{
					if (!vl.Contains(s.Line.Start))
						vl.Add(s.Line.Start);
					if (!vl.Contains(s.Line.End))
						vl.Add(s.Line.End);
				}
			}

			return vl;
		}

		/// <summary>
		/// Return Vector2D pivot, let caller calculate an accurate Z.
		/// </summary>
		/// <returns></returns>
		public Vector2D GetFloorSlopePivot()
		{
			if (floorslopevertexes.Count != 3)
				return new Vector2D(0, 0);
			return floorslopevertexes[0];
		}

		/// <summary>
		/// Return Vector2D pivot, let caller calculate an accurate Z.
		/// </summary>
		/// <returns></returns>
		public Vector2D GetCeilSlopePivot()
		{
			if (ceilslopevertexes.Count != 3)
				return new Vector2D(0, 0);
			return ceilslopevertexes[0];
		}

		public bool IsFloorSloped()
		{
			return (floorslope.GetLengthSq() > 0 && !float.IsNaN(FloorSlopeOffset / floorslope.z));
		}

		public bool IsCeilSloped()
		{
			return (ceilslope.GetLengthSq() > 0 && !float.IsNaN(CeilSlopeOffset / ceilslope.z));
		}

		/// <summary>
		/// Recalculates a Meridian 59-style slope from floor or ceiling vertexes.
		/// </summary>
		/// <param name="floor"></param>
		public void CalculateMeridianSlope(bool floor)
		{
			double[] u = new double[3];
			double[] v = new double[3];
			double[] uv = new double[3];
			Vector3D[] p = new Vector3D[3];
			double ucrossv;

			int i = 0;
			if (floor)
			{
				foreach (Vector3D V in floorslopevertexes)
				{
					p[i].x = V.x;
					p[i].y = V.y;
					p[i].z = V.z;
					++i;
				}
			}
			else
			{
				foreach (Vector3D V in ceilslopevertexes)
				{
					p[i].x = V.x;
					p[i].y = V.y;
					p[i].z = V.z;
					++i;
				}
			}

			u[0] = p[1].x - p[0].x;
			u[1] = p[1].y - p[0].y;
			u[2] = p[1].z - p[0].z;
			v[0] = p[2].x - p[0].x;
			v[1] = p[2].y - p[0].y;
			v[2] = p[2].z - p[0].z;
			uv[0] = u[2] * v[1] - u[1] * v[2];
			uv[1] = u[0] * v[2] - u[2] * v[0];
			uv[2] = u[1] * v[0] - u[0] * v[1];
			ucrossv = Math.Sqrt(uv[0] * uv[0] + uv[1] * uv[1] + uv[2] * uv[2]);
			if (floor)
			{
				floorslope.x = (float)(uv[0] / ucrossv);
				floorslope.y = (float)(uv[1] / ucrossv);
				floorslope.z = (float)(uv[2] / ucrossv);
				flooroffset = -(floorslope.x * p[0].x + floorslope.y * p[0].y + floorslope.z * p[0].z);
				if (floorslope.z < 0)
				{
					// normals of floors must point up
					floorslope.x = -floorslope.x;
					floorslope.y = -floorslope.y;
					floorslope.z = -floorslope.z;
					flooroffset = -flooroffset;
				}
			}
			else
			{
				ceilslope.x = (float)(uv[0] / ucrossv);
				ceilslope.y = (float)(uv[1] / ucrossv);
				ceilslope.z = (float)(uv[2] / ucrossv);
				ceiloffset = -(ceilslope.x * p[0].x + ceilslope.y * p[0].y + ceilslope.z * p[0].z);
				if (ceilslope.z > 0)
				{
					// normals of ceilings must point down
					ceilslope.x = -ceilslope.x;
					ceilslope.y = -ceilslope.y;
					ceilslope.z = -ceilslope.z;
					ceiloffset = -ceiloffset;
				}
			}

			//mxd. Map is changed
			General.Map.IsChanged = true;
			updateneeded = true;
		}

		/// <summary>
		/// Removes a meridian slope.
		/// </summary>
		/// <param name="floor"></param>
		public void RemoveMeridianSlope(bool floor)
		{
			if (floor)
			{
				flooroffset = 0f;
				floorslope.x = 0f;
				floorslope.y = 0f;
				floorslope.z = 0f;
				floortexrot = 0;
				floorslopevertexes.Clear();
			}
			else
			{
				ceiloffset = 0f;
				ceilslope.x = 0f;
				ceilslope.y = 0f;
				ceilslope.z = 0f;
				ceiltexrot = 0;
				ceilslopevertexes.Clear();
			}
		}

		//mxd
		public static Geometry.Plane GetFloorPlane(Sector s)
		{
			if (General.Map.UDMF || General.Map.MERIDIAN)
			{
				// UDMF Sector slope?
				if(s.FloorSlope.GetLengthSq() > 0 && !float.IsNaN(s.FloorSlopeOffset / s.FloorSlope.z)) 
					return new Geometry.Plane(s.FloorSlope, s.FloorSlopeOffset);

				if(s.sidedefs.Count == 3)
				{
					Geometry.Plane floor = new Geometry.Plane(new Vector3D(0, 0, 1), -s.FloorHeight);
					Vector3D[] verts = new Vector3D[3];
					bool sloped = false;
					int index = 0;
					
					// Check vertices
					foreach(Sidedef sd in s.Sidedefs) 
					{
						Vertex v = sd.IsFront ? sd.Line.End : sd.Line.Start;

						//create "normal" vertices
						verts[index] = new Vector3D(v.Position);

						// Check floor
						if(!float.IsNaN(v.ZFloor)) 
						{
							//vertex offset is absolute
							verts[index].z = v.ZFloor;
							sloped = true;
						} 
						else 
						{
							verts[index].z = floor.GetZ(v.Position);
						}

						index++;
					}

					// Have slope?
					return (sloped ? new Geometry.Plane(verts[0], verts[1], verts[2], true) : floor);
				}
			}

			// Have line slope?
			foreach(Sidedef side in s.sidedefs)
			{
				// Carbon copy of EffectLineSlope class here...
				if(side.Line.Action == 181 && ((side.Line.Args[0] == 1 && side == side.Line.Front) || side.Line.Args[0] == 2) && side.Other != null)
				{
					Linedef l = side.Line;
					
					// Find the vertex furthest from the line
					Vertex foundv = null;
					float founddist = -1.0f;
					foreach(Sidedef sd in s.Sidedefs) 
					{
						Vertex v = sd.IsFront ? sd.Line.Start : sd.Line.End;
						float d = l.DistanceToSq(v.Position, false);
						if(d > founddist) 
						{
							foundv = v;
							founddist = d;
						}
					}

					Vector3D v1 = new Vector3D(l.Start.Position.x, l.Start.Position.y, side.Other.Sector.FloorHeight);
					Vector3D v2 = new Vector3D(l.End.Position.x, l.End.Position.y, side.Other.Sector.FloorHeight);
					Vector3D v3 = new Vector3D(foundv.Position.x, foundv.Position.y, s.FloorHeight);

					return (l.SideOfLine(v3) < 0.0f ? new Geometry.Plane(v1, v2, v3, true) : new Geometry.Plane(v2, v1, v3, true));
				}
			}

			//TODO: other types of slopes...

			// Normal (flat) floor plane
			return new Geometry.Plane(new Vector3D(0, 0, 1), -s.FloorHeight);
		}

		//mxd
		public static Geometry.Plane GetCeilingPlane(Sector s)
		{
			if (General.Map.UDMF || General.Map.MERIDIAN)
			{
				// UDMF Sector slope?
				if(s.CeilSlope.GetLengthSq() > 0 && !float.IsNaN(s.CeilSlopeOffset / s.CeilSlope.z))
					return new Geometry.Plane(s.CeilSlope, s.CeilSlopeOffset);

				if(s.sidedefs.Count == 3) 
				{
					Geometry.Plane ceiling = new Geometry.Plane(new Vector3D(0, 0, -1), s.CeilHeight);
					Vector3D[] verts = new Vector3D[3];
					bool sloped = false;
					int index = 0;

					// Check vertices
					foreach(Sidedef sd in s.Sidedefs) 
					{
						Vertex v = sd.IsFront ? sd.Line.End : sd.Line.Start;

						//create "normal" vertices
						verts[index] = new Vector3D(v.Position);

						// Check floor
						if(!float.IsNaN(v.ZCeiling)) 
						{
							//vertex offset is absolute
							verts[index].z = v.ZCeiling;
							sloped = true;
						} 
						else 
						{
							verts[index].z = ceiling.GetZ(v.Position);
						}

						index++;
					}

					// Have slope?
					return (sloped ? new Geometry.Plane(verts[0], verts[2], verts[1], false) : ceiling);
				}
			}

			// Have line slope?
			foreach(Sidedef side in s.sidedefs) 
			{
				// Carbon copy of EffectLineSlope class here...
				if(side.Line.Action == 181 && ((side.Line.Args[1] == 1 && side == side.Line.Front) || side.Line.Args[1] == 2) && side.Other != null) 
				{
					Linedef l = side.Line;

					// Find the vertex furthest from the line
					Vertex foundv = null;
					float founddist = -1.0f;
					foreach(Sidedef sd in s.Sidedefs) 
					{
						Vertex v = sd.IsFront ? sd.Line.Start : sd.Line.End;
						float d = l.DistanceToSq(v.Position, false);
						if(d > founddist) 
						{
							foundv = v;
							founddist = d;
						}
					}

					Vector3D v1 = new Vector3D(l.Start.Position.x, l.Start.Position.y, side.Other.Sector.CeilHeight);
					Vector3D v2 = new Vector3D(l.End.Position.x, l.End.Position.y, side.Other.Sector.CeilHeight);
					Vector3D v3 = new Vector3D(foundv.Position.x, foundv.Position.y, s.CeilHeight);

					return (l.SideOfLine(v3) > 0.0f ? new Geometry.Plane(v1, v2, v3, false) : new Geometry.Plane(v2, v1, v3, false));
				}
			}

			//TODO: other types of slopes...

			// Normal (flat) ceiling plane
			return new Geometry.Plane(new Vector3D(0, 0, -1), s.CeilHeight);
		}

		// String representation
		public override string ToString()
		{
#if DEBUG
			return "Sector " + listindex + (marked ? " (marked)" : ""); //mxd
#else
			return "Sector " + listindex;
#endif
		}
		
		#endregion

		#region ================== Changes

		// Meridian specific version.
		public void Update(int hfloor, int hceil, int offsetx, int offsety, string tfloor, string tceil,
			float floorOffset, float ceilOffset, int texRotFloor, int texRotCeil, Vector3D fl, Vector3D cl,
			int tag, int brightness, int depth, int animationspeed, bool flicker,
			bool scrollfloor, bool scrollceiling)
		{
			this.sectortag = tag;
			this.depth = depth;
			this.animationspeed = animationspeed;
			this.flicker = flicker;
			this.scrollfloor = scrollfloor;
			this.scrollceiling = scrollceiling;
			this.offsetx = offsetx;
			this.offsety = offsety;
			this.floortexrot = texRotFloor;
			this.ceiltexrot = texRotCeil;
			Update(hfloor, hceil, tfloor, tceil, 0, new Dictionary<string, bool>(StringComparer.Ordinal),
				new List<int> { 0 }, brightness, floorOffset, fl, ceilOffset, cl);
		}

		//mxd. This updates all properties (Doom/Hexen version)
		public void Update(int hfloor, int hceil, string tfloor, string tceil, int effect, int tag, int brightness) 
		{
			Update(hfloor, hceil, tfloor, tceil, effect, new Dictionary<string, bool>(StringComparer.Ordinal), new List<int> { tag }, brightness, 0, new Vector3D(), 0, new Vector3D());
		}

		//mxd. This updates all properties (UDMF version)
		public void Update(int hfloor, int hceil, string tfloor, string tceil, int effect, Dictionary<string, bool> flags, List<int> tags, int brightness, float flooroffset, Vector3D floorslope, float ceiloffset, Vector3D ceilslope)
		{
			BeforePropsChange();
			
			// Apply changes
			this.floorheight = hfloor;
			this.ceilheight = hceil;
			this.effect = effect;
			this.tags = new List<int>(tags); //mxd
			this.flags = new Dictionary<string, bool>(flags); //mxd
			this.brightness = brightness;
			this.flooroffset = flooroffset; //mxd
			this.floorslope = floorslope; //mxd
			this.ceiloffset = ceiloffset; //mxd
			this.ceilslope = ceilslope; //mxd

			//mxd. Set ceil texture
			if(string.IsNullOrEmpty(tceil)) tceil = "-";
			ceiltexname = tceil;
			longceiltexname = Lump.MakeLongName(ceiltexname);

			//mxd. Set floor texture
			if(string.IsNullOrEmpty(tfloor)) tfloor = "-"; //mxd
			floortexname = tfloor;
			longfloortexname = Lump.MakeLongName(tfloor);

			//mxd. Map is changed
			General.Map.IsChanged = true;
			updateneeded = true;
		}

		// This sets texture
		public void SetFloorTexture(string name)
		{
			BeforePropsChange();
			
			if(string.IsNullOrEmpty(name)) name = "-"; //mxd
			floortexname = name;
			longfloortexname = Lump.MakeLongName(name);
			updateneeded = true;
			General.Map.IsChanged = true;
		}

		// This sets texture
		public void SetCeilTexture(string name)
		{
			BeforePropsChange();
			
			if(string.IsNullOrEmpty(name)) name = "-"; //mxd
			ceiltexname = name;
			longceiltexname = Lump.MakeLongName(name);
			updateneeded = true;
			General.Map.IsChanged = true;
		}

		//mxd
		public void UpdateFogColor() 
		{
			if(General.Map.UDMF && Fields.ContainsKey("fadecolor"))
			{
				fogcolor = new Color4((int)Fields["fadecolor"].Value);
				fogmode = SectorFogMode.FADE;
			}
			// Sector uses outisde fog when it's ceiling is sky or Sector_Outside effect (87) is set
			else if(General.Map.Data.MapInfo.HasOutsideFogColor && 
				(ceiltexname == General.Map.Config.SkyFlatName || (effect == 87 && General.Map.Config.SectorEffects.ContainsKey(effect))))
			{
				fogcolor = General.Map.Data.MapInfo.OutsideFogColor;
				fogmode = SectorFogMode.OUTSIDEFOGDENSITY;
			}
			else if(General.Map.Data.MapInfo.HasFadeColor)
			{
				fogcolor = General.Map.Data.MapInfo.FadeColor;
				fogmode = SectorFogMode.FOGDENSITY;
			}
			else
			{
				fogcolor = new Color4();
				fogmode = (brightness < 248 ? SectorFogMode.CLASSIC : SectorFogMode.NONE);
			}
		}
		
		#endregion
	}
}

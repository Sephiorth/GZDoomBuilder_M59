	
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
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.VisualModes;
using CodeImp.DoomBuilder.Data;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	internal sealed class VisualFloor : BaseVisualGeometrySector
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		private bool innerside; //mxd

		#endregion

		#region ================== Properties

		#endregion

		#region ================== Constructor / Setup

		// Constructor
		public VisualFloor(BaseVisualMode mode, VisualSector vs) : base(mode, vs)
		{
			//mxd
			geometrytype = VisualGeometryType.FLOOR;
			partname = "floor";
			performautoselection = mode.UseSelectionFromClassicMode && vs != null && vs.Sector.Selected && (General.Map.ViewMode == ViewMode.FloorTextures || General.Map.ViewMode == ViewMode.Normal);
			
			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// This builds the geometry. Returns false when no geometry created.
		public override bool Setup(SectorLevel level, Effect3DFloor extrafloor) 
		{
			return Setup(level, extrafloor, innerside);
		}

		//mxd
		public bool Setup(SectorLevel level, Effect3DFloor extrafloor, bool innerside)
		{
			Sector s = level.sector;
			Vector2D texscale;
			this.innerside = innerside;
			
			base.Setup(level, extrafloor);
			
			// Fetch ZDoom fields
			float rotate;
			Vector2D offset;
			if (General.Map.MERIDIAN)
			{
				offset = new Vector2D(level.sector.OffsetX, level.sector.OffsetY);
				rotate = Angle2D.DegToRad(s.FloorTexRot);
			}
			else
			{
				offset = new Vector2D(s.Fields.GetValue("xpanningfloor", 0.0f),
									  s.Fields.GetValue("ypanningfloor", 0.0f));
				rotate = Angle2D.DegToRad(s.Fields.GetValue("rotationfloor", 0.0f));
			}
			Vector2D scale = new Vector2D(s.Fields.GetValue("xscalefloor", 1.0f),
										  s.Fields.GetValue("yscalefloor", 1.0f));
			
			//Load floor texture
			if(s.LongFloorTexture != MapSet.EmptyLongName)
			{
				base.Texture = General.Map.Data.GetFlatImage(s.LongFloorTexture);
				if(base.Texture == null || base.Texture is UnknownImage)
				{
					base.Texture = General.Map.Data.UnknownTexture3D;
					setuponloadedtexture = s.LongFloorTexture;
				}
				else if(!base.Texture.IsImageLoaded)
				{
					setuponloadedtexture = s.LongFloorTexture;
				}
			}
			else
			{
				// Use missing texture
				base.Texture = General.Map.Data.MissingTexture3D;
				setuponloadedtexture = 0;
			}

			// Determine texture scale
			if(base.Texture.IsImageLoaded)
				texscale = new Vector2D(1.0f / base.Texture.ScaledWidth, 1.0f / base.Texture.ScaledHeight);
			else
				texscale = new Vector2D(1.0f / 64.0f, 1.0f / 64.0f);

			// Determine brightness
			int color = PixelColor.FromInt(level.color).WithAlpha((byte)General.Clamp(level.alpha, 0, 255)).ToInt();

			//mxd. Top extrafloor level should calculate fogdensity
			//from the brightness of the level above it
			int targetbrightness;
			if(extrafloor != null && extrafloor.VavoomType && !level.disablelighting)
			{
				targetbrightness = 0;
				SectorData sd = mode.GetSectorData(this.Sector.Sector);
				for(int i = 0; i < sd.LightLevels.Count - 1; i++)
				{
					if(sd.LightLevels[i] == level)
					{
						targetbrightness = sd.LightLevels[i + 1].brightnessbelow;
						break;
					}
				}
			}
			else
			{
				targetbrightness = level.brightnessbelow;
			}

			//mxd. Determine fog density
			fogfactor = CalculateFogFactor(targetbrightness);

			// Make vertices
			ReadOnlyCollection<Vector2D> triverts = Sector.Sector.Triangles.Vertices;
			WorldVertex[] verts = new WorldVertex[triverts.Count];

			Vector3D TextureOrientation, planeNormal, P0, P1, P2, v1, v2;
			float z;

			// texture angle - this is planar angle between x axis of texture & x axis of world
			// convert angle to vector
			Vector2D pivot = s.GetFloorSlopePivot();
			TextureOrientation = new Vector3D((float)Math.Sin(rotate), (float)Math.Cos(rotate), 0);
			planeNormal = new Vector3D(s.FloorSlope.x, s.FloorSlope.y, s.FloorSlope.z);
			z = (-s.FloorSlope.x * pivot.x - s.FloorSlope.y * pivot.y - s.FloorSlopeOffset) / s.FloorSlope.z;
			P0 = new Vector3D(pivot.x, pivot.y, z);
			//z = (-s.FloorSlope.x * verts[0].x - s.FloorSlope.y * verts[0].y - s.FloorSlopeOffset) / s.FloorSlope.z;
			//P0 = new Vector3D(verts[0].x, verts[0].y, z);
			// cross normal with texture orientation to get vector perpendicular to texture
			//  orientation and normal = v axis direction
			v2 = Vector3D.CrossProduct(planeNormal, TextureOrientation);
			v1 = Vector3D.CrossProduct(v2, planeNormal);
			P1 = P0 + v1;
			P2 = P0 + v2;

			bool isSloped = s.FloorSlope.GetLengthSq() > 0 && !float.IsNaN(s.FloorSlopeOffset / s.FloorSlope.z);
			if (isSloped)
			{
				offset.x = offset.x / base.Texture.Width;
				offset.y = offset.y / base.Texture.Height;
			}

			for(int i = 0; i < triverts.Count; i++)
			{
				// Color shading
				verts[i].c = color; //mxd
				
				// Vertex coordinates
				verts[i].x = triverts[i].x;
				verts[i].y = triverts[i].y;
				verts[i].z = level.plane.GetZ(triverts[i]);
				if (General.Map.MERIDIAN && isSloped)
				{
					Vector3D iTop;
					Vector3D iLeft;
					Vector3D vectorU;
					Vector3D vectorV;
					Vector3D vector;
					float distance;
					float U, temp;

					U = ((verts[i].x - P0.x) * (P1.x - P0.x)) +
						((verts[i].z - P0.z) * (P1.z - P0.z)) +
						((verts[i].y - P0.y) * (P1.y - P0.y));
					temp = ((P1.x - P0.x) * (P1.x - P0.x)) +
						   ((P1.z - P0.z) * (P1.z - P0.z)) +
						   ((P1.y - P0.y) * (P1.y - P0.y));

					if (temp == 0.0f) temp = 1.0f;
					U /= temp;

					iTop.x = P0.x + U * (P1.x - P0.x);
					iTop.z = P0.z + U * (P1.z - P0.z);
					iTop.y = P0.y + U * (P1.y - P0.y);

					verts[i].u = (float)Math.Sqrt(
						(verts[i].x - iTop.x) * (verts[i].x - iTop.x) +
						(verts[i].z - iTop.z) * (verts[i].z - iTop.z) +
						(verts[i].y - iTop.y) * (verts[i].y - iTop.y));

					// calc distance from left line (vector v)
					U = ((verts[i].x - P0.x) * (P2.x - P0.x)) +
						((verts[i].z - P0.z) * (P2.z - P0.z)) +
						((verts[i].y - P0.y) * (P2.y - P0.y));
					temp = ((P2.x - P0.x) * (P2.x - P0.x)) +
						   ((P2.z - P0.z) * (P2.z - P0.z)) +
						   ((P2.y - P0.y) * (P2.y - P0.y));

					if (temp == 0.0f) temp = 1.0f;
					U /= temp;

					iLeft.x = P0.x + U * (P2.x - P0.x);
					iLeft.z = P0.z + U * (P2.z - P0.z);
					iLeft.y = P0.y + U * (P2.y - P0.y);

					verts[i].v = (float)Math.Sqrt(
						(verts[i].x - iLeft.x) * (verts[i].x - iLeft.x)
						+ (verts[i].z - iLeft.z) * (verts[i].z - iLeft.z)
						+ (verts[i].y - iLeft.y) * (verts[i].y - iLeft.y));

					vectorU.x = P1.x - P0.x;
					vectorU.z = P1.z - P0.z;
					vectorU.y = P1.y - P0.y;

					distance = (float)Math.Sqrt((vectorU.x * vectorU.x) + (vectorU.y * vectorU.y));
					if (distance == 0.0f) distance = 1.0f;

					vectorU.x /= distance;
					vectorU.z /= distance;
					vectorU.y /= distance;

					vectorV.x = P2.x - P0.x;
					vectorV.z = P2.z - P0.z;
					vectorV.y = P2.y - P0.y;

					distance = (float)Math.Sqrt((vectorV.x * vectorV.x) + (vectorV.y * vectorV.y));
					if (distance == 0.0f) distance = 1.0f;

					vectorV.x /= distance;
					vectorV.z /= distance;
					vectorV.y /= distance;

					vector.x = verts[i].x - P0.x;
					vector.y = verts[i].y - P0.y;

					distance = (float)Math.Sqrt((vector.x * vector.x) + (vector.y * vector.y));
					if (distance == 0.0f) distance = 1.0f;

					vector.x /= distance;
					vector.y /= distance;

					if (((vector.x * vectorU.x) +
						(vector.y * vectorU.y)) <= 0)
						verts[i].v = -verts[i].v;

					if (((vector.x * vectorV.x) +
						(vector.y * vectorV.y)) > 0)
						verts[i].u = -verts[i].u;

					verts[i].u *= 1.0f / (float)(4 << 4);
					verts[i].v *= 1.0f / (float)(4 << 4);
					verts[i].u += offset.x;
					verts[i].v += offset.y;
				}
				else
				{
					// Texture coordinates
					Vector2D pos = triverts[i];
					pos = pos.GetRotated(rotate);
					pos.y = -pos.y;
					pos = (pos + offset) * scale * texscale;
					verts[i].u = pos.x;
					verts[i].v = pos.y;
				}
			}

			// The sector triangulation created clockwise triangles that
			// are right up for the floor. For the ceiling we must flip
			// the triangles upside down.
			if((extrafloor != null) && !extrafloor.VavoomType && !innerside)
				SwapTriangleVertices(verts);
			
			// Determine render pass
			if(extrafloor != null)
			{
				if(extrafloor.Sloped3dFloor) //mxd
					this.RenderPass = RenderPass.Mask;
				else if(extrafloor.RenderAdditive) //mxd
					this.RenderPass = RenderPass.Additive;
				else if(level.alpha < 255)
					this.RenderPass = RenderPass.Alpha;
				else
					this.RenderPass = RenderPass.Mask;
			}
			else
			{
				this.RenderPass = RenderPass.Solid;
			}

			//mxd. Update sky render flag
			UpdateSkyRenderFlag();
			
			// Apply vertices
			base.SetVertices(verts);
			return (verts.Length > 0);
		}

		//mxd
		protected override void UpdateSkyRenderFlag()
		{
			bool isrenderedassky = renderassky;
			renderassky = (level.sector.FloorTexture == General.Map.Config.SkyFlatName || level.sector.LongFloorTexture == MapSet.EmptyLongName);
			if(isrenderedassky != renderassky && Sector.Sides != null)
			{
				// Middle geometry may need updating...
				foreach(Sidedef side in level.sector.Sidedefs)
				{
					VisualSidedefParts parts = Sector.GetSidedefParts(side);
					if(parts.middlesingle != null) parts.middlesingle.UpdateSkyRenderFlag();
				}
			}
		}

		#endregion
		
		#region ================== Methods

		// Return texture coordinates
		protected override Point GetTextureOffset()
		{
			Point p;

			if (General.Map.MERIDIAN)
			{
				p = new Point { X = (int)Sector.Sector.OffsetX,
								Y = (int)Sector.Sector.OffsetY };
			}
			else
			{
				p = new Point { X = (int)Sector.Sector.Fields.GetValue("xpanningfloor", 0.0f),
								Y = (int)Sector.Sector.Fields.GetValue("ypanningfloor", 0.0f) };
			}

			return p;
		}

		// Move texture coordinates
		protected override void MoveTextureOffset(Point xy)
		{
			//mxd
			Sector s = GetControlSector();
			s.Fields.BeforeFieldsChange();

			float nx, ny;
			if (General.Map.MERIDIAN)
			{
				nx = s.OffsetX + xy.X % Texture.ScaledWidth;
				ny = s.OffsetY + xy.Y % Texture.ScaledHeight;
				s.OffsetX = (int)nx;
				s.OffsetY = (int)ny;
			}
			else
			{
				nx = (s.Fields.GetValue("xpanningfloor", 0.0f) + xy.X) % (Texture.ScaledWidth / s.Fields.GetValue("xscalefloor", 1.0f));
				ny = (s.Fields.GetValue("ypanningfloor", 0.0f) + xy.Y) % (Texture.ScaledHeight / s.Fields.GetValue("yscalefloor", 1.0f));
				s.Fields["xpanningfloor"] = new UniValue(UniversalType.Float, nx);
				s.Fields["ypanningfloor"] = new UniValue(UniversalType.Float, ny);
			}

			s.UpdateNeeded = true;

			mode.SetActionResult("Changed floor texture offsets to " + nx + ", " + ny + ".");
		}

		//mxd. Texture scale change
		protected override void ChangeTextureScale(int incrementX, int incrementY) 
		{
			Sector s = GetControlSector();
			float scaleX = s.Fields.GetValue("xscalefloor", 1.0f);
			float scaleY = s.Fields.GetValue("yscalefloor", 1.0f);

			s.Fields.BeforeFieldsChange();

			if(incrementX != 0) 
			{
				float pix = (int)Math.Round(Texture.Width * scaleX) - incrementX;
				float newscaleX = (float)Math.Round(pix / Texture.Width, 3);
				scaleX = (newscaleX == 0 ? scaleX * -1 : newscaleX);
				UniFields.SetFloat(s.Fields, "xscalefloor", scaleX, 1.0f);
			}

			if(incrementY != 0)
			{
				float pix = (int)Math.Round(Texture.Height * scaleY) - incrementY;
				float newscaleY = (float)Math.Round(pix / Texture.Height, 3);
				scaleY = (newscaleY == 0 ? scaleY * -1 : newscaleY);
				UniFields.SetFloat(s.Fields, "yscalefloor", scaleY, 1.0f);
			}

			// Update geometry
			if(mode.VisualSectorExists(level.sector))
			{
				BaseVisualSector vs = (BaseVisualSector)mode.GetVisualSector(level.sector);
				vs.UpdateSectorGeometry(false);
			}

			s.UpdateNeeded = true;
			s.UpdateCache();
			if(s.Index != Sector.Sector.Index) 
			{
				Sector.Sector.UpdateNeeded = true;
				Sector.Sector.UpdateCache();
			}

			mode.SetActionResult("Floor scale changed to " + scaleX.ToString("F03", CultureInfo.InvariantCulture) + ", " + scaleY.ToString("F03", CultureInfo.InvariantCulture) + " (" + (int)Math.Round(Texture.Width / scaleX) + " x " + (int)Math.Round(Texture.Height / scaleY) + ").");
		}

		//mxd
		public override void OnResetTextureOffset() 
		{
			ClearFields(new[] { "xpanningfloor", "ypanningfloor" }, "Reset texture offsets", "Texture offsets reset.");
		}

		//mxd
		public override void OnResetLocalTextureOffset() 
		{
			ClearFields(new[] { "xpanningfloor", "ypanningfloor", "xscalefloor", "yscalefloor", "rotationfloor", "lightfloor", "lightfloorabsolute" },
				"Reset texture offsets, scale, rotation and brightness", "Texture offsets, scale, rotation and brightness reset.");
		}
		
		// Paste texture
		public override void OnPasteTexture()
		{
			if(BuilderPlug.Me.CopiedFlat != null)
			{
				mode.CreateUndo("Paste floor \"" + BuilderPlug.Me.CopiedFlat + "\"");
				mode.SetActionResult("Pasted flat \"" + BuilderPlug.Me.CopiedFlat + "\" on floor.");
				
				SetTexture(BuilderPlug.Me.CopiedFlat);

				// Update
				if(mode.VisualSectorExists(level.sector))
				{
					BaseVisualSector vs = (BaseVisualSector)mode.GetVisualSector(level.sector);
					vs.UpdateSectorGeometry(false);
				}
			}
		}

		// Call to change the height
		public override void OnChangeTargetHeight(int amount)
		{
			// Only do this when not done yet in this call
			// Because we may be able to select the same 3D floor multiple times through multiple sectors
			SectorData sd = mode.GetSectorData(level.sector);
			if(!sd.FloorChanged)
			{
				sd.FloorChanged = true;
				base.OnChangeTargetHeight(amount);
			}
		}

		// This changes the height
		protected override void ChangeHeight(int amount)
		{
			mode.CreateUndo("Change floor height", UndoGroup.FloorHeightChange, level.sector.FixedIndex);
			level.sector.FloorHeight += amount;
			
			if(General.Map.UDMF || General.Map.MERIDIAN) 
			{
				//mxd. Modify vertex offsets?
				if(level.sector.Sidedefs.Count == 3)
				{
					ChangeVertexHeight(amount);
				}

				//mxd. Modify slope offset?
				if(level.sector.FloorSlope.GetLengthSq() > 0)
				{
					Vector3D center = new Vector3D(level.sector.BBox.X + level.sector.BBox.Width / 2,
												   level.sector.BBox.Y + level.sector.BBox.Height / 2, 
												   level.sector.FloorHeight);
					
					Plane p = new Plane(center, 
										level.sector.FloorSlope.GetAngleXY() + Angle2D.PIHALF, 
										-level.sector.FloorSlope.GetAngleZ(), 
										true);

					level.sector.FloorSlopeOffset = p.Offset;
				}
			}

			mode.SetActionResult("Changed floor height to " + level.sector.FloorHeight + ".");
		}

		//mxd
		private void ChangeVertexHeight(int amount) 
		{
			HashSet<Vertex> verts = new HashSet<Vertex>();

			// Do this only if all 3 verts have offsets
			foreach(Sidedef side in level.sector.Sidedefs) 
			{
				if(float.IsNaN(side.Line.Start.ZFloor) || float.IsNaN(side.Line.End.ZFloor)) return;
				verts.Add(side.Line.Start);
				verts.Add(side.Line.End);
			}

			foreach(Vertex v in verts) 
				mode.GetVisualVertex(v, true).OnChangeTargetHeight(amount);
		}

		//mxd. Sector brightness change
		public override void OnChangeTargetBrightness(bool up) 
		{
			if(level != null) 
			{
				// This floor is part of 3D-floor
				if(level.sector != Sector.Sector)
				{
					BaseVisualSector vs = (BaseVisualSector)mode.GetVisualSector(level.sector);
					vs.Floor.OnChangeTargetBrightness(up);
					vs.UpdateSectorGeometry(true);
				}
				// This is actual floor of a sector with extrafloors
				else if(Sector.ExtraFloors.Count > 0 && !Sector.ExtraFloors[0].ExtraFloor.Floor.restrictlighting && !Sector.ExtraFloors[0].ExtraFloor.Floor.disablelighting)
				{
					Sector.ExtraFloors[0].OnChangeTargetBrightness(up);
				}
				else
				{
					base.OnChangeTargetBrightness(up);
				}
			} 
			else 
			{
				base.OnChangeTargetBrightness(up);
			}
		}

		// This performs a fast test in object picking
		public override bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
		{
			// Check if our ray starts at the correct side of the plane
			if((!innerside && level.plane.Distance(from) > 0.0f) || (innerside && level.plane.Distance(from) < 0.0f))
			{
				// Calculate the intersection
				if(level.plane.GetIntersection(from, to, ref pickrayu))
				{
					if(pickrayu > 0.0f)
					{
						pickintersect = from + (to - from) * pickrayu;
						
						// Intersection point within bbox?
						RectangleF bbox = Sector.Sector.BBox;
						return ((pickintersect.x >= bbox.Left) && (pickintersect.x <= bbox.Right) &&
								(pickintersect.y >= bbox.Top) && (pickintersect.y <= bbox.Bottom));
					}
				}
			}
			
			return false;
		}
		
		// This performs an accurate test for object picking
		public override bool PickAccurate(Vector3D from, Vector3D to, Vector3D dir, ref float u_ray)
		{
			u_ray = pickrayu;
			
			// Check on which side of the nearest sidedef we are
			Sidedef sd = MapSet.NearestSidedef(Sector.Sector.Sidedefs, pickintersect);
			float side = sd.Line.SideOfLine(pickintersect);

			//mxd. Alpha based picking. Used only on extrafloors with transparent or masked textures
			if((side <= 0.0f && sd.IsFront) || (side > 0.0f && !sd.IsFront))
			{
				if(!BuilderPlug.Me.AlphaBasedTextureHighlighting || !Texture.IsImageLoaded || extrafloor == null || RenderPass == RenderPass.Solid || (!Texture.IsTranslucent && !Texture.IsMasked))
					return true;

				// Some textures (e.g. HiResImage) may lie about their size, so use bitmap size instead
				Bitmap image = Texture.GetBitmap();

				// Fetch ZDoom fields
				float rotate;
				Vector2D offset;
				if (General.Map.MERIDIAN)
				{
					offset = new Vector2D(level.sector.OffsetX, level.sector.OffsetY);
					rotate = Angle2D.DegToRad(level.sector.FloorTexRot);
				}
				else
				{
					offset = new Vector2D(level.sector.Fields.GetValue("xpanningfloor", 0.0f), level.sector.Fields.GetValue("ypanningfloor", 0.0f));
					rotate = Angle2D.DegToRad(level.sector.Fields.GetValue("rotationfloor", 0.0f));
				}
				Vector2D scale = new Vector2D(level.sector.Fields.GetValue("xscalefloor", 1.0f), level.sector.Fields.GetValue("yscalefloor", 1.0f));
				Vector2D texscale = new Vector2D(1.0f / Texture.ScaledWidth, 1.0f / Texture.ScaledHeight);

				// Texture coordinates
				
				Vector2D o = pickintersect;

				o = o.GetRotated(rotate);
				o.y = -o.y;
				o = (o + offset) * scale * texscale;
				o.x = (o.x * image.Width) % image.Width;
				o.y = (o.y * image.Height) % image.Height;

				// Make sure coordinates are inside of texture dimensions...
				if(o.x < 0) o.x += image.Width;
				if(o.y < 0) o.y += image.Height;

				// Make final texture coordinates...
				int ox = General.Clamp((int)Math.Floor(o.x), 0, image.Width - 1);
				int oy = General.Clamp((int)Math.Floor(o.y), 0, image.Height - 1);

				// Check pixel alpha
				return (image.GetPixel(ox, oy).A > 0);
			}

			return false;
		}
		
		// Return texture name
		public override string GetTextureName()
		{
			return level.sector.FloorTexture;
		}

		// This changes the texture
		protected override void SetTexture(string texturename)
		{
			// Set new texture
			level.sector.SetFloorTexture(texturename);
			General.Map.Data.UpdateUsedTextures();
		}

		//mxd
		public override void SelectNeighbours(bool select, bool withSameTexture, bool withSameHeight) 
		{
			if(!withSameTexture && !withSameHeight) return;

			if(select && !selected) 
			{
				selected = true;
				mode.AddSelectedObject(this);
			}
			else if(!select && selected)
			{
				selected = false;
				mode.RemoveSelectedObject(this);
			}
			
			List<Sector> neighbours = new List<Sector>();
			bool regularorvavoom = (extrafloor == null || extrafloor.VavoomType);

			//collect neighbour sectors
			foreach(Sidedef side in Sector.Sector.Sidedefs) 
			{
				if(side.Other != null && side.Other.Sector != Sector.Sector && !neighbours.Contains(side.Other.Sector))
				{
					BaseVisualSector vs = (BaseVisualSector)mode.GetVisualSector(side.Other.Sector);
					if(vs == null) continue;

					// When current floor is part of a 3d floor, it looks like a ceiling, so we need to select adjacent ceilings
					if(level.sector != Sector.Sector && !regularorvavoom)
					{
						if((!withSameTexture || side.Other.Sector.LongCeilTexture == level.sector.LongFloorTexture) &&
							(!withSameHeight || side.Other.Sector.CeilHeight == level.sector.FloorHeight)) 
						{
							neighbours.Add(side.Other.Sector);

							//(de)select regular visual ceiling?
							if(select != vs.Ceiling.Selected) 
								vs.Ceiling.SelectNeighbours(select, withSameTexture, withSameHeight);
						}
					}
					else // Regular floor or vavoom-type extrafloor
					{
						// (De)select adjacent floor
						if((!withSameTexture || side.Other.Sector.LongFloorTexture == level.sector.LongFloorTexture) &&
							(!withSameHeight || side.Other.Sector.FloorHeight == level.sector.FloorHeight)) 
						{
							neighbours.Add(side.Other.Sector);

							//(de)select regular visual floor?
							if(select != vs.Floor.Selected) 
								vs.Floor.SelectNeighbours(select, withSameTexture, withSameHeight);
						}
					}

					// (De)select adjacent extra floors
					foreach(VisualFloor ef in vs.ExtraFloors) 
					{
						if(select == ef.Selected || ef.extrafloor.VavoomType != regularorvavoom) continue;
						if((!withSameTexture || level.sector.LongFloorTexture == ef.level.sector.LongFloorTexture) &&
							(!withSameHeight || level.sector.FloorHeight == ef.level.sector.FloorHeight)) 
						{
							ef.SelectNeighbours(select, withSameTexture, withSameHeight);
						}
					}

					// (De)select adjacent vavoom type extra ceilings
					foreach(VisualCeiling ec in vs.ExtraCeilings) 
					{
						if(select == ec.Selected || ec.ExtraFloor.VavoomType == regularorvavoom) continue;
						if((!withSameTexture || level.sector.LongFloorTexture == ec.Level.sector.LongCeilTexture) &&
							(!withSameHeight || level.sector.FloorHeight == ec.Level.sector.CeilHeight)) 
						{
							ec.SelectNeighbours(select, withSameTexture, withSameHeight);
						}
					}
				}
			}
		}

		//mxd
		public void AlignTexture(bool alignx, bool aligny) 
		{
			if(!(General.Map.UDMF || General.Map.MERIDIAN)) return;

			//is is a surface with line slope?
			float slopeAngle = level.plane.Normal.GetAngleZ() - Angle2D.PIHALF;

			if(slopeAngle == 0) //it's a horizontal plane
			{
				AlignTextureToClosestLine(alignx, aligny);
			} 
			else //it can be a surface with line slope
			{ 
				Linedef slopeSource = null;
				bool isFront = false;

				foreach(Sidedef side in Sector.Sector.Sidedefs) 
				{
					if(side.Line.Action == 181) 
					{
						if(side.Line.Args[0] == 1 && side.Line.Front != null && side.Line.Front == side) 
						{
							slopeSource = side.Line;
							isFront = true;
							break;
						}

						if(side.Line.Args[0] == 2 && side.Line.Back != null && side.Line.Back == side) 
						{
							slopeSource = side.Line;
							break;
						}
					}
				}

				if(slopeSource != null && slopeSource.Front != null && slopeSource.Front.Sector != null && slopeSource.Back != null && slopeSource.Back.Sector != null)
					AlignTextureToSlopeLine(slopeSource, slopeAngle, isFront, alignx, aligny);
				else
					AlignTextureToClosestLine(alignx, aligny);
			}
		}
		
		#endregion
	}
}

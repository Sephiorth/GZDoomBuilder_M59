
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
using System.Text;
using System.IO;

#endregion

namespace CodeImp.DoomBuilder.IO
{
	internal class WAD : IDisposable
	{
		#region ================== Constants

		// WAD types
		private const string TYPE_IWAD = "IWAD";
		private const string TYPE_PWAD = "PWAD";
		private byte[] TYPE_ROO = { 0x52, 0x4F, 0x4F, 0xB1 }; //m59
		private const int ROO_VERSION = 15; //m59
		// Encoder
		public static readonly Encoding ENCODING = Encoding.ASCII;

		//mxd. Official IWAD MD5 hashes
		private static readonly HashSet<string> IWAD_HASHES = new HashSet<string>
		{
			////// DOOM IWADS //////
			"d9153ced9fd5b898b36cc5844e35b520",			// DOOM2 1.666g MD5
			"30e3c2d0350b67bfbf47271970b74b2f",			// DOOM2 1.666 MD5
			"ea74a47a791fdef2e9f2ea8b8a9da13b",			// DOOM2 1.7 MD5
			"d7a07e5d3f4625074312bc299d7ed33f",			// DOOM2 1.7a MD5
			"c236745bb01d89bbb866c8fed81b6f8c",			// DOOM2 1.8 MD5
			"25e1459ca71d321525f84628f45ca8cd",			// DOOM2 1.9 MD5
			"3cb02349b3df649c86290907eed64e7b",			// DOOM2 French MD5
			"c3bea40570c23e511a7ed3ebcd9865f7",			// BFG DOOM2 MD5

			"981b03e6d1dc033301aa3095acc437ce",			// DOOM 1.1 MD5
			"792fd1fea023d61210857089a7c1e351",			// DOOM 1.2 MD5
			"464e3723a7e7f97039ac9fd057096adb",			// DOOM 1.6b MD5
			"54978d12de87f162b9bcc011676cb3c0",			// DOOM 1.666 MD5
			"11e1cd216801ea2657723abc86ecb01f",			// DOOM 1.8 MD5
			"1cd63c5ddff1bf8ce844237f580e9cf3",			// DOOM 1.9 MD5
			"fb35c4a5a9fd49ec29ab6e900572c524",			// BFG DOOM MD5

			"c4fe9fd920207691a9f493668e0a2083",			// ULTIMATE DOOM MD5

			"75c8cf89566741fa9d22447604053bd7",			// PLUTONIA MD5
			"3493be7e1e2588bc9c8b31eab2587a04",			// PLUTONIA RARE MD5

			"4e158d9953c79ccf97bd0663244cc6b6",			// TNT MD5
			"1d39e405bf6ee3df69a8d2646c8d5c49",			// TNT Fixed MD5
			"be626c12b7c9d94b1dfb9c327566b4ff",			// PSN TNT MD5

			////// HERETIC IWADS //////
			"3117e399cdb4298eaa3941625f4b2923",			// HERETIC 1.0 MD5
			"1e4cb4ef075ad344dd63971637307e04",			// HERETIC 1.2 MD5
			"66d686b1ed6d35ff103f15dbd30e0341",			// HERETIC 1.3 MD5

			////// HEXEN IWADS //////
			"c88a2bb3d783e2ad7b599a8e301e099e",			// HEXEN Beta MD5
			"b2543a03521365261d0a0f74d5dd90f0",			// HEXEN 1.0 MD5
			"abb033caf81e26f12a2103e1fa25453f",			// HEXEN 1.1 MD5
			"1077432e2690d390c256ac908b5f4efa",			// HEXEN DK 1.0 MD5
			"78d5898e99e220e4de64edaa0e479593",			// HEXEN DK 1.1 MD5

			////// STRIFE IWADS //////
			"8f2d3a6a289f5d2f2f9c1eec02b47299",			// STRIFE 1.0 MD5
			"2fed2031a5b03892106e0f117f17901f",			// STRIFE 1.2 MD5
		};                                                      
		
		#endregion

		#region ================== Variables

		// File objects
		private string filename;
		private FileStream file;
		private BinaryReader reader;
		private BinaryWriter writer;
		
		// Header
		private int numlumps;
		private int lumpsoffset;
		private bool isiwad; //mxd
		private bool isofficialiwad; //mxd
		private bool isroo; //m59
		private int rooversion; //m59
		
		// Lumps
		private List<Lump> lumps;
		
		// Status
		private bool isreadonly;
		private bool isdisposed;

		#endregion

		#region ================== Properties

		public string Filename { get { return filename; } }
		public Encoding Encoding { get { return ENCODING; } }
		public bool IsReadOnly { get { return isreadonly; } }
		public bool IsDisposed { get { return isdisposed; } }
		public bool IsIWAD { get { return isiwad; } set { isiwad = value; } } //mxd
		public bool IsOfficialIWAD { get { return isofficialiwad; } } //mxd
		public bool IsRoo { get { return isroo; } } //m59
		public List<Lump> Lumps { get { return lumps; } }

		#endregion

		#region ================== Constructor / Disposer

		// Constructor to open or create a WAD file
		public WAD(string pathfilename)
		{
			// Initialize
			this.isreadonly = false;
			this.Open(pathfilename);
		}

		// Constructor to open or create a WAD file
		public WAD(string pathfilename, bool openreadonly)
		{
			// Initialize
			this.isreadonly = openreadonly;
			this.Open(pathfilename);
		}

		// Destructor
		/*~WAD()
		{
			// Make sure everything is disposed
			this.Dispose();
		}*/
		
		// Disposer
		public void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Only possible when not read-only
				if(!isreadonly)
				{
					// Flush writing changes
					if(writer != null) writer.Flush();
					if(file != null) file.Flush();
				}
				
				// Clean up
				if(lumps != null) foreach(Lump l in lumps) l.Dispose();
				if(writer != null) writer.Close();
				if(reader != null) reader.Close();
				if(file != null) file.Dispose();
				
				// Done
				isdisposed = true;
				GC.SuppressFinalize(this); //mxd
			}
		}

		#endregion

		#region ================== IO

		// Open a WAD file
		private void Open(string pathfilename)
		{
			FileAccess access;
			FileShare share;

			// Keep filename
			filename = pathfilename;

			//mxd
			CheckHash();
			
			// Determine if opening for read only
			if(isreadonly)
			{
				// Read only
				access = FileAccess.Read;
				share = FileShare.ReadWrite;
			}
			else
			{
				// Private access
				access = FileAccess.ReadWrite;
				share = FileShare.Read;
			}
			
			// Open the file stream
			file = File.Open(pathfilename, FileMode.OpenOrCreate, access, share);

			// Create file handling tools
			reader = new BinaryReader(file, ENCODING);
			if(!isreadonly) writer = new BinaryWriter(file, ENCODING);

			// Is the WAD file zero length?
			if(file.Length < 4)
			{
				// Create the headers in file
				CreateHeaders();
			}
			else
			{
				// Read information from file
				ReadHeaders();
			}
		}

		// This creates new file headers
		private void CreateHeaders()
		{
			// Default settings
			isiwad = false; //mxd
			isofficialiwad = false; //mxd
			lumpsoffset = 12;

			// New lumps array
			lumps = new List<Lump>(numlumps);
			
			// Write the headers
			if(!isreadonly) WriteHeaders();
		}
		
		// This reads the WAD header and lumps table
		private void ReadHeaders()
		{
			// Make sure the write is finished writing
			if(!isreadonly) writer.Flush();

			// Seek to beginning
			file.Seek(0, SeekOrigin.Begin);

			// Read in header as bytes.
			byte[] hdr = reader.ReadBytes(4);
			isroo = true;
			for (int i = 0; i < 4; ++i)
			{
				if (hdr[i] != TYPE_ROO[i])
					isroo = false;
			}

			if (isroo)
			{
				ReadRooFile();
				return;
			}

			// Read WAD type
			isiwad = (ENCODING.GetString(hdr) == TYPE_IWAD); //mxd
			
			// Number of lumps
			numlumps = reader.ReadInt32();
			if(numlumps < 0) throw new IOException("Invalid number of lumps in wad file.");

			// Lumps table offset
			lumpsoffset = reader.ReadInt32();
			if(lumpsoffset < 0) throw new IOException("Invalid lumps offset in wad file.");

			// Seek to the lumps table
			file.Seek(lumpsoffset, SeekOrigin.Begin);
			
			// Dispose old lumps and create new list
			if(lumps != null) foreach(Lump l in lumps) l.Dispose();
			lumps = new List<Lump>(numlumps);

			// Go for all lumps
			for(int i = 0; i < numlumps; i++)
			{
				// Read lump information
				int offset = reader.ReadInt32();
				int length = reader.ReadInt32();
				byte[] fixedname = reader.ReadBytes(8);

				// Create the lump
				lumps.Add(new Lump(file, this, fixedname, offset, length));
			}
		}

		///
		/// Roo files don't contain lumps, so we need to read the data in and
		/// create lumps out of them (sectors, linedefs, sidedefs, things) and
		/// also create lumps for the map header ~MAP and room name ROOM.
		///
		private void ReadRooFile()
		{
			// All we've read so far is the 4 byte ROO header.
			rooversion = reader.ReadInt32();
			if (rooversion != ROO_VERSION)
			{
				// Version incorrect - don't open.
				throw new IOException(String.Format("Roo version {0} too low - require at least version {1}.",
					rooversion, ROO_VERSION));
			}
			numlumps = 9;

			// Dispose old lumps and create new list
			if (lumps != null)
				foreach (Lump l in lumps)
					l.Dispose();
			lumps = new List<Lump>(numlumps);
			
			
			// These two lumps take the place of the roo version and magic bytes.
			// Map header lump.
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("~MAP"), 0, 4));
			// Add a map name.
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("Room"), 4, 4));

			lumps.Add(new Lump(file, this, ENCODING.GetBytes("SECURITY"), (int)file.Position, 4));
			// Skip room security.
			reader.ReadInt32();

			// Main offset, read and seek to it
			int offset = reader.ReadInt32();
			file.Seek(offset, SeekOrigin.Begin);

			lumps.Add(new Lump(file, this, ENCODING.GetBytes("MAPBOUND"), (int)file.Position, 8));

			// Skip width/height.
			reader.ReadBytes(8);

			int nodesOffset = reader.ReadInt32();
			int clientWallOffset = reader.ReadInt32();
			// Get map data offsets
			int editWallOffset = reader.ReadInt32();
			int sideDefOffset = reader.ReadInt32();
			int sectorOffset = reader.ReadInt32();
			int thingOffset = reader.ReadInt32();

			// Nodes lump
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("NODES"), nodesOffset, clientWallOffset - nodesOffset));
			// Client walls lump
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("CLIWALLS"), clientWallOffset, editWallOffset - clientWallOffset));
			// Linedefs lump
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("LINEDEFS"), editWallOffset, sideDefOffset - editWallOffset));
			// Sidedefs lump
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("SIDEDEFS"), sideDefOffset, sectorOffset - sideDefOffset));
			// Sectors lump
			lumps.Add(new Lump(file, this, ENCODING.GetBytes("SECTORS"), sectorOffset, thingOffset - sectorOffset));

			// Things lump
			file.Seek(thingOffset, SeekOrigin.Begin);
			int numObject = reader.ReadInt16();
			if (numObject != 2)
				throw new IOException(String.Format("Can only use 2 things, got {0}", numObject));

			lumps.Add(new Lump(file, this, ENCODING.GetBytes("THINGS"), thingOffset, numObject * 8 + 2));
		}

		// This writes the WAD header and lumps table
		public void WriteHeaders()
		{
			// Seek to beginning
			file.Seek(0, SeekOrigin.Begin);

			if (isroo || General.Map.MERIDIAN)
			{
				WriteRooHeader();
				return;
			}

			// Write WAD type
			writer.Write(ENCODING.GetBytes(isiwad ? TYPE_IWAD : TYPE_PWAD));

			// Number of lumps
			writer.Write(numlumps);

			// Lumps table offset
			writer.Write(lumpsoffset);

			// Seek to the lumps table
			file.Seek(lumpsoffset, SeekOrigin.Begin);

			// Go for all lumps
			for(int i = 0; i < lumps.Count; i++)
			{
				// Write lump information
				writer.Write(lumps[i].Offset);
				writer.Write(lumps[i].Length);
				writer.Write(lumps[i].FixedName);
			}
		}

		// Roo files don't contain lumps, need to write out the data in a way
		// that Meridian/Blakserv/Roomedit can still read it.
		private void WriteRooHeader()
		{
			// Write roo header
			writer.Write(TYPE_ROO);
			// Write roo version
			writer.Write(ROO_VERSION);
		}

		private void RooFileBackPatch(long OldOffset, long CurrentOffset)
		{
			file.Seek(OldOffset, SeekOrigin.Begin);
			writer.Write((int)CurrentOffset);
			file.Seek(CurrentOffset, SeekOrigin.Begin);
		}

		private long RooFileWriteTemp()
		{
			long offset = file.Position;
			writer.Write(0);
			return offset;
		}

		public void RooFileWriteAll(WAD source)
		{
			file.Seek(0, SeekOrigin.Begin);

			writer.Write(TYPE_ROO);
			// Write roo version
			writer.Write(ROO_VERSION);

			// Security
			Lump lmp = source.FindLump("SECURITY");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			else
			{
				General.ErrorLogger.Add(ErrorType.Warning, "Did not find security lump! Writing 0.");
				writer.Write((Int32)0);
			}

			long mainOffset = RooFileWriteTemp();
			long serverOffset = RooFileWriteTemp();

			RooFileBackPatch(mainOffset, file.Position);

			// Width/Height
			lmp = source.FindLump("MAPBOUND");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			else
			{
				General.ErrorLogger.Add(ErrorType.Warning, "Did not find map bounds lump! Writing 0.");
				writer.Write((Int32)0);
				writer.Write((Int32)0);
			}

			long nodesPos = RooFileWriteTemp();
			long cliWallPos = RooFileWriteTemp();
			long linedefPos = RooFileWriteTemp();
			long sidedefPos = RooFileWriteTemp();
			long sectorPos = RooFileWriteTemp();
			long thingPos = RooFileWriteTemp();

			RooFileBackPatch(nodesPos, file.Position);
			lmp = source.FindLump("NODES");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			RooFileBackPatch(cliWallPos, file.Position);
			lmp = source.FindLump("CLIWALLS");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			RooFileBackPatch(linedefPos, file.Position);
			lmp = source.FindLump("LINEDEFS");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			RooFileBackPatch(sidedefPos, file.Position);
			lmp = source.FindLump("SIDEDEFS");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			RooFileBackPatch(sectorPos, file.Position);
			lmp = source.FindLump("SECTORS");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());
			RooFileBackPatch(thingPos, file.Position);
			lmp = source.FindLump("THINGS");
			if (lmp != null)
				writer.Write(lmp.Stream.ReadAllBytes());

			// Room ID, not used.
			writer.Write((Int32)0);

			RooFileBackPatch(serverOffset, file.Position);
		}

		//mxd
		private void CheckHash()
		{
			// Open the file stream
			FileStream fs = File.Open(filename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
			
			// Empty file can't be official iwad
			if(fs.Length > 4)
			{
				BinaryReader r = new BinaryReader(fs, ENCODING);

				// Read WAD type
				if(ENCODING.GetString(r.ReadBytes(4)) == TYPE_IWAD)
				{
					// Rewind
					r.BaseStream.Position = 0;
					isofficialiwad = IWAD_HASHES.Contains(MD5Hash.Get(r.BaseStream));
					if(!isreadonly && isofficialiwad) isreadonly = true;
				}

				// Close the reader
				r.Close();
			}
			else
			{
				// Close the file
				fs.Dispose();
			}
		}
		
		// This flushes writing changes
		/*public void Flush()
		{
			// Only possible when not read-only
			if(!isreadonly)
			{
				// Flush writing changes
				if(writer != null) writer.Flush();
				if(file != null) file.Flush();
			}
		}*/
		
		#endregion
		
		#region ================== Lumps

		// This creates a new lump in the WAD file
		public Lump Insert(string name, int position, int datalength) { return Insert(name, position, datalength, true); } //mxd
		public Lump Insert(string name, int position, int datalength, bool writeheaders)
		{
			// We will be adding a lump
			numlumps++;
			
			// Extend the file
			file.SetLength(file.Length + datalength + 16);
			
			// Create the lump
			Lump lump = new Lump(file, this, Lump.MakeFixedName(name, ENCODING), lumpsoffset, datalength);
			lumps.Insert(position, lump);
			
			// Advance lumps table offset
			lumpsoffset += datalength;

			// Write the new headers
			if(writeheaders) WriteHeaders();

			// Return the new lump
			return lump;
		}

		// This removes a lump from the WAD file by index
		public void RemoveAt(int index)
		{
			// Remove from list
			Lump l = lumps[index];
			lumps.RemoveAt(index);
			l.Dispose();
			numlumps--;
			
			// Write the new headers
			WriteHeaders();
		}
		
		// This removes a lump from the WAD file
		public void Remove(Lump lump)
		{
			// Remove from list
			lumps.Remove(lump);
			lump.Dispose();
			numlumps--;
			
			// Write the new headers
			WriteHeaders();
		}
		
		// This finds a lump by name, returns null when not found
		public Lump FindLump(string name)
		{
			int index = FindLumpIndex(name);
			return (index == -1 ? null : lumps[index]);
		}

		// This finds a lump by name, returns null when not found
		public Lump FindLump(string name, int start)
		{
			int index = FindLumpIndex(name, start);
			return (index == -1 ? null : lumps[index]);
		}

		// This finds a lump by name, returns null when not found
		public Lump FindLump(string name, int start, int end)
		{
			int index = FindLumpIndex(name, start, end);
			return (index == -1 ? null : lumps[index]);
		}

		// This finds a lump by name, returns -1 when not found
		public int FindLumpIndex(string name)
		{
			// Do search
			return FindLumpIndex(name, 0, lumps.Count - 1);
		}

		// This finds a lump by name, returns -1 when not found
		public int FindLumpIndex(string name, int start)
		{
			// Do search
			return FindLumpIndex(name, start, lumps.Count - 1);
		}
		
		// This finds a lump by name, returns -1 when not found
		public int FindLumpIndex(string name, int start, int end)
		{
			if(name.Length > 8)	return -1;//mxd. Can't be here. Go away!
			
			long longname = Lump.MakeLongName(name);
			
			// Fix end when it exceeds length
			if(end > (lumps.Count - 1)) end = lumps.Count - 1;

			// Loop through the lumps
			for(int i = start; i <= end; i++)
			{
				// Check if the lump name matches
				if(lumps[i].LongName == longname)
				{
					// Found the lump!
					return i;
				}
			}

			// Nothing found
			return -1;
		}

		#endregion
	}
}

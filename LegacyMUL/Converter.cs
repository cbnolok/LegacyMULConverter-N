using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace LegacyMUL
{
	public enum FileType
	{
		ArtLegacyMUL,
		GumpartLegacyMUL,
		MapLegacyMUL,
		SoundLegacyMUL,
        MultiMUL
	}

	public class LegacyMULConverter
	{
		public LegacyMULConverter()
		{
		}

		private struct IdxEntry
		{
			public int m_Id;
			public int m_Offset;
			public int m_Size;
			public int m_Extra;
		}

		private struct TableEntry
		{
			public long m_Offset;
			public int m_HeaderLength;
			public int m_Size;
            public int m_SizeDecompressed;
            public ulong m_Identifier;
			public uint m_Hash;
            public short m_Compression;
		}

		//
		// IO shortcuts
		//
		private BinaryReader OpenInput( string path )
		{
			if ( path == null )
				return null;

			return new BinaryReader( new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read ) );
		}

		private BinaryWriter OpenOutput( string path )
		{
			if ( path == null )
				return null;

			return new BinaryWriter( new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None ) );
		}

		//
		// MUL -> UOP
		//
		public void ToUOP( string inFile, string inFileIdx, string outFile, FileType type, int typeIndex )
		{
			// Same for all UOP files
			long firstTable = 0x200;
            int tableSize = 100; //0x3E8; // block size (files per block)
            short compress = 0; // art, gumpart, map, etc are expected to be uncompressed, if we compress them they won't be loaded by the client...

			// Sanity, in case firstTable is customized by you!
			if ( firstTable < 0x28 )
				throw new Exception( "At least 0x28 bytes are needed for the header." );

			using ( BinaryReader reader = OpenInput( inFile ) )
			using ( BinaryReader readerIdx = OpenInput( inFileIdx ) )
			using ( BinaryWriter writer = OpenOutput( outFile ) )
			{
				List<IdxEntry> idxEntries;

				if ( type == FileType.MapLegacyMUL )
				{
					// No IDX file, just group the data into 0xC4000 long chunks
					int length = (int)reader.BaseStream.Length;
					idxEntries = new List<IdxEntry>( (int)Math.Ceiling( (double)length / 0xC4000 ) );

					int position = 0;
					int id = 0;

					while ( position < length )
					{
						IdxEntry e = new IdxEntry();
						e.m_Id = id++;
						e.m_Offset = position;
						e.m_Size = 0xC4000;
						e.m_Extra = 0;

						idxEntries.Add( e );
						
						position += 0xC4000;
					}
				}
				else
				{
					int idxEntryCount = (int)( readerIdx.BaseStream.Length / 12 );
					idxEntries = new List<IdxEntry>( idxEntryCount );

					for ( int i = 0; i < idxEntryCount; ++i )
					{
						int offset = readerIdx.ReadInt32();

						if ( offset < 0 )
						{
							readerIdx.BaseStream.Seek( 8, SeekOrigin.Current ); // skip
							continue;
						}

						IdxEntry e = new IdxEntry();
						e.m_Id = i;
						e.m_Offset = offset;
						e.m_Size = readerIdx.ReadInt32();
						e.m_Extra = readerIdx.ReadInt32();

						idxEntries.Add( e );
					}
				}

                int fileCount = idxEntries.Count;
                if (type == FileType.MultiMUL)
                {
                    ++fileCount;  // for "housing.bin"
                    idxEntries.Add(new IdxEntry());
                }

				// File header
				writer.Write( 0x50594D ); // MYP
				writer.Write( 5 ); // version
				writer.Write( 0xFD23EC43 ); // format timestamp?
				writer.Write( firstTable ); // first table
				writer.Write( tableSize ); // table (block) size (= files per block)
				writer.Write( fileCount ); // file count
				writer.Write( 1 ); // modified count?
				writer.Write( 1 ); // ?
				writer.Write( 0 ); // ?

				// Padding
				for ( int i = 0x28; i < firstTable; ++i )
					writer.Write( (byte)0 );

				int tableCount = (int)Math.Ceiling( (double)fileCount / tableSize );
				TableEntry[] tableEntries = new TableEntry[tableSize];

				int maxId;
				string[] hashFormat = GetHashFormat( type, typeIndex, out maxId );

				for ( int i = 0; i < tableCount; ++i )
				{
					long thisTable = writer.BaseStream.Position;

					int idxStart = i * tableSize;
					int idxEnd = Math.Min( ( i + 1 ) * tableSize, fileCount );

					// Table header
					writer.Write( idxEnd - idxStart );  // files in this block
					writer.Write( (long)0 ); // next table, filled in later
					writer.Seek( 34 * tableSize, SeekOrigin.Current ); // table entries, filled in later

					// Data
					int tableIdx = 0;

					for ( int j = idxStart; j < idxEnd; ++j, ++tableIdx )
					{
                        // Special case: MultiCollection.uop
                        if ((type == FileType.MultiMUL) && (i == tableCount - 1) && (j == idxEnd - 1))
                        {
                            if (File.Exists("housing.bin"))
                            {
                                FileInfo binInfo = new FileInfo("housing.bin");
                                // MultiCollection.uop has the file "build/multicollection/housing.bin", which has to be treated separately
                                using (BinaryReader readerBin = OpenInput("housing.bin"))
                                {
                                    byte[] binData = new byte[binInfo.Length];
                                    int readLen = readerBin.Read(binData, 0, (int)binInfo.Length);

                                    tableEntries[tableIdx].m_Offset = writer.BaseStream.Position;
                                    tableEntries[tableIdx].m_Size = readLen;
                                    tableEntries[tableIdx].m_Identifier = HashLittle2("build/multicollection/housing.bin");
                                    tableEntries[tableIdx].m_Hash = HashAdler32(binData);

                                    writer.Write(binData, 0, readLen);
                                }
                            }
                            continue;
                        }

                        reader.BaseStream.Seek(idxEntries[j].m_Offset, SeekOrigin.Begin);
                        byte[] data = reader.ReadBytes(idxEntries[j].m_Size);
                        int sizeDecompressed = data.Length;

                        if (type == FileType.GumpartLegacyMUL)
                        {
                            // Prepend width/height from IDX's extra
                            sizeDecompressed += 8;
                            int width = (idxEntries[j].m_Extra >> 16) & 0xFFFF;
                            int height = idxEntries[j].m_Extra & 0xFFFF;

                            byte[] dataCopy = data;
                            data = new byte[sizeDecompressed];
                            data[0] = (byte)( width & 0xFF);
                            data[1] = (byte)((width >> 8)  & 0xFF);
                            data[2] = (byte)((width >> 16) & 0xFF);
                            data[3] = (byte)((width >> 24) & 0xFF);
                            data[4] = (byte)( height & 0xFF);
                            data[5] = (byte)((height >> 8)  & 0xFF);
                            data[6] = (byte)((height >> 16) & 0xFF);
                            data[7] = (byte)((height >> 24) & 0xFF);
                            Array.Copy(dataCopy, 0, data, 8, sizeDecompressed - 8);
                        }

                        int sizeOut;
                        byte[] dataOut;
                        if (compress != 0)
                        {
                            sizeOut = Zlib.CompressBound((ulong)sizeDecompressed);  // estimated maximum size
                            dataOut = new byte[sizeOut];
                            Zlib.Compress(dataOut, ref sizeOut, data, sizeDecompressed);
                        }
                        else
                        {
                            sizeOut = sizeDecompressed;
                            dataOut = data;
                        }

                        tableEntries[tableIdx].m_Offset = writer.BaseStream.Position;
                        tableEntries[tableIdx].m_Compression = compress;
						tableEntries[tableIdx].m_Size = sizeOut;
                        tableEntries[tableIdx].m_SizeDecompressed = sizeDecompressed;

                        // hash 906142efe9fdb38a, which is file 0009834.tga (and no others, as 7.0.59.5) use a different name format (7 digits instead of 8);
                        //  if in newer versions more of these files will have adopted that format, someone should update this list of exceptions
                        //  (even if this seems so much like a typo from someone from the UO development team :P )
                        if ((type == FileType.GumpartLegacyMUL) && (idxEntries[j].m_Id == 9834))
                            tableEntries[tableIdx].m_Identifier = HashLittle2(String.Format(hashFormat[1], idxEntries[j].m_Id));
                        else
                            tableEntries[tableIdx].m_Identifier = HashLittle2( String.Format( hashFormat[0], idxEntries[j].m_Id ) );
						tableEntries[tableIdx].m_Hash = HashAdler32(dataOut);

						writer.Write(dataOut, 0, sizeOut);
					}

                    long nextTable = writer.BaseStream.Position;

					// Go back and fix table header
					if ( i < tableCount - 1 )
					{
						writer.BaseStream.Seek( thisTable + 4, SeekOrigin.Begin );
						writer.Write( nextTable );
					}
					else
					{
						writer.BaseStream.Seek( thisTable + 12, SeekOrigin.Begin );
						// No need to fix the next table address, it's the last
					}

					// Table entries
					tableIdx = 0;

					for ( int j = idxStart; j < idxEnd; ++j, ++tableIdx )
					{
						writer.Write( tableEntries[tableIdx].m_Offset );
						writer.Write( 0 ); // header length
						writer.Write( tableEntries[tableIdx].m_Size ); // compressed size
						writer.Write( tableEntries[tableIdx].m_SizeDecompressed ); // decompressed size
						writer.Write( tableEntries[tableIdx].m_Identifier );
						writer.Write( tableEntries[tableIdx].m_Hash );
						writer.Write( tableEntries[tableIdx].m_Compression ); // compression method
					}

					// Fill remainder with empty entries
					for ( ; tableIdx < tableSize; ++tableIdx )
						writer.Write( m_EmptyTableEntry );

					writer.BaseStream.Seek( nextTable, SeekOrigin.Begin );
				}

			}
		}

		private static readonly byte[] m_EmptyTableEntry = new byte[8 + 4 + 4 + 4 + 8 + 4 + 2];

		// 
		// UOP -> MUL
		//
		public void FromUOP( string inFile, string outFile, string outFileIdx, FileType type, int typeIndex )
		{
			Dictionary<ulong, int> chunkIds = new Dictionary<ulong, int>();
            Dictionary<ulong, int> chunkIds2 = new Dictionary<ulong, int>();

            int maxId;
			string[] formats = GetHashFormat( type, typeIndex, out maxId );

			for ( int i = 0; i < maxId; ++i )
                chunkIds[HashLittle2(String.Format(formats[0], i))] = i;
            if (formats[1] != "")
            {
                for (int i = 0; i < maxId; ++i)
                    chunkIds2[HashLittle2(String.Format(formats[1], i))] = i;
            }

            bool[] used = new bool[maxId];

			using ( BinaryReader reader = OpenInput( inFile ) )
			using ( BinaryWriter writer = OpenOutput( outFile ) )
			using ( BinaryWriter writerIdx = OpenOutput( outFileIdx ) )
			{
				if ( reader.ReadInt32() != 0x50594D ) // MYP
					throw new ArgumentException( "inFile is not a UOP file." );

				Stream stream = reader.BaseStream;

				int version = reader.ReadInt32();
				reader.ReadInt32(); // format timestamp? 0xFD23EC43
				long nextTable = reader.ReadInt64();

                //List<string> toLog = new List<string>();

                do
                {
					// Table header
					stream.Seek( nextTable, SeekOrigin.Begin );
					int entries = reader.ReadInt32();
					nextTable = reader.ReadInt64();

					// Table entries
					TableEntry[] offsets = new TableEntry[entries];

					for ( int i = 0; i < entries; ++i )
					{
						/*
						 * Empty entries are read too, because they do not always indicate the
						 * end of the table. (Example: 7.0.26.4+ Fel/Tram maps)
						 */
						offsets[i].m_Offset = reader.ReadInt64();
						offsets[i].m_HeaderLength = reader.ReadInt32(); // header length
						offsets[i].m_Size = reader.ReadInt32(); // compressed size
                        offsets[i].m_SizeDecompressed = reader.ReadInt32(); // decompressed size
						offsets[i].m_Identifier = reader.ReadUInt64(); // filename hash (HashLittle2)
						offsets[i].m_Hash = reader.ReadUInt32(); // data hash (Adler32)
                        offsets[i].m_Compression = reader.ReadInt16(); // compression method (0 = none, 1 = zlib)
					}

                    // Copy chunks
					for ( int i = 0; i < offsets.Length; ++i )
					{
						if ( offsets[i].m_Offset == 0 )
							continue; // skip empty entry

                        if ((type == FileType.MultiMUL) && (offsets[i].m_Identifier == 0x126D1E99DDEDEE0A))
                        {
                            // MultiCollection.uop has the file "build/multicollection/housing.bin", which has to be handled separately
                            using (BinaryWriter writerBin = OpenOutput("housing.bin"))
                            {
                                stream.Seek(offsets[i].m_Offset + offsets[i].m_HeaderLength, SeekOrigin.Begin);
                                byte[] binData = reader.ReadBytes(offsets[i].m_Size);
                                byte[] binDataToWrite;

                                if (offsets[i].m_Compression == 1)
                                {
                                    byte[] binDataDecompressed = new byte[offsets[i].m_SizeDecompressed];
                                    Zlib.Decompress(binDataDecompressed, ref offsets[i].m_SizeDecompressed, binData, offsets[i].m_Size);
                                    binDataToWrite = binDataDecompressed;
                                }
                                else
                                {
                                    binDataToWrite = binData;
                                }

                                writerBin.Write(binDataToWrite, 0, binDataToWrite.Length);
                            }
                            continue;
                        }
                    
						int chunkID = -1;
						if ( !chunkIds.TryGetValue( offsets[i].m_Identifier, out chunkID ) )
                        {
                            int tmpChunkID = -1;
                            if ( !chunkIds2.TryGetValue(offsets[i].m_Identifier, out tmpChunkID) )
                                throw new Exception(String.Format("Unknown identifier encountered ({0:X})", offsets[i].m_Identifier));
                            else
                            {
                                chunkID = tmpChunkID;
                                //toLog.Add(String.Format("[DevInfo] Hash {0} has format type 2! ChunkID: {1}", offsets[i].m_Identifier, chunkID));
                            }
                        }

						stream.Seek( offsets[i].m_Offset + offsets[i].m_HeaderLength, SeekOrigin.Begin );
						byte[] chunkDataRaw = reader.ReadBytes( offsets[i].m_Size );

                        byte[] chunkData;
                        if (offsets[i].m_Compression == 1)
                        {
                            byte[] chunkDataDecompressed = new byte[offsets[i].m_SizeDecompressed];
                            Zlib.Decompress(chunkDataDecompressed, ref offsets[i].m_SizeDecompressed, chunkDataRaw, offsets[i].m_Size);
                            chunkData = chunkDataDecompressed;
                        }
                        else
                        {
                            chunkData = chunkDataRaw;
                        }

                        if ( type == FileType.MapLegacyMUL )
						{
							// Write this chunk on the right position (no IDX file to point to it)
							writer.Seek( chunkID * 0xC4000, SeekOrigin.Begin );
							writer.Write( chunkData );
						}
						else
						{
							int dataOffset = 0;

							#region Idx
							writerIdx.Seek( chunkID * 12, SeekOrigin.Begin );
							writerIdx.Write( (uint)writer.BaseStream.Position ); // Position

							switch ( type )
							{
								case FileType.GumpartLegacyMUL:
								{
									// Width and height are prepended to the data
									int width = ( chunkData[0] | ( chunkData[1] << 8 ) | ( chunkData[2] << 16 ) | ( chunkData[3] << 24 ) );
									int height = ( chunkData[4] | ( chunkData[5] << 8 ) | ( chunkData[6] << 16 ) | ( chunkData[7] << 24 ) );

									writerIdx.Write( chunkData.Length - 8 );
									writerIdx.Write( ( width << 16 ) | height );
									dataOffset = 8;
									break;
								}
								case FileType.SoundLegacyMUL:
								{
									// Extra contains the ID of this sound file + 1
									writerIdx.Write(chunkData.Length);
									writerIdx.Write( chunkID + 1 );
									break;
								}
								default:
								{
									writerIdx.Write(chunkData.Length); // Size
									writerIdx.Write( (int)0 ); // Extra
									break;
								}
							}

							used[chunkID] = true;
							#endregion

							writer.Write( chunkData, dataOffset, chunkData.Length - dataOffset );
						}
					}

					// Move to next table
					if ( nextTable != 0 )
						stream.Seek( nextTable, SeekOrigin.Begin );
				}
				while ( nextTable != 0 );

				// Fix idx
				// TODO: Only go until the last used entry? Does the client mind?
				if ( writerIdx != null )
				{
					for ( int i = 0; i < used.Length; ++i )
					{
						if ( !used[i] )
						{
							writerIdx.Seek( i * 12, SeekOrigin.Begin );
							writerIdx.Write( -1 );
							writerIdx.Write( (long)0 );
						}
					}
				}

                /*
                if (toLog.Count != 0)
                {
                    using (StreamWriter file = new StreamWriter(@"uop2mul_log.txt"))
                    {
                        for (int i = 0; i < toLog.Count; ++i)
                            file.WriteLine(toLog[i]);
                    }
                }
                toLog.Clear();
                */
			}
		}

		//
		// Hash filename formats (remember: lower case!)
		//
		public static string[] GetHashFormat( FileType type, int typeIndex, out int maxId )
		{
			/*
			 * MaxID is only used for constructing a lookup table.
			 * Decrease to save some possibly unneeded computation.
			 */
			maxId = 0x7FFFF;

			switch ( type )
			{
				case FileType.ArtLegacyMUL:
				{
					maxId = 0x13FDC; // UOFiddler requires this exact idx length to recognize UOHS art files (it checks with == operator, not with >=)
					return new string[] { "build/artlegacymul/{0:00000000}.tga", "" };
				}
				case FileType.GumpartLegacyMUL:
				{
					// MaxID = 0xEF3C on 7.0.8.2
					return new string[] { "build/gumpartlegacymul/{0:00000000}.tga", "build/gumpartlegacymul/{0:0000000}.tga" };
				}
				case FileType.MapLegacyMUL:
				{
					// MaxID = 0x71 on 7.0.8.2 for Fel/Tram
					return new string[] { String.Concat("build/map", typeIndex, "legacymul/{0:00000000}.dat"), "" };
				}
				case FileType.SoundLegacyMUL:
				{
					// MaxID = 0x1000 on 7.0.8.2
					return new string[] { "build/soundlegacymul/{0:00000000}.dat", "" };
				}
                case FileType.MultiMUL:
                    {
                        return new string[] { "build/multicollection/{0:000000}.bin", "" };
                    }
                default:
				{
					throw new ArgumentException( "Unknown file type!" );
				}
			}
		}

		//
		// Hash functions (EA didn't write these, see http://burtleburtle.net/bob/c/lookup3.c)
		//
		public static ulong HashLittle2( string s )
		{
			int length = s.Length;

			uint a, b, c;
			a = b = c = 0xDEADBEEF + (uint)length;

			int k = 0;

			while ( length > 12 )
			{
				a += s[k];
				a += ( (uint)s[k + 1] ) << 8;
				a += ( (uint)s[k + 2] ) << 16;
				a += ( (uint)s[k + 3] ) << 24;
				b += s[k + 4];
				b += ( (uint)s[k + 5] ) << 8;
				b += ( (uint)s[k + 6] ) << 16;
				b += ( (uint)s[k + 7] ) << 24;
				c += s[k + 8];
				c += ( (uint)s[k + 9] ) << 8;
				c += ( (uint)s[k + 10] ) << 16;
				c += ( (uint)s[k + 11] ) << 24;

				a -= c; a ^= ( ( c << 4 ) | ( c >> 28 ) ); c += b;
				b -= a; b ^= ( ( a << 6 ) | ( a >> 26 ) ); a += c;
				c -= b; c ^= ( ( b << 8 ) | ( b >> 24 ) ); b += a;
				a -= c; a ^= ( ( c << 16 ) | ( c >> 16 ) ); c += b;
				b -= a; b ^= ( ( a << 19 ) | ( a >> 13 ) ); a += c;
				c -= b; c ^= ( ( b << 4 ) | ( b >> 28 ) ); b += a;

				length -= 12;
				k += 12;
			}

			if ( length != 0 )
			{
				switch ( length )
				{
					case 12: c += ( (uint)s[k + 11] ) << 24; goto case 11;
					case 11: c += ( (uint)s[k + 10] ) << 16; goto case 10;
					case 10: c += ( (uint)s[k + 9] ) << 8; goto case 9;
					case 9: c += s[k + 8]; goto case 8;
					case 8: b += ( (uint)s[k + 7] ) << 24; goto case 7;
					case 7: b += ( (uint)s[k + 6] ) << 16; goto case 6;
					case 6: b += ( (uint)s[k + 5] ) << 8; goto case 5;
					case 5: b += s[k + 4]; goto case 4;
					case 4: a += ( (uint)s[k + 3] ) << 24; goto case 3;
					case 3: a += ( (uint)s[k + 2] ) << 16; goto case 2;
					case 2: a += ( (uint)s[k + 1] ) << 8; goto case 1;
					case 1: a += s[k]; break;
				}

				c ^= b; c -= ( ( b << 14 ) | ( b >> 18 ) );
				a ^= c; a -= ( ( c << 11 ) | ( c >> 21 ) );
				b ^= a; b -= ( ( a << 25 ) | ( a >> 7 ) );
				c ^= b; c -= ( ( b << 16 ) | ( b >> 16 ) );
				a ^= c; a -= ( ( c << 4 ) | ( c >> 28 ) );
				b ^= a; b -= ( ( a << 14 ) | ( a >> 18 ) );
				c ^= b; c -= ( ( b << 24 ) | ( b >> 8 ) );
			}

			return ( (ulong)b << 32 ) | c;
		}

		public static uint HashAdler32( byte[] d )
		{
			uint a = 1;
			uint b = 0;

			for ( int i = 0; i < d.Length; i++ )
			{
				a = ( a + d[i] ) % 65521;
				b = ( b + a ) % 65521;
			}

			return ( b << 16 ) | a;
		}
	}
}

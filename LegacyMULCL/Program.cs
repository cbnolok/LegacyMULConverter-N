using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using LegacyMUL;

namespace LegacyMULCL
{
	public class Program
	{
		private static LegacyMULConverter m_Converter;
		private static string m_Path;

		private static int m_Success;
		private static int m_Total;

		public static void Main( string[] args )
		{
            Console.WriteLine("LegacyMULConverter version N-2.1.");
            if ( args.Length == 0 || args[0] == "-h" || args[0] == "--help" || args[0] == "/?" )
			{
                Console.WriteLine( "Syntax:" );
				Console.WriteLine( "  -x <path>  Extracts known UOP files in <path> into MUL format." );
				Console.WriteLine( "  <path>     Packs known MUL files in <path> into UOP format." );
				Console.WriteLine();
				return;
			}

			string mode = args[0];

			switch ( mode )
			{
				case "-x":
				{
                    Console.WriteLine("Mode: Extract from UOP.");
                    Console.WriteLine();
					if ( args.Length < 2 )
					{
						Console.WriteLine( "Missing path name." );
						return;
					}

					string uopDir = args[1];

					if ( !Directory.Exists( uopDir ) )
					{
						Console.WriteLine( "Directory '{0}' does not exist!", uopDir );
						return;
					}

					m_Path = uopDir;
					m_Converter = new LegacyMULConverter();

					m_Success = m_Total = 0;

					Extract( "artLegacyMUL.uop", "art.mul", "artidx.mul", FileType.ArtLegacyMUL, 0 );
					Extract( "gumpartLegacyMUL.uop", "gumpart.mul", "gumpidx.mul", FileType.GumpartLegacyMUL, 0 );
                    Extract( "MultiCollection.uop", "multi.mul", "multiidx.mul", FileType.MultiMUL, 0 );
					Extract( "soundLegacyMUL.uop", "sound.mul", "soundidx.mul", FileType.SoundLegacyMUL, 0 );

					for ( int i = 0; i <= 5; ++i )
					{
						string map = String.Format( "map{0}", i );

						Extract( map + "LegacyMUL.uop", map + ".mul", null, FileType.MapLegacyMUL, i );
						Extract( map + "xLegacyMUL.uop", map + "x.mul", null, FileType.MapLegacyMUL, i );
					}

					PrintResults();
					break;
				}
				default:
				{
                    Console.WriteLine("Mode: Pack to UOP.");
                    Console.WriteLine();

					string mulDir = args[0];
					if ( !Directory.Exists( mulDir ) )
					{
						Console.WriteLine( "Directory '{0}' does not exist!", mulDir );
						return;
					}

					m_Path = mulDir;
					m_Converter = new LegacyMULConverter();

					m_Success = m_Total = 0;

					Pack( "art.mul", "artidx.mul", "artLegacyMUL.uop", FileType.ArtLegacyMUL, 0 );
                    Pack( "gumpart.mul", "gumpidx.mul", "gumpartLegacyMUL.uop", FileType.GumpartLegacyMUL, 0 );

                    if (!File.Exists("housing.bin"))
                    {
                        Console.WriteLine(" Warning: \"housing.bin\" not found, it won't be packed inside MultiCollection.uop (which probably won't work).");
                        Console.WriteLine("  First, unpack a vanilla MultiCollection.uop to extract \"housing.bin\" in the working directory.");
                    }
                    Pack( "multi.mul", "multiidx.mul", "MultiCollection.uop", FileType.MultiMUL, 0 );
                    
                    Pack( "sound.mul", "soundidx.mul", "soundLegacyMUL.uop", FileType.SoundLegacyMUL, 0 );

					for ( int i = 0; i <= 5; ++i )
					{
						string map = String.Format( "map{0}", i );

						Pack( map + ".mul", null, map + "LegacyMUL.uop", FileType.MapLegacyMUL, i );
						Pack( map + "x.mul", null, map + "xLegacyMUL.uop", FileType.MapLegacyMUL, i );
					}

					PrintResults();
					break;
				}
			}
		}

		private static string FixPath( string file )
		{
			return ( file == null ) ? null : Path.Combine( m_Path, file );
		}

		private static void PrintResults()
		{
			Console.WriteLine();

			if ( m_Success < m_Total )
				Console.WriteLine( "Errors: {0}", m_Total - m_Success );
			else
				Console.WriteLine( "All actions completed successfully." );
		}

		private static void Extract( string inFile, string outFile, string outIdx, FileType type, int typeIndex )
		{
			try
			{
				inFile = FixPath( inFile );

				if ( !File.Exists( inFile ) )
					return;

				Console.Write( "Extracting '{0}'... ", inFile );
				outFile = FixPath( outFile );

				if ( File.Exists( outFile ) )
				{
					Console.WriteLine( "file already exists." );
					return;
				}

				outIdx = FixPath( outIdx );
				++m_Total;

				m_Converter.FromUOP( inFile, outFile, outIdx, type, typeIndex );

				Console.WriteLine( "done." );
				++m_Success;
			}
			catch ( Exception e )
			{
				Console.WriteLine( "failed!" );
				Console.WriteLine();
				Console.WriteLine( e );

				Console.WriteLine();
				Console.WriteLine( "Press return to continue extraction." );
				Console.ReadLine();
			}
		}

		private static void Pack( string inFile, string inIdx, string outFile, FileType type, int typeIndex )
		{
			try
			{
				inFile = FixPath( inFile );

				if ( !File.Exists( inFile ) )
					return;

				Console.Write( "Packing '{0}'... ", inFile );
				outFile = FixPath( outFile );

				if ( File.Exists( outFile ) )
				{
					Console.WriteLine( "file already exists." );
					return;
				}

				inIdx = FixPath( inIdx );
				++m_Total;

				m_Converter.ToUOP( inFile, inIdx, outFile, type, typeIndex );

				Console.WriteLine( "done." );
				++m_Success;
			}
			catch ( Exception e )
			{
				Console.WriteLine( "failed!" );
				Console.WriteLine();
				Console.WriteLine( e );

				Console.WriteLine();
				Console.WriteLine( "Press return to continue extraction." );
				Console.ReadLine();
			}
		}
	}
}

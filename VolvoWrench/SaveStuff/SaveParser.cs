﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using VolvoWrench.ExtensionMethods;



/*  NOTES
 *  SaveGameState is .hl1
 *  RestoreAdjacenClientState is .hl2
 *  EntityPatch is .hl3
 *  TODO: figure out this:https://github.com/LestaD/SourceEngine2007/blob/43a5c90a5ada1e69ca044595383be67f40b33c61/src_main/engine/host_saverestore.cpp#L1399
 *  Note we need different parsers for .hl? files so the enum is indeed necesarry
 * 
 * 
 * 
 * 
 */




namespace VolvoWrench.SaveStuff
{
    public class Flag
    {
        public Flag(int t, float s, string type)
        {
            Ticks = t.ToString();
            Time = s.ToString(CultureInfo.InvariantCulture) + "s";
            Type = type;
        }

        public string Ticks { get; set; }
        public string Time { get; set; }
        public string Type { get; set; }
    }

    public class Listsave
    {
        [Serializable]
        public enum Hlfile
        {
            Hl1,
            Hl2,
            Hl3
        }

        public static string Chaptername(int chapter)
        {
            #region MapSwitch

            switch (chapter)
            {
                case 0:
                    return "Point Insertion";
                case 1:
                    return "A Red Letter Day";
                case 2:
                    return "Route Kanal";
                case 3:
                    return "Water Hazard";
                case 4:
                    return "Black Mesa East";
                case 5:
                    return "We don't go to Ravenholm";
                case 6:
                    return "Highway 17";
                case 7:
                    return "Sandtraps";
                case 8:
                    return "Nova Prospekt";
                case 9:
                    return "Entanglement";
                case 10:
                    return "Anticitizen One";
                case 11:
                    return "Follow Freeman!";
                case 12:
                    return "Our Benefactors";
                case 13:
                    return "Dark Energy";
                default:
                    return "Mod/Unknown";
            }

            #endregion
        }

        public static SaveFile ParseSaveFile(string file)
        {
            var result = new SaveFile();
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                result.FileName = Path.GetFileName(file);
                result.Files = new List<StateFileInfo>();
                result.Header = (Encoding.ASCII.GetString(br.ReadBytes(sizeof (int))));
                result.SaveVersion = br.ReadInt32();
                result.TokenTableFileTableOffset = br.ReadInt32();
                result.TokenCount = br.ReadInt32();
                result.TokenTableSize = br.ReadInt32();
                br.BaseStream.Seek(result.TokenTableSize + result.TokenTableFileTableOffset, SeekOrigin.Current);
                var endoffile = false;
                var check = br.ReadBytes(4);
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                if (check.Any(b => b == 0))
                {
                    var filenum = br.ReadInt32();
                }
                while (!endoffile && result.SaveVersion <= 116)
                {
                    if (UnexpectedEof(br, 260))
                    {
                        var tempvalv = new StateFileInfo
                        {
                            Data = new byte[0],
                            FileName = Encoding.ASCII.GetString(br.ReadBytes(260)).TrimEnd('\0').Replace("\0", "")
                            //BUG: bunch of \0 in string
                        };
                        if (UnexpectedEof(br, 8))
                        {
                            var filelength = br.ReadInt32();
                            tempvalv.MagicWord = Encoding.ASCII.GetString(br.ReadBytes(4))
                                .Trim('\0')
                                .Replace("\0", string.Empty);
                            br.BaseStream.Seek(-4, SeekOrigin.Current);
                            if (UnexpectedEof(br, 8) && filelength > 0)
                                tempvalv.Data = br.ReadBytes(filelength);
                            else
                                endoffile = true;
                        }
                        else
                            endoffile = true;
                        result.Files.Add(tempvalv);
                    }
                    else
                        endoffile = true;
                }
                for (var i = 0; i < result.Files.Count; i++)
                {
                    result.Files[i] = ParseStateFile(result.Files[i]);
                }
                result.Map = (result.Files.Last().FileName);
                return result;
            }
        }

        public static StateFileInfo ParseStateFile(StateFileInfo stateFile)
        {
            if (stateFile.Data.Length < 16)
                return stateFile;
            using (var br = new BinaryReader(new MemoryStream(stateFile.Data)))
            {
                var si = new SaveFileSectionsInfo_t();
                if (!UnexpectedEof(br,20))
                    return stateFile;
                stateFile.MagicWord = br.ReadString(4);
                stateFile.Version = br.ReadByte();
                si.nBytesSymbols = Math.Abs(br.ReadInt32());
                si.nSymbols = Math.Abs(br.ReadInt32());
                si.nBytesDataHeaders = Math.Abs(br.ReadInt32());
                si.nBytesData = Math.Abs(br.ReadInt32());
                if(!UnexpectedEof(br,si.nSymbols+si.nBytesDataHeaders+si.nBytesData))
                    return stateFile;
                stateFile.pSymbols = br.ReadBytes(si.nSymbols);
                stateFile.pDataHeaders = br.ReadBytes(si.nBytesDataHeaders);
                stateFile.pData = br.ReadBytes(si.nBytesData);
            }
            return stateFile;
        }

        public static Tuple<int, int[]> ParseEntityPatch(StateFileInfo stateFile)
        {
            using (var br = new BinaryReader(new MemoryStream(stateFile.Data)))
            {
                var entityIds = new List<int>();
                var size = br.ReadInt32();
                for (var i = 0; i < size; i++)
                {   
                    entityIds.Add(br.ReadInt32());
                }
                return new Tuple<int,int[]>(size,entityIds.ToArray());
            }
        }

        public static uint rotr(uint val, int shift)
        {
            var num = val;
            shift &= 0x1f; 
            while (Convert.ToBoolean(shift--))
            {
                var lobit = num & 1;
                num >>= 1;
                if (Convert.ToBoolean(lobit))
                    num |= 0x80000000;
            }
            return num;
        }

        /// <summary>
        /// Checks if the length the binaryreader is trying to read will be over the end of the file
        /// </summary>
        /// <param name="b"></param>
        /// <param name="lengthtocheck"></param>
        /// <returns></returns>
        public static bool UnexpectedEof(BinaryReader b, int lengthtocheck)
            => b.BaseStream.Position + lengthtocheck < b.BaseStream.Length;

        [Serializable]
        public class SaveFile
        {
            [Category("File")]
            [Description("The name of the file.")]
            public string FileName { get; set; }
            [Category("File")]
            [Description("The header or magic word of the file which identifies it. Should be ('J','S','A','V')")]
            public string Header { get; set; }
            [Category("File")]
            [Description("The version of the save files. This is for some reason 115 for nearly any save file. The people at valve forgot to change it for some reason probably.")]
            public int SaveVersion { get; set; }
            [Category("File")]
            [Description("The map the save was made on. This is the filename of the last statefile since that is the last one, that is the one the save was made on.")]
            public string Map { get; set; }
            [Category("Tokentable details")]
            [Description("The byte offset from the begining to the end of the File table.")]
            public int TokenTableFileTableOffset { get; set; }
            [Category("Tokentable details")]
            [Description("The byte offset from the begining until the end of the Token table.")]
            public int TokenTableSize { get; set; }
            [Category("Tokentable details")]
            [Description("The number of tokens in the Tokentable.")]
            public int TokenCount { get; set; }
            [Category("Statefiles")]
            [Description("The statefiles in the save. These store the actual state of the game. The last one is the current one.")]
            public List<StateFileInfo> Files { get; set; }
        }

        [Serializable]
        public class StateFileInfo
        {
            [Category("Statefile details")]
            [Description("This is the contents of the statefile as a byte array.")]
            public byte[] Data { get; set; }
            [Category("Statefile details")]
            [Description("Name of the statefile. (Mapname.hl?) There are mostly 3 of this per map.")]
            public string FileName { get; set; }
            [Category("Statefile details")]
            [Description("Length of the statefile")]
            public int Length { get; set; }
            [Category("Statefile details")]
            [Description("The identifier/header or magic word of the statefile. Should be ('V','A','L','V')")]
            public string MagicWord { get; set; }
            [Category("Statefile details")]
            [Description("Version of the statefile. Mostly 115 (same as the save for the same reason)")]
            public int Version { get; set; }
            [Category("Sections")]
            [Description("The offsets and lengths of the sections in the statefile.")]
            public SaveFileSectionsInfo_t SectionsInfo { get; set; }
            [Category("Sections")]
            public byte[] pData { get; set; }
            [Category("Sections")]
            public byte[] pDataHeaders { get; set; }
            [Category("Sections")]
            public byte[] pSymbols { get; set; }
        }

        #region DataDesc

        public const int SAVEGAME_MAPNAME_LEN = 32;
        public const int SAVEGAME_COMMENT_LEN = 80;
        public const int SAVEGAME_ELAPSED_LEN = 32;
        public const int SECTION_MAGIC_NUMBER = 0x54541234;
        public const int SECTION_VERSION_NUMBER = 2;
        public const int MAX_MAP_NAME = 32;

        private unsafe struct GAME_HEADER
        {
            private fixed char comment [80];
            private fixed char landmark [256];
            private int mapCount;
            // the number of map state files in the save file.  This is usually number of maps * 3 (.hl1, .hl2, .hl3 files)

            private fixed char mapName [32];
            private fixed char originMapName [32];
        };

        public class baseclientsections_t
        {
            public int entitysize;
            public int headersize;
            public int decalsize;
            public int musicsize;
            public int symbolsize;
             
            public int decalcount;
            public int musiccount;
            public int symbolcount;

            public int SumBytes()
            {
                return entitysize + headersize + decalsize + symbolsize + musicsize;
            }
        };

        public class clientsections_t : baseclientsections_t
        {
           public byte[] symboldata;
           public byte[] entitydata;
           public byte[] headerdata;
           public byte[] decaldata;
           public byte[] musicdata;
        }

        private unsafe struct SaveGameDescription_t
        {
            private int iSize;
            private int iTimestamp;
            private fixed char szComment [SAVEGAME_COMMENT_LEN];
            private fixed char szElapsedTime [SAVEGAME_ELAPSED_LEN];
            private fixed char szFileName [128];
            private fixed char szFileTime [32];
            private fixed char szMapName [SAVEGAME_MAPNAME_LEN];
            private fixed char szShortName [64];
            private fixed char szType [64];
        };

        private unsafe struct SaveHeader
        {
            private int connectionCount;
            private int lightStyleCount;
            private fixed char mapName [32];
            private int mapVersion;
            private int saveId;
            private int skillLevel;
            private fixed char skyName [32];
            private float time;
            private int version;
        };

        [Serializable]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class SaveFileSectionsInfo_t
        {
            public int nBytesData { get; set; }
            public int nBytesDataHeaders { get; set; }
            public int nBytesSymbols { get; set; }
            public int nSymbols { get; set; }

            public int SumBytes()
	        {
	        	return ( nBytesSymbols + nBytesDataHeaders + nBytesData );
	        }

    }

        public struct SaveFileSections_t
        {
        	public byte[] pSymbols { get; set; }
            public byte[] pDataHeaders { get; set; }
            public byte[] pData { get; set; }
        };

        public class saverestorelevelinfo_t
        {
            public int connectionCount; // Number of elements in the levelList[]
            // smooth transition
            public int fUseLandmark;
            public levellist_t[] levelList = new levellist_t[16]; // List of connections from this level
            public int mapVersion;
            public char[] szCurrentMapName = new char[MAX_MAP_NAME]; // To check global entities
            public char[] szLandmarkName = new char[20]; // landmark we'll spawn near in next level
            public float time;
            public Vector vecLandmarkOffset; // for landmark transitions
        }

        public class CSaveRestoreSegment
        {
            byte[] pBaseData;        // Start of all entity save data
            byte[] pCurrentData; // Current buffer pointer for sequential access
            int size;           // Current data size, aka, pCurrentData - pBaseData
            int bufferSize;     // Total space for data

            //---------------------------------
            // Symbol table
            //
            int tokenCount;     // Number of elements in the pTokens table
            byte[,] pTokens;		// Hash table of entity strings (sparse)
        }

        public class levellist_t
        {
            public char[] landmarkName = new char[MAX_MAP_NAME];
            public char[] mapName = new char[MAX_MAP_NAME];
            //edict_t* pentLandmark;
            public Vector vecLandmarkOrigin;
        }

        private struct entitytable_t
        {
            private string classname; // entity class name

            private int edictindex;
                // saved for if the entity requires a certain edict number when restored (players, world)

            private int flags; // This could be a short -- bit mask of transitions that this entity is in the PVS of
            private string globalname; // entity global name
            private int id; // Ordinal ID of this entity (used for entity <--> pointer conversions)
            private Vector landmarkModelSpace; // a fixed position in model space for comparison
            private int location; // Offset from the base data of this entity
            // NOTE: Brush models can be built in different coordiante systems
            //		in different levels, so this fixes up local quantities to match
            //		those differences.
            private string modelname;
            private int restoreentityindex; // the entity index given to this entity at restore time

            private int saveentityindex;
                // the entity index the entity had at save time ( for fixing up client side entities )

            private int size; // Byte size of this entity's data

            private void Clear()
            {
                id = -1;
                edictindex = -1;
                saveentityindex = -1;
                restoreentityindex = -1;
                location = 0;
                size = 0;
                flags = 0;
                classname = "";
                globalname = "";
                landmarkModelSpace = new Vector();
                modelname = "";
            }
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace System.IO.Compression.Structure
{
	public class ZipStructureBuilder
	{
		private readonly string _filenameInZip;

		private readonly uint _crc32;

		private readonly long _compressedSize;

		private readonly long _uncompressedSize;

		private readonly DateTime _currentTimestamp = DateTime.Now;

		private readonly Encoding _utf8Encoder = Encoding.UTF8;

		// Static CRC32 Table
		private static UInt32[] CrcTable = null;

		// Static constructor. Just invoked once in order to create the CRC32 lookup table.
		static ZipStructureBuilder()
		{
			// Generate CRC32 table
			CrcTable = new UInt32[256];
			for (int i = 0; i < CrcTable.Length; i++)
			{
				UInt32 c = (UInt32)i;
				for (int j = 0; j < 8; j++)
				{
					if ((c & 1) != 0)
						c = 3988292384 ^ (c >> 1);
					else
						c >>= 1;
				}
				CrcTable[i] = c;
			}
		}

		// ZIP Data structures
		private readonly List<byte> _localHeader = new List<byte>();

		private readonly List<byte> _centralDir = new List<byte>();

		private readonly List<byte> _trailer = new List<byte>();

		public byte[] LocalHeader
		{
			get
			{
				return _localHeader.ToArray();
			}
		}

		public byte[] CentralDirectoryRecord
		{
			get
			{
				return _centralDir.ToArray();
			}
		}

		public byte[] EOCDRecord
		{
			get
			{
				return _trailer.ToArray();
			}
		}

		public ZipStructureBuilder(
			string fileNameInZip,
			long compressedSize,
			long uncompressedSize,
			uint crc32
		)
		{
			_filenameInZip = fileNameInZip;
			_compressedSize = compressedSize;
			_uncompressedSize = uncompressedSize;
			_crc32 = crc32;

			long fileOffset, centralDirOffset;

			WriteLocalHeader();

			fileOffset = _localHeader.Count;
			fileOffset += _compressedSize;

			centralDirOffset = fileOffset;

			WriteCentralDirRecord();

			fileOffset += _centralDir.Count;

			WriteEndOfCentralDirRecord(_centralDir.Count, centralDirOffset, fileOffset);
		}

		/* Local file header:
            local file header signature     4 bytes  (0x04034b50)
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes

            filename (variable size)
            extra field (variable size)
      */
		private void WriteLocalHeader()
		{
			byte[] encodedFilename = _utf8Encoder.GetBytes(_filenameInZip);

			_localHeader.AddRange(new byte[] { 80, 75, 3, 4, 20, 0 }); // No extra header
			_localHeader.AddRange(BitConverter.GetBytes((ushort)0x0800)); // filename and comment encoding - hardcoded to UTF-8
			_localHeader.AddRange(BitConverter.GetBytes((ushort)8));  // zipping method
			_localHeader.AddRange(BitConverter.GetBytes(DateTimeToDosTime(_currentTimestamp))); // zipping date and time

			_localHeader.AddRange(BitConverter.GetBytes(_crc32));  // File CRC
			_localHeader.AddRange(BitConverter.GetBytes(get32bitSize(_compressedSize)));  // Compressed size
			_localHeader.AddRange(BitConverter.GetBytes(get32bitSize(_uncompressedSize)));  // Uncompressed size

			_localHeader.AddRange(BitConverter.GetBytes((ushort)encodedFilename.Length)); // filename length
			_localHeader.AddRange(BitConverter.GetBytes((ushort)0)); // extra length

			_localHeader.AddRange(encodedFilename);
		}

		/* Central directory's File header:
            central file header signature   4 bytes  (0x02014b50)
            version made by                 2 bytes
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes
            file comment length             2 bytes
            disk number start               2 bytes
            internal file attributes        2 bytes
            external file attributes        4 bytes
            relative offset of local header 4 bytes

            filename (variable size)
            extra field (variable size)
            file comment (variable size)
      */
		private void WriteCentralDirRecord()
		{
			byte[] encodedFilename = _utf8Encoder.GetBytes(_filenameInZip);

			_centralDir.AddRange(new byte[] { 80, 75, 1, 2, 23, 0, 20, 0 });
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0x0800)); // filename and comment encoding 
			_centralDir.AddRange(BitConverter.GetBytes((ushort)8));  // zipping method
			_centralDir.AddRange(BitConverter.GetBytes(DateTimeToDosTime(_currentTimestamp)));  // zipping date and time
			_centralDir.AddRange(BitConverter.GetBytes(_crc32)); // file CRC
			_centralDir.AddRange(BitConverter.GetBytes(get32bitSize(_compressedSize))); // compressed file size
			_centralDir.AddRange(BitConverter.GetBytes(get32bitSize(_uncompressedSize))); // uncompressed file size
			_centralDir.AddRange(BitConverter.GetBytes((ushort)encodedFilename.Length)); // Filename in zip
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0)); // extra length
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0)); // Comment (skipped)

			_centralDir.AddRange(BitConverter.GetBytes((ushort)0)); // disk=0
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0)); // file type: binary
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0)); // Internal file attributes
			_centralDir.AddRange(BitConverter.GetBytes((ushort)0x0)); // External file attributes (normal/readable)
			_centralDir.AddRange(BitConverter.GetBytes(get32bitSize(0)));  // Offset of header

			_centralDir.AddRange(encodedFilename);
		}

		/*
        End of central dir record:
            end of central dir signature    4 bytes  (0x06054b50)
            number of this disk             2 bytes
            number of the disk with the
            start of the central directory  2 bytes
            total number of entries in
            the central dir on this disk    2 bytes
            total number of entries in
            the central dir                 2 bytes
            size of the central directory   4 bytes
            offset of start of central
            directory with respect to
            the starting disk number        4 bytes
            zipfile comment length          2 bytes
            zipfile comment (variable size)
      */
		private void WriteEndOfCentralDirRecord(long centralDirSize, long centralDirOffset, long fileOffset)
		{
			_trailer.AddRange(new byte[] { 80, 75, 5, 6 });
			_trailer.AddRange(BitConverter.GetBytes((ushort)0)); // number of the disk 
			_trailer.AddRange(BitConverter.GetBytes((ushort)0)); // disk where central directory starts
			_trailer.AddRange(BitConverter.GetBytes((ushort)1)); // number of central directory records in disk
			_trailer.AddRange(BitConverter.GetBytes((ushort)1)); // total number of central directory records
			_trailer.AddRange(BitConverter.GetBytes((uint)centralDirSize)); // size of the central directory
			_trailer.AddRange(BitConverter.GetBytes((uint)centralDirOffset)); // offset of start of central directory with respect to the starting disk number
			_trailer.AddRange(BitConverter.GetBytes((ushort)0)); // Comment (skipped)
		}

		private uint get32bitSize(long size)
		{
			return size >= 0xFFFFFFFF ? 0xFFFFFFFF : (uint)size;
		}

		private uint DateTimeToDosTime(DateTime _dt)
		{
			return (uint)(
				(_dt.Second / 2) | (_dt.Minute << 5) | (_dt.Hour << 11) |
				(_dt.Day << 16) | (_dt.Month << 21) | ((_dt.Year - 1980) << 25));
		}

		// CRC32 utility functions
		public static uint InitializeCRC32Sum()
		{
			return 0 ^ 0xFFFFFFFF;
		}

		public static uint AddDataToCRC32Sum(byte data, uint currentCrc32)
		{
			return ZipStructureBuilder.CrcTable[(currentCrc32 ^ data) & 0xFF] ^ (currentCrc32 >> 8);
		}

		public static uint AddDataToCRC32Sum(byte[] content, uint currentCrc32)
		{
			for (uint i = 0; i < content.Length; i++)
			{
				currentCrc32 = ZipStructureBuilder.CrcTable[(currentCrc32 ^ content[i]) & 0xFF] ^ (currentCrc32 >> 8);
			}

			return currentCrc32;
		}

		public static uint GetFinalCRC32Sum(uint currentCrc32)
		{
			currentCrc32 ^= 0xFFFFFFFF;

			return currentCrc32;
		}
	}
}

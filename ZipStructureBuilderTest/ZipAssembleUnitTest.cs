using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Compression.Structure;
using System.Text;
using Xunit;

namespace Test
{
	public class ZipAssembleUnitTest
	{
		[Fact]
		public void Assemble_And_Test_Zip_File_Contents_And_Integrity()
		{
			// Arrange
			string content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
			
			string tempZip = Path.GetTempFileName();
			File.WriteAllBytes(tempZip, PrepareTestFileContent(content));

			string tempOut = Path.GetTempFileName();
			File.Delete(tempOut);

			string contentTest = string.Empty;

			// Act
			var exception = Record.Exception(() =>
			{
				var zip = ZipFile.Open(tempZip, ZipArchiveMode.Read, Encoding.UTF8);
				string pathOut = Path.GetFullPath(tempOut);
				zip.ExtractToDirectory(pathOut);

				string unzippedFile = Path.Combine(pathOut, "Test.txt");
				if (!File.Exists(unzippedFile))
					throw new Exception("File not found!");

				contentTest = File.ReadAllText(unzippedFile);

				zip.Dispose();

				Directory.Delete(pathOut, true);
			});

			// Assert
			Assert.Null(exception);
			Assert.Equal(content, contentTest);

			// Cleanup
			File.Delete(tempZip);
		}

		private static byte[] PrepareTestFileContent(string fileContent)
		{
			byte[] contentBytes = Encoding.UTF8.GetBytes(fileContent);

			using var memStream = new MemoryStream();

			var compressedStream = new DeflateStream(memStream, CompressionLevel.Optimal, true);

			compressedStream.Write(contentBytes);
			compressedStream.Flush();
			compressedStream.Dispose();

			byte[] compressedContent = memStream.ToArray();

			uint crc = ZipStructureBuilder.InitializeCRC32Sum();

			crc = ZipStructureBuilder.AddDataToCRC32Sum(contentBytes, crc);

			ZipStructureBuilder zipMdBuilder = new ZipStructureBuilder(
				"Test.txt",
				compressedContent.LongLength,
				contentBytes.LongLength,
				ZipStructureBuilder.GetFinalCRC32Sum(crc)
			);

			List<byte> zipFile = new List<byte>();
			zipFile.AddRange(zipMdBuilder.LocalHeader);
			zipFile.AddRange(compressedContent);
			zipFile.AddRange(zipMdBuilder.CentralDirectoryRecord);
			zipFile.AddRange(zipMdBuilder.EOCDRecord);

			File.WriteAllBytes( // Save the zip file inside the project root so it can be manually inspected later
				Path.Combine(
					AppContext.BaseDirectory,
					"..",
					"..",
					"..",
					"test.zip"
				),
				zipFile.ToArray());

			return zipFile.ToArray();
		}
	}
}
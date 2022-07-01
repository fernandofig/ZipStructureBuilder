# ZipStructureBuilder

This is an utility library to help you build a ZIP archive structure around a single pre-compressed file/data block, using only known parameters that you should have already, having compressed said file. It's basically a heavily stripped-down derivation of Jaime Olivares' excellent [ZipStorer library](https://github.com/jaime-olivares/zipstorer), so most credits go to him.

## What is it?

This is NOT (yet another) library to create Zip Archives. For that, you have ZipStorer mentioned above, and .NET's own ZipArchive class.

This library is meant only to be a helper in generating the required data structures so you can just concatenate them together to get a Zip file. As such, this library *doesn't compress anything* - it expects that you've already done that using some other method that implements the Deflate algorithm. There are native .NET classes for that or third parties' - it's your choice.

## Why? (don't read if bored)

On a project I was working on, I was dealing with storing files on a blob container on Azure Storage, and due to resource usage optimization concerns and possibility that those files could be rather large, those files were sent to the storage in a sort of streaming fashion (using `AppendBlobClient`).

Eventually, a requirement was added that the file on the storage needed to be compressed. "No problem", I thought, "I can just run the data stream through `GZipStream`, and that's done!". Well, the PO eventually came back to me that that was not gonna work: the files on the storage would eventually reach end-users, and the layman user wouldn't know what to do with a ".gz" file. So, the file needed to be ZIP compressed.

Here's the problem with the ZIP format - it's not really a compression format, it's an *archival* format, that actually can use many compression algorithms (although the Deflate algorithm is  more commonly used) and it's structured in a way that makes it unsuitable for streaming (be it transmission or reception). I won't go into details as to why - see [this excellent blog writeup](https://games.greggman.com/game/zip-rant/) on the ZIP format and its woes.

In my case ZIP was problematic for transmission (as I needed to send a ZIPed file to the blob stroage), and the problem in this case is that most of the metadata on its headers contains data that you only know when you've had compressed the entire block of data (or file, in my case). Obviously I don't know (e.g.) the compressed file size of the data I'm sending because I'd only know that once I have finished uploading the stream to blob!

As mentioned before, ZIP usually uses the Deflate compression algorithm. So, I thought: "What if I send just the compressed data to a temporary file on the blob storage (running it through `DeflateStream`), after that's done I'll have accumulated the byte counters I need to properly fill in the data on the headers and central directory structure of zip, so I can send the headers to a new blob, then append the data from the temporary blob into it, then append the trailer ZIP structure, and there I have a full ZIP file!". Thus, ZipStructureBuilder was born as a way to make it easier to generate the headers and ZIP structures.

## How to use it

This is very simplified - I'm purposefully leaving out the details on compressing the content, because that's not the point of the library, and some of the things below you'd actually do differently or in a different order if you were working in a stream fashion (e.g. update the CRC and have variables for byte counters for compressed and uncompressed totals and update them as you're running through the stream).

````csharp
byte[] plainContent = "This is a test";
byte[] compressedContent = deflateCompressMyContent(plainContent);

uint zipCrc = ZipStructureBuilder.InitializeCRC32Sum();
for (int i = 0; i < plainContent.Length; i++);
	zipCrc = ZipStructureBuilder.AddDataToCRC32Sum(plainContent[i], zipCrc);

var zipStructure = new ZipStructureBuilder(
	"Test.txt", // Filename inside the ZIP file
	compressedContent.LongLength,
	plainContent.LongLength,
	ZipStructureBuilder.GetFinalCRC32Sum(zipCrc)
);

List<byte> zipFile = new List<byte>();
zipFile.AddRange(zipStructure.LocalHeader);
zipFile.AddRange(compressedContent);
zipFile.AddRange(zipStructure.CentralDirectoryRecord);
zipFile.AddRange(zipStructure.EOCDRecord);

File.WriteAllBytes("test.zip", zipFile.ToArray());
````

One gotcha that's not really related to my library: If you're going to use `DeflateStream` to build your compressed data, make sure the DeflateStream instance is `Dispose`ed before commiting its contents to wherever and feeding its length to the ZipStructureBuilder constructor, because the compressed data stream is only 'finished' when the stream is closed.

## Limitations

There are a lot of them. Other than the file having to be zipped, the requirements on my project were pretty lax, so I took a lot of shortcuts and hard-coded a lot of stuff (that on ZipStorer was configurable) in order to keep the library usage simple and straightforward. I may revisit that in the future if there's demand:

 - Probably the biggest limitation currently: there's support for just a **single file** inside the ZIP;
 - There's no support for ZIP64: the specifications on my project didn't expect files larger than 4GiB or remotely close to it. ZIP64 on ZipStorer seemed slightly buggy anyway, and since I was pressed for time and wanted to keep the code simple, I just removed whatever ZIP64 support there was on ZipStorer;
 - There's no support for setting advanced properties/attributes on the files inside the zip, like filename encoding (hardcoded to UTF-8) or file timestamps (hardcoded to current date/time).

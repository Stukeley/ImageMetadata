using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ImageMetadata
{
	internal class Program
	{
		// The signature that starts each PNG file. This array is used to check if the opened file is a PNG or not
		private static byte[] PngSignature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

		// The signature that starts each BMP file. The first two bytes (0-1) are "BM" (66, 77). Bytes 6-9 are unused (0, 0, 0, 0). Bytes 21-24 are the width, 25-28 are the height.
		private static (byte, byte) BmpHeaderBM = (66, 77);
		private static (byte, byte, byte, byte) BmpHeaderReserved = (0, 0, 0, 0);

		// This is the function that will identify the file type (PNG/BMP)
		// If it's a PNG, it will call the function PngFile to do further operations
		// If it's a BMP, it will display the resolution right away (no more operations needed)
		private static void IdentifyFileType(string filePath)
		{
			// Buffer of the FileStream
			byte[] bytes = new byte[1024];

			using (var fs = new FileStream(filePath, FileMode.Open))
			{
				// First 1024 bytes - we'll use them to check if the file is a PNG or BMP
				int c = fs.Read(bytes, 0, bytes.Length);

				if (c <= 54)
				{
					Console.WriteLine("Error reading file! No bytes were read.");
					return;
				}
				else
				{
					if (bytes.Take(8).ToArray().SequenceEqual(PngSignature))
					{
						// PNG signature has 8 bytes that are always the same
						Console.WriteLine("This is a .png image.");
					}
					else
					{
						// BMP header has 14 bytes
						var header = bytes.Take(14).ToArray();

						if ((header[0], header[1]) == BmpHeaderBM && (header[6], header[7], header[8], header[9]) == BmpHeaderReserved)
						{
							Console.WriteLine("This is a .bmp image.");

							// Bytes 4-7 are the width, 8-11 are the height (in signature)
							// Take the whole signature - 40 bytes (skipping the first 14 bytes which are the header) and convert width and height to int32
							var signature = bytes.Skip(14).Take(40).ToArray();

							var width = BitConverter.ToInt32(signature, 4);
							var height = BitConverter.ToInt32(signature, 8);

							Console.WriteLine($"Resolution: {width}x{height} pixels.");
							return;
						}
						else
						{
							Console.WriteLine("This is not a valid .bmp or .png file!");
							return;
						}
					}
				}
			}

			PngFile(filePath);
		}

		// This method is called if a PNG is identified. It then goes over the chunks and displays their details
		private static void PngFile(string filePath)
		{
			// This is the first 8 bytes in the file - only read once
			byte[] signature = new byte[8];

			// This will be converted into chunk size (uint)
			byte[] chunkLength = new byte[4];

			// 4 bytes right after the length of each chunk
			byte[] chunkType = new byte[4];

			// The chunk's data can be up to 2^31 bytes, but we cannot store that many so we'll just read them in blocks of up to 1024
			// TODO: implement the above
			byte[] chunkData = new byte[1024];

			// Used for counting the chunks and displaying them in order
			int i = 1;

			// Total amount of bytes read - we often cannot read all at once (we don't have the memory to allocat an array for 2 million elements), so we need to do it partially until we read all
			int c = 0;

			using (var fs = new FileStream(filePath, FileMode.Open))
			{
				// Read the signature - we don't need to check it again, though
				fs.Read(signature, 0, signature.Length);

				// The IHDR chunk must appear first. It contains: Width (4 bytes) and Height (4 bytes)
				// So the first chunk: 4 bytes (length), 4 bytes (type), 8 bytes (width and height), 4 bytes (CRC) => 20 bytes

				byte[] width = new byte[4];
				byte[] height = new byte[4];

				uint size = 0;

				fs.Read(chunkLength, 0, chunkLength.Length);
				fs.Read(chunkType, 0, chunkType.Length);
				fs.Read(width, 0, width.Length);
				fs.Read(height, 0, height.Length);

				//! Important
				// Due to endianness (MSB, LSB), we might have to reverse the order of our arrays. Otherwise we will get incorrect values.
				// If the computer's architecture is Little Endian, reverse the order - because the chunk's length, as well as the values are in Big Endian
				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(chunkLength);
					Array.Reverse(width);
					Array.Reverse(height);
				}

				size = BitConverter.ToUInt32(chunkLength, 0);

				// Size - 8 (width + height) + 4 (CRC)
				fs.Read(chunkData, 0, (int)size - 4);

				Console.WriteLine($"Resolution: {BitConverter.ToUInt32(width, 0)}x{BitConverter.ToUInt32(height, 0)} pixels.\n");

				// size is just the length of chunk's DATA - chunk length itself, chunk type and CRC are 12 additional bytes in total
				Console.WriteLine($"Chunk {i++}. Type: {Encoding.ASCII.GetString(chunkType)}; size: {size + 12}");

				// While we can still read the length - subsequent chunks
				while (fs.Read(chunkLength, 0, chunkLength.Length) > 0)
				{
					if (BitConverter.IsLittleEndian)
					{
						Array.Reverse(chunkLength);
					}

					size = BitConverter.ToUInt32(chunkLength, 0);

					// Read the chunk type
					fs.Read(chunkType, 0, chunkType.Length);

					// Convert the chunk type
					string type = Encoding.ASCII.GetString(chunkType);

					Console.WriteLine($"Chunk {i++}. Type: {type}; size: {size}");

					// Read the rest of the chunk - read data in blocks
					while (size > 0)
					{
						if (size > 1024)
						{
							c += fs.Read(chunkData, 0, 1024);
							size -= 1024;
						}
						else
						{
							c += fs.Read(chunkData, 0, (int)size);
							size = 0;
						}
					}

					// CRC
					fs.Read(chunkData, 0, 4);

					c = 0;

					// End of chunk
				}
			}
		}

		public static void Main()
		{
			Console.WriteLine("Enter the file's path:");

			string filePath = Console.ReadLine();

			// Check if the file exists - if yes, proceed

			if (!File.Exists(filePath))
			{
				Console.WriteLine("File not found!");
				return;
			}

			IdentifyFileType(filePath);

			Console.ReadKey();
		}
	}
}

using System.Runtime.InteropServices;
using System.Text;

namespace SillySynth.SoundFont;

public readonly struct Sf2FileInfo()
{
	public string SoundEngine { get; init; } = "";
	public string BankName { get; init; } = "";
	public string RomName { get; init; } = "";
	public ushort RomVersionMajor { get; init; } = 0;
	public ushort RomVersionMinor { get; init; } = 0;
	public string Date { get; init; } = "";
	public string Designers { get; init; } = "";
	public string Product { get; init; } = "";
	public string Copyright { get; init; } = "";
	public string Comments { get; init; } = "";
	public string Tools { get; init; } = "";

	public static Sf2FileInfo Read(Stream stream, long end)
	{
		ushort soundFontVersionMajor = 0;
		ushort soundFontVersionMinor = 0;
		var soundEngine = "";
		var bankName = "";
		var romName = "";
		ushort romVersionMajor = 0;
		ushort romVersionMinor = 0;
		var date = "";
		var designers = "";
		var product = "";
		var copyright = "";
		var comments = "";
		var tools = "";

		var stringBuffer = Array.Empty<byte>();

		while (stream.Position < end && stream.Position < stream.Length)
		{
			var subHeader = Sf2RiffHeader.Read(stream);
			var subEnd = stream.Position + subHeader.Size;

			// Converting to a string is not optimal but this is good enough
			switch (Encoding.ASCII.GetString(subHeader.Type))
			{
				case "ifil": // Version
					stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref soundFontVersionMajor, 1)));
					stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref soundFontVersionMinor, 1)));
					break;
				case "isng": // Target Sound Engine
					soundEngine = ReadString(subHeader);
					break;
				case "INAM": // Sound Font Bank Names
					bankName = ReadString(subHeader);
					break;
				case "irom": // Sound ROM Name
					romName = ReadString(subHeader);
					break;
				case "iver": // Sound ROM Version
					stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref romVersionMajor, 1)));
					stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref romVersionMinor, 1)));
					break;
				case "ICRD": // Date of Creation
					date = ReadString(subHeader);
					break;
				case "IENG": // Sound Designers and Engineers
					designers = ReadString(subHeader);
					break;
				case "IPRD": // Product
					product = ReadString(subHeader);
					break;
				case "ICOP": // Copyright
					copyright = ReadString(subHeader);
					break;
				case "ICMT": // Comments
					comments = ReadString(subHeader);
					break;
				case "ISFT": // Tools
					tools = ReadString(subHeader);
					break;
			}

			stream.Position = subEnd;
		}

		return new()
		{
			SoundEngine = soundEngine,
			BankName = bankName,
			RomName = romName,
			RomVersionMajor = romVersionMajor,
			RomVersionMinor = romVersionMinor,
			Date = date,
			Designers = designers,
			Product = product,
			Copyright = copyright,
			Comments = comments,
			Tools = tools,
		};

		string ReadString(Sf2RiffHeader header)
		{
			if (header.Size > stringBuffer.Length)
				stringBuffer = new byte[header.Size];

			var buffer = stringBuffer.AsSpan()[..(int)header.Size];

			stream.ReadExactly(buffer);

			var nullIndex = buffer.IndexOf((byte)0);
			if (nullIndex >= 0)
				buffer = buffer[..nullIndex];

			return Encoding.UTF8.GetString(buffer);
		}
	}
}

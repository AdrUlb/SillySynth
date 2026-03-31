using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SillySynth.SoundFont;

internal sealed class Sf2File
{
	public required Sf2FileInfo Info { get; init; }
	public required short[] SampleData { get; init; }
	public required Sf2FilePresetData PresetData { get; init; }

	public static Sf2File Load(string filePath)
	{
		using var stream = File.OpenRead(filePath);
		var header = Sf2RiffHeader.Read(stream);
		if (!header.Type.SequenceEqual("RIFF"u8))
			throw new("FIXME");

		var end = stream.Position + header.Size;

		Span<byte> subtype = stackalloc byte[4];
		stream.ReadExactly(subtype);
		if (!subtype.SequenceEqual("sfbk"u8))
			throw new("FIXME");

		var info = new Sf2FileInfo();
		var sampleData = Array.Empty<short>();
		Sf2FilePresetData presetData = default;

		while (stream.Position < end && stream.Position < stream.Length)
		{
			var listHeader = Sf2RiffHeader.Read(stream);
			var listEnd = stream.Position + listHeader.Size;

			if (!listHeader.Type.SequenceEqual("LIST"u8))
				throw new("FIXME");

			stream.ReadExactly(subtype);

			if (subtype.SequenceEqual("INFO"u8))
			{
				info = Sf2FileInfo.Read(stream, listEnd);
			}
			else if (subtype.SequenceEqual("sdta"u8))
			{
				sampleData = ReadSampleData(stream, listEnd);
			}
			else if (subtype.SequenceEqual("pdta"u8))
			{
				presetData = ReadPresetData(stream, listEnd);
			}
			else
				throw new("FIXME");

			stream.Position = listEnd;
		}

		return new()
		{
			Info = info,
			SampleData = sampleData,
			PresetData = presetData,
		};
	}

	private static short[] ReadSampleData(Stream stream, long end)
	{
		var data = Array.Empty<short>();

		while (stream.Position < end && stream.Position < stream.Length)
		{
			var subHeader = Sf2RiffHeader.Read(stream);
			var subEnd = stream.Position + subHeader.Size;

			// Converting to a string is not optimal but this is good enough
			switch (Encoding.ASCII.GetString(subHeader.Type))
			{
				// TODO: 24-bit sample data with sm24 chunk
				case "smpl": // Version
					data = new short[(end - stream.Position) / 2];
					stream.ReadExactly(MemoryMarshal.AsBytes(data.AsSpan()));
					break;
			}

			stream.Position = subEnd;
		}

		return data;
	}

	private static Sf2FilePresetData ReadPresetData(Stream stream, long end)
	{
		Span<byte> presetNameBytes = stackalloc byte[20];
		var headers = Array.Empty<Sf2FilePresetData.PresetHeader>();
		var bags = Array.Empty<Sf2FilePresetData.Bag>();
		var modulatorLists = Array.Empty<Sf2FilePresetData.Modulator>();
		var generatorLists = Array.Empty<Sf2FilePresetData.Generator>();
		var instruments = Array.Empty<Sf2FilePresetData.Instrument>();
		var instrumentBags = Array.Empty<Sf2FilePresetData.Bag>();
		var instrumentModulatorLists = Array.Empty<Sf2FilePresetData.Modulator>();
		var instrumentGeneratorLists = Array.Empty<Sf2FilePresetData.Generator>();
		var sampleHeaders = Array.Empty<Sf2FilePresetData.SampleHeader>();

		while (stream.Position < end && stream.Position < stream.Length)
		{
			var subHeader = Sf2RiffHeader.Read(stream);
			var subEnd = stream.Position + subHeader.Size;

			// Converting to a string is not optimal but this is good enough
			var type = Encoding.ASCII.GetString(subHeader.Type);

			switch (type)
			{
				case "phdr":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.PresetHeader>();
						headers = new Sf2FilePresetData.PresetHeader[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref headers[i], 1)));

						break;
					}
				case "pbag":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Bag>();
						bags = new Sf2FilePresetData.Bag[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref bags[i], 1)));

						break;
					}
				case "pmod":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Modulator>();
						modulatorLists = new Sf2FilePresetData.Modulator[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref modulatorLists[i], 1)));

						break;
					}
				case "pgen":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Generator>();
						generatorLists = new Sf2FilePresetData.Generator[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref generatorLists[i], 1)));

						break;
					}
				case "inst":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Instrument>();
						instruments = new Sf2FilePresetData.Instrument[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instruments[i], 1)));

						break;
					}
				case "ibag":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Bag>();
						instrumentBags = new Sf2FilePresetData.Bag[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instrumentBags[i], 1)));

						break;
					}
				case "imod":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Modulator>();
						instrumentModulatorLists = new Sf2FilePresetData.Modulator[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instrumentModulatorLists[i], 1)));

						break;
					}
				case "igen":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.Generator>();
						instrumentGeneratorLists = new Sf2FilePresetData.Generator[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instrumentGeneratorLists[i], 1)));

						break;
					}
				case "shdr":
					{
						var count = (subEnd - stream.Position) / Unsafe.SizeOf<Sf2FilePresetData.SampleHeader>();
						sampleHeaders = new Sf2FilePresetData.SampleHeader[count];

						for (var i = 0; i < count; i++)
							stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref sampleHeaders[i], 1)));

						break;
					}
				default:
					Console.WriteLine(type);
					break;
			}

			stream.Position = subEnd;
		}

		return new()
		{
			PresetHeaders = headers,
			Bags = bags,
			Modulators = modulatorLists,
			Generators = generatorLists,
			Instruments = instruments,
			InstrumentBags = instrumentBags,
			InstrumentModulators = instrumentModulatorLists,
			InstrumentGenerators = instrumentGeneratorLists,
			SampleHeaders = sampleHeaders,
		};
	}
}

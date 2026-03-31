// https://moddingwiki.shikadi.net/wiki/MUS_Format

using System.Runtime.InteropServices;

namespace SillySynth;

public enum MusController : byte
{
	ChangeInstrument,
	BankSelect,
	Modulation,
	Volume,
	Pan,
	Expression,
	ReverbDepth,
	ChorusDepth,
	SustainPedal,
	SoftPedal,
}

public enum MusEvent
{
	ReleaseNote,
	PlayNote,
	PitchBend,
	System,
	Controller,
	EndOfMeasure,
	Finish,
}

public sealed class MusData
{
	public required ushort PrimaryChannelCount { get; init; }
	public required ushort SecondaryChannelCount { get; init; }
	public required ushort InstrumentCount { get; init; }
	public required byte[] InstructionBytes { get; init; }

	public static MusData Read(Stream stream)
	{
		var offset = stream.Position;

		Span<byte> header = stackalloc byte[4];
		stream.ReadExactly(header);
		if (!header.SequenceEqual("MUS\x1A"u8))
			throw new("FIXME");

		ushort songLength = 0;
		ushort songStart = 0;
		ushort primaryChannelCount = 0;
		ushort secondaryChannelCount = 0;
		ushort instrumentCount = 0;
		ushort reserved = 0;
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref songLength, 1)));
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref songStart, 1)));
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref primaryChannelCount, 1)));
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref secondaryChannelCount, 1)));
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instrumentCount, 1)));
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref reserved, 1)));

		stream.Position = offset + songStart;
		var instructions = new byte[songLength];
		stream.ReadExactly(instructions);

		return new()
		{
			PrimaryChannelCount = primaryChannelCount,
			SecondaryChannelCount = secondaryChannelCount,
			InstrumentCount = instrumentCount,
			InstructionBytes = instructions,
		};
	}
}
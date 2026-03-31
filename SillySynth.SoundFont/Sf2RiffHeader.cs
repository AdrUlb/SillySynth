using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SillySynth.SoundFont;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Sf2RiffHeader
{
	private readonly uint _type;
	private readonly uint _size;

	// ReSharper disable ConvertToAutoProperty

	public ReadOnlySpan<byte> Type => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in _type, 1));
	public uint Size => _size;

	// ReSharper restore ConvertToAutoProperty

	public static Sf2RiffHeader Read(Stream stream)
	{
		Unsafe.SkipInit(out Sf2RiffHeader header);
		stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref header, 1)));
		return header;
	}
}

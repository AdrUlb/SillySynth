using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SillySynth.SoundFont;

internal readonly struct Sf2FilePresetData
{
	[InlineArray(20)]
	private struct Name
	{
		private byte _element0;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct PresetHeader
	{
		private readonly Name _presetName;
		private readonly ushort _presetNumber;
		private readonly ushort _bankNumber;
		private readonly ushort _presetBagIndex;
		private readonly uint _library;
		private readonly uint _genre;
		private readonly uint _morphology;

		// ReSharper disable ConvertToAutoProperty

		public unsafe string PresetName
		{
			get
			{
				fixed (void* ptr = &_presetName)
					return Marshal.PtrToStringUTF8((nint)ptr) ?? "";
			}
		}

		public ushort PresetNumber => _presetNumber;
		public ushort BankNumber => _bankNumber;
		public ushort PresetBagIndex => _presetBagIndex;
		public uint Library => _library;
		public uint Genre => _genre;
		public uint Morphology => _morphology;

		// ReSharper restore ConvertToAutoProperty
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct Bag
	{
		private readonly ushort _generatorsIndex;
		private readonly ushort _modulatorsIndex;

		// ReSharper disable ConvertToAutoProperty

		public ushort GeneratorsIndex => _generatorsIndex;
		public ushort ModulatorsIndex => _modulatorsIndex;

		// ReSharper restore ConvertToAutoProperty
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct Modulator
	{
		private readonly ushort _sourceOperator;
		private readonly ushort _destinationOperator;
		private readonly short _amount;
		private readonly ushort _amountSourceOperator;
		private readonly ushort _transformOperator;

		// ReSharper disable ConvertToAutoProperty

		public ushort SourceOperator => _sourceOperator;
		public ushort DestinationOperator => _destinationOperator;
		public short Amount => _amount;
		public ushort AmountSourceOperator => _amountSourceOperator;
		public ushort TransformOperator => _transformOperator;

		// ReSharper restore ConvertToAutoProperty
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct Generator
	{
		private readonly ushort _generatorOperator;
		private readonly short _generatorAmount;

		// ReSharper disable ConvertToAutoProperty

		public ushort GeneratorOperator => _generatorOperator;
		public short GeneratorAmount => _generatorAmount;

		// ReSharper restore ConvertToAutoProperty
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct Instrument
	{
		private readonly Name _instrumentName;
		private readonly ushort _instrumentBagIndex;

		// ReSharper disable ConvertToAutoProperty

		public unsafe string InstrumentName
		{
			get
			{
				fixed (void* ptr = &_instrumentName)
					return Marshal.PtrToStringUTF8((nint)ptr) ?? "";
			}
		}

		public ushort InstrumentBagIndex => _instrumentBagIndex;

		// ReSharper restore ConvertToAutoProperty
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct SampleHeader
	{
		private readonly Name _sampleName;
		private readonly int _start;
		private readonly int _end;
		private readonly int _startLoop;
		private readonly int _endLoop;
		private readonly uint _sampleRate;
		private readonly byte _originalPitch;
		private readonly sbyte _pitchCorrection;
		private readonly ushort _sampleLink;
		private readonly ushort _sampleType;

		// ReSharper disable ConvertToAutoProperty

		public unsafe string SampleName
		{
			get
			{
				fixed (void* ptr = &_sampleName)
					return Marshal.PtrToStringUTF8((nint)ptr) ?? "";
			}
		}

		public int Start => _start;
		public int End => _end;
		public int StartLoop => _startLoop;
		public int EndLoop => _endLoop;
		public uint SampleRate => _sampleRate;
		public byte OriginalPitch => _originalPitch;
		public sbyte PitchCorrection => _pitchCorrection;
		public ushort SampleLink => _sampleLink;
		public ushort SampleType => _sampleType;

		// ReSharper restore ConvertToAutoProperty
	}

	public PresetHeader[] PresetHeaders { get; init; }
	public Bag[] Bags { get; init; }
	public Modulator[] Modulators { get; init; }
	public Generator[] Generators { get; init; }
	public Instrument[] Instruments { get; init; }
	public Bag[] InstrumentBags { get; init; }
	public Modulator[] InstrumentModulators { get; init; }
	public Generator[] InstrumentGenerators { get; init; }
	public SampleHeader[] SampleHeaders { get; init; }
}

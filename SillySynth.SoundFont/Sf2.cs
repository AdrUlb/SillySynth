using System.Diagnostics.CodeAnalysis;

namespace SillySynth.SoundFont;

public sealed class Sf2
{
	private record struct PresetKey(int Preset, int Bank);

	private record struct ModulatorKey(ushort Source, ushort Destination, ushort AmountSource, ushort Transform);

	public Sf2FileInfo Info { get; }
	private readonly Dictionary<PresetKey, Sf2Preset> _presets;
	private readonly Sf2Instrument[] _instruments;
	private readonly Sf2Sample[] _samples;

	private Sf2(Sf2FileInfo info, Dictionary<PresetKey, Sf2Preset> presets, Sf2Instrument[] instruments, Sf2Sample[] samples)
	{
		Info = info;
		_presets = presets;
		_instruments = instruments;
		_samples = samples;
	}

	public bool TryGetPreset(int index, int bank, [MaybeNullWhen(false)] out Sf2Preset preset)
	{
		return _presets.TryGetValue(new(index, bank), out preset);
	}

	public bool TryGetInstrument(int index, [MaybeNullWhen(false)] out Sf2Instrument instrument)
	{
		if (index < 0 || index >= _instruments.Length)
		{
			instrument = null;
			return false;
		}

		instrument = _instruments[index];
		return true;
	}

	public bool TryGetSample(int index, [MaybeNullWhen(false)] out Sf2Sample sample)
	{
		if (index < 0 || index >= _samples.Length)
		{
			sample = null;
			return false;
		}

		sample = _samples[index];
		return true;
	}

	public static Sf2 Load(Stream stream)
	{
		var file = Sf2File.Load(stream);

		var presets = new Dictionary<PresetKey, Sf2Preset>();

		for (var i = 0; i < file.PresetData.PresetHeaders.Length - 1; i++)
		{
			var header = file.PresetData.PresetHeaders[i];
			var headerNext = file.PresetData.PresetHeaders[i + 1];

			var zones = ParseZones(
				file.PresetData.Bags,
				file.PresetData.Generators,
				file.PresetData.Modulators,
				header.PresetBagIndex,
				headerNext.PresetBagIndex,
				Sf2SynthParam.Instrument
			);

			var preset = new Sf2Preset(header.PresetName, header.PresetNumber, header.BankNumber, zones.ToArray());

			presets.Add(new(header.PresetNumber, header.BankNumber), preset);
		}

		var instruments = new Sf2Instrument[file.PresetData.Instruments.Length - 1];

		for (var i = 0; i < instruments.Length; i++)
		{
			var instrument = file.PresetData.Instruments[i];
			var instrumentNext = file.PresetData.Instruments[i + 1];

			var zones = ParseZones(
				file.PresetData.InstrumentBags,
				file.PresetData.InstrumentGenerators,
				file.PresetData.InstrumentModulators,
				instrument.InstrumentBagIndex,
				instrumentNext.InstrumentBagIndex,
				Sf2SynthParam.SampleId
			);

			instruments[i] = new(instrument.InstrumentName, zones);
		}

		var samples = new Sf2Sample[file.PresetData.SampleHeaders.Length - 1];

		for (var i = 0; i < samples.Length; i++)
		{
			ref var sample = ref file.PresetData.SampleHeaders[i];
			var name = sample.SampleName;
			var data = file.SampleData[sample.Start..sample.End];
			var loopStart = sample.StartLoop - sample.Start;
			var loopEnd = sample.EndLoop - sample.Start;
			samples[i] = new(name, data, loopStart, loopEnd, sample.SampleRate, sample.OriginalPitch, sample.PitchCorrection, sample.SampleType, sample.SampleLink);
		}

		return new(file.Info, presets, instruments, samples);
	}

	private static Sf2Zone[] ParseZones(
		Sf2FilePresetData.Bag[] fileBags,
		Sf2FilePresetData.Generator[] fileGenerators,
		Sf2FilePresetData.Modulator[] fileModulators,
		int bagStart,
		int bagEnd,
		Sf2SynthParam globalOpId)
	{
		var globalGenerators = new Sf2Generators();
		var globalModulators = new Dictionary<ModulatorKey, Sf2Modulator>();

		// TODO: default modulators (only for instruments)

		var zones = new List<Sf2Zone>();

		for (var bagIndex = bagStart; bagIndex < bagEnd; bagIndex++)
		{
			var bag = fileBags[bagIndex];
			var bagNext = fileBags[bagIndex + 1];

			var genStart = bag.GeneratorsIndex;
			var genEnd = bagNext.GeneratorsIndex;

			// If this is the first bag and either
			//   - there are no generators or
			//   - the last generator is NOT an instrument
			// this bag is the global zone
			var isGlobalZone = bagIndex == bagStart && (genStart == genEnd || (Sf2SynthParam)fileGenerators[genEnd - 1].GeneratorOperator != globalOpId);

			var generators = isGlobalZone ? globalGenerators : globalGenerators.Clone();
			var modulators = isGlobalZone ? globalModulators : globalModulators.ToDictionary();

			for (var genIndex = genStart; genIndex < genEnd; genIndex++)
			{
				var gen = fileGenerators[genIndex];
				generators.SetValue((Sf2SynthParam)gen.GeneratorOperator, gen.GeneratorAmount);
			}

			var modStart = bag.ModulatorsIndex;
			var modEnd = bagNext.ModulatorsIndex;

			for (var modIndex = modStart; modIndex < modEnd; modIndex++)
			{
				var mod = fileModulators[modIndex];
				var key = new ModulatorKey(mod.SourceOperator, mod.DestinationOperator, mod.AmountSourceOperator, mod.TransformOperator);
				modulators[key] = new(mod.SourceOperator, mod.DestinationOperator, mod.AmountSourceOperator, mod.TransformOperator, mod.Amount);
			}

			if (!isGlobalZone)
				zones.Add(new(generators, modulators.Values.ToArray()));
		}

		return zones.ToArray();
	}
}

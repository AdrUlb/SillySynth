using SillySynth.SoundFont;

namespace SillySynth;

internal sealed class MusSynthesizerChannel
{
	private const int _maxVoices = 64;

	public byte Velocity = 100;
	public byte Volume = 100;
	public byte Pan = 64;
	public byte Instrument = 0;

	private const float _pitchBendRange = 2.0f;
	public float PitchBendMultiplier { get; private set; } = 1.0f;

	public byte PitchBend
	{
		get => field;
		set
		{
			field = value;
			var normalized = (value - 128) / 128.0;
			var cents = normalized * _pitchBendRange * 100.0;
			PitchBendMultiplier = (float)double.Pow(2.0, cents / 1200.0);
		}
	}

	public readonly MusSynthesizerVoice[] Voices = new MusSynthesizerVoice[_maxVoices];

	public MusSynthesizerChannel()
	{
		for (var i = 0; i < Voices.Length; i++)
			Voices[i] = new(this);
	}

	public void SpawnVoice(
		Sf2Sample sample,
		byte note,
		int velocity,
		uint outputSampleRate,
		Sf2Generators generators)
	{
		var exclusiveClass = generators.GetValue(Sf2SynthParam.ExclusiveClass);

		if (exclusiveClass != 0)
		{
			for (var i = 0; i < Voices.Length; i++)
			{
				ref var v = ref Voices[i];
				if (v.IsActive && v.ExclusiveClass == exclusiveClass)
					v.ExclusiveKill();
			}
		}

		var slot = 0;
		while (slot < Voices.Length && Voices[slot].IsActive)
			slot++;

		if (slot >= Voices.Length)
		{
			//Console.WriteLine("[MusSynthesizer] Maximum number of voices per channel exceeded!");
			return;
		}

		Voices[slot].Initialize(sample, note, velocity, outputSampleRate, generators);
	}
}

public sealed class MusSynthesizer : IMusSynthesizer
{
	private readonly Sf2 _soundFont;
	private readonly ushort _sampleRate;
	private readonly MusSynthesizerChannel[] _channels = new MusSynthesizerChannel[16];

	public MusSynthesizer(Sf2 soundFont, ushort sampleRate)
	{
		_soundFont = soundFont;
		_sampleRate = sampleRate;
		for (var i = 0; i < _channels.Length; i++)
			_channels[i] = new();
	}

	public void PlayNote(byte channel, byte note)
	{
		PlayNote(channel, note, _channels[channel].Velocity);
	}

	public void PlayNote(byte channel, byte note, byte velocity)
	{
		if (velocity == 0)
		{
			ReleaseNote(channel, note);
			return;
		}

		channel %= (byte)_channels.Length;
		ref var chan = ref _channels[channel];

		SpawnVoices(channel, note, velocity);

		chan.Velocity = velocity;
	}

	public void ReleaseNote(byte channel, byte note)
	{
		channel %= (byte)_channels.Length;
		ref var chan = ref _channels[channel];

		for (var i = 0; i < chan.Voices.Length; i++)
		{
			ref var voice = ref chan.Voices[i];
			if (voice.Note == note)
				voice.Release();
		}
	}

	public void PitchBend(byte channel, byte amount)
	{
		channel %= (byte)_channels.Length;
		_channels[channel].PitchBend = amount;
	}

	public void Controller(byte channel, MusController controller, byte value)
	{
		switch (controller)
		{
			case MusController.ChangeInstrument:
				_channels[channel].Instrument = value;
				break;
			case MusController.Volume:
				_channels[channel].Volume = value;
				break;
			case MusController.Pan:
				_channels[channel].Pan = value;
				break;
			default:
				//Console.WriteLine($"[MusSynthesizer] TODO: Controller {controller}");
				break;
		}
	}

	public void Render(Span<float> outputBuffer)
	{
		outputBuffer.Clear();

		for (var i = 0; i < _channels.Length; i++)
		{
			ref var chan = ref _channels[i];

			foreach (var voice in chan.Voices)
			{
				if (voice.IsActive)
					voice.Render(outputBuffer);
			}
		}

		// Apply a master gain and soft clipping
		const float masterGain = 0.5f;
		for (var i = 0; i < outputBuffer.Length; i++)
			outputBuffer[i] = float.Tanh(outputBuffer[i] * masterGain);
	}

	private void SpawnVoices(byte channel, byte note, byte velocity)
	{
		var bankNumber = channel == 15 ? 128 : 0;

		var presetNumber = _channels[channel].Instrument;

		if (!_soundFont.TryGetPreset(presetNumber, bankNumber, out var preset))
		{
			//Console.WriteLine($"[MusSynthesizer] Failed to find preset for channel {channel} ({bankNumber:000}:{presetNumber:000})");
			return;
		}

		foreach (var zone in preset.Zones)
		{
			zone.Generators.GetRange(Sf2SynthParam.KeyRange, out var minKey, out var maxKey);
			if (note < minKey || note > maxKey)
				continue;

			zone.Generators.GetRange(Sf2SynthParam.VelRange, out var minVel, out var maxVel);
			if (velocity < minVel || velocity > maxVel)
				continue;

			var instrumentIndex = zone.Generators.GetValue(Sf2SynthParam.Instrument);

			if (_soundFont.TryGetInstrument(instrumentIndex, out var instrument))
			{
				SpawnInstrumentVoices(channel, note, velocity, instrument, zone.Generators);
			}
		}

		//Console.WriteLine(_channels.Sum(c => c.Voices.Count(v => v.IsActive)));
	}

	private void SpawnInstrumentVoices(byte channel, byte note, byte velocity, Sf2Instrument instrument, Sf2Generators presetGenerators)
	{
		foreach (var zone in instrument.Zones)
		{
			zone.Generators.GetRange(Sf2SynthParam.KeyRange, out var minKey, out var maxKey);
			if (note < minKey || note > maxKey)
				continue;

			zone.Generators.GetRange(Sf2SynthParam.VelRange, out var minVel, out var maxVel);
			if (velocity < minVel || velocity > maxVel)
				continue;

			var sampleIndex = zone.Generators.GetValue(Sf2SynthParam.SampleId);

			if (_soundFont.TryGetSample(sampleIndex, out var sample))
			{
				var generators = Sf2Generators.Flatten(presetGenerators, zone.Generators);
				/*
				Console.WriteLine(instrument.Name);
				Console.WriteLine($"  Sample: {sample.Name}");
				Console.WriteLine($"  Note: {note}");
				Console.WriteLine($"  Sample Type: {sample.SampleType}");
				Console.WriteLine($"  Pan: {generators.GetValue(Sf2SynthParam.Pan)}");
				Console.WriteLine($"  Decay Timecents: {generators.GetValue(Sf2SynthParam.DecayVolEnv)}");
				Console.WriteLine($"  Release Timecents: {generators.GetValue(Sf2SynthParam.ReleaseVolEnv)}");
				Console.WriteLine($"  Root Override: {generators.GetValue(Sf2SynthParam.OverridingRootKey)}");
				Console.WriteLine($"  Scale Tuning: {generators.GetValue(Sf2SynthParam.ScaleTuning)}");
				Console.WriteLine($"  Exclusive Class: {generators.GetValue(Sf2SynthParam.ExclusiveClass)}");
				Console.WriteLine($"  Sustain cB: {generators.GetValue(Sf2SynthParam.SustainVolEnv)}");
				*/

				_channels[channel].SpawnVoice(sample, note, velocity, _sampleRate, generators);
			}
		}
	}
}

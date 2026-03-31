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

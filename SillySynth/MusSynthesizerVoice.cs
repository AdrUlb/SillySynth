using SillySynth.SoundFont;
using System.Runtime.CompilerServices;

namespace SillySynth;

internal struct MusSynthesizerVoice(MusSynthesizerChannel channel)
{
	public byte Note { get; private set; }

	private Memory<short> _samples;
	private uint _outputSampleRate;
	private int _loopStart;
	private int _loopEnd;

	public bool IsActive => !_volEnv.IsDone;

	public short ExclusiveClass { get; private set; }

	private int _sampleModes;

	private double _increment;
	private double _playhead;

	private float _velocityGain;
	private float _initialAttenuation;
	private short _pan;

	private VolumeEnvelope _volEnv = new();
	private ModEnvelope _modEnv = new();

	private float _modEnvToFilterCents;
	private float _modEnvToPitchCents;

	private LowPassFilter _filter;

	private LowFrequencyOscillator _vibratoLowFrequencyOscillator;
	private LowFrequencyOscillator _modLowFrequencyOscillator;

	private float _vibLfoToPitchCents;
	private float _modLfoToPitchCents;
	private float _modLfoToVolumeCb;

	private const float _gainFilterStrength = 0.99f;
	private float _gainFilter;
	private const int _filterUpdateInterval = 32;
	private int _filterUpdateStep;

	public void Initialize(
		Sf2Sample sample,
		byte note,
		int velocity,
		uint outputSampleRate,
		Sf2Generators generators)
	{
		Note = note;
		_samples = sample.Data;
		_outputSampleRate = outputSampleRate;
		_loopStart = sample.LoopStart;
		_loopEnd = sample.LoopEnd;

		var overridingRootKey = generators.GetValue(Sf2SynthParam.OverridingRootKey);
		var coarseTune = generators.GetValue(Sf2SynthParam.CoarseTune);
		var fineTune = generators.GetValue(Sf2SynthParam.FineTune);
		var scaleTuning = generators.GetValue(Sf2SynthParam.ScaleTuning);

		var rootKey = overridingRootKey >= 0 ? overridingRootKey : sample.OriginalPitch;

		var keyDiffCents = (note - rootKey) * scaleTuning;
		var totalCents = (coarseTune * 100.0) + fineTune + sample.PitchCorrection + keyDiffCents;

		var pitchShift = double.Pow(2.0, totalCents / 1200.0);

		_increment = (double)sample.SampleRate / outputSampleRate * pitchShift;
		_playhead = 0;

		ExclusiveClass = generators.GetValue(Sf2SynthParam.ExclusiveClass);
		_sampleModes = generators.GetValue(Sf2SynthParam.SampleModes);

		var initialAttenuation = generators.GetValue(Sf2SynthParam.InitialAttenuation);
		_velocityGain = (velocity / 127.0f) * (velocity / 127.0f);
		_initialAttenuation = float.Max(0.0f, initialAttenuation);
		_pan = generators.GetValue(Sf2SynthParam.Pan);

		_modEnv.Initialize(
			outputSampleRate,
			generators.GetValue(Sf2SynthParam.DelayModEnv),
			generators.GetValue(Sf2SynthParam.AttackModEnv),
			generators.GetValue(Sf2SynthParam.HoldModEnv),
			generators.GetValue(Sf2SynthParam.DecayModEnv),
			generators.GetValue(Sf2SynthParam.SustainModEnv),
			generators.GetValue(Sf2SynthParam.ReleaseModEnv)
		);

		_volEnv.Initialize(
			outputSampleRate,
			generators.GetValue(Sf2SynthParam.DelayVolEnv),
			generators.GetValue(Sf2SynthParam.AttackVolEnv),
			generators.GetValue(Sf2SynthParam.HoldVolEnv),
			generators.GetValue(Sf2SynthParam.DecayVolEnv),
			generators.GetValue(Sf2SynthParam.SustainVolEnv),
			generators.GetValue(Sf2SynthParam.ReleaseVolEnv)
		);

		_modEnvToFilterCents = generators.GetValue(Sf2SynthParam.ModEnvToFilterFc);
		_modEnvToPitchCents = generators.GetValue(Sf2SynthParam.ModEnvToPitch);

		_filter.Initialize(
			_outputSampleRate,
			generators.GetValue(Sf2SynthParam.InitialFilterFc),
			generators.GetValue(Sf2SynthParam.InitialFilterQ)
		);

		_vibratoLowFrequencyOscillator.Initialize(
			outputSampleRate,
			generators.GetValue(Sf2SynthParam.DelayVibLfo),
			generators.GetValue(Sf2SynthParam.FreqVibLfo)
		);

		_vibLfoToPitchCents = generators.GetValue(Sf2SynthParam.VibLfoToPitch);

		_modLowFrequencyOscillator.Initialize(
			outputSampleRate,
			generators.GetValue(Sf2SynthParam.DelayModLfo),
			generators.GetValue(Sf2SynthParam.FreqModLfo)
		);

		_modLfoToPitchCents = generators.GetValue(Sf2SynthParam.ModLfoToPitch);
		_modLfoToVolumeCb = generators.GetValue(Sf2SynthParam.ModLfoToVolume);

		_filterUpdateStep = 0;
	}

	public void Release()
	{
		_modEnv.Release();
		_volEnv.Release();
	}

	public void ExclusiveKill()
	{
		_modEnv.FastRelease();
		_volEnv.FastRelease();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public void Render(Span<float> buffer)
	{
		if (!IsActive)
			return;

		var sampleSpan = _samples.Span;
		var channelVolume = channel.Volume / 127.0f;
		var channelGain = channelVolume * channelVolume;

		var midiPan = (channel.Pan - 64) / 63.0f;
		var combinedPan = float.Clamp(_pan / 500.0f + midiPan, -1.0f, 1.0f);

		var p = (combinedPan + 1.0f) * 0.5f;

		var panLeft = float.Cos(p * float.Pi / 2.0f);
		var panRight = float.Sin(p * float.Pi / 2.0f);

		for (var i = 0; i < buffer.Length; i += 2)
		{
			var envCb = _volEnv.Process();

			if (!IsActive)
				return;

			// Process LFOs
			var vibValue = _vibratoLowFrequencyOscillator.Process();
			var modValue = _modLowFrequencyOscillator.Process();

			var modEnvValue = _modEnv.Process();

			if (_filterUpdateStep++ >= _filterUpdateInterval)
			{
				_filter.Update(modEnvValue * _modEnvToFilterCents);
				_filterUpdateStep = 0;
			}

			// Apply LFO pitch
			var totalPitchModCents = (vibValue * _vibLfoToPitchCents) + (modValue * _modLfoToPitchCents) + (modEnvValue * _modEnvToPitchCents);
			var lfoPitchMultiplier = (float)double.Pow(2.0, totalPitchModCents / 1200.0);
			var currentIncrement = _increment * channel.PitchBendMultiplier * lfoPitchMultiplier;

			// Apply LFO volume
			var modVolumeOffsetCb = modValue * _modLfoToVolumeCb;
			var lfoVolumeMultiplier = (float)Math.Pow(10.0, -modVolumeOffsetCb / 200.0f);

			var totalCb = _initialAttenuation + envCb;
			var envGain = (float)double.Pow(10.0, -totalCb / 200.0);
			var finalGainTarget = envGain * _velocityGain * channelGain * lfoVolumeMultiplier;
			_gainFilter = finalGainTarget * (1.0f - _gainFilterStrength) + _gainFilter * _gainFilterStrength;
			var finalGain = _gainFilter;

			var isLoopingMode = _sampleModes == 1 || _sampleModes == 3 && _volEnv.CurrentStage < EnvelopeStage.Release;
			var doLoop = isLoopingMode && _loopStart < _loopEnd;

			var index0 = (int)_playhead;
			var indexM1 = index0 - 1;
			var index1 = index0 + 1;
			var index2 = index0 + 2;
			var t = (float)(_playhead - index0);

			if (indexM1 < 0)
				indexM1 = index0;

			if (doLoop)
			{
				if (indexM1 < _loopStart && _playhead >= _loopStart)
					indexM1 += _loopEnd - _loopStart;

				while (index1 >= _loopEnd)
					index1 -= _loopEnd - _loopStart;

				while (index2 >= _loopEnd)
					index2 -= _loopEnd - _loopStart;
			}
			else
			{
				if (index1 >= _samples.Length)
					index1 = index0;

				if (index2 >= _samples.Length)
					index2 = index1;
			}

			var yM1 = sampleSpan[indexM1] / 32768.0f;
			var y0 = sampleSpan[index0] / 32768.0f;
			var y1 = sampleSpan[index1] / 32768.0f;
			var y2 = sampleSpan[index2] / 32768.0f;

			var sample = Interpolate(yM1, y0, y1, y2, t);
			sample = _filter.Process(sample);

			var fadeSamples = _outputSampleRate / 100;

			if (!doLoop && _playhead >= _samples.Length - fadeSamples) // Fade out the last 10ms of samples to avoid popping artifacts
			{
				var fadeSamplesLeft = _samples.Length - _playhead;
				var fadeT = (float)(fadeSamplesLeft / fadeSamples);
				finalGain *= float.Max(0.0f, fadeT);
			}

			buffer[i] += sample * finalGain * panLeft;
			buffer[i + 1] += sample * finalGain * panRight;
			_playhead += currentIncrement;

			if (doLoop)
			{
				while (_playhead >= _loopEnd)
					_playhead -= (_loopEnd - _loopStart);
			}
			else if (!doLoop && _playhead >= _samples.Length)
			{
				// Kill the volume and by extension the entire voice
				_volEnv.Kill();
			}
		}
	}

	private static float Interpolate(float yM1, float y0, float y1, float y2, float t)
	{
		var z = t - 0.5f;
		var even1 = y1 + y0;
		var odd1 = y1 - y0;
		var even2 = y2 + yM1;
		var odd2 = y2 - yM1;

		var c0 = even1 * 0.4656726f + even2 * 0.03432729f;
		var c1 = odd1 * 0.5374383f + odd2 * 0.1542066f;
		var c2 = even1 * -0.2519421f + even2 * 0.2519474f;
		var c3 = odd1 * -0.3688224f + odd2 * 0.1167402f;

		return ((c3 * z + c2) * z + c1) * z + c0;
	}
}

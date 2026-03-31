using SillySynth.SoundFont;

namespace SillySynth;

internal sealed class MusSynthesizerVoice
{
	private struct LowPassFilter
	{
		private float _b0, _b1, _b2, _a1, _a2;
		private float _x1, _x2, _y1, _y2;

		public void Update(float sampleRate, float cutoffHz, float resonanceCb)
		{
			cutoffHz = float.Clamp(cutoffHz, 10.0f, sampleRate * 0.49f);

			var qDb = resonanceCb / 10.0f;
			var q = float.Max(0.707f, float.Pow(10.0f, qDb / 20.0f));

			var w0 = 2.0f * float.Pi * cutoffHz / sampleRate;
			var cosW0 = float.Cos(w0);
			var alpha = float.Sin(w0) / (2.0f * q);

			var a0 = 1.0f + alpha;

			_b0 = (1.0f - cosW0) / 2.0f / a0;
			_b1 = (1.0f - cosW0) / a0;
			_b2 = _b0;
			_a1 = -2.0f * cosW0 / a0;
			_a2 = (1.0f - alpha) / a0;
		}

		public float Process(float input)
		{
			var output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

			_x2 = _x1;
			_y2 = _y1;

			_x1 = input;
			_y1 = output;

			return output;
		}
	}

	private struct LowFrequencyOscillator
	{
		private float _phase;
		private float _phaseIncrement;
		private float _delaySamples;
		private float _samplesElapsed;

		public void Initialize(float sampleRate, float delayTimecents, float freqCents)
		{
			var delaySeconds = float.Pow(2.0f, delayTimecents / 1200.0f);
			_delaySamples = (int)(delaySeconds * sampleRate);

			var freqHz = 8.176f * float.Pow(2.0f, freqCents / 1200.0f);
			_phaseIncrement = freqHz / sampleRate;

			_phase = 0.0f;
			_samplesElapsed = 0;
		}

		public float Process()
		{
			if (_samplesElapsed < _delaySamples)
			{
				_samplesElapsed++;
				return 0.0f;
			}

			var output = float.Sin(_phase * 2.0f * float.Pi);

			_phase += _phaseIncrement;
			if (_phase >= 1.0f)
				_phase -= 1.0f;

			return output;
		}
	}

	private enum EnvelopeStage
	{
		Delay,
		Attack,
		Hold,
		Decay,
		Sustain,
		Release,
		Done,
	}

	private struct VolumeEnvelope
	{
		private uint _sampleRate;
		public EnvelopeStage CurrentStage { get; private set; }
		public bool IsDone => CurrentStage == EnvelopeStage.Done;

		private int _samplesElapsed;

		private int _delaySamples;
		private int _attackSamples;
		private int _holdSamples;
		private int _decaySamples;
		private int _releaseSamples;

		private float _sustainCb;
		private float _releaseStartCb;

		public void Initialize(uint sampleRate, short delay, short attack, short hold, short decay, short sustain, short release)
		{
			_sampleRate = sampleRate;

			_delaySamples = TimecentsToSamples(delay, sampleRate);
			_attackSamples = TimecentsToSamples(attack, sampleRate);
			_holdSamples = TimecentsToSamples(hold, sampleRate);
			_decaySamples = TimecentsToSamples(decay, sampleRate);
			_releaseSamples = TimecentsToSamples(release, sampleRate);

			_sustainCb = float.Max(0.0f, sustain);
			CurrentStage = EnvelopeStage.Delay;
			_samplesElapsed = 0;
		}

		public void Release()
		{
			if (CurrentStage is EnvelopeStage.Done or EnvelopeStage.Release)
				return;

			_releaseStartCb = Process();
			CurrentStage = EnvelopeStage.Release;
			_samplesElapsed = 0;
		}

		public void FastRelease()
		{
			if (CurrentStage is EnvelopeStage.Done or EnvelopeStage.Release)
				return;

			Release();

			// Force release to happen in at most 20ms
			_releaseSamples = int.Min(_releaseSamples, (int)(_sampleRate * 0.2f));
		}

		public void Kill() => CurrentStage = EnvelopeStage.Done;

		public float Process()
		{
			if (CurrentStage == EnvelopeStage.Done)
				return 1000.0f;

			var stageTotal = GetStageLength(CurrentStage);
			var t = stageTotal > 0 ? (float)_samplesElapsed / stageTotal : 1.0f;
			var envCb = 1000.0f;

			switch (CurrentStage)
			{
				case EnvelopeStage.Delay:
					envCb = 1000.0f;
					break;
				case EnvelopeStage.Attack:
					envCb = t <= 0.00001f ? 1000.0f : float.Min(1000.0f, -200.0f * float.Log10(t));
					break;
				case EnvelopeStage.Hold:
					envCb = 0.0f;
					break;
				case EnvelopeStage.Decay:
					envCb = t * 1000.0f;

					if (envCb >= _sustainCb)
					{
						envCb = _sustainCb;
						CurrentStage = EnvelopeStage.Sustain;
						_samplesElapsed = 0;
					}

					break;
				case EnvelopeStage.Sustain:
					envCb = _sustainCb;
					break;
				case EnvelopeStage.Release:
					//envCb = float.Min(1000.0f, _releaseStartCb + (1000.0f * t));
					envCb = float.Lerp(_releaseStartCb, 1000.0f, t);
					break;
			}

			if (CurrentStage is EnvelopeStage.Sustain or EnvelopeStage.Done)
				return envCb;

			_samplesElapsed++;

			if (_samplesElapsed < stageTotal)
				return envCb;

			_samplesElapsed = 0;
			CurrentStage++;
			while (CurrentStage < EnvelopeStage.Sustain && GetStageLength(CurrentStage) <= 0)
				CurrentStage++;

			return envCb;
		}

		private int GetStageLength(EnvelopeStage stage) => stage switch
		{
			EnvelopeStage.Delay => _delaySamples,
			EnvelopeStage.Attack => _attackSamples,
			EnvelopeStage.Hold => _holdSamples,
			EnvelopeStage.Decay => _decaySamples,
			EnvelopeStage.Release => _releaseSamples,
			_ => 1,
		};
	}

	private struct ModEnvelope
	{
		private uint _sampleRate;
		private EnvelopeStage CurrentEnvelopeStage { get; set; }

		private int _samplesElapsed;

		private int _delaySamples;
		private int _attackSamples;
		private int _holdSamples;
		private int _decaySamples;
		private int _releaseSamples;

		private float _sustainLevel;
		private float _currentValue;
		private float _releaseStartValue;

		public void Initialize(uint sampleRate, short delay, short attack, short hold, short decay, short sustain, short release)
		{
			_sampleRate = sampleRate;

			_delaySamples = TimecentsToSamples(delay, sampleRate);
			_attackSamples = TimecentsToSamples(attack, sampleRate);
			_holdSamples = TimecentsToSamples(hold, sampleRate);
			_decaySamples = TimecentsToSamples(decay, sampleRate);
			_releaseSamples = TimecentsToSamples(release, sampleRate);

			_sustainLevel = float.Clamp(1.0f - (sustain / 1000.0f), 0.0f, 1.0f);
			CurrentEnvelopeStage = EnvelopeStage.Delay;
			_samplesElapsed = 0;
		}

		public void Release()
		{
			if (CurrentEnvelopeStage is EnvelopeStage.Done or EnvelopeStage.Release)
				return;

			_releaseStartValue = _currentValue;
			CurrentEnvelopeStage = EnvelopeStage.Release;
			_samplesElapsed = 0;
		}

		public void FastRelease()
		{
			if (CurrentEnvelopeStage is EnvelopeStage.Done or EnvelopeStage.Release)
				return;

			Release();
			_releaseSamples = int.Min(_releaseSamples, (int)(_sampleRate * 0.2f));
		}

		public float Process()
		{
			if (CurrentEnvelopeStage == EnvelopeStage.Done)
				return 0.0f;

			var stageTotal = GetStageLength(CurrentEnvelopeStage);
			var t = stageTotal > 0 ? (float)_samplesElapsed / stageTotal : 1.0f;

			switch (CurrentEnvelopeStage)
			{
				case EnvelopeStage.Delay:
					_currentValue = 0.0f;
					break;
				case EnvelopeStage.Attack:
					_currentValue = t;
					break;
				case EnvelopeStage.Hold:
					_currentValue = 1.0f;
					break;
				case EnvelopeStage.Decay:
					_currentValue = 1.0f - t;

					if (_currentValue <= _sustainLevel)
					{
						_currentValue = _sustainLevel;
						CurrentEnvelopeStage = EnvelopeStage.Sustain;
						_samplesElapsed = 0;
					}

					break;
				case EnvelopeStage.Sustain:
					_currentValue = _sustainLevel;
					break;
				case EnvelopeStage.Release:
					//_currentValue = float.Lerp(_releaseStartValue, 0.0f, t);
					_currentValue = float.Max(0.0f, _releaseStartValue - t);
					break;
			}

			if (CurrentEnvelopeStage != EnvelopeStage.Sustain && CurrentEnvelopeStage != EnvelopeStage.Done)
			{
				_samplesElapsed++;

				if (_samplesElapsed >= stageTotal)
				{
					_samplesElapsed = 0;
					CurrentEnvelopeStage++;
					while (CurrentEnvelopeStage < EnvelopeStage.Sustain && GetStageLength(CurrentEnvelopeStage) <= 0)
						CurrentEnvelopeStage++;
				}
			}

			return _currentValue;
		}

		private int GetStageLength(EnvelopeStage envelopeStage) => envelopeStage switch
		{
			EnvelopeStage.Delay => _delaySamples,
			EnvelopeStage.Attack => _attackSamples,
			EnvelopeStage.Hold => _holdSamples,
			EnvelopeStage.Decay => _decaySamples,
			EnvelopeStage.Release => _releaseSamples,
			_ => 1,
		};
	}

	private readonly MusSynthesizerChannel _channel;
	private readonly uint _outputSampleRate;
	public byte Note { get; }
	public bool IsActive => !_volEnv.IsDone;
	public short ExclusiveClass { get; }

	private readonly int _sampleModes;

	private readonly Memory<short> _samples;
	private readonly int _loopStart;
	private readonly int _loopEnd;

	private readonly double _increment;
	private double _playhead;

	private readonly float _velocityGain;
	private readonly float _initialAttenuation;
	private readonly short _pan;

	private VolumeEnvelope _volEnv;
	private ModEnvelope _modEnv;

	private readonly float _modEnvToFilterCents;
	private readonly float _modEnvToPitchCents;

	private LowPassFilter _filter;
	private readonly float _baseFilterCents;
	private readonly float _filterResonance;

	private LowFrequencyOscillator _vibratoLowFrequencyOscillator;
	private LowFrequencyOscillator _modLowFrequencyOscillator;

	private readonly float _vibLfoToPitchCents;
	private readonly float _modLfoToPitchCents;
	private readonly float _modLfoToVolumeCb;

	private const int _filterUpdateInterval = 32;
	private int _filterUpdateStep = 0;


	public MusSynthesizerVoice(
		MusSynthesizerChannel channel,
		Sf2Sample sample,
		byte note,
		int velocity,
		uint outputSampleRate,
		Sf2Generators generators)
	{
		_channel = channel;
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

		var initialAttenuation = generators.GetValue(Sf2SynthParam.InitialAttenuation);
		_velocityGain = (velocity / 127.0f) * (velocity / 127.0f);
		_initialAttenuation = float.Max(0.0f, initialAttenuation);

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

		_sampleModes = generators.GetValue(Sf2SynthParam.SampleModes);
		ExclusiveClass = generators.GetValue(Sf2SynthParam.ExclusiveClass);

		_pan = generators.GetValue(Sf2SynthParam.Pan);

		_baseFilterCents = generators.GetValue(Sf2SynthParam.InitialFilterFc);
		_filterResonance = generators.GetValue(Sf2SynthParam.InitialFilterQ);

		UpdateFilter(0);

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

	private void UpdateFilter(float modulationCents)
	{
		var totalCents = _baseFilterCents + modulationCents;
		var cutoffHz = 8.176f * float.Pow(2.0f, totalCents / 1200.0f);
		_filter.Update(_outputSampleRate, cutoffHz, _filterResonance);
	}

	public void Render(Span<float> buffer)
	{
		if (!IsActive)
			return;

		var sampleSpan = _samples.Span;
		var channelVolume = _channel.Volume / 127.0f;
		var channelGain = channelVolume * channelVolume;

		var midiPan = (_channel.Pan - 64) / 63.0f;
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
				UpdateFilter(modEnvValue * _modEnvToFilterCents);
				_filterUpdateStep = 0;
			}

			// Apply LFO pitch
			var totalPitchModCents = (vibValue * _vibLfoToPitchCents) + (modValue * _modLfoToPitchCents) + (modEnvValue * _modEnvToPitchCents);
			var lfoPitchMultiplier = (float)double.Pow(2.0, totalPitchModCents / 1200.0);
			var currentIncrement = _increment * _channel.PitchBendMultiplier * lfoPitchMultiplier;

			// Apply LFO volume
			var modVolumeOffsetCb = modValue * _modLfoToVolumeCb;
			var lfoVolumeMultiplier = (float)Math.Pow(10.0, -modVolumeOffsetCb / 200.0f);

			var totalCb = _initialAttenuation + envCb;
			var envGain = (float)double.Pow(10.0, -totalCb / 200.0);
			var finalGain = envGain * _velocityGain * channelGain * lfoVolumeMultiplier;

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

	private static int TimecentsToSamples(short timecents, uint sampleRate)
	{
		var seconds = double.Pow(2.0, timecents / 1200.0);
		return (int)(sampleRate * seconds);
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

internal sealed class MusSynthesizerChannel
{
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

	public readonly List<MusSynthesizerVoice> Voices = [];

	public void SpawnVoice(MusSynthesizerVoice voice)
	{
		if (voice.ExclusiveClass != 0)
		{
			foreach (var v in Voices)
			{
				if (v.IsActive && v.ExclusiveClass == voice.ExclusiveClass)
					v.ExclusiveKill();
			}
		}

		var slot = 0;
		while (slot < Voices.Count && Voices[slot].IsActive)
			slot++;

		if (slot >= Voices.Count)
			Voices.Add(voice);
		else
			Voices[slot] = voice;
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
		foreach (var voice in chan.Voices.Where(voice => voice.Note == note))
			voice.Release();
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
				var voice = new MusSynthesizerVoice(_channels[channel], sample, note, velocity, _sampleRate, generators);
				_channels[channel].SpawnVoice(voice);
			}
		}
	}
}

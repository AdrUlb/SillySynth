namespace SillySynth;

internal struct VolumeEnvelope()
{
	private uint _sampleRate;
	public EnvelopeStage CurrentStage { get; private set; } = EnvelopeStage.Done;
	public bool IsDone => CurrentStage == EnvelopeStage.Done;

	private int _delaySamples;
	private int _attackSamples;
	private int _holdSamples;
	private int _decaySamples;
	private int _releaseSamples;

	private float _sustainCb;
	private float _releaseStartCb;

	private int _samplesElapsed;

	public void Initialize(uint sampleRate, short delay, short attack, short hold, short decay, short sustain, short release)
	{
		_sampleRate = sampleRate;

		CurrentStage = EnvelopeStage.Delay;

		_delaySamples = TimecentsToSamples(delay, sampleRate);
		_attackSamples = TimecentsToSamples(attack, sampleRate);
		_holdSamples = TimecentsToSamples(hold, sampleRate);
		_decaySamples = TimecentsToSamples(decay, sampleRate);
		_releaseSamples = TimecentsToSamples(release, sampleRate);

		_sustainCb = float.Max(0.0f, sustain);
		_releaseStartCb = 0;
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
	
	private static int TimecentsToSamples(short timecents, uint sampleRate)
	{
		var seconds = double.Pow(2.0, timecents / 1200.0);
		return (int)(sampleRate * seconds);
	}
}

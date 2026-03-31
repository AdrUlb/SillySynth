namespace SillySynth;

internal struct ModEnvelope
{
	private uint _sampleRate;
	private EnvelopeStage CurrentEnvelopeStage { get; set; }

	private int _delaySamples;
	private int _attackSamples;
	private int _holdSamples;
	private int _decaySamples;
	private int _releaseSamples;

	private float _sustainLevel;
	private float _currentValue;
	private float _releaseStartValue;

	private int _samplesElapsed;

	public void Initialize(uint sampleRate, short delay, short attack, short hold, short decay, short sustain, short release)
	{
		_sampleRate = sampleRate;

		CurrentEnvelopeStage = EnvelopeStage.Delay;

		_delaySamples = TimecentsToSamples(delay, sampleRate);
		_attackSamples = TimecentsToSamples(attack, sampleRate);
		_holdSamples = TimecentsToSamples(hold, sampleRate);
		_decaySamples = TimecentsToSamples(decay, sampleRate);
		_releaseSamples = TimecentsToSamples(release, sampleRate);

		_sustainLevel = float.Clamp(1.0f - (sustain / 1000.0f), 0.0f, 1.0f);
		_currentValue = 0;
		_releaseStartValue = 0;

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
	
	private static int TimecentsToSamples(short timecents, uint sampleRate)
	{
		var seconds = double.Pow(2.0, timecents / 1200.0);
		return (int)(sampleRate * seconds);
	}
}

namespace SillySynth;

internal struct LowPassFilter
{
	private uint _sampleRate;
	private float _baseCents;
	private float _resonanceCb;
	private float _b0, _b1, _b2, _a1, _a2;
	private float _x1, _x2, _y1, _y2;

	public void Initialize(uint sampleRate, float baseCents, float resonanceCb)
	{
		_sampleRate = sampleRate;
		_baseCents = baseCents;
		_resonanceCb = resonanceCb;
		_b0 = 0.0f;
		_b1 = 0.0f;
		_b2 = 0.0f;
		_a1 = 0.0f;
		_a2 = 0.0f;
		_x1 = 0.0f;
		_x2 = 0.0f;
		_y1 = 0.0f;
		_y2 = 0.0f;

		Update(0);
	}

	public void Update(float modulationCents)
	{
		var totalCents = _baseCents + modulationCents;
		var cutoffHz = 8.176f * float.Pow(2.0f, totalCents / 1200.0f);

		cutoffHz = float.Clamp(cutoffHz, 10.0f, _sampleRate * 0.49f);

		var qDb = _resonanceCb / 10.0f;
		var q = float.Max(0.707f, float.Pow(10.0f, qDb / 20.0f));

		var w0 = 2.0f * float.Pi * cutoffHz / _sampleRate;
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

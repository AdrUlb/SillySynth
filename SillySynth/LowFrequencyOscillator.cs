using System.Runtime.CompilerServices;

namespace SillySynth;

internal struct LowFrequencyOscillator
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

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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

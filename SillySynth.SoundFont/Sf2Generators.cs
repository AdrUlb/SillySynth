namespace SillySynth.SoundFont;

public sealed class Sf2Generators
{
	private readonly short[] _generators = new short[60];
	private ulong _setMask = 0;

	public Sf2Generators()
	{
		SetValue(Sf2SynthParam.InitialFilterFc, 13500);
		SetValue(Sf2SynthParam.DelayModLfo, -12000);
		SetValue(Sf2SynthParam.DelayVibLfo, -12000);
		SetValue(Sf2SynthParam.DelayModEnv, -12000);
		SetValue(Sf2SynthParam.AttackModEnv, -12000);
		SetValue(Sf2SynthParam.HoldModEnv, -12000);
		SetValue(Sf2SynthParam.DecayModEnv, -12000);
		SetValue(Sf2SynthParam.ReleaseModEnv, -12000);
		SetValue(Sf2SynthParam.DelayVolEnv, -12000);
		SetValue(Sf2SynthParam.AttackVolEnv, -12000);
		SetValue(Sf2SynthParam.HoldVolEnv, -12000);
		SetValue(Sf2SynthParam.DecayVolEnv, -12000);
		SetValue(Sf2SynthParam.ReleaseVolEnv, -12000);
		SetRange(Sf2SynthParam.KeyRange, 0, 127);
		SetRange(Sf2SynthParam.VelRange, 0, 127);
		SetValue(Sf2SynthParam.Keynum, -1);
		SetValue(Sf2SynthParam.Velocity, -1);
		SetValue(Sf2SynthParam.ScaleTuning, 100);
		SetValue(Sf2SynthParam.OverridingRootKey, -1);
		_setMask = 0;
	}

	internal void SetValue(Sf2SynthParam type, short value)
	{
		var index = (int)type;
		if (index < 0 || index >= _generators.Length)
			return;

		_generators[index] = value;

		_setMask |= (1UL << index);
	}

	private void SetRange(Sf2SynthParam type, int min, int max)
	{
		var index = (int)type;
		if (index < 0 || index >= _generators.Length)
			return;

		var value = (max << 8) | min;
		_generators[index] = (short)(ushort)value;
	}

	public short GetValue(Sf2SynthParam type)
	{
		var index = (int)type;
		if (index < 0 || index >= _generators.Length)
			return 0;

		return _generators[index];
	}

	public short GetRelativeValue(Sf2SynthParam type)
	{
		var index = (int)type;
		if (index < 0 || index >= _generators.Length)
			return 0;

		var isSet = ((_setMask >> index) & 1) != 0;
		return isSet ? _generators[index] : (short)0;

	}

	public void GetRange(Sf2SynthParam type, out byte min, out byte max)
	{
		var index = (int)type;

		if (index < 0 || index >= _generators.Length)
		{
			min = 0;
			max = 0;
			return;
		}

		var value = (ushort)_generators[index];
		min = (byte)(value & 0xFF);
		max = (byte)((value >> 8) & 0xFF);
	}

	public Sf2Generators Clone()
	{
		var clone = new Sf2Generators();
		_generators.CopyTo(clone._generators);
		clone._setMask = _setMask;
		return clone;
	}

	public static Sf2Generators Flatten(Sf2Generators presetGens, Sf2Generators instrumentGens)
	{
		var ret = instrumentGens.Clone();

		for (var i = 0; i < 60; i++)
		{
			var generatorType = (Sf2SynthParam)i;

			// No index generators
			if (generatorType is
			    Sf2SynthParam.Instrument or
			    Sf2SynthParam.SampleId)
				continue;

			// No range generators
			if (generatorType is
			    Sf2SynthParam.KeyRange or
			    Sf2SynthParam.VelRange)
				continue;

			// No sample generators
			if (generatorType is
			    Sf2SynthParam.OverridingRootKey or
			    Sf2SynthParam.ExclusiveClass)
				continue;
			
			// No instrument level generators
			if (generatorType is
			    Sf2SynthParam.OverridingRootKey or
			    Sf2SynthParam.ExclusiveClass or
			    Sf2SynthParam.ScaleTuning or
			    Sf2SynthParam.SampleModes or
			    Sf2SynthParam.Keynum or
			    Sf2SynthParam.Velocity)
				continue;
			var value = ret.GetValue(generatorType);
			var offset = presetGens.GetRelativeValue(generatorType);
			ret.SetValue(generatorType, (short)(value + offset));
		}

		return ret;
	}
}

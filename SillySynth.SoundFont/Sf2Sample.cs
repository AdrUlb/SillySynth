namespace SillySynth.SoundFont;

public sealed class Sf2Sample
{
	public string Name { get; }
	public short[] Data { get; }
	public int LoopStart { get; }
	public int LoopEnd { get; }
	public uint SampleRate { get; }
	public byte OriginalPitch { get; }
	public sbyte PitchCorrection { get; }
	public uint SampleType { get; }
	public uint SampleLink { get; }

	internal Sf2Sample(string name, short[] data, int loopStart, int loopEnd, uint sampleRate, byte originalPitch, sbyte pitchCorrection, uint sampleType, uint sampleLink)
	{
		Name = name;
		Data = data;
		LoopStart = loopStart;
		LoopEnd = loopEnd;
		SampleRate = sampleRate;
		OriginalPitch = originalPitch;
		PitchCorrection = pitchCorrection;
		SampleType = sampleType;
		SampleLink = sampleLink;
	}
}

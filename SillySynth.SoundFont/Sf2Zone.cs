namespace SillySynth.SoundFont;

public sealed class Sf2Zone(Sf2Generators generators, Sf2Modulator[] modulators)
{
	public Sf2Generators Generators { get; } = generators;
	public Sf2Modulator[] Modulators { get; } = modulators;
}

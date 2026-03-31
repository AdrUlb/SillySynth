namespace SillySynth.SoundFont;

public sealed class Sf2Instrument
{
	public string Name { get; }

	public Sf2Zone[] Zones { get; }

	internal Sf2Instrument(string name, Sf2Zone[] zones)
	{
		Name = name;
		Zones = zones;
	}
}

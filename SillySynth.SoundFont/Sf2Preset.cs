namespace SillySynth.SoundFont;

public sealed class Sf2Preset
{
	public string Name { get; }
	public int PresetNumber { get; }
	public int BankNumber { get; }
	public Sf2Zone[] Zones { get; }

	internal Sf2Preset(string name, int presetNumber, int bankNumber, Sf2Zone[] zones)
	{
		Name = name;
		PresetNumber = presetNumber;
		BankNumber = bankNumber;
		Zones = zones;
	}
}

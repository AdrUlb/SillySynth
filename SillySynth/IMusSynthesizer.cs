namespace SillySynth;

public interface IMusSynthesizer
{
	void PlayNote(byte channel, byte note);
	void PlayNote(byte channel, byte note, byte velocity);
	void ReleaseNote(byte channel, byte note);
	void PitchBend(byte channel, byte amount);
	void Controller(byte channel, MusController controller, byte value);
}

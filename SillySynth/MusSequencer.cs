namespace SillySynth;

public sealed class MusSequencer(MusData mus, IMusSynthesizer synth)
{
	private int _index = 0;

	public int Tick()
	{
		bool eventLast;

		do
		{
			var eventData = GetInstructionByte();
			var eventType = (MusEvent)((eventData >> 4) & 0b111);
			var eventChannel = (byte)(eventData & 0x0F);
			eventLast = (eventData & 0x80) != 0;

			switch (eventType)
			{
				case MusEvent.ReleaseNote:
					{
						var data = GetInstructionByte();
						var note = (byte)(data & 0x7F);
						synth.ReleaseNote(eventChannel, note);
						break;
					}
				case MusEvent.PlayNote:
					{
						var data = GetInstructionByte();
						var note = (byte)(data & 0x7F);
						var changeVolume = (data & 0x80) != 0;

						if (changeVolume)
						{
							var volume = (byte)(GetInstructionByte() & 0x7F);
							synth.PlayNote(eventChannel, note, volume);
						}
						else
							synth.PlayNote(eventChannel, note);

						break;
					}
				case MusEvent.PitchBend:
					{
						var amount = GetInstructionByte();
						synth.PitchBend(eventChannel, amount);
						break;
					}
				case MusEvent.System: // System
					{
						throw new NotSupportedException(); // TODO?
					}
				case MusEvent.Controller: // Controller
					{
						var data1 = GetInstructionByte();
						var data2 = GetInstructionByte();
						var controller = (MusController)(data1 & 0x7F);
						var value = (byte)(data2 & 0x7F);

						synth.Controller(eventChannel, controller, value);
						break;
					}
				case MusEvent.EndOfMeasure: // End of Measure (do nothing, purely informative)
					break;
				case MusEvent.Finish: // Finish
					_index = 0;
					return -1;
				default:
					throw new InvalidDataException();
			}
		} while (!eventLast);

		var delay = 0;

		do
		{
			var data = GetInstructionByte();
			eventLast = (data & 0x80) != 0;
			delay = (delay << 7) + (data & 0x7F);
		} while (eventLast);

		return delay;
	}

	private byte GetInstructionByte()
	{
		var b = mus.InstructionBytes[_index++];
		_index %= mus.InstructionBytes.Length;
		return b;
	}
}

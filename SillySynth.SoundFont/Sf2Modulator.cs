namespace SillySynth.SoundFont;

public sealed class Sf2Modulator(ushort sourceOperator, ushort destinationOperator, ushort amountSourceOperator, ushort transformOperator, short amount)
{
	public ushort SourceOperator { get; } = sourceOperator;
	public ushort DestinationOperator { get; } = destinationOperator;
	public ushort AmountSourceOperator { get; } = amountSourceOperator;
	public ushort TransformOperator { get; } = transformOperator;
	public short Amount { get; } = amount;
}

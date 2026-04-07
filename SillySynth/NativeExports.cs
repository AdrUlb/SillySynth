using SillySynth.SoundFont;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SillySynth;

internal static class NativeExports
{
	private static nint CreateHandle<T>(T obj) where T : class
	{
		return GCHandle.ToIntPtr(GCHandle.Alloc(obj));
	}

	private static void FreeHandle(nint handle)
	{
		var h = GCHandle.FromIntPtr(handle);

		if (h.IsAllocated)
			h.Free();
	}

	private static bool TryFromHandle<T>(nint handle, [MaybeNullWhen(false)] out T obj)
	{
		var h = GCHandle.FromIntPtr(handle);

		if (!h.IsAllocated)
		{
			obj = default;
			return false;
		}

		if (h.Target is not T target)
		{
			obj = default;
			return false;
		}

		obj = target;
		return true;
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_OpenFileStream")]
	private static nint OpenFileStream(nint filePathPtr)
	{
		try
		{
			var filePath = Marshal.PtrToStringUTF8(filePathPtr);
			if (filePath == null)
				return 0;

			return CreateHandle(File.OpenRead(filePath));
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_CreateBufferStream")]
	private static unsafe nint CreateBufferStream(nint bufferPtr, nint bufferLength)
	{
		try
		{
			return CreateHandle(new UnmanagedMemoryStream((byte*)bufferPtr, bufferLength));
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_FreeStream")]
	private static void FreeStream(nint streamHandle)
	{
		if (!TryFromHandle<Stream>(streamHandle, out var stream))
			return;

		try
		{
			stream.Dispose();
			FreeHandle(streamHandle);
		}
		catch
		{
			// ignored
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_LoadSoundFont2")]
	private static nint LoadSoundFont2(nint streamHandle)
	{
		if (!TryFromHandle<Stream>(streamHandle, out var stream))
			return 0;

		try
		{
			return CreateHandle(Sf2.Load(stream));
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_FreeSoundFont2")]
	private static void FreeSoundFont2(nint soundFontHandle)
	{
		FreeHandle(soundFontHandle);
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_CreateMusSynthesizer")]
	public static nint CreateMusSynthesizer(nint soundFontHandle, ushort sampleRate)
	{
		if (!TryFromHandle<Sf2>(soundFontHandle, out var soundFont))
			return 0;

		try
		{
			return CreateHandle(new MusSynthesizer(soundFont, sampleRate));
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_RenderMusSynthesizer")]
	private static unsafe void RenderMusSynthesizer(nint musSynthHandle, nint samples, nint sampleCount)
	{
		if (!TryFromHandle<MusSynthesizer>(musSynthHandle, out var musSynth))
			return;

		try
		{
			musSynth.Render(new((float*)samples, (int)sampleCount));
		}
		catch
		{
			// ignored
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_FreeMusSynthesizer")]
	private static void FreeMusSynthesizer(nint musSynthHandle)
	{
		FreeHandle(musSynthHandle);
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_LoadMusData")]
	public static nint LoadMusData(nint streamHandle)
	{
		if (!TryFromHandle<Stream>(streamHandle, out var stream))
			return 0;

		try
		{
			return CreateHandle(MusData.Read(stream));
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_FreeMusData")]
	private static void FreeMusData(nint musDataHandle)
	{
		FreeHandle(musDataHandle);
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_CreateMusSequencer")]
	public static nint CreateMusSequencer(nint musDataHandle, nint musSynthHandle)
	{
		if (!TryFromHandle<MusData>(musDataHandle, out var musData))
			return 0;

		if (!TryFromHandle<IMusSynthesizer>(musSynthHandle, out var musSynth))
			return 0;

		return CreateHandle(new MusSequencer(musData, musSynth));
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_TickMusSequencer")]
	private static int SYN_TickMusSequencer(nint musSeqHandle)
	{
		if (!TryFromHandle<MusSequencer>(musSeqHandle, out var seq))
			return -1;

		try
		{
			return seq.Tick();
		}
		catch
		{
			return -1;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "SYN_FreeMusSequencer")]
	private static void FreeMusSequencer(nint musSeqHandle)
	{
		FreeHandle(musSeqHandle);
	}
}

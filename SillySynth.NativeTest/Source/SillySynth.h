#pragma once

#include <stddef.h>
#include <stdint.h>

typedef struct SYN_Stream SYN_Stream;
typedef struct SYN_Sf2 SYN_Sf2;
typedef struct SYN_Sf2 SYN_Sf2;
typedef struct SYN_MusSynthesizer SYN_MusSynthesizer;
typedef struct SYN_MusSequencer SYN_MusSequencer;
typedef struct SYN_MusData SYN_MusData;

SYN_Stream* SYN_OpenFileStream(const char* filePath);
SYN_Stream* SYN_CreateBufferStream(void* buffer, size_t bufferSize);
void SYN_FreeStream(SYN_Stream* stream);

SYN_Sf2* SYN_LoadSoundFont2(SYN_Stream* stream);
void SYN_FreeSoundFont2(SYN_Sf2* soundFont);

SYN_MusData* SYN_LoadMusData(SYN_Stream* stream);
void SYN_FreeMusData(SYN_MusData* musData);

SYN_MusSynthesizer* SYN_CreateMusSynthesizer(SYN_Sf2* soundFont, uint16_t sampleRate);
void SYN_RenderMusSynthesizer(SYN_MusSynthesizer* musSynthesizer, float* samples, int sampleCount);
void SYN_FreeMusSynthesizer(SYN_MusSynthesizer* musSynthesizer);

SYN_MusSequencer* SYN_CreateMusSequencer(SYN_MusData* musData, SYN_MusSynthesizer* MusSynthesizer);
int SYN_TickMusSequencer(SYN_MusSequencer* musSequencer);
void SYN_FreeMusSequencer(SYN_MusSequencer* musSequencer);

static SYN_Sf2* SYN_LoadSoundFont2File(const char* filePath)
{
    SYN_Stream* stream = SYN_OpenFileStream(filePath);
    if (!stream)
        return NULL;

    SYN_Sf2* soundFont = SYN_LoadSoundFont2(stream);
    SYN_FreeStream(stream);
    return soundFont;
}

static SYN_MusData* SYN_LoadMusDataFile(const char* filePath)
{
    SYN_Stream* stream = SYN_OpenFileStream(filePath);
    if (!stream)
        return NULL;

    SYN_MusData* musData = SYN_LoadMusData(stream);
    SYN_FreeStream(stream);
    return musData;
}

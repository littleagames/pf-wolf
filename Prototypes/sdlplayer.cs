using NScumm.Audio.Players;
using SDL2;

namespace NScumm.Audio.SDLPlayer;

internal sealed class SDLPlayer
{
    private readonly IMusicPlayer _player;
    private readonly int _rate;

    static bool isPlaying = false;
    int samplesPerMusicTick;


    public SDLPlayer(IMusicPlayer player, int rate)
    {
        _player = player;

        if (SDL.SDL_Init(SDL.SDL_INIT_AUDIO) < 0)
        {
            Console.WriteLine($"SDL Init Failed: {SDL.SDL_GetError()}");
            SDL.SDL_Quit();
            return;
        }
        var param_samplerate = 44100;
        var chunksize = 2048;

        if (SDL_mixer.Mix_OpenAudio(param_samplerate, SDL.AUDIO_S16, 1, chunksize) != 0)
        {
            Console.WriteLine($"Unable to open audio device: {SDL_mixer.Mix_GetError()}\n");
            return;
        }

        SDL_mixer.Mix_QuerySpec(out param_samplerate, out ushort format, out int channels);

        SDL_mixer.Mix_ReserveChannels(2);  // reserve player and boss weapon channels
        SDL_mixer.Mix_GroupChannels(2, SDL_mixer.MIX_CHANNELS - 1, 1); // group remaining channels
        samplesPerMusicTick = param_samplerate / 700;
        SDL_mixer.Mix_HookMusic(AudioCallback, 0);
    }


    public void Play()
    {
        isPlaying = true;

        while (isPlaying)
        {
            SDL.SDL_Delay(100);
            continue;
        }

        Console.WriteLine();
        Console.WriteLine("End!");

        SDL.SDL_Quit();
    }

    static int numreadysamples = 0;
    static int alTimeCount = 0;
    static int sqHackTime = 0;
    private void AudioCallback(IntPtr userdata, nint stream, int len)
    {
        int stereolen = len >> 1;
        int sampleslen = stereolen >> 1;
        unsafe {
            short* stream16 = (short*)stream;

            while (true)
            {
                if (numreadysamples > 0)
                {
                    if (numreadysamples < sampleslen)
                    {
                        var data = new short[numreadysamples];
                        //Console.WriteLine($"Read buffer {numreadysamples}");
                        //_player.Opl.ReadBuffer(data, 0, numreadysamples);
                        // TODO: assign to stream16
                        //stream16 += numreadysamples * 2;
                        //fixed (short* dataPtr = data)
                        //{
                        //    System.Buffer.MemoryCopy(dataPtr, stream16, numreadysamples, numreadysamples);
                        //}
                        sampleslen -= numreadysamples;
                    }
                    else
                    {
                        var data = new short[sampleslen];
                        //Console.WriteLine($"Read buffer {sampleslen}");
                        //_player.Opl.ReadBuffer(data, 0, sampleslen);
                        // TODO: assign to stream16
                        //fixed (short* dataPtr = data)
                        //{
                        //    System.Buffer.MemoryCopy(dataPtr, stream16, numreadysamples, numreadysamples);
                        //}
                        numreadysamples -= sampleslen;
                        return;
                    }
                }

                if (isPlaying)
                {
                    if (sqHackTime <= alTimeCount)
                    {
                        var playing = _player.Update();
                        if (!playing)
                        {
                            Console.WriteLine("Music end");
                            alTimeCount = 0;
                            sqHackTime = 0;
                            continue;
                        }

                        var time = (int)Math.Round((700.0f / _player.RefreshRate)); // or midpoint round
                        sqHackTime = (alTimeCount + time);
                    }
                    
                    alTimeCount++;
                }

                numreadysamples = samplesPerMusicTick;
            }
        }


        // base
        //var data = new short[len/2];
        //var numSamples = GetDataChunk(data, len/2);
        //unsafe
        //{
        //    //short* stream16 = (short*)stream;
        //    fixed (short* dataPtr = data)
        //    {
        //        System.Buffer.MemoryCopy(dataPtr, (byte*)stream, len, numSamples);
        //    }
        //}
        //end base
    }
}

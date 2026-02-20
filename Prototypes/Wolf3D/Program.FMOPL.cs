//namespace Wolf3D;

//internal struct OPL_SLOT
//{
//    public uint ar;          /* attack rate: AR<<2			*/
//    public uint dr;          /* decay rate:  DR<<2			*/
//    public uint rr;          /* release rate:RR<<2			*/
//    public byte KSR;      /* key scale rate				*/
//    public byte ksl;      /* keyscale level				*/
//    public byte ksr;      /* key scale rate: kcode>>KSR	*/
//    public byte mul;      /* multiple: mul_tab[ML]		*/

//    /* Phase Generator */
//    public uint Cnt;     /* frequency counter			*/
//    public uint Incr;        /* frequency counter step		*/
//    public byte FB;           /* feedback shift value			*/
//    public int connect1;    /* slot1 output pointer			*/
//    public int[] op1_out;   /* slot1 output for feedback	*/
//    public byte CON;      /* connection (algorithm) type	*/

//    /* Envelope Generator */
//    public byte eg_type;  /* percussive/non-percussive mode */
//    public byte state;        /* phase type					*/
//    public uint TL;          /* total level: TL << 2			*/
//    public int TLL;      /* adjusted now TL				*/
//    public int volume;       /* envelope counter				*/
//    public uint sl;          /* sustain level: sl_tab[SL]	*/
//    public byte eg_sh_ar; /* (attack state)				*/
//    public byte eg_sel_ar;    /* (attack state)				*/
//    public byte eg_sh_dr; /* (decay state)				*/
//    public byte eg_sel_dr;    /* (decay state)				*/
//    public byte eg_sh_rr; /* (release state)				*/
//    public byte eg_sel_rr;    /* (release state)				*/
//    public uint key;     /* 0 = KEY OFF, >0 = KEY ON		*/

//    /* LFO */
//    public uint AMmask;      /* LFO Amplitude Modulation enable mask */
//    public byte vib;      /* LFO Phase Modulation enable flag (active high)*/

//    /* waveform select */
//    public uint wavetable;

//    public OPL_SLOT()
//    {
//        op1_out = new int[2];
//    }
//}

//internal struct OPL_CH
//{
//    public OPL_SLOT[] SLOT;
//    /* phase generator state */
//    public uint block_fnum;  /* block+fnum					*/
//    public uint fc;          /* Freq. Increment base			*/
//    public uint ksl_base;    /* KeyScaleLevel Base step		*/
//    public byte kcode;        /* key code (for key scaling)	*/
//    public bool muted;

//    public OPL_CH()
//    {
//        SLOT = new OPL_SLOT[2];
//    }
//}

//internal struct FM_OPL
//{
//    /* FM channel slots */
//    public OPL_CH[] P_CH;             /* OPL/OPL2 chips have 9 channels*/

//    public uint eg_cnt;                  /* global envelope generator counter	*/
//    public uint eg_timer;                /* global envelope generator counter works at frequency = chipclock/72 */
//    public uint eg_timer_add;            /* step of eg_timer						*/
//    public uint eg_timer_overflow;       /* envelope generator timer overlfows every 1 sample (on real chip) */

//    public byte rhythm;                   /* Rhythm mode					*/

//    public uint[] fn_tab;            /* fnumber->increment counter	*/

//    /* LFO */
//    public byte lfo_am_depth;
//    public byte lfo_pm_depth_range;
//    public uint lfo_am_cnt;
//    public uint lfo_am_inc;
//    public uint lfo_pm_cnt;
//    public uint lfo_pm_inc;

//    public uint noise_rng;               /* 23 bit noise shift register	*/
//    public uint noise_p;             /* current noise 'phase'		*/
//    public uint noise_f;             /* current noise period			*/

//    public byte wavesel;              /* waveform select enable flag	*/

//    public int[] T;                   /* timer counters				*/
//    public byte[] st;                    /* timer enable					*/

//    /* external event callback handlers */
//    public OPL_TIMERHANDLER TimerHandler;  /* TIMER handler				*/
//    public int TimerParam;                 /* TIMER parameter				*/
//    public OPL_IRQHANDLER IRQHandler;  /* IRQ handler					*/
//    public int IRQParam;                   /* IRQ parameter				*/
//    public OPL_UPDATEHANDLER UpdateHandler;/* stream update handler		*/
//    public int UpdateParam;                /* stream update parameter		*/

//    public byte type;                     /* chip type					*/
//    public byte address;                  /* address register				*/
//    public byte status;                   /* status flag					*/
//    public byte statusmask;               /* status mask					*/
//    public byte mode;                     /* Reg.08 : CSM,notesel,etc.	*/

//    public int clock;                      /* master clock  (Hz)			*/
//    public int rate;                       /* sampling rate (Hz)			*/
//    public double freqbase;                /* frequency base				*/
//    public double TimerBase;				/* Timer base time (==sampling time)*/

//    public FM_OPL()
//    {
//        P_CH = new OPL_CH[9];
//        fn_tab = new uint[1024];
//        T = new int[2];
//        st = new byte[2];
//    }
//}

//internal partial class Program
//{
//    private const int OPL_SAMPLE_BITS = 16;
//    static FM_OPL[] OPL_YM3812 = new FM_OPL[MAX_OPL_CHIPS];	/* array of pointers to the YM3812's */
//    static int YM3812NumChips = 0;				/* number of chips */
//    internal static int YM3812Write(int num,, int clock, int rate)
//    {
//        int i;

//        if (YM3812NumChips != 0)
//            return -1;  /* duplicate init. */

//        YM3812NumChips = num;

//        for (i = 0; i < YM3812NumChips; i++)
//        {
//            /* emulator create */
//            OPL_YM3812[i] = OPLCreate(OPL_TYPE_YM3812, clock, rate);
//            if (OPL_YM3812[i] == null)
//            {
//                /* it's really bad - we run out of memeory */
//                YM3812NumChips = 0;
//                return -1;
//            }
//            /* reset */
//            YM3812ResetChip(i);
//        }

//        return 0;
//    }
//}

namespace Wolf3D;

internal partial class Program
{
    internal const string QUITSTR = $"Are you sure you want\nto quit this great game?";
    internal const string CURGAME = $"You are currently in\na game. Continuing will\nerase old game. Ok?";
    internal const string GAMESVD = $"There's already a game\nsaved at this position.\n      Overwrite?";
    internal const string ENDGAMESTR = @$"Are you sure you want\nto end the game you\nare playing? (""{YESBUTTONNAME}"" or ""{NOBUTTONNAME}""):";

    internal const string STR_NG = "New Game";
    internal const string STR_SD = "Sound";
    internal const string STR_CL = "Control";
    internal const string STR_LG = "Load Game";
    internal const string STR_SG = "Save Game";
    internal const string STR_CV = "Change View";
    internal const string STR_VS = "View Scores";
    internal const string STR_EG = "End Game";
    internal const string STR_BD = "Back to Demo";
    internal const string STR_BG = "Back to Game";
    internal const string STR_QT = "Quit";

    internal const string STR_LOADING = "Loading";
    internal const string STR_SAVING = "Saving";

    internal const string STR_GAME = "Game";
    internal const string STR_DEMO = "Demo";
    internal const string STR_LGC = "Load Game called\n\"";
    internal const string STR_EMPTY = "empty";
    internal const string STR_CALIB = "Calibrate";
    internal const string STR_JOYST = "Joystick";
    internal const string STR_MOVEJOY = "Move joystick to\nupper left and\npress button 0\n";
    internal const string STR_MOVEJOY2 = "Move joystick to\nlower right and\npress button 1\n";
    internal const string STR_ESCEXIT = "ESC to exit";

    internal const string STR_NONE = "None";
    internal const string STR_PC = "PC Speaker";
    internal const string STR_ALSB = "AdLib/Sound Blaster";
    internal const string STR_DISNEY = "Disney Sound Source";
    internal const string STR_SB = "Sound Blaster";

    internal const string STR_MOUSEEN = "Mouse Enabled";
    internal const string STR_JOYEN = "Joystick Enabled";
    internal const string STR_PORT2 = "Use joystick port 2";
    internal const string STR_GAMEPAD = "Gravis GamePad Enabled";
    internal const string STR_SENS = "Mouse Sensitivity";
    internal const string STR_CUSTOM = "Customize controls";

    internal const string STR_DADDY = "Can I play, Daddy?";
    internal const string STR_HURTME = "Don't hurt me.";
    internal const string STR_BRINGEM = "Bring 'em on!";
    internal const string STR_DEATH = "I am Death incarnate!";

    internal const string STR_MOUSEADJ = "Adjust Mouse Sensitivity";
    internal const string STR_SLOW = "Slow";
    internal const string STR_FAST = "Fast";

    internal const string STR_CRUN = "Run";
    internal const string STR_COPEN = "Open";
    internal const string STR_CFIRE = "Fire";
    internal const string STR_CSTRAFE = "Strafe";

    internal const string STR_LEFT = "Left";
    internal const string STR_RIGHT = "Right";
    internal const string STR_FRWD = "Frwd";
    internal const string STR_BKWD = "Bkwrd";
    internal const string STR_THINK = "Thinking";

    internal const string STR_SIZE1 = "Use arrows to size";
    internal const string STR_SIZE2 = "ENTER to accept";
    internal const string STR_SIZE3 = "ESC to cancel";

    internal const string STR_YOUWIN = "you win!";

    internal const string STR_TOTALTIME = "total time";

    internal const string STR_RATKILL = "kill    %";
    internal const string STR_RATSECRET = "secret    %";
    internal const string STR_RATTREASURE = "treasure    %";

    internal const string STR_BONUS = "bonus";
    internal const string STR_TIME = "time";
    internal const string STR_PAR = " par";

    internal const string STR_RAT2KILL = "kill ratio    %";
    internal const string STR_RAT2SECRET = "secret ratio    %";
    internal const string STR_RAT2TREASURE = "treasure ratio    %";

    internal const string STR_DEFEATED = "defeated!";

    internal const string STR_CHEATER1 = "You now have 100% Health,";
    internal const string STR_CHEATER2 = "99 Ammo and both Keys!";
    internal const string STR_CHEATER3 = "Note that you have basically";
    internal const string STR_CHEATER4 = "eliminated your chances of";
    internal const string STR_CHEATER5 = "getting a high score!";

    internal const string STR_NOSPACE1 = "There is not enough space";
    internal const string STR_NOSPACE2 = "on your disk to Save Game!";

    internal const string STR_SAVECHT1 = "Your Save Game file is,";
    internal const string STR_SAVECHT2 = "shall we say, \"corrupted\".";
    internal const string STR_SAVECHT3 = "But I'll let you go on and";
    internal const string STR_SAVECHT4 = "play anyway....";

    internal const string STR_SEEAGAIN = "Let's see that again!";


//#ifdef SPEAR
    //internal const string ENDSTR1 = $"Heroes don't quit, but\ngo ahead and press {YESBUTTONNAME}\nif you aren't one.";
    //internal const string ENDSTR2 = $"Press {YESBUTTONNAME} to quit,\nor press {NOBUTTONNAME} to enjoy\nmore violent diversion.";
    //internal const string ENDSTR3 = $"Depressing the {YESBUTTONNAME} key means\nyou must return to the\nhumdrum workday world.";
    //internal const string ENDSTR4 = $"Hey, quit or play,\n{YESBUTTONNAME} or {NOBUTTONNAME}:\nit's your choice.";
    //internal const string ENDSTR5 = "Sure you don't want to\nwaste a few more\nproductive hours?";
    //internal const string ENDSTR6 = $"I think you had better\nplay some more. Please\npress {NOBUTTONNAME}...please?";
    //internal const string ENDSTR7 = $"If you are tough, press {NOBUTTONNAME}.\nIf not, press {YESBUTTONNAME} daintily.";
    //internal const string ENDSTR8 = $"I'm thinkin' that\nyou might wanna press {NOBUTTONNAME}\nto play more. You do it.";
    //internal const string ENDSTR9 = $"Sure. Fine. Quit.\nSee if we care.\nGet it over with.\nPress {YESBUTTONNAME}.";
//#else
    internal const string ENDSTR1 = "Dost thou wish to\nleave with such hasty\nabandon?";
    internal const string ENDSTR2 = "Chickening out...\nalready?";
    internal const string ENDSTR3 = $"Press {NOBUTTONNAME} for more carnage.\nPress {YESBUTTONNAME} to be a weenie.";
    internal const string ENDSTR4 = "So, you think you can\nquit this easily, huh?";
    internal const string ENDSTR5 = $"Press {NOBUTTONNAME} to save the world.\nPress {YESBUTTONNAME} to abandon it in\nits hour of need.";
    internal const string ENDSTR6 = $"Press {NOBUTTONNAME} if you are brave.\nPress {YESBUTTONNAME} to cower in shame.";
    internal const string ENDSTR7 = $"Heroes, press \" {NOBUTTONNAME} \".\nWimps, press {YESBUTTONNAME}.";
    internal const string ENDSTR8 = $"You are at an intersection.\nA sign says, 'Press {YESBUTTONNAME} to quit.'\n>";
    internal const string ENDSTR9 = $"For guns and glory, press {NOBUTTONNAME}.\nFor work and worry, press {YESBUTTONNAME}.";
//#endif
}

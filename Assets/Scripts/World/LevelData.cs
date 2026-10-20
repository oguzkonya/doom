using UnityEngine;

namespace Doom
{
    /// <summary>
    /// A level is an ASCII grid; every character is one 2x2m cell. Legend:
    ///
    ///  Structure           Floors                    Doors
    ///   ' ' void            '.' floor                 'D' door
    ///   '#' default wall    ',' outdoor floor (sky)   'R' red key door
    ///   'B' brick wall      '~' nukage (hurts)        'U' blue key door
    ///   'T' tech wall       'S' secret (floor)        'Y' yellow key door
    ///   'M' metal wall      'P' player start          'H' secret door (looks like a wall)
    ///   'K' stone wall
    ///   'X' exit switch
    ///
    ///  Monsters: z zombieman, g shotgun guy, i imp, d demon, c cacodemon, l lost soul, b baron
    ///  Health/armor: h bonus, + stimpack, m medikit, * soulsphere, v armor bonus, a green armor, A blue armor, p backpack
    ///  Ammo: 1 clip, 2 bullet box, 3 shells, 4 shell box, 5 rocket, 6 rocket box, 7 cell, 8 cell pack
    ///  Weapons: ^ chainsaw, $ shotgun, % chaingun, &amp; rocket launcher, = plasma gun, @ BFG9000
    ///  Keys: r red, u blue, y yellow       Other: o explosive barrel
    ///
    /// Optional Heights grid (same layout): digits 0-9 raise the floor by 0.25m per step.
    /// </summary>
    public class LevelDef
    {
        public string Id;
        public string Name;
        public string[] Map;
        public string[] Heights;
        public string WallTexture = "wall_brick";
        public string FloorTexture = "floor_tile";
        public string OutdoorFloorTexture = "floor_dirt";
        public string CeilingTexture = "ceil_tile";
        public float CeilingHeight = 4f;
        public float Light = 0.9f;
        public float StartAngle;
        public int ParSeconds = 60;
        public string Music;
    }

    public static class Levels
    {
        public static readonly LevelDef[] All =
        {
            new LevelDef
            {
                Id = "E1M1", Name = "Hangar", Music = "e1m1", ParSeconds = 30,
                WallTexture = "wall_tech", FloorTexture = "floor_tile", StartAngle = 180f,
                Map = new[]
                {
                    "                                          ",
                    " TTTTTTTTTTT          KKKKKKKKKKKKKKKKK   ",
                    " T....h....TTTTT      K,,,,,,,,,,,,,,,K   ",
                    " T.........T...T      K,,i,,,,,,,,,i,,K   ",
                    " T..1...1..H.S*T      K,,,,,,,o,,,,,,,K   ",
                    " T.........TTTTT      K,,,,,,,,,,,,,,,K   ",
                    " T....P....T          K,,,,,,,,,,,,,,,K   ",
                    " TTTT...TTTT          KKKKKKKK.KKKKKKKK   ",
                    "    T...T                    K.K          ",
                    "    T...T  BBBBBBBBBBBBBBBBBBB.BBBBBBB    ",
                    "    T...T  B....z..............z.....B    ",
                    "    T...TTTB.........................B    ",
                    "    T......D.......BBBBBB............B    ",
                    "    T...TTTB...3...B~~~~B....z.......B    ",
                    "    T...T  B.......B~~a~B............B    ",
                    "    T...T  B..z....B~~~~B..........g.B    ",
                    "    T+..T  B.......BB..BB............B    ",
                    "    TTTTT  BBBBBBBBBB..BBBBBBBBDBBBBBB    ",
                    "                    M..M      M.M         ",
                    "                 MMMM..MMM  MMM.MMMMMMM   ",
                    "                 M.i...i.MMMM..z....o.M   ",
                    "                 M...3...D......g.....M   ",
                    "                 M..$..h.MMMM.........M   ",
                    "                 MMMMMMMMM  MMMMXMMMMMM   ",
                }
            },
            new LevelDef
            {
                Id = "E1M2", Name = "Nuclear Plant", Music = "e1m2", ParSeconds = 75,
                WallTexture = "wall_metal", FloorTexture = "floor_stone", Light = 0.8f,
                Map = new[]
                {
                    "                                            ",
                    "              TTTTTTTTTTTTTTT               ",
                    "              T.............T               ",
                    "              T..i.......i..X               ",
                    "              T....+...m....T               ",
                    "              TTTTTT...TTTTTT               ",
                    "                   T...T                    ",
                    "                   T...T                    ",
                    "          MMMMMMMMMMMUMMMMMMMMMM            ",
                    "          M..~~~~~~....~~~~~~..M  TTTTTTTTTT",
                    "          M..~~~~~~....~~~~~~..M  Ti......iT",
                    "          M..~~v~~~.i..~~~~~~..M  T.%.TT...T",
                    "          M....................MTTT..g.....T",
                    "          M..z.......o.......i.D......u....T",
                    "          M....................MTTT........T",
                    "          M..~~~~~~....~~~~3~..M  T...TT...T",
                    "          M..~~~~~~....~~~~~~..M  Tz..5....T",
                    "          M..~~~~~~..z.~~~~~~..M  TTTTTTTTTT",
                    "          MMMMMDMMMMMMMMMMMMMMMM            ",
                    "              M.M                           ",
                    "              M.M                           ",
                    "          MMMMM.MMMMM                       ",
                    "      MMMMM.........M                       ",
                    "      MpS.H..1...1..M                       ",
                    "      MMMMM....P....M                       ",
                    "          M..$...h..M                       ",
                    "          MMMMMMMMMMM                       ",
                },
                Heights = new[]
                {
                    "",
                    "",
                    "0000000000000006666666666666",
                    "0000000000000006666666666666",
                    "0000000000000006666666666666",
                    "00000000000000000000666",
                    "00000000000000000000444",
                    "00000000000000000000222",
                }
            },
            new LevelDef
            {
                Id = "E1M3", Name = "Toxin Refinery", Music = "e1m3", ParSeconds = 120,
                WallTexture = "wall_stone", FloorTexture = "floor_tile", Light = 0.85f,
                Map = new[]
                {
                    "                                                ",
                    "                TTTTTTTTTTTTTTTTT               ",
                    "                T..i....=......iT               ",
                    "                T.......r.......T               ",
                    "                T..c..........c.T               ",
                    "                T...............T               ",
                    "                Tz......5......zT               ",
                    "                TTTTTTT...TTTTTTT               ",
                    "                      T...T                     ",
                    "   BBBBB              T.h.T                     ",
                    "   B@S.B              T...T                     ",
                    "   B8..B              TT.TT                     ",
                    " BBBBHBBBB    KKKKKKKKKKYKKKKKKKKKK   MMMMMMMMMM",
                    " B.......B    K,,,,,,,,,,,,,,,,,,,K   M........M",
                    " B..c....B    K,,~~~~~,,,,,,,i,,,,K   M..i..i..M",
                    " B.......B    K,,~~~~~,,,,,,,,,,,,K   M........M",
                    " B..i....BBBBBK,,,,,,,,,o,,,,,,,,,KMMMM........M",
                    " B...y........D,,,,,,,,,,,,,,,,,,,R.......b....X",
                    " B.......BBBBBK,,,,i,,,,,,,,,,,,,,KMMMM........M",
                    " B..&....B    K,,,,,,,,,,,,~~~~~,,K   M..6.....M",
                    " B.......B    K,,,3,,,,,,,,~~~~~,,K   M........M",
                    " B.......B    K,,,,,,,,,,,,,,,,,g,K   M..+..m..M",
                    " BBBBBBBBB    KKKKKKKKKKDKKKKKKKKKK   MMMMMMMMMM",
                    "                       K.K                      ",
                    "                       K.K                      ",
                    "                  KKKKKK.KKKKKK                 ",
                    "                  K...........K                 ",
                    "                  K..1..P..1..K                 ",
                    "                  K...........K                 ",
                    "                  K..2.....+..K                 ",
                    "                  KKKKKKKKKKKKK                 ",
                    "                                                ",
                },
                Heights = new[]
                {
                    "",
                    "",
                    "000000000000000000000444444",
                    "000000000000000000000444444",
                    "000000000000000000000444444",
                    "00000000000000000000000222",
                }
            },
        };
    }
}

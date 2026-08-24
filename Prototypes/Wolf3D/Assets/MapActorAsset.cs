using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets
{
    internal record MapActorAsset
    {
        public string Class { get; set; }
        public int Angles { get; set; }
        public int Patrol { get; set; }
        public int MinSkill { get; set; }
    }
}

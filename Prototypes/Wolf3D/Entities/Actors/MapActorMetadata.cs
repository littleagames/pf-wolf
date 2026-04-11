using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Entities.Actors
{
    internal class MapActorMetadata
    {
        public Dictionary<int, ActorSpawnData> Things { get; internal set; } = new();
    }
}

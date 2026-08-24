using System;
using System.Collections.Generic;
using System.Text;
using Wolf3D.Assets;

namespace Wolf3D.Entities.Actors
{
    internal class MapActorMetadata
    {
        public Dictionary<int, ActorSpawnData> Things { get; internal set; } = new();

        public MapObjectTranslationAsset ToAsset()
        {
            var asset = new MapObjectTranslationAsset();
            foreach (var kvp in Things)
            {
                asset.Things.Add(kvp.Key, new MapActorAsset
                {
                    Class = kvp.Value.Class,
                    Angles = kvp.Value.Angles,
                    Patrol = kvp.Value.Patrol,
                    MinSkill = kvp.Value.MinSkill
                });
            }
            return asset;
        }
    }
}

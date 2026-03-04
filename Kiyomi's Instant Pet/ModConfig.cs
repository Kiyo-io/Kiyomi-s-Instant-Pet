using System;

namespace Kiyomi_s_Instant_Pet
{
    internal class ModConfig
    {
        public string PetType { get; set; } = "Dog";
        public string PetName { get; set; } = "Max";
        public bool AllowMultiplePets { get; set; } = false;

        public bool SpawnWithoutBowl { get; set; } = true;
    }
}

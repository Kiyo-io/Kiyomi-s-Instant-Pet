using StardewValley.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiyomi_s_Instant_Pet
{
    internal class PetRegistry
    {

        public class InstantPetRegistry
        {
            public Dictionary<string, RegisteredPet> Pets = new();
        }

        public class RegisteredPet
        {
            public string Id;
            public string DisplayName;
            public Action<Pet> OnInteract;
        }
    }
}

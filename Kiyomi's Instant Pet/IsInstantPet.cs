using StardewValley.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Kiyomi_s_Instant_Pet
{
    internal class IsInstantPet : InstantPetData
    {
        private bool InstantPetData(Pet pet)
        {
            return pet.modData.ContainsKey("Kiyomi.InstantPetID");
        }

        private void SpawnAllInstantPets()
        {
            foreach (var data in ModEntry.Instance.PetBank)
                ModEntry.Instance.SpawnInstantPet(data);
        }
    }
   }


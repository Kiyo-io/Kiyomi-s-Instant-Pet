using Microsoft.Xna.Framework;
using StardewValley.Characters;

namespace Kiyomi_s_Instant_Pet
{
    public interface IInstantPetApi
    {
        void RegisterPetType(
            string petId,
            string displayName,
            Action<Pet> onInteract = null
        );
    }
}
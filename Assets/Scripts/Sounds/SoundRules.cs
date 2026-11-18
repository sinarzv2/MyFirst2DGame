using UnityEngine;

namespace Sounds
{
    // Rules shared by every sound component (the hero's, the enemies'), without components: Edit Mode
    // tests call them directly.
    public static class SoundRules
    {
        // How far to one side a sound may sit (panStereo: -1 = left speaker only, 1 = right only).
        // Not all the way: in a 2D game a sound jumping into one ear is tiring.
        public const float MaxPan = 0.6f;

        // A chain of pickups (coins taken one soon after another) climbs one semitone a pickup, up to
        // this many (7 = a fifth), then stays there: a reward that grows, without squeaking.
        public const int MaxChainSteps = 7;

        // The chain after this pickup: one longer if the last pickup was at most `window` seconds ago,
        // else a new chain (0).
        public static int NextChain(int chain, float sinceLast, float window)
        {
            return sinceLast <= window ? chain + 1 : 0;
        }

        // Pitch for a place in the chain: each step a semitone higher (12 semitones = double).
        public static float GetChainPitch(int chain)
        {
            return Mathf.Pow(2f, Mathf.Clamp(chain, 0, MaxChainSteps) / 12f);
        }

        // The variation after this one, back to the first after the last (footsteps take turns).
        public static int NextIndex(int index, int count)
        {
            return (index + 1) % count;
        }

        // t is a random number from 0 to 1: the pitch goes from 1 - variation to 1 + variation.
        public static float GetPitch(float variation, float t)
        {
            return 1f + variation * (2f * t - 1f);
        }

        // Volume by distance from the listener: full up to `near`, fading in a straight line to
        // silence at `far`.
        public static float GetVolume(float distance, float near, float far)
        {
            if (far <= near)
            {
                return distance <= near ? 1f : 0f;
            }

            return Mathf.Clamp01((far - distance) / (far - near));
        }

        // Left/right balance from how far to the side the sound is (dx, units; negative = left).
        public static float GetPan(float dx, float far)
        {
            return far > 0f ? Mathf.Clamp(dx / far, -1f, 1f) * MaxPan : 0f;
        }
    }
}

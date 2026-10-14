using System;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The settings a cue starts with, by folder (ARCHITECTURE §26): the first time a folder holds a sound, the bank builder
    /// gives its cue these. After that the bank keeps whatever is set in its Inspector. One table, so every sound of a kind
    /// behaves alike.
    /// </summary>
    public static class CueDefaults
    {
        public static CueSettings For(string key)
        {
            var s = new CueSettings();
            bool Is(string prefix) => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            bool Ends(string suffix) => key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

            if (Is("Music/"))
            {
                s.volume = 0.7f;         // under the beeps (#47)
                s.spatialBlend = 0f;
                s.maxVoices = 1;
                s.priority = 0;
            }
            else if (Is("Ambience/Emitters/"))
            {
                s.volume = 0.7f;
                s.priority = 200;        // under the beeps
            }
            else if (Is("Ambience/") && Ends("/Beds"))
            {
                s.volume = 0.5f;
                s.spatialBlend = 0f;
                s.maxVoices = 8;
                s.priority = 200;
            }
            else if (Is("Ambience/") && Ends("/Scatter"))
            {
                s.volume = 0.6f;
                s.volumeJitter = 0.25f;
                s.pitchJitter = 0.06f;
                s.maxVoices = 3;
                s.minDistance = 4f;
                s.maxDistance = 60f;
                s.priority = 210;
            }
            else if (Is("SFX/Bomb/"))
            {
                s.maxVoices = 2;
                s.minDistance = 3f;
                s.maxDistance = 45f;
                s.priority = 0;          // the beep is gameplay: never culled
                if (Is("SFX/Bomb/FuseLoop")) s.volume = 0.5f;
            }
            else if (Is("SFX/Player/Footsteps/"))
            {
                s.volume = 0.55f;
                s.volumeJitter = 0.15f;
                s.pitchJitter = 0.08f;
                s.maxVoices = 6;
                s.minDistance = 1.5f;
                s.maxDistance = 25f;
                s.priority = 160;
            }
            else if (Is("SFX/Player/"))
            {
                s.volume = 0.7f;
                s.volumeJitter = 0.1f;
                s.pitchJitter = 0.06f;
                s.maxVoices = 4;
                s.minDistance = 1.5f;
                s.maxDistance = 30f;
                s.priority = 150;
            }
            else if (Is("SFX/UI/"))
            {
                s.volume = 0.6f;
                s.spatialBlend = 0f;
                s.pitchJitter = Is("SFX/UI/Hover") ? 0.03f : 0f;
                s.cooldown = Is("SFX/UI/Hover") || Is("SFX/UI/Slider") ? 0.06f : 0f;
                s.maxVoices = 3;
                s.priority = 40;
            }
            else if (Is("SFX/Course/"))
            {
                s.spatialBlend = 0f;
                s.maxVoices = 1;
                s.cooldown = 0.25f;
                s.priority = 20;
            }
            else if (Is("SFX/Obstacles/"))
            {
                s.volume = 0.7f;
                s.volumeJitter = 0.1f;
                s.pitchJitter = 0.05f;
                s.maxVoices = Ends("/Loop") ? 4 : 6;
                s.minDistance = 3f;
                s.maxDistance = 40f;
                s.priority = 170;
            }
            return s;
        }
    }
}

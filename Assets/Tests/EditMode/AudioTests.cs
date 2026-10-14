using System.Collections.Generic;
using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>M16 (ARCHITECTURE §26): the sound folders' pure rules - picking, routing, music places, keys, tracks, surfaces.</summary>
    public class AudioTests
    {
        // ------------------------------------------------------------------ picking

        [Test]
        public void Pick_NeverRepeatsTheLastOne()
        {
            var random = new System.Random(7);
            int last = -1;
            for (int i = 0; i < 500; i++)
            {
                int next = CuePicker.Next(4, last, (min, max) => random.Next(min, max));
                Assert.That(next, Is.InRange(0, 3));
                Assert.AreNotEqual(last, next);
                last = next;
            }
        }

        [Test]
        public void Pick_ReachesEveryOtherVariation()
        {
            var seen = new HashSet<int>();
            for (int r = 0; r < 3; r++) seen.Add(CuePicker.Next(4, 2, (min, max) => r));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3 }, seen);
        }

        [Test]
        public void Pick_OneVariation_IsAlwaysIt() => Assert.AreEqual(0, CuePicker.Next(1, 0, (min, max) => 0));

        [Test]
        public void Jitter_StaysInRange()
        {
            Assert.AreEqual(0.9f, CuePicker.Jitter(0.1f, 0f), 1e-5f);
            Assert.AreEqual(1.1f, CuePicker.Jitter(0.1f, 1f), 1e-5f);
            Assert.AreEqual(1f, CuePicker.Jitter(0f, 0.3f), 1e-5f);
        }

        // ------------------------------------------------------------------ routing

        [Test]
        public void Route_FollowsTheFolder()
        {
            Assert.AreEqual(SoundBank.AudioRoute.Music, SoundBank.Route(CueKeys.MusicLobby));
            Assert.AreEqual(SoundBank.AudioRoute.Ambience, SoundBank.Route(CueKeys.AmbienceEmitter("River")));
            Assert.AreEqual(SoundBank.AudioRoute.Bomb, SoundBank.Route(CueKeys.BombBeeps[3]));
            Assert.AreEqual(SoundBank.AudioRoute.Movement, SoundBank.Route(CueKeys.Footsteps("Grass")));
            Assert.AreEqual(SoundBank.AudioRoute.Ui, SoundBank.Route(CueKeys.UiClick));
            Assert.AreEqual(SoundBank.AudioRoute.World, SoundBank.Route(CueKeys.CourseCheckpoint));
            Assert.AreEqual(SoundBank.AudioRoute.World, SoundBank.Route(CueKeys.Obstacle("PressurePlate", CueKeys.ObstacleOn)));
        }

        // ------------------------------------------------------------------ music places

        [Test]
        public void MusicPlace_MenuLobbyLoadingCourseResults()
        {
            Assert.AreEqual(MusicContext.Place.Menu, MusicContext.Resolve(NetworkBootstrap.Mode.Menu, false, null));
            Assert.AreEqual(MusicContext.Place.Menu, MusicContext.Resolve(NetworkBootstrap.Mode.Working, false, null));
            Assert.AreEqual(MusicContext.Place.Lobby, MusicContext.Resolve(NetworkBootstrap.Mode.Lobby, false, null));
            Assert.AreEqual(MusicContext.Place.Loading, MusicContext.Resolve(NetworkBootstrap.Mode.InGame, false, null));
            Assert.AreEqual(MusicContext.Place.Loading, MusicContext.Resolve(NetworkBootstrap.Mode.InGame, true, RunState.Initializing));
            Assert.AreEqual(MusicContext.Place.Course, MusicContext.Resolve(NetworkBootstrap.Mode.InGame, true, RunState.WaitingForPlayers));
            Assert.AreEqual(MusicContext.Place.Course, MusicContext.Resolve(null, true, RunState.Resetting));
            Assert.AreEqual(MusicContext.Place.Results, MusicContext.Resolve(null, true, RunState.Completed));
        }

        [Test]
        public void MusicCandidates_FallBackFromActToCourse()
        {
            CollectionAssert.AreEqual(new[] { "Music/Courses/PatataWilds/Act3", "Music/Courses/PatataWilds" },
                MusicContext.Candidates(MusicContext.Place.Course, "PatataWilds", 3));
            Assert.AreEqual(CueKeys.MusicResults, MusicContext.Candidates(MusicContext.Place.Results, "PatataWilds", 3)[0]);
            CollectionAssert.AreEqual(new[] { CueKeys.MusicLobby, CueKeys.MusicMenu },
                MusicContext.Candidates(MusicContext.Place.Lobby, "Bootstrap", 1));
            Assert.IsEmpty(MusicContext.Candidates(MusicContext.Place.Loading, "PatataWilds", 1));
        }

        // ------------------------------------------------------------------ folders and tracks

        [Test]
        public void Key_IsTheFolderUnderAssetsAudio()
        {
            Assert.AreEqual("SFX/Player/Footsteps/Grass", SoundBankBuilder.KeyOf("Assets/Audio/SFX/Player/Footsteps/Grass/step_01.wav"));
            Assert.AreEqual("Music/Menu", SoundBankBuilder.KeyOf("Assets/Audio/Music/Menu/theme.ogg"));
            Assert.IsNull(SoundBankBuilder.KeyOf("Assets/Audio/loose.wav"));
            Assert.IsNull(SoundBankBuilder.KeyOf("Assets/Art/whatever.wav"));
        }

        [Test]
        public void Tracks_PairIntroWithItsLoop()
        {
            var clips = new[] { Clip("Dawn_intro"), Clip("Dawn_loop"), Clip("Noon"), Clip("Lonely_intro") };
            try
            {
                var tracks = SoundBankBuilder.PairTracks(clips, "Music/Test");
                Assert.AreEqual(2, tracks.Count, "an intro is never a track of its own");
                var dawn = tracks.Single(t => t.clip.name == "Dawn_loop");
                Assert.AreEqual("Dawn_intro", dawn.intro.name);
                Assert.IsNull(tracks.Single(t => t.clip.name == "Noon").intro);
            }
            finally
            {
                foreach (var c in clips) Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void Surface_ComesFromTheMaterialName()
        {
            Assert.AreEqual("Rock", SoundBankBuilder.SurfaceFromName("Nature_RockPath"));
            Assert.AreEqual("Grass", SoundBankBuilder.SurfaceFromName("Nature_GrassPath"));
            Assert.AreEqual("Wood", SoundBankBuilder.SurfaceFromName("Nature_Planks"));
            Assert.AreEqual("Leaves", SoundBankBuilder.SurfaceFromName("Nature_LeafLitter"));
            Assert.AreEqual("Mud", SoundBankBuilder.SurfaceFromName("Nature_Mud"));
            Assert.AreEqual("Dirt", SoundBankBuilder.SurfaceFromName("Nature_ForestFloor"));
            Assert.IsNull(SoundBankBuilder.SurfaceFromName("Nature_Smoke"));
        }

        [Test]
        public void Skeleton_HasEveryCodeKeyOnce()
        {
            var keys = CueKeys.Fixed().ToList();
            Assert.AreEqual(keys.Count, keys.Distinct().Count());
            Assert.That(keys, Has.Member(CueKeys.FootstepsDefault));
            Assert.That(keys, Has.Member(CueKeys.BombBeeps[0]));
            CollectionAssert.Contains(CueKeys.ForCourse("PatataTemple", 3).ToList(), "Ambience/Courses/PatataTemple/Act3/Beds");
        }

        [Test]
        public void Defaults_KeepMusicAndMenusFlat_AndBeepsFirst()
        {
            Assert.AreEqual(0f, CueDefaults.For(CueKeys.MusicMenu).spatialBlend);
            Assert.AreEqual(0f, CueDefaults.For(CueKeys.UiClick).spatialBlend);
            Assert.AreEqual(0f, CueDefaults.For(CueKeys.Beds(CueKeys.AmbienceMenu)).spatialBlend);
            Assert.AreEqual(0, CueDefaults.For(CueKeys.BombBeeps[0]).priority);
            Assert.Greater(CueDefaults.For(CueKeys.Footsteps("Grass")).pitchJitter, 0f, "footsteps vary");
        }

        static AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 64, 1, 8000, false);
            clip.name = name;
            return clip;
        }
    }
}

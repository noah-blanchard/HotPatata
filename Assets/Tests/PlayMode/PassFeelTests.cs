using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The pass feel (PROJECT_SPEC §8–9) on the PassSandbox range lane: a competent throw (sensible charge, aimed where
    /// the receiver will be) is caught at 4, 8 and 12 m, while moving and while jumping; a weak throw never becomes a
    /// long pass, the assist never turns a clearly bad throw into a catch, and near misses are forgiven on the receiver's
    /// side. Every pass logs one [PassFeel] line with its numbers.
    /// </summary>
    public class PassFeelTests : SandboxTestBase
    {
        // The PassRange lane on the east side of the sandbox: the thrower stands on the start line, receivers along +Z.
        static readonly Vector3 Lane = new Vector3(12f, 0.05f, -16f);
        const float PressDistance = 2.5f;   // the receiver presses catch when the bomb is this close (a human-like reaction)
        const float PlayerGravity = 28f;

        struct Outcome
        {
            public bool Caught;
            public float Flight;
            public float Closest;
            public ThrowShot Shot;
            public override string ToString() =>
                $"{(Caught ? "CAUGHT" : "missed")} flight {Flight:F2}s closest {Closest:F2} m speed {Shot.Velocity.magnitude:F1} " +
                (Shot.HasAssist ? $"assist {Shot.AssistTarget} off {Shot.AssistAngle:F1}° turned {Shot.AssistCorrection:F1}°{(Shot.AssistYawOnly ? " yaw-only" : "")}" : "no assist");
        }

        struct PassSetup
        {
            public float Distance, Charge;
            public float YawError;            // degrees added to a competent aim (+ = right)
            public float OffAimDegrees;       // if > 0: aim so the receiver's body is exactly this far off the aim (overrides YawError)
            public float Lead;                // share of the receiver's motion the thrower leads (1 = where they will be)
            public bool AimAtChest;           // look straight at the chest instead of a competent aim (no drop compensation)
            public Vector2 ThrowerMove, ReceiverMove;
            public bool ThrowerJumps, ReceiverJumps;
            public bool ReceiverPressesLate;  // press only after the bomb has passed its closest point
        }

        Outcome last;

        IEnumerator Pass(string name, PassSetup s)
        {
            yield return Place(p1, Lane);
            yield return Place(p2, Lane + Vector3.forward * s.Distance);
            p1.Look.SetAim(0f, 0f);
            p2.Look.SetAim(180f, 0f);   // the receiver faces the thrower
            var rx = p2.Input.Scripted ??= new PlayerInputReader.ScriptedInput();
            yield return WaitUntil(() => bomb.State == BombState.Held && bomb.Carrier == p1, 3f, "player 1 never held the bomb");
            bomb.Fuse.Refresh();

            Drive.Move = s.ThrowerMove;
            rx.Move = s.ReceiverMove;
            if (s.ThrowerMove != Vector2.zero || s.ReceiverMove != Vector2.zero) yield return WaitSeconds(0.35f);   // up to speed

            float hold = s.Charge * tuning.throwChargeTime;
            if (s.ThrowerJumps) Drive.PressJump();
            if (s.ReceiverJumps) rx.PressJump();
            if (s.ThrowerJumps) yield return WaitSeconds(Mathf.Max(0f, 0.3f - hold));   // release near the top of the jump

            if (s.Charge > 0f)
            {
                Drive.SetThrowHeld(true);
                yield return WaitSeconds(hold);
            }

            // Aim at the release moment, then let go (a tap is a press and release in the same frame).
            if (s.AimAtChest) LookAt(p1, p2.CatchVolume.CatchCenter);
            else AimCompetently(p1, p2, s.Lead);
            if (s.OffAimDegrees > 0f) TurnUntilOffAim(p1, p2, s.OffAimDegrees);
            else if (s.YawError != 0f) p1.Look.SetAim(p1.Look.Yaw + s.YawError, p1.Look.Pitch);

            if (s.Charge > 0f) Drive.SetThrowHeld(false);
            else Drive.PressThrow();
            yield return null;
            Assert.AreEqual(BombState.Thrown, bomb.State, name + ": the bomb should have left on release");

            last = new Outcome { Shot = p1.Thrower.LastShot, Closest = float.MaxValue };
            float release = Time.time;
            bool pressed = false;
            float previous = float.MaxValue;
            Vector3 lastPosition = bomb.transform.position;
            while (bomb.State == BombState.Thrown && Time.time - release < 3f)
            {
                // Swept, in the same "reach space" the resolver uses, so Closest compares with the reach.
                float d = CatchResolver.ReachDistance(tuning, lastPosition, bomb.transform.position, p2.CatchVolume.CatchCenter, out _);
                lastPosition = bomb.transform.position;
                last.Closest = Mathf.Min(last.Closest, d);
                bool passing = d > previous && previous < 1.4f;
                if (!pressed && (s.ReceiverPressesLate ? passing : d <= PressDistance))
                {
                    rx.PressCatch();
                    pressed = true;
                }
                previous = d;
                yield return null;
            }

            last.Flight = Time.time - release;
            last.Caught = bomb.Carrier == p2;
            Drive.Move = Vector2.zero;
            rx.Move = Vector2.zero;
            Debug.Log($"[PassFeel] {name}: {last}");
        }

        // ------------------------------------------------------------------ aiming like a player

        static void LookAt(Player p, Vector3 point)
        {
            Vector3 d = point - p.CameraTarget.position;
            p.Look.SetAim(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        /// <summary>Aims so the raw (unassisted) arc at this charge meets the receiver where they will be.</summary>
        void AimCompetently(Player from, Player to, float lead)
        {
            float g = ThrowBallistics.Gravity(tuning);
            float speed = PlayerThrower.SpeedFor(tuning, from.Thrower.Charge01);
            LookAt(from, to.CatchVolume.CatchCenter);

            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = from.Thrower.ThrowOriginNow();
                Vector3 target = to.CatchVolume.CatchCenter;
                Vector3 inherited = PlayerThrower.InheritedVelocity(tuning, from.Velocity, target - origin);

                // Where they will be when it arrives (horizontal lead, plus their jump arc if airborne).
                Vector3 v = to.Velocity;
                Vector3 dir = Vector3.forward;
                float time = 0f;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 predicted = target + new Vector3(v.x, 0f, v.z) * (lead * time);
                    if (!to.Motor.Grounded || v.y > 0.5f)   // mid-jump: follow their arc, but not below standing height
                        predicted.y = Mathf.Max(tuning.catchCenterHeight, target.y + v.y * time - 0.5f * PlayerGravity * time * time);
                    Vector3 rel = predicted - origin - inherited * time;
                    if (!ThrowBallistics.TrySolveLowArc(rel, speed, g, out dir, out time)) return;   // out of range: keep looking at them
                }

                // Nudge the view until the raw launch direction matches the solution.
                var shot = from.Thrower.ComputeShot(origin, from.Thrower.Charge01);
                Vector3 raw = (shot.RawVelocity - inherited).normalized;
                float dYaw = Mathf.DeltaAngle(Mathf.Atan2(raw.x, raw.z) * Mathf.Rad2Deg, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                float dElev = Elevation(dir) - Elevation(raw);
                from.Look.SetAim(from.Look.Yaw + dYaw, from.Look.Pitch - dElev);
            }
        }

        static void TurnUntilOffAim(Player from, Player to, float degrees)
        {
            for (int i = 0; i < 400; i++)
            {
                Vector3 forward = from.Look.ViewRotation * Vector3.forward;
                if (AimAssist.OffAimAngle(forward, to.CatchVolume.CatchCenter - from.CameraTarget.position) >= degrees) return;
                from.Look.SetAim(from.Look.Yaw + 0.05f, from.Look.Pitch);
            }
        }

        static float Elevation(Vector3 d) => Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ standing passes

        [UnityTest]
        public IEnumerator Pass4m_TapAtTheChest_IsCaught()
        {
            yield return Pass("4 m tap", new PassSetup { Distance = 4f, Charge = 0f, AimAtChest = true });
            Assert.IsTrue(last.Caught, last.ToString());
        }

        [UnityTest]
        public IEnumerator Pass8m_HalfCharge_IsCaught_Quickly()
        {
            yield return Pass("8 m half charge", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f });
            Assert.IsTrue(last.Caught, last.ToString());
            Assert.Less(last.Flight, 0.55f, "a normal pass is fast, not floaty: " + last);
        }

        [UnityTest]
        public IEnumerator Pass12m_FullCharge_IsCaught_Quickly()
        {
            yield return Pass("12 m full charge", new PassSetup { Distance = 12f, Charge = 1f, Lead = 1f });
            Assert.IsTrue(last.Caught, last.ToString());
            Assert.Less(last.Flight, 0.6f, "a long pass is fast too: " + last);
        }

        [UnityTest]
        public IEnumerator Pass12m_TapAtTheChest_WithTheLockShown_FallsShort()
        {
            yield return Pass("12 m tap at the chest", new PassSetup { Distance = 12f, Charge = 0f, AimAtChest = true });
            Assert.AreSame(p2, last.Shot.AssistTarget, "the receiver is dead on the aim, so the lock shows: " + last);
            Assert.IsFalse(last.Caught, "a weak tap must not become a 12 m pass: " + last);
        }

        [UnityTest]
        public IEnumerator Pass8m_TapAtTheChest_FallsShort()
        {
            yield return Pass("8 m tap at the chest", new PassSetup { Distance = 8f, Charge = 0f, AimAtChest = true });
            Assert.IsFalse(last.Caught, "the shortest pass needs aiming up to go 8 m: " + last);
        }

        // ------------------------------------------------------------------ moving and jumping

        [UnityTest]
        public IEnumerator MovingReceiver_StrafingAtFullSpeed_IsCaught()
        {
            // (Strafes run east, +X: the KitDemo moving platform is just west of the lane.)
            yield return Pass("moving receiver", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 0.6f, ReceiverMove = new Vector2(-1f, 0f) });
            Assert.IsTrue(last.Caught, "a pass led a bit short of a strafing receiver: " + last);
        }

        [UnityTest]
        public IEnumerator MovingThrower_Strafing_IsCaught()
        {
            yield return Pass("strafing thrower", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, ThrowerMove = new Vector2(1f, 0f) });
            Assert.IsTrue(last.Caught, "strafing must not push the throw off the aim: " + last);
        }

        [UnityTest]
        public IEnumerator MovingThrower_RunningForward_IsCaught()
        {
            yield return Pass("running thrower", new PassSetup { Distance = 12f, Charge = 0.4f, Lead = 1f, ThrowerMove = new Vector2(0f, 1f) });
            Assert.IsTrue(last.Caught, last.ToString());
            Assert.Greater(last.Shot.Velocity.magnitude, PlayerThrower.SpeedFor(tuning, last.Shot.Charge01) + 1f, "running forward throws harder");
        }

        [UnityTest]
        public IEnumerator BothMoving_IsCaught()
        {
            yield return Pass("both moving", new PassSetup
            {
                Distance = 8f, Charge = 0.5f, Lead = 0.6f, ThrowerMove = new Vector2(-1f, 0f), ReceiverMove = new Vector2(-1f, 0f)
            });
            Assert.IsTrue(last.Caught, last.ToString());
        }

        [UnityTest]
        public IEnumerator JumpingReceiver_IsCaught()
        {
            yield return Pass("jumping receiver", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, ReceiverJumps = true });
            Assert.IsTrue(last.Caught, "caught mid-jump: " + last);
        }

        [UnityTest]
        public IEnumerator JumpingThrower_IsCaught()
        {
            yield return Pass("jumping thrower", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, ThrowerJumps = true });
            Assert.IsTrue(last.Caught, "jumping must not push the throw off the aim: " + last);
        }

        // ------------------------------------------------------------------ assist limits and receiver forgiveness

        [UnityTest]
        public IEnumerator EdgeOfTheAssistCone_StillGetsALittleHelp()
        {
            yield return Pass("edge of cone", new PassSetup { Distance = 10f, Charge = 0.6f, Lead = 1f, OffAimDegrees = tuning.assistConeDegrees - 0.5f });
            Assert.AreSame(p2, last.Shot.AssistTarget, last.ToString());
            Assert.Greater(last.Shot.AssistCorrection, 0f, last.ToString());
            Assert.LessOrEqual(last.Shot.AssistCorrection, tuning.assistMaxCorrectionDegrees + 0.01f);
        }

        [UnityTest]
        public IEnumerator JustOutsideTheAssistCone_GetsNoHelp_AndMisses()
        {
            yield return Pass("outside cone", new PassSetup { Distance = 10f, Charge = 0.6f, Lead = 1f, OffAimDegrees = tuning.assistConeDegrees + 1f });
            Assert.IsNull(last.Shot.AssistTarget, last.ToString());
            Assert.IsFalse(last.Caught, "a throw aimed clearly beside the receiver misses: " + last);
        }

        [UnityTest]
        public IEnumerator BeyondTheAssistRange_GetsNoHelp()
        {
            yield return Pass("16 m", new PassSetup { Distance = 16f, Charge = 1f, Lead = 1f });
            Assert.IsNull(last.Shot.AssistTarget, "no assist beyond assistMaxRange: " + last);
        }

        [UnityTest]
        public IEnumerator ClearlyOffAim_Misses()
        {
            yield return Pass("12 deg off", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, YawError = 12f });
            Assert.IsFalse(last.Caught, last.ToString());
        }

        [UnityTest]
        public IEnumerator NearMiss_AMetreToTheSide_IsForgivenWhenFacingIt()
        {
            float assist = tuning.assistStrength;
            tuning.assistStrength = 0f;   // receiver-side forgiveness only
            try
            {
                // Offset the aim until the arc passes ~1.2 m beside the chest: outside the bare radius, inside the facing reach.
                yield return Pass("near miss", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, YawError = Mathf.Atan2(1.0f, 8f) * Mathf.Rad2Deg });
                Assert.Greater(last.Closest, tuning.catchRadius - 0.15f, "the arc really was a near miss: " + last);
                Assert.IsTrue(last.Caught, "facing the pass, a near miss is still caught: " + last);
            }
            finally
            {
                tuning.assistStrength = assist;
            }
        }

        [UnityTest]
        public IEnumerator LatePress_JustAfterTheBombPassed_StillCatches()
        {
            float window = tuning.catchLateGrace;
            Assume.That(window, Is.GreaterThan(0.02f));
            yield return Pass("late press", new PassSetup { Distance = 8f, Charge = 0.5f, Lead = 1f, ReceiverPressesLate = true });
            Assert.IsTrue(last.Caught, "a press a frame after the bomb went by is within the late grace: " + last);
        }
    }
}

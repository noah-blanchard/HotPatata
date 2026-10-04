using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The players' Mixamo characters (ARCHITECTURE §25): each slot shows its character and a ring at the feet in its
    /// colour and shape; the first-person player sees only the shadow of theirs; in third person the potato rides the
    /// right palm; a held throw winds the arm back and waits there until the button is released. Presentation only:
    /// the throw's flight and the catch are covered by the bomb and pass-feel suites.
    /// </summary>
    public class PlayerCharacterTests : SandboxTestBase
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [UnityTest]
        public IEnumerator EachSlot_ShowsItsCharacter_AndARingInItsColourAndShape()
        {
            yield return null;
            foreach (var p in new[] { p1, p2 })
            {
                var expected = PlayerIdentity.CharacterFor(tuning, p.PlayerId);
                Assert.AreEqual(expected.Id, p.Presentation.Character.Id, p.name + " shows its slot's character");
                var block = new MaterialPropertyBlock();
                p.Presentation.SlotRing.GetPropertyBlock(block);
                Assert.Less(Vector4.Distance(p.Color, block.GetColor(BaseColor)), 1e-3f, p.name + ": the ring takes the slot colour");
                Assert.AreSame(PlayerShapeMesh.Flat(p.Shape), p.Presentation.SlotRingShape.sharedMesh, p.name + ": the slot shape on the ring (spec §19)");
            }
            Assert.AreNotEqual(p1.Presentation.Character.Id, p2.Presentation.Character.Id, "slots 1 and 2 look different");
        }

        [UnityTest]
        public IEnumerator ChangingTheSlot_SwapsTheCharacter_AndTheAnimatorFollows()
        {
            p2.ConfigureSlot(2);
            yield return null;
            Assert.AreEqual(PlayerIdentity.CharacterFor(tuning, 2).Id, p2.Presentation.Character.Id);
            Assert.AreEqual(1, p2.GetComponentsInChildren<PlayerCharacter>().Length, "the old character is gone");
            var animator = p2.Presentation.Character.Animator;
            Assert.IsNotNull(animator.runtimeAnimatorController, "the swapped-in character animates");
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "driven by PlayerAnimator");
        }

        [UnityTest]
        public IEnumerator TheFirstPersonPlayer_SeesOnlyTheShadowOfTheirCharacter_EvenAfterASwap()
        {
            p1.ConfigureSlot(2);
            yield return null;
            foreach (var r in p1.Presentation.Character.Renderers)
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, r.name + " is hidden from its own camera");
            Assert.IsFalse(p1.Presentation.SlotRing.enabled, "no ring under your own camera");
            foreach (var r in p2.Presentation.Character.Renderers)
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, r.shadowCastingMode, "the other player is seen");
            Assert.IsTrue(p2.Presentation.SlotRing.enabled);
        }

        [UnityTest]
        public IEnumerator InThirdPerson_ThePotatoRidesTheRightPalm()
        {
            Assert.AreSame(p1, bomb.Carrier, "player 1 starts with the potato");
            FirstPersonCamera.Instance.Target = p2;   // player 1 is now seen from outside
            Drive.Move = Vector2.up;                  // running: the hand swings, the potato follows it
            yield return WaitSeconds(0.6f);
            var socket = p1.Presentation.Character.HandSocket;
            Assert.Less(Vector3.Distance(socket.position, p1.HandAnchor.position), 0.15f, "the hand anchor sits at the palm");
            Assert.Less(Vector3.Distance(bomb.transform.position, p1.HandAnchor.position), 0.05f, "the potato is in the hand");
            Drive.Move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator AHeldThrow_WindsTheArmBack_AndWaits_ThenThrowsOnRelease()
        {
            FirstPersonCamera.Instance.Target = p2;
            var animator = p1.Presentation.Character.Animator;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            float restY = p1.transform.InverseTransformPoint(hand.position).y;

            Drive.SetThrowHeld(true);
            yield return WaitSeconds(1.2f);   // longer than the wind-up: the arm must be waiting
            var state = animator.GetCurrentAnimatorStateInfo(1);
            Assert.IsTrue(state.IsName("Throw"), "the throw plays on the upper body");
            Assert.AreEqual(tuning.throwAnimationHoldNormalized, state.normalizedTime, 0.02f, "it waits at the hold point");
            Assert.AreEqual(0f, animator.GetFloat("ThrowSpeed"), "while the button is held");
            Assert.Greater(p1.transform.InverseTransformPoint(hand.position).y, restY + 0.3f, "the arm is cocked: the hand up and back, by the head");
            Assert.AreEqual(BombState.Held, bomb.State, "nothing is thrown yet");

            Drive.SetThrowHeld(false);
            yield return WaitUntil(() => bomb.State == BombState.Thrown, 1f, "release throws");
            Assert.Greater(animator.GetFloat("ThrowSpeed"), 1f, "the arm follows through");
        }
    }
}

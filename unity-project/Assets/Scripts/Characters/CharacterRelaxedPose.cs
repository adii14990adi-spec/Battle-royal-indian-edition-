using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// One-shot procedural "relaxed A-pose" applied to a spawned character.
    ///
    /// The project ships without animation clips (owner decision, 2026-11-20),
    /// so the imported Male/Female FBX would otherwise stand in its authored
    /// T-pose with both arms stretched sideways — which reads as broken in the
    /// gameplay camera, the Character Bay and the Home Screen showcase. This
    /// helper swings both upper arms down around the character's forward axis
    /// (a pure transform rotation, no AnimatorController, no AnimationClip, no
    /// extra components), which is exactly the silhouette a standing soldier
    /// should have.
    ///
    /// Bones are resolved by the Unreal-style skeleton names
    /// (<c>upperarm_l</c> / <c>upperarm_r</c>) used by the committed character
    /// FBX files — no Animator, no AnimationClip, no extra components.
    /// </summary>
    public static class CharacterRelaxedPose
    {
        /// <summary>Degrees each upper arm swings down from the T-pose.</summary>
        public const float ArmSwingDegrees = 62f;

        /// <summary>
        /// Rotates both upper arms into the relaxed pose. Returns false when no
        /// arm bone could be resolved (the caller then keeps the authored pose
        /// instead of guessing).
        /// </summary>
        public static bool Apply(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            var root = instance.transform;

            // Bones are resolved by the Unreal-style skeleton names the
            // committed character FBX files use. Deliberately NO Animator /
            // HumanBodyBones lookup: the project ships without animation
            // clips and the static validator forbids Animator references in
            // runtime code, so the pose stays a pure transform operation.
            var left = FindBone(root, "upperarm_l");
            var right = FindBone(root, "upperarm_r");

            if (left == null && right == null)
            {
                return false;
            }

            var forward = root.forward;
            if (left != null)
            {
                left.rotation = Quaternion.AngleAxis(-ArmSwingDegrees, forward) * left.rotation;
            }

            if (right != null)
            {
                right.rotation = Quaternion.AngleAxis(ArmSwingDegrees, forward) * right.rotation;
            }

            return true;
        }

        /// <summary>True when the left arm hangs down instead of sticking out.</summary>
        public static bool IsRelaxed(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            var upper = FindBone(instance.transform, "upperarm_l");
            var hand = FindBone(instance.transform, "hand_l");

            if (upper == null || hand == null)
            {
                return false;
            }

            var direction = (hand.position - upper.position).normalized;
            return direction.y < -0.5f;
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}

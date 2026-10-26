using UnityEngine;

namespace Gothic.VR.Interaction.Components
{
    /// <summary>
    /// Forwards the collisions of a physical hand's rigidbody, or of the item it holds, to the VrPhysicsHand (contact
    /// haptics, held item blocked by a wall). Added at runtime by VrPhysicsHand: on its body when it's created, on a
    /// held item while it's held (one per holding hand).
    /// </summary>
    public class VrPhysicsHandContact : MonoBehaviour
    {
        private VrPhysicsHand _hand;
        private bool _isHeldItem;

        public VrPhysicsHand Hand => _hand;

        public void Init(VrPhysicsHand hand, bool isHeldItem)
        {
            _hand = hand;
            _isHeldItem = isHeldItem;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hand != null)
                _hand.OnContact(collision, _isHeldItem, true);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_hand != null)
                _hand.OnContact(collision, _isHeldItem, false);
        }
    }
}

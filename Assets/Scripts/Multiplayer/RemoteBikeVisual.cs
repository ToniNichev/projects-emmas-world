using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.Multiplayer
{
    // Shows a bike under another player's avatar while they're riding.
    // The bike is a copy of the local player's bike (see BikeRider), and the
    // avatar's body is lifted onto the pedals the same way.
    public class RemoteBikeVisual : MonoBehaviour
    {
        private const float RiderLift = 0.45f;

        private GameObject bike;
        private readonly List<Transform> liftedParts = new List<Transform>();
        private bool riding;

        public void SetRiding(bool shouldRide, GameObject bikeTemplate)
        {
            if (shouldRide == riding)
                return;
            if (shouldRide && bike == null && bikeTemplate == null)
                return;

            riding = shouldRide;

            if (riding)
            {
                if (bike == null)
                {
                    bike = Instantiate(bikeTemplate, transform);
                    bike.name = "Bike";
                    bike.transform.localPosition = new Vector3(0f, -1f, 0f);
                    bike.transform.localRotation = Quaternion.identity;
                    // Other players use the old full-size avatar, so a
                    // full-size bike (the local copy may be shrunk to fit
                    // a smaller avatar).
                    bike.transform.localScale = Vector3.one;
                }
                bike.SetActive(true);

                // Lift the avatar's own body parts, but not the bike or the
                // chat bubble (which sizes itself above the avatar).
                liftedParts.Clear();
                foreach (Transform child in transform)
                {
                    if (child.gameObject == bike || child.GetComponent<Canvas>() != null)
                        continue;
                    child.localPosition += Vector3.up * RiderLift;
                    liftedParts.Add(child);
                }
            }
            else
            {
                if (bike != null)
                    bike.SetActive(false);
                foreach (Transform part in liftedParts)
                {
                    if (part != null)
                        part.localPosition -= Vector3.up * RiderLift;
                }
                liftedParts.Clear();
            }
        }
    }
}

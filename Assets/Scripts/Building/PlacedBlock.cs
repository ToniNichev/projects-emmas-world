using UnityEngine;

namespace Sandbox.Building
{
    public class PlacedBlock : MonoBehaviour
    {
        public int ShapeIndex;

        // Server-assigned id once this block is confirmed by the multiplayer
        // backend (see MultiplayerManager); -1 for a block that only exists
        // locally (offline/Editor testing, or a placement request that hasn't
        // round-tripped yet).
        public int NetworkId = -1;
    }
}

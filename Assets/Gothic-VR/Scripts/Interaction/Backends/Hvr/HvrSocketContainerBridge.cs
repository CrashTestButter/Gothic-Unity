#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core.Logging;
using Gothic.VR.Interaction.Components;
using Gothic.VR.Interaction.Ports;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Sockets;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Backends.Hvr
{
    /// <summary>
    /// Sits next to an HVRSocketContainer. Every socket of the container needs an HvrSocketBridge.
    /// </summary>
    public class HvrSocketContainerBridge : MonoBehaviour, IVrSocketContainer
    {
        private HVRSocketContainer _container;

        private HVRSocketContainer Container
        {
            get
            {
                if (_container == null)
                    _container = GetComponent<HVRSocketContainer>();
                return _container;
            }
        }

        public IReadOnlyList<IVrSocket> Sockets
        {
            get
            {
                var result = new List<IVrSocket>(Container.Sockets.Count);
                foreach (var hvrSocket in Container.Sockets)
                {
                    var socket = GetSocket(hvrSocket);
                    if (socket != null)
                        result.Add(socket);
                }

                return result;
            }
        }

        public bool TryFindFree(VrInteractable interactable, out IVrSocket socket)
        {
            socket = null;
            if (interactable == null || !interactable.TryGetComponent(out HVRGrabbable grabbable))
                return false;
            if (!Container.TryFindAvailableSocket(grabbable, out var hvrSocket))
                return false;

            socket = GetSocket(hvrSocket);
            return socket != null;
        }

        public bool TryAdd(VrInteractable interactable)
        {
            if (interactable == null || !interactable.TryGetComponent(out HVRGrabbable grabbable))
                return false;

            return Container.TryAddGrabbable(grabbable);
        }

        private static IVrSocket GetSocket(Component hvrSocket)
        {
            if (hvrSocket == null)
                return null;

            var socket = hvrSocket.GetComponent<IVrSocket>();
            if (socket == null)
                Logger.LogError($"[HVR] Socket {hvrSocket.name} has no HvrSocketBridge.", LogCat.VR);
            return socket;
        }
    }
}
#endif

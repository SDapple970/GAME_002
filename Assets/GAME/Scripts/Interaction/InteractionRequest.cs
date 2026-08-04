using System.Collections.Generic;
using UnityEngine;

namespace Game.Interaction
{
    public readonly struct InteractionRequest
    {
        public InteractionRequest(
            InteractableObject source,
            string interactionId,
            GameObject interactor,
            InteractionController controller,
            InteractionUsePolicy usePolicy,
            IReadOnlyList<InteractionEventSO> events)
        {
            Source = source;
            InteractionId = InteractionIdentity.Normalize(interactionId);
            Interactor = interactor;
            Controller = controller;
            UsePolicy = usePolicy;
            Events = events;
        }

        public InteractableObject Source { get; }
        public string InteractionId { get; }
        public GameObject Interactor { get; }
        public InteractionController Controller { get; }
        public InteractionUsePolicy UsePolicy { get; }
        public IReadOnlyList<InteractionEventSO> Events { get; }
        public bool IsValid => Source != null && Events != null;
    }
}

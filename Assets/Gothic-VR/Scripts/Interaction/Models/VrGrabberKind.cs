namespace Gothic.VR.Interaction.Models
{
    /// <summary>
    /// What grabbed (or released) an interactable.
    /// </summary>
    public enum VrGrabberKind
    {
        /// <summary>
        /// A hand grabbing directly.
        /// </summary>
        Hand,

        /// <summary>
        /// A hand pulling an object from a distance (remote grab). The same object is grabbed by the Hand afterwards.
        /// </summary>
        Distance,

        /// <summary>
        /// A socket: holster, backpack slot, chest slot, ...
        /// </summary>
        Socket,

        /// <summary>
        /// Any other grabber of the backend, e.g. a collector that forwards items into a socket container.
        /// </summary>
        Other
    }
}

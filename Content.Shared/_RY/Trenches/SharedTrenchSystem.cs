using Content.Shared.DoAfter;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Serialization;

namespace Content.Shared._RY.Trenches;

public abstract class SharedTrenchSystem : VirtualController { }

[Serializable, NetSerializable]
public sealed partial class DigTrenchDoAfterEvent : SimpleDoAfterEvent
{
}

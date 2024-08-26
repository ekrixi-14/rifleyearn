using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RY.Trenches;

public abstract class SharedTrenchSystem : EntitySystem { }

[Serializable, NetSerializable]
public sealed partial class DigTrenchDoAfterEvent : SimpleDoAfterEvent
{
}

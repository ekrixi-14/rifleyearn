using System.Numerics;
using Content.Shared._RY.Trenches;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server._RY.Trenches;

/// <summary>
/// This handles trench building and management of entities within trenches.
/// </summary>
/// <remarks>
/// Most of this taken from ClimbSystem.
/// Essentially, entities inside of trenches collide with outer trenches - entities outside of trenches DON'T collide with outer trenches.
/// </remarks>
public sealed class TrenchesSystem : SharedTrenchSystem
{
}

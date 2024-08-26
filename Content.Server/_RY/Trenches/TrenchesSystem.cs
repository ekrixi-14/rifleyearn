using System.Numerics;
using Content.Shared._RY.Trenches;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;

namespace Content.Server._RY.Trenches;

/// <summary>
/// This handles...
/// </summary>
public sealed class TrenchesSystem : SharedTrenchSystem
{
    [Dependency] protected readonly SharedToolSystem ToolSystem = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<OuterTrenchComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<OuterTrenchComponent, DigTrenchDoAfterEvent>(OnDoAfter);
    }

    private bool TryPutTrenchAtPosition(EntityCoordinates coordinates)
    {
        var innerQuery = EntityQueryEnumerator<InnerTrenchComponent, TransformComponent>();
        var outerQuery = EntityQueryEnumerator<OuterTrenchComponent, TransformComponent>();

        while (innerQuery.MoveNext(out var entity, out _, out var xform))
        {
            if (xform.Coordinates.Position == coordinates.Position &&
                xform.Coordinates.EntityId == coordinates.EntityId)
            {
                return false;
            }
        }

        while (outerQuery.MoveNext(out var entity, out _, out var xform))
        {
            if (xform.Coordinates.Position == coordinates.Position &&
                xform.Coordinates.EntityId == coordinates.EntityId)
            {
                return false;
            }
        }

        Spawn("RYOuterTrench", coordinates);

        return true;
    }

    private void OnDoAfter(Entity<OuterTrenchComponent> ent, ref DigTrenchDoAfterEvent args)
    {
        var xform = Transform(ent.Owner);
        var origCoords = xform.Coordinates;
        Spawn("RYInnerTrench", origCoords);

        // Absolutely horrible
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(1, 0)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(-1, 0)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(0, 1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(0, -1)));

        Del(ent.Owner);
    }

    private void OnInteractUsing(Entity<OuterTrenchComponent> ent, ref InteractUsingEvent args)
    {
        // I honestly could do this with the construction system, but if I did, it would take 5 hours of messing around with YML to get to work; so.
        if (args.Handled)
            return;

        if (!TryComp<ToolComponent>(args.Used, out var tool))
            return;

        if (!ToolSystem.HasQuality(args.Used, "Prying", tool))
            return;

        if (!ToolSystem.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            3.0f,
            "Prying",
            new DigTrenchDoAfterEvent()))
        {
            return;
        }
        AdminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} is digging {ToPrettyString(ent):target} at {Transform(ent).Coordinates:targetlocation}");
        args.Handled = true;
    }
}

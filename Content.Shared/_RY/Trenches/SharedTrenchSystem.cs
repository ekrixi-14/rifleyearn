using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._RY.Trenches;

public abstract class SharedTrenchSystem : VirtualController
{
    [Dependency] protected readonly SharedToolSystem ToolSystem = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;

    public const string TrenchedFixtureName = "trenched";
    private const int TrenchedCollisionGroup = (int) (CollisionGroup.TableLayer | CollisionGroup.LowImpassable);

    private EntityQuery<InnerTrenchComponent> _climbableQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        _climbableQuery = GetEntityQuery<InnerTrenchComponent>();
        SubscribeLocalEvent<OuterTrenchComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<OuterTrenchComponent, DigTrenchDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<TrenchedComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TrenchedComponent, ComponentInit>(OnTrenchedComponentInit);
        SubscribeLocalEvent<TrenchedComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<TrenchedComponent, EndClimbEvent>(OnEndClimb);
    }

    private void OnEndClimb(Entity<TrenchedComponent> ent, ref EndClimbEvent args)
    {
        if (!EntityManager.TryGetComponent<FixturesComponent>(ent.Owner, out var fixtures))
            return;
        ent.Comp.IsTrenched = false;
        UpdateTrenched(ent.Owner, ent.Comp, fixtures);
    }

    private void OnTrenchedComponentInit(Entity<TrenchedComponent> ent, ref ComponentInit args)
    {

    }

    /// <summary>
    /// Updates the IsTrenched value on an entity
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="component"></param>
    /// <param name="fixtures"></param>
    private void UpdateTrenched(EntityUid ent, TrenchedComponent component, FixturesComponent fixtures)
    {
        // This feels jank but essentially it just checks if the person is climbing an object
        if (TryComp<ClimbingComponent>(ent, out var climbingComponent))
        {
            if (climbingComponent.IsClimbing)
                component.IsTrenched = false;
        }

        if (component.IsTrenched)
        {
            _alertsSystem.ShowAlert(ent, component.Alert);
            Log.Debug("Entity entered trench");

            foreach (var (name, fixtureMask) in component.DisabledFixtureMasks)
            {
                if (!fixtures.Fixtures.TryGetValue(name, out var fixture))
                {
                    continue;
                }

                _physicsSystem.SetCollisionMask(ent, name, fixture, fixture.CollisionMask | fixtureMask, fixtures);
            }
            component.DisabledFixtureMasks.Clear();
            _fixtureSystem.DestroyFixture(ent, TrenchedFixtureName, manager: fixtures);
        }
        else
        {
            _alertsSystem.ClearAlert(ent, component.Alert);
            Log.Debug("Entity has exited trench");

            // Swap fixtures
            foreach (var (name, fixture) in fixtures.Fixtures)
            {
                if (component.DisabledFixtureMasks.ContainsKey(name)
                    || fixture.Hard == false
                    || (fixture.CollisionMask & TrenchedCollisionGroup) == 0)
                {
                    continue;
                }

                component.DisabledFixtureMasks.Add(name, fixture.CollisionMask & TrenchedCollisionGroup);
                _physicsSystem.SetCollisionMask(ent, name, fixture, fixture.CollisionMask & ~TrenchedCollisionGroup, fixtures);
            }

            _fixtureSystem.TryCreateFixture(
                ent,
                new PhysShapeCircle(0.35f),
                TrenchedFixtureName,
                collisionLayer: (int) CollisionGroup.None,
                collisionMask: TrenchedCollisionGroup,
                hard: false,
                manager: fixtures);
        }
    }

    private void OnEndCollide(Entity<TrenchedComponent> ent, ref EndCollideEvent args)
    {
        if (!EntityManager.TryGetComponent<FixturesComponent>(ent.Owner, out var fixtures))
            return;

        if (HasComp<ClimbableComponent>(args.OtherEntity) && !ent.Comp.IsTrenched)
        {
            // if (EntityManager.TryGetComponent<ClimbingComponent>(ent.Owner, out var climbing))
            //     ClimbSystem.StopClimb(ent.Owner, climbing, fixtures);
            return;
        }

        if (args.OtherFixtureId != TrenchedFixtureName || !ent.Comp.IsTrenched)
            return;

        Log.Debug("Entity trying to exit trench");
        // Do not let the entity exit the trench if they overlap with an entity that has the InnerTrench component
        if (args.OurFixture.Contacts.Count > 1)
        {
            foreach (var contact in args.OurFixture.Contacts.Values)
            {
                if (!contact.IsTouching)
                    continue;

                var otherEnt = contact.EntityA;
                var otherFixture = contact.FixtureA;
                var otherFixtureId = contact.FixtureAId;
                if (ent.Owner == contact.EntityA)
                {
                    otherEnt = contact.EntityB;
                    otherFixture = contact.FixtureB;
                    otherFixtureId = contact.FixtureBId;
                }

                var meta = MetaData(otherEnt);
                Log.Info(meta.EntityName);

                if (!HasComp<InnerTrenchComponent>(otherEnt))
                    continue;
                ent.Comp.IsTrenched = true;
                UpdateTrenched(ent.Owner, ent.Comp, fixtures);
                return;
            }
        }
        ent.Comp.IsTrenched = false;
        UpdateTrenched(ent.Owner, ent.Comp, fixtures);
    }


    private void OnStartCollide(Entity<TrenchedComponent> ent, ref StartCollideEvent args)
    {
        // Enable collisions because we're inside of a trench
        if (!EntityManager.TryGetComponent<FixturesComponent>(ent.Owner, out var fixtures))
            return;
        Log.Debug("Entity trying to enter trench");

        if (args.OurFixture.Contacts.Count < 1)
            return;

        foreach (var contact in args.OurFixture.Contacts.Values)
        {
            if (!contact.IsTouching)
                continue;

            var otherEnt = contact.EntityA;
            var otherFixture = contact.FixtureA;
            var otherFixtureId = contact.FixtureAId;
            if (ent.Owner == contact.EntityA)
            {
                otherEnt = contact.EntityB;
                otherFixture = contact.FixtureB;
                otherFixtureId = contact.FixtureBId;
            }

            var meta = MetaData(otherEnt);

            if (!HasComp<InnerTrenchComponent>(otherEnt))
                continue;
            ent.Comp.IsTrenched = true;
            UpdateTrenched(ent.Owner, ent.Comp, fixtures);
            return;
        }
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

        Spawn("RYTrenchOuter", coordinates);

        return true;
    }

    private void OnDoAfter(Entity<OuterTrenchComponent> ent, ref DigTrenchDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var xform = Transform(ent.Owner);
        var origCoords = xform.Coordinates;
        Spawn("RYTrenchInner", origCoords);

        // Absolutely horrible
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(1, 0)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(-1, 0)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(0, 1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(0, -1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(1, 1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(-1, -1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(-1, 1)));
        TryPutTrenchAtPosition(new EntityCoordinates(origCoords.EntityId, origCoords.Position + new Vector2(1, -1)));

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
            0.01f,
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

[Serializable, NetSerializable]
public sealed partial class DigTrenchDoAfterEvent : SimpleDoAfterEvent
{
}

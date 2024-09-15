using System.Numerics;
using Content.Shared._RY.Trenches;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Components;
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
    [Dependency] protected readonly SharedToolSystem ToolSystem = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ClimbSystem ClimbSystem = default!;
    [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;

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
    }

    private void OnTrenchedComponentInit(Entity<TrenchedComponent> ent, ref ComponentInit args)
    {

    }

    private void OnEndCollide(Entity<TrenchedComponent> ent, ref EndCollideEvent args)
    {
        if (!EntityManager.TryGetComponent<FixturesComponent>(ent.Owner, out var fixtures))
            return;

        if (HasComp<ClimbableComponent>(args.OtherEntity) && !ent.Comp.IsTrenched)
        {
            if (EntityManager.TryGetComponent<ClimbingComponent>(ent.Owner, out var climbing))
                ClimbSystem.StopClimb(ent.Owner, climbing, fixtures);
            return;
        }

        if (args.OtherFixtureId != TrenchedFixtureName || !ent.Comp.IsTrenched)
            return;

        Log.Info("Entity trying to exit trench");
        // Do not let the entity exit the trench if they overlap with an entity that has the InnerTrench component
        if (args.OurFixture.Contacts.Count >= 1)
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

                // if (args.OtherEntity == otherEnt && args.OtherFixtureId == otherFixtureId)
                //     continue;

                if (HasComp<InnerTrenchComponent>(otherEnt))
                {
                    return;
                }
            }
        }

        Log.Info("Entity has exited trench");
        var trenched = EnsureComp<TrenchedComponent>(ent.Owner);
        trenched.IsTrenched = false;

        // Swap fixtures
        foreach (var (name, fixture) in fixtures.Fixtures)
        {
            if (trenched.DisabledFixtureMasks.ContainsKey(name)
                || fixture.Hard == false
                || (fixture.CollisionMask & TrenchedCollisionGroup) == 0)
            {
                continue;
            }

            trenched.DisabledFixtureMasks.Add(name, fixture.CollisionMask & TrenchedCollisionGroup);
            _physicsSystem.SetCollisionMask(ent.Owner, name, fixture, fixture.CollisionMask & ~TrenchedCollisionGroup, fixtures);
        }

        _fixtureSystem.TryCreateFixture(
            ent.Owner,
            new PhysShapeCircle(0.35f),
            TrenchedFixtureName,
            collisionLayer: (int) CollisionGroup.None,
            collisionMask: TrenchedCollisionGroup,
            hard: false,
            manager: fixtures);
    }

    private void OnStartCollide(Entity<TrenchedComponent> ent, ref StartCollideEvent args)
    {
        // Enable collisions because we're inside of a trench
        if (!EntityManager.TryGetComponent<FixturesComponent>(ent.Owner, out var fixtures))
            return;
        Log.Info("Entity trying to enter trench");

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
            Log.Info(meta.EntityName);

            if (!HasComp<InnerTrenchComponent>(otherEnt))
            {
                return;
            }
        }

        Log.Info("Entity entered trench");
        ent.Comp.IsTrenched = true;

        foreach (var (name, fixtureMask) in ent.Comp.DisabledFixtureMasks)
        {
            if (!fixtures.Fixtures.TryGetValue(name, out var fixture))
            {
                continue;
            }

            _physicsSystem.SetCollisionMask(ent.Owner, name, fixture, fixture.CollisionMask | fixtureMask, fixtures);
        }
        ent.Comp.DisabledFixtureMasks.Clear();
        _fixtureSystem.DestroyFixture(ent.Owner, TrenchedFixtureName, manager: fixtures);
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

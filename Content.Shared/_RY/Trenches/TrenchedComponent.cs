using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._RY.Trenches;

/// <summary>
/// Added onto an entity who is inside of a trench
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class TrenchedComponent : Component
{
    /// <summary>
    /// Whether or not this entity is trenched
    /// </summary>
    [DataField] public bool IsTrenched;


    /// <summary>
    /// The alert that is shown when the player is trenched
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "RYTrenched";

    [AutoNetworkedField, DataField]
    public Dictionary<string, int> DisabledFixtureMasks = new();
}

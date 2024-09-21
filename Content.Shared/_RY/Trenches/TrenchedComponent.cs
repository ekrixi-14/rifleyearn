namespace Content.Shared._RY.Trenches;

/// <summary>
/// Added onto an entity who is inside of a trench
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class TrenchedComponent : Component
{
    [DataField] public bool IsTrenched;

    [AutoNetworkedField, DataField]
    public Dictionary<string, int> DisabledFixtureMasks = new();
}

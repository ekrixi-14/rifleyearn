using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;

namespace Content.Shared.CCVar;
// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed class RYCVars : CVars
{
    /// <summary>
    /// Should atmos tiles equalize?
    /// </summary>
    public static readonly CVarDef<bool> AtmosTileEqualize =
        CVarDef.Create("ry.atmos_tile_equalize", false, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Should the gas tile overlay update?
    /// </summary>
    public static readonly CVarDef<bool> GasTileOverlayUpdate =
        CVarDef.Create("ry.gas_tile_overlay_update", false, CVar.REPLICATED | CVar.SERVER);
}

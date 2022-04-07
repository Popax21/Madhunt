module MadhuntStartZoneIndicator
using ..Ahorn, Maple

@mapdef Entity "Madhunt/StartZoneIndicator" StartZoneIndicator(x::Integer, y::Integer, width::Integer=1, height::Integer=8)

const placements = Ahorn.PlacementDict(
    "Start Zone Indicator (Madhunt)" => Ahorn.EntityPlacement(
        StartZoneIndicator,
        "rectangle"
    )
)

Ahorn.minimumSize(entity::StartZoneIndicator) = 1, 1
Ahorn.resizable(entity::StartZoneIndicator) = true, true

function Ahorn.selection(entity::StartZoneIndicator)
    x, y = Ahorn.position(entity)
    return Ahorn.Rectangle(x, y, get(entity.data, "width", 1), get(entity.data, "height", 8))
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::StartZoneIndicator, room::Maple.Room)
    Ahorn.drawRectangle(ctx, 0, 0, get(entity.data, "width", 1), get(entity.data, "height", 8), (1.0, 1.0, 1.0, 1.0))
end
end
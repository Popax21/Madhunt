module MadhuntTransitionBooster
using ..Ahorn, Maple

@mapdef Entity "Madhunt/TransitionBooster" TransitionBooster(x::Integer, y::Integer, targetDir::Integer=0, targetArea::String="", targetLevel::String="", targetID::Integer=0)
@mapdef Entity "Madhunt/TransitionBoosterTarget" TransitionBoosterTarget(x::Integer, y::Integer, targetID::Integer=0, boosterDir::Integer=0)


const placements = Ahorn.PlacementDict(
    "Transition Booster (Madhunt)" => Ahorn.EntityPlacement(
        TransitionBooster
    ),
    "Transition Booster Target (Madhunt)" => Ahorn.EntityPlacement(
        TransitionBoosterTarget
    )
)

const dirNames = Dict{String, Integer}(
    "LD" => 0,
    "L" => 1,
    "LU" => 2,
    "U" => 3,
    "RU" => 4,
    "R" => 5,
    "RD" => 6,
    "D" => 7
)

const dirVectors = Dict{Integer, Any}(
    0 => [ -1, +1 ],
    1 => [ -1,  0 ],
    2 => [ -1, -1 ],
    3 => [  0, -1 ],
    4 => [ +1, -1 ],
    5 => [ +1,  0 ],
    6 => [ +1, +1 ],
    7 => [  0, +1 ]
)

Ahorn.editingOptions(entity::TransitionBooster) = Dict{String, Any}(
    "targetDir" => dirNames
)

Ahorn.editingOptions(entity::TransitionBoosterTarget) = Dict{String, Any}(
    "boosterDir" => dirNames
)

const sprite = "objects/booster/boosterRed00"

function Ahorn.selection(entity::TransitionBooster)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

function Ahorn.selection(entity::TransitionBoosterTarget)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::TransitionBooster, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0, tint=(0.4, 0.25, 0.75, 1.0))
function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::TransitionBoosterTarget, room::Maple.Room)
    Ahorn.drawSprite(ctx, sprite, 0, 0, tint=(0.4, 0.25, 0.75, 0.7))
    ax, ay = get(dirVectors, get(entity.data, "boosterDir", 0), [0, 0])
    Ahorn.drawArrow(ctx, 0, 0, 8*ax, 8*ay, (0.0, 0.0, 1.0, 1.0), headLength=4)
end

end